using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Valida o layout atual da grelha contra o AnimalPattern ativo.
/// Regras de Eye:
///  • Eye    -> verso com olho (F1/F2)
///  • NoEye  -> verso sem olho (F3/F4)
///  • None   -> “não-Eye” (frente OU verso sem olho)
public class GridValidator : MonoBehaviour
{
    [Header("Referências")]
    public RectTransform GridRoot;     // Parent das células (cada filho tem CelulaGrelha)
    public DeckController Deck;        // Referência ao Deck (opcional mas recomendado)

    [Header("QoL")]
    [Tooltip("Quando o padrão estiver completo e válido, recolhe as peças automaticamente para a mão.")]
    public bool RecolherAoAcertar = true;

    [Tooltip("Atraso (em segundos) antes de recolher as peças (0 = próximo frame).")]
    public float AtrasoRecolha = 0f;

    [Tooltip("Se ligado, limpa a imagem de preview da carta quando acerta (com fade).")]
    public bool LimparPreviewAoAcertar = true;

    [Header("Animação de Recolha")]
    public float DurRecolha = 0.25f; // duração do “arrastar” automático para a mão

    [Header("Debug")]
    public bool debugLogs = true;

    public bool EstadoValido  { get; private set; }
    public bool EstadoCompleto{ get; private set; }

    AnimalPattern _ativo;
    string _ultimoHash = null;
    bool _resetAgendado = false;

    // Chama isto quando a carta entra em preview
    public void DefinirPadraoAtivo(AnimalPattern ap)
    {
        _ativo = ap;
        _ultimoHash = null; // força revalidar já
        if (debugLogs && _ativo)
            Debug.Log($"[GridValidator] Pattern ativo: {_ativo.name} (tiles={_ativo.cellsRelatives?.Count ?? 0})", this);
    }

    void Awake()
    {
        // tentar descobrir GridRoot automaticamente
        if (!GridRoot)
        {
#if UNITY_2023_1_OR_NEWER
            var allCells = UnityEngine.Object.FindObjectsByType<CelulaGrelha>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var allCells = UnityEngine.Object.FindObjectsOfType<CelulaGrelha>(true);
#endif
            var any = allCells.FirstOrDefault();
            if (any) GridRoot = any.transform.parent as RectTransform;
        }

        // tentar obter Deck se faltar
        if (!Deck)
        {
#if UNITY_2023_1_OR_NEWER
            Deck = UnityEngine.Object.FindFirstObjectByType<DeckController>(FindObjectsInactive.Include);
#else
            Deck = UnityEngine.Object.FindObjectOfType<DeckController>(true);
#endif
        }
    }

    void LateUpdate()
    {
        // revalida apenas quando o "estado" do tabuleiro mudar
        var h = HashDoTabuleiro();
        if (h != _ultimoHash)
        {
            _ultimoHash = h;
            Validar();
        }
    }

    void Validar()
    {
        if (_ativo == null) { Sinalizar(false, false); return; }

        int alvo = _ativo.cellsRelatives != null ? _ativo.cellsRelatives.Count : 0;
        if (alvo <= 0) { Sinalizar(false, false); return; }

        var colocadas = ColherPecasColocadas();

        if (debugLogs)
            Debug.Log($"[GridValidator] colocadas={colocadas.Count} / alvo={alvo}", this);

        if (colocadas.Count < alvo) { Sinalizar(false, false); return; }
        if (colocadas.Count > alvo) { Sinalizar(false, true ); return; }

        bool match = TentaCasar(colocadas, _ativo, exigirRotacao: _ativo.ExigirRotacao, out string dbg);
        if (debugLogs) Debug.Log($"[GridValidator] match={match} {dbg}", this);

        Sinalizar(match, true);
    }

