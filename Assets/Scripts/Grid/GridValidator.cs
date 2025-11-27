// GridValidator.cs — Compara o estado atual da grelha com o AnimalPattern ativo.
// Observa o tabuleiro (hash) e só revalida quando algo muda. Quando casa:
// pára o timer, aplica recompensa, opcionalmente toca VFX, recolhe peças, limpa preview,
// e no fim desbloqueia o deck.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;              // reflection para disparar o bounce
using UnityEngine;
using UnityEngine.UI;                 // necessário para 'Image'

public class GridValidator : MonoBehaviour
{
    [Header("Referências")]
    public RectTransform GridRoot;
    public DeckController Deck;

    [Header("FX de Vitória")]
    [Tooltip("VFX de vídeo do animal + acesso ao shuffle extra.")]
    public AnimalWinVFX VitoriaFX;

    [Header("QoL")]
    [Tooltip("Quando o padrão estiver completo e válido, recolhe as peças automaticamente para a mão.")]
    public bool RecolherAoAcertar = true;

    [Tooltip("Atraso (em segundos) antes de iniciar a sequência de VFX/recolha.")]
    public float AtrasoRecolha = 0f;

    [Tooltip("Se ligado, limpa a imagem de preview da carta quando acerta (com pequeno atraso/fade).")]
    public bool LimparPreviewAoAcertar = true;

    [Header("Animação de Recolha")]
    [Tooltip("Duração base usada por AnimarVoltarParaMao em cada peça.")]
    public float DurRecolha = 0.25f;

    [Header("Debug")]
    public bool debugLogs = true;

    public bool EstadoValido   { get; private set; }
    public bool EstadoCompleto { get; private set; }

    AnimalPattern _ativo;
    string _ultimoHash = null;
    bool _resetAgendado = false;

    // ————————————————————————————————————————————————————————————
    // Ciclo de vida / wiring
    // ————————————————————————————————————————————————————————————
    public void DefinirPadraoAtivo(AnimalPattern ap)
    {
        _ativo = ap;
        _ultimoHash = null;

        if (debugLogs && _ativo)
        {
            Debug.Log(
                "[GridValidator] Pattern ativo: " + _ativo.name +
                " (tiles=" + (_ativo.cellsRelatives != null ? _ativo.cellsRelatives.Count : 0) + ")",
                this
            );

            Debug.Log(
                "[GridValidator] Flags: RotGlob=" + _ativo.PermitirRotacoesGlobais +
                ", ExigirRot=" + _ativo.ExigirRotacao +
                ", IgnorarRotEye=" + _ativo.IgnorarRotacaoNaCelulaEye +
                ", MeiaVolta=" + _ativo.AceitarMeiaVolta,
                this
            );
        }
    }

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (!GridRoot)
        {
            var allCells = UnityEngine.Object.FindObjectsByType<CelulaGrelha>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var any = allCells.FirstOrDefault();
            if (any) GridRoot = any.transform.parent as RectTransform;
        }

        if (!Deck)
            Deck = UnityEngine.Object.FindFirstObjectByType<DeckController>(FindObjectsInactive.Include);
#else
        if (!GridRoot)
        {
            var allCells = UnityEngine.Object.FindObjectsOfType<CelulaGrelha>(true);
            var any = allCells.FirstOrDefault();
            if (any) GridRoot = any.transform.parent as RectTransform;
        }

