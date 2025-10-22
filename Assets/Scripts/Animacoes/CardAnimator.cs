// CardAnimator.cs — Anima visualmente a compra de uma carta do deck:
// 1) cria um clone visual da carta no fxLayer (fora de LayoutGroups),
// 2) move do deck para o centro, faz um "flip" de verso→frente e um "pop",
// 3) desloca do centro para a posição de preview e, no fim, atualiza a imagem do preview.
// O foco é puramente visual: sem alterar estados de jogo, apenas animações UI.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardAnimator : MonoBehaviour
{
    [Header("Layer para clones (sem LayoutGroups)")]
    public RectTransform fxLayer;

    [Header("Timings")]
    public float durMoverParaCentro = 0.50f;
    public float durFlipNoCentro    = 0.18f;
    public float durPopNoCentro     = 0.20f;
    public float durMoverParaPreview= 0.45f;

    [Header("Easing / Visual")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0,1,1);
    public float popScale = 1.12f;
    public float minFlipScaleX = 0.06f;
    public bool usarDeltaUnscaled = true;

    [Header("Debug")]
    public bool debugLogs = true;

    Camera _uiCam;

    // Descobre a câmara correta para coordenadas UI (caso ScreenSpaceCamera/WorldSpace)
    // e loga a layer de FX configurada.
    void Awake()
    {
        var canvas = GetComponentInParent<Canvas>();
        _uiCam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        if (debugLogs) Debug.Log($"[CardAnimator] Awake. fxLayer={(fxLayer!=null ? fxLayer.name : "NULL")}", this);
    }

    // Ponto de entrada da animação. Valida referências essenciais e arranca a rotina.
    // deckBack: imagem do verso no deck; posCentro: âncora para o centro; posPreview: alvo final;
    // frente/verso: sprites a usar no flip; imgPreview: opcional para receber o sprite final;
    // onFinish: callback no fim da animação completa.
    public void Play(Image deckBack, RectTransform posCentro, RectTransform posPreview,
                     Sprite frente, Sprite verso, Image imgPreview = null,
                     Action onFinish = null)
    {
        if (deckBack == null || posCentro == null || posPreview == null || fxLayer == null)
        {
            Debug.LogError("[CardAnimator] Referências em falta.", this);
            return;
        }
        StartCoroutine(CoPlay(deckBack, posCentro, posPreview, frente, verso, imgPreview, onFinish));
    }

    // Rotina principal: constrói o clone UI e executa as 4 fases (mover→centro, flip, pop, centro→preview).
    // No final, atualiza o preview (se existir) ou reparenta o clone para o transform de preview.
    IEnumerator CoPlay(Image deckBack, RectTransform posCentro, RectTransform posPreview,
                       Sprite frente, Sprite verso, Image imgPreview, Action onFinish)
    {
        long mem0 = GC.GetTotalMemory(false);

        // Criação do clone visual (fora de LayoutGroups)
        var go = new GameObject("CartaClone_UI", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(LayoutElement));
        var rt = go.GetComponent<RectTransform>();
        var img = go.GetComponent<Image>();
        var cg  = go.GetComponent<CanvasGroup>();
        var le  = go.GetComponent<LayoutElement>();

        rt.SetParent(fxLayer, worldPositionStays:false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = deckBack.rectTransform.rect.size;
        img.preserveAspect = true; img.raycastTarget = false;
        if (le) le.ignoreLayout = true;
        if (cg) { cg.blocksRaycasts = false; cg.interactable = false; }
        rt.SetAsLastSibling();

        img.sprite = verso;

        // Posições (convertidas para o espaço do fxLayer)
        Vector2 p0   = WorldToLocal(fxLayer, deckBack.rectTransform.position, _uiCam);
        Vector2 pMid = WorldToLocal(fxLayer, posCentro.position, _uiCam);
        Vector2 pEnd = WorldToLocal(fxLayer, posPreview.position, _uiCam);

        // A) mover → centro (interpolação posição)
        rt.anchoredPosition = p0;
        rt.localScale = Vector3.one;
        if (debugLogs) Debug.Log("[CardAnimator] Fase A (mover→centro)", this);
        yield return Run(durMoverParaCentro, t =>
        {
            float k = ease.Evaluate(t);
            rt.anchoredPosition = Vector2.LerpUnclamped(p0, pMid, k);
        });

        // B) flip parado no centro (scale X até mínimo, troca sprite, scale X de volta)
        if (debugLogs) Debug.Log("[CardAnimator] Fase B (flip no centro)", this);
        yield return Run(durFlipNoCentro, t =>
        {
            if (t < 0.5f)
            {
                float x = Mathf.Lerp(1f, minFlipScaleX, t * 2f);
                rt.localScale = new Vector3(x, 1f, 1f);
            }
            else
            {
                if (img.sprite != frente) img.sprite = frente;
                float x = Mathf.Lerp(minFlipScaleX, 1f, (t - 0.5f) * 2f);
                rt.localScale = new Vector3(x, 1f, 1f);
            }
        });

        // C) pop (pequeno overshoot de escala para dar impacto visual)
        if (debugLogs) Debug.Log("[CardAnimator] Fase C (pop)", this);
        Vector3 s0 = Vector3.one, s1 = Vector3.one * Mathf.Max(1f, popScale);
        yield return Run(durPopNoCentro, t =>
        {
            float up = t <= 0.5f ? t * 2f : (1f - (t - 0.5f) * 2f);
            rt.localScale = Vector3.LerpUnclamped(s0, s1, up);
        });

        // D) centro → preview (interpolação posição até ao alvo de preview)
        if (debugLogs) Debug.Log("[CardAnimator] Fase D (centro→preview)", this);
        yield return Run(durMoverParaPreview, t =>
        {
            float k = ease.Evaluate(t);
            rt.anchoredPosition = Vector2.LerpUnclamped(pMid, pEnd, k);
        });

        // Finalização: aplica sprite no preview (se existir) ou fixa o clone no alvo.
        if (imgPreview != null)
        {
            if (imgPreview.canvasRenderer != null) imgPreview.canvasRenderer.SetAlpha(1f);
            imgPreview.sprite = frente;
            Destroy(rt.gameObject);
        }
        else
        {
            rt.SetParent(posPreview, worldPositionStays:false);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        long mem1 = GC.GetTotalMemory(false);
        if (debugLogs) Debug.Log($"[CardAnimator] GC Δ={(mem1 - mem0 >= 0 ? "+" : "")}{mem1 - mem0} bytes", this);

        onFinish?.Invoke();
    }

    // Pequeno utilitário de timeline: corre uma interpolação de 0→1 em 'dur' chamando 'step(t)'.
    // Respeita Time.unscaledDeltaTime se 'usarDeltaUnscaled' estiver ativo.
    IEnumerator Run(float dur, Action<float> step)
    {
        float t = 0f;
        while (t < 1f)
        {
            float dt = usarDeltaUnscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            t = Mathf.Min(1f, t + (Mathf.Approximately(dur, 0f) ? 1f : dt / dur));
            step?.Invoke(t);
            yield return null;
        }
    }

    // Converte uma posição em world (ou screen, dependendo do canvas) para coords locais do parent.
    static Vector2 WorldToLocal(RectTransform parent, Vector3 worldPos, Camera cam)
    {
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, cam, out var local);
        return local;
    }
}