    void Sinalizar(bool valido, bool completo)
    {
        if (EstadoValido == valido && EstadoCompleto == completo) return;

        EstadoValido = valido;
        EstadoCompleto = completo;

        // callback direto ao Deck (se existir)
        if (Deck != null)
        {
            try { Deck.OnGridValidationChanged(valido, completo); } catch { }
        }

        if (valido && completo)
        {
            // ❶ pára timer e dá recompensa (+1 ZOO, +20s)
            ControladorJogo.Instancia?.PararTimer();
            ControladorJogo.Instancia?.RecompensaAcerto();

            // ❷ bloquear interação dos tiles até à próxima carta
            ControladorJogo.Instancia?.DefinirInteracaoTiles(false);

            // ❸ iniciar ciclo de recolha/limpeza se ainda não em curso
            if (RecolherAoAcertar && !_resetAgendado)
            {
                _resetAgendado = true;
                StartCoroutine(CoRecolherAposAcerto());
            }
        }
    }

    IEnumerator CoRecolherAposAcerto()
    {
        if (AtrasoRecolha <= 0f) yield return null;
        else yield return new WaitForSeconds(AtrasoRecolha);

        // Recolha ANIMADA das peças para a mão
        yield return RecolherTodasPecasParaMaoAnimado(DurRecolha);

        // Limpar preview (com fade se Image tiver CanvasRenderer)
        if (LimparPreviewAoAcertar && Deck != null)
        {
            Deck.LimparCartaAtual();
            // pequena espera para respeitar o fade (~0.25s)
            yield return new WaitForSeconds(0.26f);
        }

        // Deck pronto para nova compra
        if (Deck != null) Deck.DesbloquearDeck();

        // força revalidação “limpa”
        _ultimoHash = null;
        _resetAgendado = false;
        Validar();
    }

    // --------- recolha & matching ----------

    struct Colocada
    {
        public Vector2Int cell;   // coords da célula (índice da grelha)
        public int rot;           // 0/90/180/270 (normalizado)
        public EyeRequirement eye;// None/Eye/NoEye
#if UNITY_EDITOR
        public string debug;      // info extra (frente/verso, tile nº)
#endif
    }

    List<Colocada> ColherPecasColocadas()
    {
        var outList = new List<Colocada>();
        if (!GridRoot) return outList;

        for (int i = 0; i < GridRoot.childCount; i++)
        {
            var cellRT = GridRoot.GetChild(i) as RectTransform;
            if (!cellRT) continue;
            var cel = cellRT.GetComponent<CelulaGrelha>();
            if (!cel) continue;

            Peca peca = null;
            for (int j = 0; j < cellRT.childCount; j++)
            {
                peca = cellRT.GetChild(j).GetComponent<Peca>();
                if (peca) break;
            }
            if (!peca) continue;

            var rt = peca.transform as RectTransform;
            int rot = NormRot(rt ? rt.localEulerAngles.z : 0f);

            // ---- LÓGICA DO EYE ----
            var flip = peca.GetComponent<PecaFlip>();
            EyeRequirement eye = EyeRequirement.None;
            string dbg = "";
            if (flip)
            {
                if (flip.EstaNaFrente)
                {
                    eye = EyeRequirement.None;
#if UNITY_EDITOR
                    dbg = "FRENTE";
#endif
                }
                else
                {
                    string fname = flip.Frente ? flip.Frente.name : "";
                    int lastDigit = -1;
                    for (int k = fname.Length - 1; k >= 0; k--)
                        if (char.IsDigit(fname[k])) { lastDigit = fname[k] - '0'; break; }

                    if (lastDigit == 1 || lastDigit == 2)
                    {
                        eye = EyeRequirement.Eye;
#if UNITY_EDITOR
                        dbg = $"VERSO(F{lastDigit}=Eye)";
#endif
                    }
                    else if (lastDigit == 3 || lastDigit == 4)
                    {
                        eye = EyeRequirement.NoEye;
#if UNITY_EDITOR
                        dbg = $"VERSO(F{lastDigit}=NoEye)";
#endif
                    }
                    else
                    {
                        eye = EyeRequirement.NoEye; // fallback conservador
#if UNITY_EDITOR
                        dbg = $"VERSO(F?=NoEye)";
#endif
                    }
                }
            }
            // ------------------------

            outList.Add(new Colocada {
                cell = cel.Index, rot = rot, eye = eye
#if UNITY_EDITOR
                , debug = dbg
#endif
            });
        }

        return outList;
    }

