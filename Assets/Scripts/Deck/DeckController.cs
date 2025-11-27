using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DeckController : MonoBehaviour
{
    [Header("Refs")]
    public Image imgDeckBack;
    public RectTransform posCentro;
    public RectTransform posPreview;
    public Image imgPreview;
    public CardAnimator animator;

    [Header("Cartas")]
    public FacePair[] Cartas;
    public bool aleatorio = false;

    [Header("Tutorial (opcional)")]
    public TutorialDeckHint tutorialHint;   // referência para o hint da seta/coruja

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
        else LogWarn("imgDeckBack sem Button — o clique não será capturado automaticamente.");
    }

    public void BloquearDeck()    { _deckBloqueado = true; }
    public void DesbloquearDeck() { _deckBloqueado = false; }

    public void LimparCartaAtual()
    {
        TemCartaAtual = false;
        _cartaAtual = null;
        ControladorJogo.Instancia?.DefinirInteracaoTiles(false);

        float dur = 0.25f;
        if (imgPreview != null)
        {
            if (imgPreview.canvasRenderer != null)
            {
                imgPreview.CrossFadeAlpha(0f, dur, ignoreTimeScale: true);
                StartCoroutine(_CoClearPreviewImage(imgPreview, dur));
            }
            else imgPreview.sprite = null;
            return;
        }

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
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, delay));
        if (target) target.sprite = null;
        if (target && target.canvasRenderer != null) target.canvasRenderer.SetAlpha(0f);
    }

    IEnumerator FadeAndDestroy(GameObject go, CanvasGroup cg, float dur)
    {
        float t = 0f;
        while (t < dur && go != null && cg != null)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = 1f - Mathf.Clamp01(t / dur);
            yield return null;
        }
        if (go) Destroy(go);
    }

    void OnDeckClick()
    {
        // Tutorial: coruja desce / balão desaparece na fase 1, etc.
        if (tutorialHint != null)
        {
            tutorialHint.OnDeckClicked_IniciarTransicao();
        }

        if (_busy)          { Log("Ignorado (busy).");            return; }
        if (_deckBloqueado) { Log("Ignorado (deck bloqueado).");  return; }
        if (!ValidarRefs()) return;

        var par = EscolherPar();
        if (par.Frente == null || par.Verso == null)
        {
            LogErr("Par inválido.");
            return;
        }

        _deckBloqueado = true;
        _busy = true;

        Log($"Compra: {(string.IsNullOrEmpty(par.Id) ? "(sem Id)" : par.Id)}");

        // verso usado na animação = sprite actual do deck (fallback: Verso do par)
        Sprite versoAnim = (imgDeckBack != null && imgDeckBack.sprite != null)
            ? imgDeckBack.sprite
            : par.Verso;

        animator.Play(
            imgDeckBack,
            posCentro,
            posPreview,
            par.Frente,
            versoAnim,
            imgPreview,
            () =>
            {
                _busy = false;
                Log("Animação concluída (deck permanece bloqueado até validação).");

                _cartaAtual = par.Frente;
                TemCartaAtual = true;

                if (imgPreview && imgPreview.canvasRenderer != null)
                    imgPreview.canvasRenderer.SetAlpha(1f);

                var gv = FindOne<GridValidator>();
                if (gv != null)
                {
                    var pattern = ResolverPattern(par);
                    if (pattern != null)
                    {
                        gv.DefinirPadraoAtivo(pattern);
                        if (gv.Deck == null) gv.Deck = this;

                        // início de nova ronda → permite sparkle novamente
                        PecaSparkleTrigger.NovaRonda();
                    }
                    else
                    {
                        Debug.LogWarning("[DeckController] Nenhum AnimalPattern compatível encontrado para a carta atual. Verifica PatternRegistry/CardSprite/Id.", this);
                    }
                }

                // ⏱️ Lógica do timer:
                //  - O ControladorJogo decide sozinho se esta é a 1ª carta "livre" (modo tutorial)
                //    ou se o timer deve arrancar já nesta carta.
                ControladorJogo.Instancia?.IniciarTimerSeAindaNao();

                // Tiles só podem ser usados depois da carta estar em preview
                ControladorJogo.Instancia?.DefinirInteracaoTiles(true);

                // Tutorial – Fase 2 (preview à esquerda + coruja + balão + texto 2)
                if (tutorialHint != null)
                {
                    tutorialHint.OnCartaChegouAoPreview(par.Frente, posPreview);
                }
            });
    }

    public void OnGridValidationChanged(bool valido, bool completo)
    {
        Log($"[Deck] Grid ok? valido={valido} completo={completo}");
        if (!completo) return;

        if (valido)
        {
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

        var par = Cartas[Mathf.Clamp(_nextIndex, 0, Cartas.Length - 1)];
        _nextIndex = (_nextIndex + 1) % Cartas.Length;
        return par;
    }

    AnimalPattern ResolverPattern(FacePair par)
    {
        var reg = FindOne<PatternRegistry>();
        if (reg == null || reg.Patterns == null || reg.Patterns.Count == 0) return null;

        if (par.Frente != null)
        {
            var bySprite = reg.Patterns.FirstOrDefault(p => p && p.CardSprite == par.Frente);
            if (bySprite) return bySprite;
        }

        string id = (par.Id ?? "").Trim().ToLowerInvariant();
        if (id.Length > 0)
        {
            var byIdExact = reg.Patterns.FirstOrDefault(p =>
                p && (p.name.Trim().ToLowerInvariant() == id ||
                      (p.CardSprite && p.CardSprite.name.Trim().ToLowerInvariant() == id)));
            if (byIdExact) return byIdExact;

            var byContains = reg.Patterns.FirstOrDefault(p =>
                p && (p.name.ToLowerInvariant().Contains(id) ||
                      (p.CardSprite && p.CardSprite.name.ToLowerInvariant().Contains(id))));
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
        if (Cartas == null || Cartas.Length == 0) { LogErr("Cartas vazio."); return false; }

        for (int i = 0; i < Cartas.Length; i++)
        {
            if (Cartas[i].Frente == null) { LogErr($"Cartas[{i}] Frente NULL ({Cartas[i].Id})"); return false; }
            if (Cartas[i].Verso  == null) { LogErr($"Cartas[{i}] Verso  NULL ({Cartas[i].Id})");  return false; }
        }
        return true;
    }

    void Log(string m)     { if (debugLogs) Debug.Log($"[DeckController] {m}", this); }
    void LogWarn(string m) { if (debugLogs) Debug.LogWarning($"[DeckController] {m}", this); }
    void LogErr(string m)  { Debug.LogError($"[DeckController] {m}", this); }

    static T FindOne<T>() where T : UnityEngine.Object
    {
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<T>();
#else
        return UnityEngine.Object.FindObjectOfType<T>();
#endif
    }
}
