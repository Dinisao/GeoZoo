using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controla o deck: ao clicar anima uma carta (deck→centro→flip→preview),
/// define a carta ativa e diz ao GridValidator qual é o AnimalPattern correspondente.
/// Fica bloqueado após a animação até a grelha ficar COMPLETA (e válida).
/// </summary>
public class DeckController : MonoBehaviour
{
    [Header("Refs")]
    public Image imgDeckBack;        // botão do deck (Image com Button)
    public RectTransform posCentro;  // ponto no centro (RectTransform no Canvas)
    public RectTransform posPreview; // ponto do preview (RectTransform)
    public Image imgPreview;         // (opcional) mostra a Frente no fim
    public CardAnimator animator;    // componente que faz a animação

    [Header("Cartas")]
    public FacePair[] Cartas;        // definir no Inspector
    public bool aleatorio = false;

    [Header("Estado")]
    public bool debugLogs = true;

    public bool TemCartaAtual { get; private set; }
    public Sprite CartaAtual => _cartaAtual;

    bool _busy;
    bool _deckBloqueado;
    int _nextIndex;
    Sprite _cartaAtual;

    [Serializable]
    public struct FacePair
    {
        public string Id;
        public Sprite Frente;
        public Sprite Verso;
    }

    void Awake()
    {
        Log("Awake.");
        var btn = imgDeckBack ? imgDeckBack.GetComponent<Button>() : null;
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnDeckClick);
            btn.onClick.AddListener(OnDeckClick);
        }
        else
        {
            LogWarn("imgDeckBack sem Button — o clique não será capturado automaticamente.");
        }
    }

    public void BloquearDeck()    { _deckBloqueado = true; }
    public void DesbloquearDeck() { _deckBloqueado = false; }

    /// <summary>
    /// Limpa a carta atual e faz fade-out do preview, quer seja por Image dedicada
    /// (imgPreview) quer seja por clone filho de posPreview.
    /// </summary>
    public void LimparCartaAtual()
    {
        TemCartaAtual = false;
        _cartaAtual = null;

        ControladorJogo.Instancia?.DefinirInteracaoTiles(false);

        float dur = 0.25f;

        // Caso 1: temos uma Image dedicada para o preview (recomendado)
        if (imgPreview != null)
        {
            if (imgPreview.canvasRenderer != null)
            {
                // Faz fade para 0 e só depois limpa o sprite
                imgPreview.CrossFadeAlpha(0f, dur, ignoreTimeScale: true);
                StartCoroutine(_CoClearPreviewImage(imgPreview, dur));
            }
            else
            {
                imgPreview.sprite = null; // fallback
            }
            return;
        }

        // Caso 2: não há imgPreview — o CardAnimator deixou um clone dentro de posPreview
        if (posPreview != null && posPreview.childCount > 0)
        {
            var child = posPreview.GetChild(0) as RectTransform;
            if (child != null)
            {
                var cg = child.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                StartCoroutine(FadeAndDestroy(child.gameObject, cg, dur));
                return;
            }
        }
    }

    IEnumerator _CoClearPreviewImage(Image target, float delay)
    {
        // espera o fade terminar antes de remover o sprite
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, delay));
        if (target) target.sprite = null;
        // mantém invisível até sair nova carta
        if (target && target.canvasRenderer != null) target.canvasRenderer.SetAlpha(0f);
    }

    IEnumerator FadeAndDestroy(GameObject go, CanvasGroup cg, float dur)
    {
        float t = 0f;
        while (t < dur && go != null && cg != null)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            cg.alpha = 1f - k;
            yield return null;
        }
        if (go) Destroy(go);
    }

    void OnDeckClick()
    {
        if (_busy) { Log("Ignorado (busy)."); return; }
        if (_deckBloqueado) { Log("Ignorado (deck bloqueado)."); return; }
        if (!ValidarRefs()) return;

        var par = EscolherPar();
        if (par.Frente == null || par.Verso == null) { LogErr("Par inválido."); return; }

        _deckBloqueado = true;
        _busy = true;

        Log($"Compra: {(string.IsNullOrEmpty(par.Id) ? "(sem Id)" : par.Id)}");

        animator.Play(imgDeckBack, posCentro, posPreview, par.Frente, par.Verso, imgPreview, () =>
        {
            _busy = false;
            Log("Animação concluída (deck permanece bloqueado até validação).");

            _cartaAtual = par.Frente;
            TemCartaAtual = true;

            // MOSTRAR preview (garante alpha=1 caso viesse de 0)
            if (imgPreview && imgPreview.canvasRenderer != null)
                imgPreview.canvasRenderer.SetAlpha(1f);

            // Encontrar Validator e definir o padrão ativo
            var gv = FindOne<GridValidator>();
            if (gv != null)
            {
                var pattern = ResolverPattern(par);
                if (pattern != null)
                {
                    gv.DefinirPadraoAtivo(pattern);
                    if (gv.Deck == null) gv.Deck = this; // auto-wire para o callback
                }
                else
                {
                    Debug.LogWarning("[DeckController] Nenhum AnimalPattern compatível encontrado para a carta atual. Verifica PatternRegistry/CardSprite/Id.", this);
                }
            }

            // Recomeça o timer apenas quando nova carta chega a preview
            ControladorJogo.Instancia?.IniciarTimerSeAindaNao();
            ControladorJogo.Instancia?.DefinirInteracaoTiles(true);
        });
    }

    /// <summary>
    /// CHAMADO pelo GridValidator quando o estado de validação muda.
    /// Mantemos o deck bloqueado aqui; o desbloqueio agora acontece no fim da limpeza (GridValidator).
    /// </summary>
    public void OnGridValidationChanged(bool valido, bool completo)
    {
        Log($"[Deck] Grid ok? valido={valido} completo={completo}");

        if (!completo) return;

        if (valido)
        {
            // 1) GridValidator pára timer e dá recompensa (+1 ZOO, +20s), corta interação
            // 2) Recolhe peças (animado)
            // 3) Limpa preview (fade até 0 / destroy do clone)
            // 4) Desbloqueia o deck (no fim)
            Log("Padrão correto ✅ — aguardando recolha/limpeza para desbloquear deck.");
        }
        else
        {
            Log("Padrão incorreto ❌ — mantém deck bloqueado até corrigir.");
        }
    }

    FacePair EscolherPar()
    {
        if (Cartas == null || Cartas.Length == 0) return default;

        if (aleatorio)
        {
            int idx = UnityEngine.Random.Range(0, Cartas.Length);
            return Cartas[idx];
        }
        else
        {
            var par = Cartas[Mathf.Clamp(_nextIndex, 0, Cartas.Length - 1)];
            _nextIndex = (_nextIndex + 1) % Cartas.Length;
            return par;
        }
    }

    // Mapeia FacePair -> AnimalPattern via PatternRegistry
    AnimalPattern ResolverPattern(FacePair par)
    {
        var reg = FindOne<PatternRegistry>();
        if (reg == null || reg.Patterns == null || reg.Patterns.Count == 0) return null;

        // 1) por Sprite
        if (par.Frente != null)
        {
            var bySprite = reg.Patterns.FirstOrDefault(p => p && p.CardSprite == par.Frente);
            if (bySprite) return bySprite;
        }

        // 2) por Id exato
        string id = (par.Id ?? "").Trim().ToLowerInvariant();
        if (id.Length > 0)
        {
            var byIdExact = reg.Patterns.FirstOrDefault(p =>
                p && (
                    p.name.Trim().ToLowerInvariant() == id ||
                    (p.CardSprite && p.CardSprite.name.Trim().ToLowerInvariant() == id)
                )
            );
            if (byIdExact) return byIdExact;
        }

        // 3) contains (fallback)
        if (id.Length > 0)
        {
            var byContains = reg.Patterns.FirstOrDefault(p =>
                p && (
                    p.name.ToLowerInvariant().Contains(id) ||
                    (p.CardSprite && p.CardSprite.name.ToLowerInvariant().Contains(id))
                )
            );
            if (byContains) return byContains;
        }

        return null;
    }

    bool ValidarRefs()
    {
        if (imgDeckBack == null) { LogErr("imgDeckBack NULL."); return false; }
        if (posCentro == null)   { LogErr("posCentro NULL.");   return false; }
        if (posPreview == null)  { LogErr("posPreview NULL.");  return false; }
        if (animator == null)    { LogErr("animator NULL.");    return false; }
        if (Cartas == null || Cartas.Length == 0)
        { LogErr("Cartas vazio."); return false; }

        for (int i = 0; i < Cartas.Length; i++)
        {
            if (Cartas[i].Frente == null) { LogErr($"Cartas[{i}] Frente NULL ({Cartas[i].Id})"); return false; }
            if (Cartas[i].Verso  == null) { LogErr($"Cartas[{i}] Verso  NULL ({Cartas[i].Id})");  return false; }
        }
        return true;
    }

    void Log(string m){ if (debugLogs) Debug.Log($"[DeckController] {m}", this); }
    void LogWarn(string m){ if (debugLogs) Debug.LogWarning($"[DeckController] {m}", this); }
    void LogErr(string m){ Debug.LogError($"[DeckController] {m}", this); }

    // Helper compatível com versões do Unity
    static T FindOne<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }
}
