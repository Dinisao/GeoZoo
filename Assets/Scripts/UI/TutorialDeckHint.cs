using UnityEngine;
using UnityEngine.UI;

public class TutorialDeckHint : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Seta cheia (sprite azul)")]
    public RectTransform SetaBase;

    [Tooltip("Glow em outline (sprite verde da seta)")]
    public RectTransform SetaGlow;

    [Tooltip("Texto tipo 'Click on the deck to get started'")]
    public RectTransform Texto;

    [Tooltip("Image que escurece o resto do ecrã")]
    public Image FundoEscurecer;

    [Header("Movimento da seta")]
    [Tooltip("Direção do vai-e-vem em ecrã. (-1,1) = diagonal para cima-esquerda")]
    public Vector2 DirecaoMovimento = new Vector2(-1f, 1f);

    [Tooltip("Amplitude em píxeis do movimento")]
    public float AmplitudePixels = 12f;

    [Tooltip("Velocidade da oscilação (Hz aproximado)")]
    public float VelocidadeMovimento = 2f;

    [Header("Pulse do glow (seta e deck)")]
    [Range(0f, 1f)]
    public float GlowAlphaMin = 0.2f;

    [Range(0f, 1f)]
    public float GlowAlphaMax = 1f;

    [Tooltip("Velocidade do pulso do glow")]
    public float VelocidadeGlow = 3f;

    [Header("Pulse do texto (escala)")]
    public float TextoScaleMin = 0.95f;
    public float TextoScaleMax = 1.05f;
    public float VelocidadeTexto = 2f;

    [Header("Deck highlight (opcional)")]
    [Tooltip("RectTransform do deck real (IMG_DeckBack em UI_Deck)")]
    public RectTransform DeckSource;

    [Tooltip("Imagem clara por cima do fundo escuro")]
    public RectTransform DeckBase;

    [Tooltip("Glow/contorno à volta do deck")]
    public RectTransform DeckGlow;

    // --- Estado interno ---
    Vector2 _setaBasePosInicial;
    Vector2 _setaGlowPosInicial;
    Vector3 _textoScaleInicial;

    RectTransform _root;
    Image _setaGlowImg;
    Image _deckGlowImg;

    void Awake()
    {
        Debug.Log("[TutorialDeckHint] Awake em " + gameObject.name, this);

        _root = transform as RectTransform;

        if (SetaBase != null)
            _setaBasePosInicial = SetaBase.anchoredPosition;

        if (SetaGlow != null)
        {
            _setaGlowPosInicial = SetaGlow.anchoredPosition;
            _setaGlowImg = SetaGlow.GetComponent<Image>();
        }

        _textoScaleInicial = Texto != null ? Texto.localScale : Vector3.one;

        if (DirecaoMovimento.sqrMagnitude < 0.0001f)
            DirecaoMovimento = new Vector2(-1f, 1f);

        DirecaoMovimento = DirecaoMovimento.normalized;

        if (DeckGlow != null)
            _deckGlowImg = DeckGlow.GetComponent<Image>();

        // --- Alinhar highlight do deck com o deck real ---
        if (_root != null && DeckSource != null)
        {
            // posição
            Vector3 worldPos = DeckSource.position;
            Vector2 localPos = _root.InverseTransformPoint(worldPos);

            if (DeckBase != null)
            {
                DeckBase.anchoredPosition = localPos;
                DeckBase.sizeDelta = DeckSource.rect.size;

                var srcImg = DeckSource.GetComponent<Image>();
                var dstImg = DeckBase.GetComponent<Image>();
                if (srcImg != null && dstImg != null)
                {
                    dstImg.sprite = srcImg.sprite;      // copia o verso do deck
                    dstImg.preserveAspect = true;
                }
            }

            if (DeckGlow != null)
            {
                DeckGlow.anchoredPosition = localPos;
                DeckGlow.sizeDelta = DeckSource.rect.size;
            }
        }

        // Garantir ordem: FundoEscuro no fundo, resto por cima
        if (FundoEscurecer != null)
        {
            var fRt = FundoEscurecer.transform as RectTransform;
            fRt.SetAsFirstSibling();
        }
        if (DeckBase != null) DeckBase.SetAsLastSibling();
        if (DeckGlow != null) DeckGlow.SetAsLastSibling();
        if (SetaBase != null) SetaBase.SetAsLastSibling();
        if (SetaGlow != null) SetaGlow.SetAsLastSibling();
        if (Texto != null) Texto.SetAsLastSibling();
    }

    void Update()
    {
        float t = Time.unscaledTime;

        // --- Movimento da seta (base + glow) ---
        float desloc = Mathf.Sin(t * VelocidadeMovimento) * AmplitudePixels;
        Vector2 offset = DirecaoMovimento * desloc;

        if (SetaBase != null)
            SetaBase.anchoredPosition = _setaBasePosInicial + offset;

        if (SetaGlow != null)
            SetaGlow.anchoredPosition = _setaGlowPosInicial + offset;

        // --- Pulse do glow (seta) ---
        float faseGlow = 0.5f + 0.5f * Mathf.Sin(t * VelocidadeGlow);
        float aGlow = Mathf.Lerp(GlowAlphaMin, GlowAlphaMax, faseGlow);

        if (_setaGlowImg != null)
        {
            Color c = _setaGlowImg.color;
            c.a = aGlow;
            _setaGlowImg.color = c;
        }

        // --- Pulse do glow do deck (usa o mesmo ritmo da seta) ---
        if (_deckGlowImg != null)
        {
            Color c = _deckGlowImg.color;
            c.a = aGlow;
            _deckGlowImg.color = c;
        }

        // --- Pulse do texto (escala) ---
        if (Texto != null)
        {
            float faseTexto = 0.5f + 0.5f * Mathf.Sin(t * VelocidadeTexto);
            float s = Mathf.Lerp(TextoScaleMin, TextoScaleMax, faseTexto);
            Texto.localScale = _textoScaleInicial * s;
        }
    }

    /// <summary>
    /// Chamado no OnClick do deck. Desliga o tutorial imediatamente.
    /// </summary>
    public void FecharHint()
    {
        Debug.Log("[TutorialDeckHint] FecharHint chamado → desligar UI_Tutorial_Deck", this);
        gameObject.SetActive(false);
    }
}
