// GameOverUI_Simple — Overlay de Game Over com mensagem dinâmica,
// fade + escurecer do fundo e entrada do balão com "queda + bounce + wobble".
//
// Coloca os botões Retry/Quit como filhos do ImgFundo (imagens sem texto)
// e usa GameOverButtons.cs para as ações.
//
// Requer: TextMeshPro

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class GameOverUI_Simple : MonoBehaviour
{
    [Header("Overlay")]
    public CanvasGroup Overlay;      // CanvasGroup no próprio UI_GameOver
    public Image FundoEscuro;        // Image preto translúcido que cobre o ecrã
    [Range(0f,1f)] public float IntensidadeEscuro = 0.60f; // alpha base

    [Header("Efeito de Escurecer (fade-in)")]
    public bool AnimarEscurecer = true;
    [Range(0f,1f)] public float EscurecerAlvo = 0.85f;
    public float DurEscurecer = 0.35f;
    public AnimationCurve CurvaEscurecer = null;  // se null → EaseInOut

    [Header("Balão (fundo principal)")]
    public Image ImgFundo;                 // Image do balão (a tua Imagem 1)
    public List<Sprite> FundosPorZoo = new();
    public bool UsarUltimoSpriteSeUltrapassar = true;
    public bool PreservarAspetoFundo = true;
    public Vector2 TamanhoFundo = new Vector2(980, 640);

    [Header("Mensagem")]
    [TextArea(1,2)] public string FormatoMensagem = "Congratulations, you've made {0} animal{1}!";
    public TMP_Text TxtMensagem;           // filho do ImgFundo
    public TMP_FontAsset FonteMensagem;    // TMP Font Asset (a tua fonte)
    [Range(18,120)] public int TamanhoFonteMensagem = 44;
    public Color CorMensagem = Color.black;
    public Vector2 MargemMensagem = new Vector2(60, 80);

    [Header("Entrada do Balão (queda + bounce)")]
    public bool AnimarEntradaBalao = true;
    public float DropAltura = 420f;                // px acima do alvo
    public float DropDuracao = 0.35f;              // tempo da queda
    public AnimationCurve DropCurva = null;        // se null → EaseIn (mais “gravidade”)
    [Tooltip("Intensidade do 'esmagar' inicial (0.12 = 12%).")]
    [Range(0f,0.4f)] public float BounceImpactScale = 0.12f;
    [Tooltip("Número de bounces após o impacto.")]
    [Range(0,5)] public int BounceRepeticoes = 2;
    [Tooltip("Quanto o bounce decai a cada oscilação (0.6 = 60% do anterior).")]
    [Range(0.3f,0.9f)] public float BounceDamping = 0.65f;
    public float BounceDuracao = 0.12f;            // duração de cada meia-osc.
    [Header("Wobble (abanico)")]
    [Tooltip("Ângulo máximo inicial em graus.")]
    [Range(0f,15f)] public float WobbleAngulo = 6f;
    public float WobbleDuracao = 0.9f;
    [Range(0.3f,1.0f)] public float WobbleDamping = 0.6f;

    [Header("Preview (Editor)")]
    public bool PreviewAtivo = false;
    [Range(0,50)] public int PreviewZoo = 4;

    [Header("Debug")]
    public bool LogDebug = false;

    RectTransform _fundoRT;
    bool _shown;

#if UNITY_EDITOR
    bool _editorRefreshPending;
#endif

    // ===== Refs =====
    void EnsureRefs(bool allowCreate)
    {
        if (!Overlay) { Overlay = GetComponent<CanvasGroup>(); if (!Overlay && allowCreate) Overlay = gameObject.AddComponent<CanvasGroup>(); }
        if (!FundoEscuro) { FundoEscuro = GetComponent<Image>(); if (!FundoEscuro && allowCreate) FundoEscuro = gameObject.AddComponent<Image>(); }
        if (FundoEscuro) { FundoEscuro.color = new Color(0,0,0, Mathf.Clamp01(IntensidadeEscuro)); FundoEscuro.raycastTarget = true; }

        if (!ImgFundo)
        {
            var t = transform.Find("ImgFundo");
            if (t) ImgFundo = t.GetComponent<Image>();
            else if (allowCreate)
            {
                var go = new GameObject("ImgFundo", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                ImgFundo = go.GetComponent<Image>();
            }
        }
        if (ImgFundo) _fundoRT = ImgFundo.rectTransform;

        if (!TxtMensagem && ImgFundo && allowCreate)
        {
            var go = new GameObject("TxtMensagem", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(ImgFundo.transform, false);
            TxtMensagem = go.GetComponent<TextMeshProUGUI>();
            TxtMensagem.alignment = TextAlignmentOptions.Center;
            TxtMensagem.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    // ===== Lifecycle =====
    void Awake()
    {
        if (CurvaEscurecer == null)
            CurvaEscurecer = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (DropCurva == null)
            DropCurva = AnimationCurve.EaseInOut(0, 0, 1, 1); // EaseInOut por default
        // Uma queda mais “fisica” pode usar EaseInQuad:
        // DropCurva = new AnimationCurve(new Keyframe(0,0, 0, 2), new Keyframe(1,1));

        EnsureRefs(true);

        if (ImgFundo)
        {
            ImgFundo.preserveAspect = PreservarAspetoFundo;
            if (_fundoRT != null) _fundoRT.sizeDelta = TamanhoFundo;
        }

        if (Overlay)
        {
            Overlay.alpha = 0f;
            Overlay.blocksRaycasts = false;
            Overlay.interactable = false;
        }

        ApplyVisualsEditorSafe();
        SanitizeAllImages();
    }

    void Start()
    {
        TryHookControlador();
#if UNITY_EDITOR
 if (!Application.isPlaying && PreviewAtivo) EditorRefresh();
#endif
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && !_editorRefreshPending)
        {
            _editorRefreshPending = true;
            EditorApplication.delayCall += EditorRefresh;
        }
    }
    void EditorRefresh()
    {
        _editorRefreshPending = false;
        if (this == null) return;
        EnsureRefs(true);
        if (FundoEscuro) FundoEscuro.color = new Color(0,0,0, Mathf.Clamp01(IntensidadeEscuro));
        if (ImgFundo) ImgFundo.preserveAspect = PreservarAspetoFundo;
        ApplyVisualsEditorSafe();
        if (PreviewAtivo) MostrarPreviewEditor(PreviewZoo); else EsconderPreviewEditor();
        SanitizeAllImages();
    }
#endif

    void OnDestroy()
    {
        var cj = ControladorJogo.Instancia;
        if (cj != null) cj.OnTempoEsgotado -= HandleTempoEsgotado;
#if UNITY_EDITOR
        EditorApplication.delayCall -= EditorRefresh;
#endif
    }

    // ===== Hook ao ControladorJogo =====
    void TryHookControlador()
    {
        var cj = ControladorJogo.Instancia;
        if (cj != null) { cj.OnTempoEsgotado -= HandleTempoEsgotado; cj.OnTempoEsgotado += HandleTempoEsgotado; }
        else { StartCoroutine(WaitAndHook()); }
    }
    IEnumerator WaitAndHook()
    {
        yield return null;
        var cj = ControladorJogo.Instancia;
        if (cj != null) { cj.OnTempoEsgotado -= HandleTempoEsgotado; cj.OnTempoEsgotado += HandleTempoEsgotado; }
    }

    void HandleTempoEsgotado(int zoo)
    {
        if (_shown) return;
        _shown = true;

        ControladorJogo.Instancia?.DefinirInteracaoTiles(false);
        var deck = FindDeckControllerCompat(); if (deck) deck.BloquearDeck();

        AplicarFundoPorZoo(zoo);
        AtualizarMensagem(zoo);
        SanitizeAllImages();

        // Animações em paralelo
        StartCoroutine(FadeInOverlay());
        if (AnimarEntradaBalao) StartCoroutine(AnimarEntradaBalaoRoutine());
    }

    // ===== Visual/Mensagem =====
    void AplicarFundoPorZoo(int zoo)
    {
        EnsureRefs(true);
        var sprite = EscolherSpritePorZoo(zoo);
        if (!ImgFundo) return;

        if (sprite != null)
        {
            ImgFundo.sprite = sprite;
            ImgFundo.enabled = true;
            var c = ImgFundo.color; c.a = 1f; ImgFundo.color = c;

            if (PreservarAspetoFundo && sprite.rect.width > 0 && _fundoRT != null)
            {
                float aspect = sprite.rect.width / sprite.rect.height;
                _fundoRT.sizeDelta = new Vector2(TamanhoFundo.x, TamanhoFundo.x / Mathf.Max(0.001f, aspect));
            }
        }
        else
        {
            ImgFundo.sprite = null;
            ImgFundo.enabled = true;
            ImgFundo.color = new Color(1f,1f,1f,0.92f);
            if (_fundoRT != null) _fundoRT.sizeDelta = TamanhoFundo;
        }
    }
    Sprite EscolherSpritePorZoo(int zoo)
    {
        if (zoo < 0) zoo = 0;
        if (FundosPorZoo != null && zoo < FundosPorZoo.Count) return FundosPorZoo[zoo];
        if (UsarUltimoSpriteSeUltrapassar && FundosPorZoo != null && FundosPorZoo.Count > 0) return FundosPorZoo[FundosPorZoo.Count-1];
        return null;
    }

    void AtualizarMensagem(int zoo)
    {
        if (!TxtMensagem) return;
        if (FonteMensagem) TxtMensagem.font = FonteMensagem;
        TxtMensagem.fontSize = TamanhoFonteMensagem;
        TxtMensagem.color = CorMensagem;

        string plural = (zoo == 1) ? "" : "s";
        TxtMensagem.text = string.Format(FormatoMensagem, Mathf.Max(0, zoo), plural);

        if (_fundoRT)
        {
            var r = TxtMensagem.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(
                Mathf.Max(100, _fundoRT.sizeDelta.x - 2 * MargemMensagem.x),
                Mathf.Max(80,  _fundoRT.sizeDelta.y - 2 * MargemMensagem.y)
            );
            r.anchoredPosition = Vector2.zero;
        }
    }

    void ApplyVisualsEditorSafe()
    {
        AtualizarMensagem(PreviewZoo);
    }

    // ===== Fade + Escurecer =====
    IEnumerator FadeInOverlay()
    {
        if (!Overlay) yield break;

        Overlay.blocksRaycasts = true;
        Overlay.interactable  = true;

        float t = 0f;
        float dur = Mathf.Max(0.0001f, DurEscurecer);
        float alvoEscuro = AnimarEscurecer ? EscurecerAlvo : IntensidadeEscuro;
        float a0 = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = CurvaEscurecer.Evaluate(Mathf.Clamp01(t / dur));

            Overlay.alpha = k;

            if (FundoEscuro)
            {
                var c = FundoEscuro.color;
                c.a = Mathf.Lerp(a0, alvoEscuro, k);
                FundoEscuro.color = c;
            }
            yield return null;
        }

        Overlay.alpha = 1f;
        if (FundoEscuro)
        {
            var c = FundoEscuro.color;
            c.a = alvoEscuro;
            FundoEscuro.color = c;
        }
    }

    // ===== Entrada do Balão =====
    IEnumerator AnimarEntradaBalaoRoutine()
    {
        if (_fundoRT == null) yield break;

        // guardar alvo e estado inicial
        Vector2 alvo = _fundoRT.anchoredPosition;
        Vector3 alvoScale = Vector3.one;
        float alvoRotZ = 0f;

        // estado de partida (acima e esticado verticalmente)
        Vector2 startPos = alvo + new Vector2(0f, Mathf.Abs(DropAltura));
        _fundoRT.anchoredPosition = startPos;
        _fundoRT.localScale = new Vector3(0.98f, 1.02f, 1f);
        _fundoRT.localEulerAngles = new Vector3(0, 0, -Mathf.Sign(Random.value - 0.5f) * (WobbleAngulo * 0.6f));

        // QUEDA
        float t = 0f;
        float durQueda = Mathf.Max(0.0001f, DropDuracao);
        while (t < durQueda)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / durQueda);
            float e = DropCurva.Evaluate(k);      // easing da queda
            _fundoRT.anchoredPosition = Vector2.LerpUnclamped(startPos, alvo, e);

            // leve rotação durante a queda
            float rot = Mathf.Sin(e * Mathf.PI) * (WobbleAngulo * 0.25f);
            _fundoRT.localEulerAngles = new Vector3(0,0,rot);

            yield return null;
        }
        _fundoRT.anchoredPosition = alvo;

        // IMPACTO + BOUNCES (squash & stretch)
        float amp = Mathf.Clamp01(BounceImpactScale);
        int reps = Mathf.Max(0, BounceRepeticoes);

        // impacto (esmagar)
        yield return ScaleTo(new Vector3(1f + amp, 1f - amp, 1f), Mathf.Max(0.04f, BounceDuracao * 0.6f));

        // oscilações decrescentes
        for (int i = 0; i < reps; i++)
        {
            float a = amp * Mathf.Pow(BounceDamping, i + 1);
            // esticar
            yield return ScaleTo(new Vector3(1f - a * 0.7f, 1f + a * 0.7f, 1f), Mathf.Max(0.06f, BounceDuracao));
            // voltar ao 1
            yield return ScaleTo(alvoScale, Mathf.Max(0.06f, BounceDuracao));
        }

        // wobble (abanico) decrescente
        if (WobbleAngulo > 0f && WobbleDuracao > 0.01f)
            yield return Wobble(alvoRotZ, WobbleAngulo, WobbleDuracao, WobbleDamping);

        // garantir estado final exato
        _fundoRT.localScale = alvoScale;
        _fundoRT.localEulerAngles = new Vector3(0,0,alvoRotZ);
        _fundoRT.anchoredPosition = alvo;
    }

    IEnumerator ScaleTo(Vector3 target, float dur)
    {
        Vector3 from = _fundoRT.localScale;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            // EaseOutQuad
            float e = 1f - (1f - k) * (1f - k);
            _fundoRT.localScale = Vector3.LerpUnclamped(from, target, e);
            yield return null;
        }
        _fundoRT.localScale = target;
    }

    IEnumerator Wobble(float alvoRotZ, float angulo, float dur, float damping)
    {
        float t = 0f;
        float damp = Mathf.Clamp01(damping);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = t / dur;
            float a = angulo * Mathf.Pow(damp, k * 4f);           // decaimento rápido
            float rot = Mathf.Sin(k * Mathf.PI * 2f) * a;
            _fundoRT.localEulerAngles = new Vector3(0, 0, alvoRotZ + rot);
            yield return null;
        }
        _fundoRT.localEulerAngles = new Vector3(0, 0, alvoRotZ);
    }

    // ===== Compat =====
    DeckController FindDeckControllerCompat()
    {
#if UNITY_2023_1_OR_NEWER
        var d = Object.FindFirstObjectByType<DeckController>();
        if (d == null) d = Object.FindAnyObjectByType<DeckController>();
        return d;
#else
        return FindObjectOfType<DeckController>();
#endif
    }

    // ===== Preview =====
    void MostrarPreviewEditor(int previewZoo)
    {
        AplicarFundoPorZoo(Mathf.Max(0, previewZoo));
        AtualizarMensagem(previewZoo);
        if (Overlay) { Overlay.alpha = 1f; Overlay.blocksRaycasts = true; Overlay.interactable = true; }
        if (FundoEscuro)
        {
            var c = FundoEscuro.color; c.a = EscurecerAlvo; FundoEscuro.color = c;
        }
        if (_fundoRT) { _fundoRT.localScale = Vector3.one; _fundoRT.localEulerAngles = Vector3.zero; }
    }
    void EsconderPreviewEditor()
    {
        if (Overlay) { Overlay.alpha = 0f; Overlay.blocksRaycasts = false; Overlay.interactable = false; }
        if (FundoEscuro)
        {
            var c = FundoEscuro.color; c.a = IntensidadeEscuro; FundoEscuro.color = c;
        }
        if (_fundoRT) { _fundoRT.localScale = Vector3.one; _fundoRT.localEulerAngles = Vector3.zero; }
    }

    // ===== Anti “quadrados” =====
    [ContextMenu("Debug/Scan & Fix Ghost Squares")]
    void SanitizeAllImages()
    {
        if (!gameObject.activeInHierarchy) return;
        var whitelist = new HashSet<Image>();
        void Add(Image i){ if(i) whitelist.Add(i); }
        Add(FundoEscuro); Add(ImgFundo);

        var images = GetComponentsInChildren<Image>(true);
        foreach (var im in images)
        {
            if (im == null || whitelist.Contains(im)) continue;
            if (im.sprite == null)
            {
                if (LogDebug) Debug.Log($"[GameOverUI] Desativado Image sem sprite: {im.name}", im);
                im.enabled = false; im.raycastTarget = false;
                var c = im.color; c.a = 0f; im.color = c;
            }
        }
    }
}