    bool TentaCasar(List<Colocada> colocadas, AnimalPattern ap, bool exigirRotacao, out string dbg)
    {
        dbg = "";

        // Normalizar posições para (0,0) no topo-esquerdo do conjunto
        int minX = colocadas.Min(c => c.cell.x);
        int minY = colocadas.Min(c => c.cell.y);

        var colocadasNorm = colocadas
            .Select(c => new Colocada { cell = new Vector2Int(c.cell.x - minX, c.cell.y - minY), rot = c.rot, eye = c.eye
#if UNITY_EDITOR
                    , debug = c.debug
#endif
            })
            .OrderBy(c => c.cell.y).ThenBy(c => c.cell.x)
            .ToList();

        // Opções de rotação global de acordo com o pattern
        var rOptions = ap.PermitirRotacoesGlobais ? new[] { 0, 90, 180, 270 } : new[] { 0 };

        // Testa com duas convenções (Math e "UI")
        var rotFns = new System.Func<Vector2Int, int, Vector2Int>[] { RotacionarMath, RotacionarUI };

        foreach (var rotFn in rotFns)
        {
            foreach (int rGlobal in rOptions)
            {
                // Rotaciona todas as posições esperadas
                var expCells = ap.cellsRelatives.Select(v => rotFn(v, rGlobal)).ToList();

                // Normaliza expCells para (0,0)
                int eMinX = expCells.Min(v => v.x);
                int eMinY = expCells.Min(v => v.y);
                for (int i = 0; i < expCells.Count; i++)
                    expCells[i] = new Vector2Int(expCells[i].x - eMinX, expCells[i].y - eMinY);

                // Rotações esperadas (com rGlobal aplicado)
                var expRots = new List<int>();
                for (int i = 0; i < ap.cellsRelatives.Count; i++)
                {
                    int baseRot = (ap.cellsRotations != null && i < ap.cellsRotations.Count) ? ap.cellsRotations[i] : 0;
                    expRots.Add(NormRot(baseRot + rGlobal));
                }

                // Eye esperado (preenche faltas como None = "não-Eye")
                var expEyes = ap.cellsEyeReq ?? new List<EyeRequirement>();
                while (expEyes.Count < ap.cellsRelatives.Count) expEyes.Add(EyeRequirement.None);

                var expected = Enumerable.Range(0, ap.cellsRelatives.Count)
                    .Select(i => new { pos = expCells[i], rot = expRots[i], eye = expEyes[i] })
                    .OrderBy(e => e.pos.y).ThenBy(e => e.pos.x)
                    .ToList();

                bool ok = true;
                for (int i = 0; i < expected.Count; i++)
                {
                    var got = colocadasNorm[i];
                    var exp = expected[i];

                    if (got.cell != exp.pos) { ok = false; break; }

                    // Rotação por tile
                    if (exigirRotacao)
                    {
                        // Ignorar rotação apenas na célula Eye se o pattern assim o disser
                        bool ignorarRotCelulaEye = ap.IgnorarRotacaoNaCelulaEye && exp.eye == EyeRequirement.Eye;
                        if (!ignorarRotCelulaEye)
                        {
                            int gr = NormRot(got.rot);
                            int er = NormRot(exp.rot);
                            bool rotOk = (gr == er) || (ap.AceitarMeiaVolta && gr == NormRot(er + 180));
                            if (!rotOk) { ok = false; break; }
                        }
                    }

                    // Eye rules
                    if (exp.eye == EyeRequirement.Eye)
                    {
                        if (got.eye != EyeRequirement.Eye) { ok = false; break; }
                    }
                    else if (exp.eye == EyeRequirement.NoEye)
                    {
                        if (got.eye != EyeRequirement.NoEye) { ok = false; break; }
                    }
                    else // None -> não Eye (aceita frente ou verso sem olho)
                    {
                        if (got.eye == EyeRequirement.Eye) { ok = false; break; }
                    }
                }

#if UNITY_EDITOR
                if (!ok && debugLogs)
                {
                    var lines = new List<string>();
                    for (int i = 0; i < expected.Count; i++)
                    {
                        var got = colocadasNorm[i];
                        var exp = expected[i];
                        lines.Add($" i={i} pos={got.cell} rot={got.rot} eye={got.eye} [{got.debug}]  expPos={exp.pos} expRot={exp.rot} expEye={exp.eye}");
                    }
                    Debug.Log($"[GridValidator] detalhes falha (rGlobal={rGlobal}, rotFn={(rotFn==RotacionarMath?"Math":"UI")}, exigirRot={exigirRotacao})\n" +
                              string.Join("\n", lines), this);
                }
#endif

                if (ok) { dbg = $"(rGlobal={rGlobal}, rotFn={(rotFn==RotacionarMath?"Math":"UI")}, exigirRot={exigirRotacao})"; return true; }
            }
        }

        dbg = "(nenhuma rotação casou)";
        return false;
    }