        if (!Deck)
            Deck = UnityEngine.Object.FindObjectOfType<DeckController>(true);
#endif
    }

    void OnEnable()  { Peca.OnPecaStateChanged += ForcarRevalidacao; }
    void OnDisable() { Peca.OnPecaStateChanged -= ForcarRevalidacao; }

    void ForcarRevalidacao()
    {
        _ultimoHash = null;
        Validar();
    }

    void LateUpdate()
    {
        var h = HashDoTabuleiro();
        if (h != _ultimoHash)
        {
            _ultimoHash = h;
            Validar();
        }
    }

    // ————————————————————————————————————————————————————————————
    // Validação principal
    // ————————————————————————————————————————————————————————————
    void Validar()
    {
        if (_ativo == null)
        {
            Sinalizar(false, false);
            return;
        }

        int alvo = _ativo.cellsRelatives != null ? _ativo.cellsRelatives.Count : 0;
        if (alvo <= 0)
        {
            Sinalizar(false, false);
            return;
        }

        var colocadas = ColherPecasColocadas();

        if (debugLogs)
            Debug.Log("[GridValidator] colocadas=" + colocadas.Count + " / alvo=" + alvo, this);

        if (colocadas.Count < alvo)
        {
            Sinalizar(false, false);
            return;
        }
        if (colocadas.Count > alvo)
        {
            Sinalizar(false, true);
            return;
        }

        bool match = TentaCasar(colocadas, _ativo, _ativo.ExigirRotacao, out string dbg);
        if (debugLogs)
            Debug.Log("[GridValidator] match=" + match + " " + dbg, this);

        Sinalizar(match, true);
    }

    void Sinalizar(bool valido, bool completo)
    {
        if (EstadoValido == valido && EstadoCompleto == completo)
            return;

        EstadoValido   = valido;
        EstadoCompleto = completo;

        if (Deck != null)
        {
            try
            {
                Deck.OnGridValidationChanged(valido, completo);
            }
            catch
            {
                // silencioso
            }
        }

        if (valido && completo)
        {
            // ✅ Impact bounce imediato (feedback “juicy”)
            TentarDispararImpactBounce(1f);

            // Fluxo de jogo
            ControladorJogo.Instancia?.PararTimer();
            ControladorJogo.Instancia?.RecompensaAcerto();

            if (RecolherAoAcertar && !_resetAgendado)
            {
                _resetAgendado = true;
                StartCoroutine(CoRecolherAposAcerto());
            }
        }
    }

    // ————————————————————————————————————————————————————————————
    // Sequência após acerto (vídeo animal → recolha → shuffle)
    // ————————————————————————————————————————————————————————————
    IEnumerator CoRecolherAposAcerto()
    {
        // Pequeno atraso opcional antes de começar tudo (se quiseres dar “respiro”).
        if (AtrasoRecolha > 0f)
            yield return new WaitForSeconds(AtrasoRecolha);
        else
            yield return null;

        // 1) VÍDEO DO ANIMAL — cartas continuam na grelha.
        if (VitoriaFX != null)
        {
            yield return VitoriaFX.PlayForRoutine(_ativo);
        }

        // 2) RECOLHA ANIMADA das peças para a mão, UMA A UMA.
        yield return RecolherTodasPecasParaMaoAnimado(DurRecolha);

        // 3) Limpar preview (com pequeno atraso para o fade/UX).
        if (LimparPreviewAoAcertar && Deck != null)
        {
            Deck.LimparCartaAtual();
            yield return new WaitForSeconds(0.26f);
        }

        // 4) VÍDEO DE SHUFFLE DO DECK (se estiver configurado).
        if (VitoriaFX != null)
        {
            // PlayShuffleExtraOnly já verifica internamente se há ShuffleVFX ou não.
            yield return VitoriaFX.PlayShuffleExtraOnly();
        }

        // 5) Deck pronto para nova compra.
        if (Deck != null)
            Deck.DesbloquearDeck();

        // 6) Reset & revalidação
        _ultimoHash    = null;
        _resetAgendado = false;
        Validar();
    }

    // ————————————————————————————————————————————————————————————
    // Colheita do estado da grelha
    // ————————————————————————————————————————————————————————————
    struct Colocada
    {
        public Vector2Int      cell;
        public int             rot;
        public EyeRequirement  eye;
#if UNITY_EDITOR
        public string          debug;
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

            // Ignorar peça ainda em interação (evita “acerto” sem pousar)
            var cg = peca.GetComponent<CanvasGroup>();
            if (peca.IsDragging) continue;
            if (peca.IsHeld)     continue;
            if (cg != null && cg.blocksRaycasts == false) continue;

            var rt = peca.transform as RectTransform;
            int rot = NormRot(rt ? rt.localEulerAngles.z : 0f);

            var flip = peca.GetComponent<PecaFlip>();
            EyeRequirement eye = EyeRequirement.None;
            string dbg = "FRENTE";

            if (flip != null)
            {
                if (!flip.EstaNaFrente)
                {
                    string fname = flip.Frente ? flip.Frente.name : "";
                    int lastDigit = -1;

                    for (int k = fname.Length - 1; k >= 0; k--)
                    {
                        if (char.IsDigit(fname[k]))
                        {
                            lastDigit = fname[k] - '0';
                            break;
                        }
                    }

                    if (lastDigit == 1 || lastDigit == 2)
                    {
                        eye = EyeRequirement.Eye;
                        dbg = "VERSO(F→Eye)";
                    }
                    else if (lastDigit == 3 || lastDigit == 4)
                    {
                        eye = EyeRequirement.NoEye;
                        dbg = "VERSO(F→NoEye)";
                    }
                    else
                    {
                        eye = EyeRequirement.NoEye;
                        dbg = "VERSO(F?=NoEye)";
                    }
                }
            }

            outList.Add(new Colocada
            {
                cell = cel.Index,
                rot  = rot,
                eye  = eye
#if UNITY_EDITOR
                , debug = dbg
#endif
            });
        }

        return outList;
    }

    // ————————————————————————————————————————————————————————————
    // Matching contra o pattern
    // ————————————————————————————————————————————————————————————
    bool TentaCasar(List<Colocada> colocadas, AnimalPattern ap, bool exigirRotacao, out string dbg)
    {
        dbg = "";

        int minX = colocadas.Min(c => c.cell.x);
        int minY = colocadas.Min(c => c.cell.y);

        var colocadasNorm = colocadas
            .Select(c => new Colocada
            {
                cell = new Vector2Int(c.cell.x - minX, c.cell.y - minY),
                rot  = c.rot,
                eye  = c.eye
#if UNITY_EDITOR
                , debug = c.debug
#endif
            })
            .OrderBy(c => c.cell.y)
            .ThenBy(c => c.cell.x)
            .ToList();

        var rel  = ap.cellsRelatives ?? new List<Vector2Int>();
        var rots = ap.cellsRotations ?? new List<int>();
        var eyes = ap.cellsEyeReq    ?? new List<EyeRequirement>();

        var expectedBase = new List<(Vector2Int pos, int rot, EyeRequirement eye)>();
        for (int i = 0; i < rel.Count; i++)
        {
            var pos = rel[i];
            int rot = (i < rots.Count ? NormRot(rots[i]) : 0);
            var eye = (i < eyes.Count ? eyes[i] : EyeRequirement.None);
            expectedBase.Add((pos, rot, eye));
        }

        var rotacoesGlobais = ap.PermitirRotacoesGlobais
            ? new[] { 0, 90, 180, 270 }
            : new[] { 0 };

        var rotadores = new System.Func<Vector2Int, int, Vector2Int>[]
        {
            RotacionarMath,
            RotacionarUI
        };

        foreach (int rGlobal in rotacoesGlobais)
        {
            foreach (var rotFn in rotadores)
            {
                var expCells = expectedBase
                    .Select(e => rotFn(e.pos, rGlobal))
                    .ToList();

                int eMinX = expCells.Min(v => v.x);
                int eMinY = expCells.Min(v => v.y);

                for (int i = 0; i < expCells.Count; i++)
                    expCells[i] = new Vector2Int(expCells[i].x - eMinX, expCells[i].y - eMinY);

                var expRots = new List<int>(expectedBase.Count);
                for (int i = 0; i < expectedBase.Count; i++)
                {
                    int baseRotMath = expectedBase[i].rot;
                    int apply = (rotFn == RotacionarUI)
                        ? NormRot(baseRotMath - rGlobal)
                        : NormRot(baseRotMath + rGlobal);

                    expRots.Add(apply);
                }

                var expEyes = expectedBase.Select(e => e.eye).ToList();

                var expected = Enumerable.Range(0, expectedBase.Count)
                    .Select(i => new
                    {
                        pos = expCells[i],
                        rot = expRots[i],
                        eye = expEyes[i]
                    })
                    .OrderBy(e => e.pos.y)
                    .ThenBy(e => e.pos.x)
                    .ToList();

                if (expected.Count != colocadasNorm.Count)
                    continue;

                bool ok = true;

                for (int i = 0; i < expected.Count; i++)
                {
                    var got = colocadasNorm[i];
                    var exp = expected[i];

                    if (got.cell != exp.pos)
                    {
                        ok = false;
                        break;
                    }

                    if (exigirRotacao)
                    {
                        bool ignorarRotCelulaEye = (exp.eye == EyeRequirement.Eye);
                        if (!ignorarRotCelulaEye)
                        {
                            int gr = NormRot(got.rot);
                            int er = NormRot(exp.rot);

                            bool rotOk =
                                (gr == er) ||
                                (ap.AceitarMeiaVolta && gr == NormRot(er + 180));

                            if (!rotOk)
                            {
                                ok = false;
                                break;
                            }
                        }
                    }

                    if (exp.eye == EyeRequirement.Eye)
                    {
                        if (got.eye != EyeRequirement.Eye)
                        {
                            ok = false;
                            break;
                        }
                    }
                    else if (exp.eye == EyeRequirement.NoEye)
                    {
                        if (got.eye != EyeRequirement.NoEye)
                        {
                            ok = false;
                            break;
                        }
                    }
                    else
                    {
                        if (got.eye == EyeRequirement.Eye)
                        {
                            ok = false;
                            break;
                        }
                    }
                }

                if (ok)
                {
                    dbg = "(rGlobal=" + rGlobal +
                          ", rotFn=" + (rotFn == RotacionarMath ? "Math" : "UI") +
                          ", exigirRot=" + exigirRotacao + ")";
                    return true;
                }
            }
        }

        dbg = "(nenhuma rotação casou)";
        return false;
    }

    // ————————————————————————————————————————————————————————————
    // Utilitários
    // ————————————————————————————————————————————————————————————
    static int NormRot(float graus)
    {
        int g = Mathf.RoundToInt(graus) % 360;
        if (g < 0) g += 360;

        int q = Mathf.RoundToInt(g / 90f) * 90;
        return (q % 360 + 360) % 360;
    }

    static Vector2Int RotacionarMath(Vector2Int v, int graus)
    {
        int g = ((graus % 360) + 360) % 360;
        switch (g)
        {
            case 0:   return new Vector2Int(v.x,   v.y);
            case 90:  return new Vector2Int(v.y,  -v.x);
            case 180: return new Vector2Int(-v.x, -v.y);
            case 270: return new Vector2Int(-v.y,  v.x);
            default:  return v;
        }
    }

    static Vector2Int RotacionarUI(Vector2Int v, int graus)
    {
        int g = ((graus % 360) + 360) % 360;
        switch (g)
        {
            case 0:   return new Vector2Int(v.x,   v.y);
            case 90:  return new Vector2Int(-v.y,  v.x);
            case 180: return new Vector2Int(-v.x, -v.y);
            case 270: return new Vector2Int(v.y,  -v.x);
            default:  return v;
        }
    }

    string HashDoTabuleiro()
    {
        var list = ColherPecasColocadas()
            .OrderBy(c => c.cell.y)
            .ThenBy(c => c.cell.x)
            .Select(c => c.cell.x + "," + c.cell.y + "," + c.rot + "," + (int)c.eye)
            .ToArray();

        return _ativo
            ? _ativo.GetInstanceID() + "|" + string.Join(";", list)
            : string.Join(";", list);
    }

    // ——— Recolhas ———
    public IEnumerator RecolherTodasPecasParaMaoAnimado(float dur)
    {
        if (!GridRoot) yield break;

        for (int i = 0; i < GridRoot.childCount; i++)
        {
            var cellRT = GridRoot.GetChild(i) as RectTransform;
            if (!cellRT) continue;

            var peca = cellRT.GetComponentInChildren<Peca>(includeInactive: false);
            if (peca != null)
            {
                // corre cada peça de forma sequencial: 1 → 2 → 3 → 4
                yield return peca.AnimarVoltarParaMao(dur);
            }
        }
    }

    public void RecolherTodasPecasParaMao()
    {
        if (!GridRoot) return;

        for (int i = 0; i < GridRoot.childCount; i++)
        {
            var cellRT = GridRoot.GetChild(i) as RectTransform;
            if (!cellRT) continue;

            var peca = cellRT.GetComponentInChildren<Peca>(includeInactive: false);
            if (peca != null)
                peca.VoltarParaMao();
        }
    }

    // ————————————————————————————————————————————————————————————
    // Helper: dispara ImpactBounce (ou ImpactBounceUIRoot) sem dependência direta
    // ————————————————————————————————————————————————————————————
    void TentarDispararImpactBounce(float intensity)
    {
        // 1) Tenta classe "ImpactBounce" com singleton Instancia
        if (TentarPlayPorNome("ImpactBounce", intensity)) return;

        // 2) Tenta classe alternativa "ImpactBounceUIRoot"
        if (TentarPlayPorNome("ImpactBounceUIRoot", intensity)) return;

        // 3) Fallback: procurar qualquer MonoBehaviour com esses nomes e invocar Play
#if UNITY_2023_1_OR_NEWER
        var todos = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var todos = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
#endif
        foreach (var mb in todos)
        {
            if (mb == null) continue;
            var n = mb.GetType().Name;
            if (n == "ImpactBounce" || n == "ImpactBounceUIRoot")
            {
                var m = mb.GetType().GetMethod("Play", new Type[] { typeof(float) })
                        ?? mb.GetType().GetMethod("Play", Type.EmptyTypes);
                if (m != null)
                {
                    try
                    {
                        if (m.GetParameters().Length == 0) m.Invoke(mb, null);
                        else m.Invoke(mb, new object[] { intensity });
                        return;
                    }
                    catch
                    {
                        // silencioso
                    }
                }
            }
        }
    }

    bool TentarPlayPorNome(string tipoNome, float intensity)
    {
        try
        {
            var t = Type.GetType(tipoNome);
            if (t == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetTypes().FirstOrDefault(x => x.Name == tipoNome);
                    if (t != null) break;
                }
            }
            if (t == null) return false;

            var instProp = t.GetProperty("Instancia", BindingFlags.Public | BindingFlags.Static);
            var inst = instProp != null ? instProp.GetValue(null) : null;
            if (inst == null) return false;

            var m = t.GetMethod("Play", new Type[] { typeof(float) })
                  ?? t.GetMethod("Play", Type.EmptyTypes);

            if (m == null) return false;

            if (m.GetParameters().Length == 0) m.Invoke(inst, null);
            else m.Invoke(inst, new object[] { intensity });

            return true;
        }
        catch
        {
            return false;
        }
    }
}