    static int NormRot(float graus)
    {
        int g = Mathf.RoundToInt(graus) % 360; if (g < 0) g += 360;
        int q = Mathf.RoundToInt(g / 90f) * 90;
        return (q % 360 + 360) % 360;
    }

    static Vector2Int RotacionarMath(Vector2Int v, int graus)
    {
        switch (((graus % 360) + 360) % 360)
        {
            case 0:   return new Vector2Int(v.x, v.y);
            case 90:  return new Vector2Int(v.y, -v.x);
            case 180: return new Vector2Int(-v.x, -v.y);
            case 270: return new Vector2Int(-v.y, v.x);
            default:  return v;
        }
    }

    static Vector2Int RotacionarUI(Vector2Int v, int graus)
    {
        switch (((graus % 360) + 360) % 360)
        {
            case 0:   return new Vector2Int(v.x, v.y);
            case 90:  return new Vector2Int(-v.y, v.x);
            case 180: return new Vector2Int(-v.x, -v.y);
            case 270: return new Vector2Int(v.y, -v.x);
            default:  return v;
        }
    }

    string HashDoTabuleiro()
    {
        var list = ColherPecasColocadas()
            .OrderBy(c => c.cell.y).ThenBy(c => c.cell.x)
            .Select(c => $"{c.cell.x},{c.cell.y},{c.rot},{(int)c.eye}")
            .ToArray();
        return _ativo ? $"{_ativo.GetInstanceID()}|{string.Join(";", list)}" : string.Join(";", list);
    }

    // ---- recolha animada para a mão ----
    public IEnumerator RecolherTodasPecasParaMaoAnimado(float dur)
    {
        if (!GridRoot) yield break;

        // Faz sequencialmente (4 peças -> ok). Se quiseres paralelo, digo-te já como.
        for (int i = 0; i < GridRoot.childCount; i++)
        {
            var cellRT = GridRoot.GetChild(i) as RectTransform;
            if (!cellRT) continue;

            var peca = cellRT.GetComponentInChildren<Peca>(includeInactive: false);
            if (peca != null)
            {
                yield return peca.AnimarVoltarParaMao(dur);
            }
        }
    }

    // Versão instantânea (não usada agora, fica para utilidade)
    public void RecolherTodasPecasParaMao()
    {
        if (!GridRoot) return;

        for (int i = 0; i < GridRoot.childCount; i++)
        {
            var cellRT = GridRoot.GetChild(i) as RectTransform;
            if (!cellRT) continue;

            var peca = cellRT.GetComponentInChildren<Peca>(includeInactive: false);
            if (peca != null) peca.VoltarParaMao();
        }
    }
}
