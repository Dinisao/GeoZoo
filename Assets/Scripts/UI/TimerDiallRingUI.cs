using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TimerDialRingUI : MonoBehaviour
{
    [Header("Referências")]
    public Image RingImage;   // Image do ARO (o teu PNG com os animais)
    public Image FillImage;   // Criado/reaproveitado como IRMÃO atrás do Ring

    [Header("Layout do Fill")]
    [Range(0f, 0.5f)] public float InnerInsetPercent = 0.16f;
    [Min(64)] public int GeneratedResolution = 512;

    [Header("Direção do radial")]
    [Tooltip("Quando true, a borda radial do Fill é Clockwise. Com fillAmount a descer, isto dá movimento CCW (contra-relógio).")]
    public bool fillClockwise = true;

    [Header("Cores acima do limiar (gradiente verde→vermelho)")]
    public Gradient CoresPorProgresso;

    [Header("Piscar abaixo do limiar")]
    [Tooltip("Quando tempo <= este valor (em s), o preenchimento entra em modo de piscar.")]
    public float LimiarPiscarSeg = 20f;

    [Tooltip("Frequência mínima do piscar (Hz) ao entrar no limiar.")]
    public float FreqPiscarMin = 1.2f;

    [Tooltip("Frequência máxima do piscar (Hz) junto ao 0s).")]
    public float FreqPiscarMax = 3.5f;

    [Tooltip("Percentagem do tempo 'OFF' por ciclo no início do limiar (0..1).")]
    [Range(0f,1f)] public float OffDutyMin = 0.30f;

    [Tooltip("Percentagem do tempo 'OFF' por ciclo perto do 0s (0..1).")]
    [Range(0f,1f)] public float OffDutyMax = 0.55f;

    [Tooltip("Opacidade quando 'OFF' (0 = invisível, 0.1 = semi).")]
    [Range(0f,1f)] public float AlphaOffBlink = 0.0f;

    [Tooltip("Opacidade quando 'ON'.")]
    [Range(0f,1f)] public float AlphaOnBlink = 1.0f;

    [Tooltip("Quando a piscar, força a cor a ser o extremo vermelho do gradiente (mais claro visualmente).")]
    public bool CongelarVermelhoDurantePiscar = true;

    [Header("Timing/Responsividade")]
    public bool usarUnscaledTime = true;
    public bool autoFitOnResize = true;

    RectTransform _ringRT, _fillRT, _parentRT;
    float _lastW, _lastH;

    void Reset() { RingImage = GetComponent<Image>(); }

    void Awake()
    {
        if (!RingImage) RingImage = GetComponent<Image>();
        if (!RingImage) { Debug.LogError("TimerDialRingUI: coloca o script no GO do aro (com Image)."); enabled = false; return; }

        _ringRT = RingImage.rectTransform;
        _parentRT = _ringRT.parent as RectTransform;

        // Reaproveitar "TimerFill" irmão, se existir
        if (!FillImage && _parentRT)
        {
            var exist = _parentRT.Find("TimerFill");
            if (exist) FillImage = exist.GetComponent<Image>();
        }

        // Criar/ajustar o Fill como IRMÃO atrás do aro
        if (!FillImage)
        {
            var go = new GameObject("TimerFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _fillRT = go.GetComponent<RectTransform>();
            _fillRT.SetParent(_parentRT ?? _ringRT.parent, false);
            CopyLayoutFromRing();

            FillImage = go.GetComponent<Image>();
            FillImage.sprite = CreateDisc(GeneratedResolution);
            FillImage.type = Image.Type.Filled;
            FillImage.fillMethod = Image.FillMethod.Radial360;
            FillImage.fillOrigin = (int)Image.Origin360.Top;
            FillImage.fillClockwise = fillClockwise;
            FillImage.raycastTarget = false;

            // Garantir que o Fill fica ATRÁS do Ring
            int ringIndex = _ringRT.GetSiblingIndex();
            _fillRT.SetSiblingIndex(ringIndex);
            _ringRT.SetSiblingIndex(ringIndex + 1);
        }
        else
        {
            _fillRT = FillImage.rectTransform;

            // Se o Fill for filho do Ring, mover para IRMÃO
            if (_fillRT.parent == _ringRT && _parentRT != null)
            {
                _fillRT.SetParent(_parentRT, false);
                CopyLayoutFromRing();
            }

            FillImage.type = Image.Type.Filled;
            FillImage.fillMethod = Image.FillMethod.Radial360;
            FillImage.fillOrigin = (int)Image.Origin360.Top;
            FillImage.fillClockwise = fillClockwise;

            int ringIndex = _ringRT.GetSiblingIndex();
            _fillRT.SetSiblingIndex(ringIndex);
            _ringRT.SetSiblingIndex(ringIndex + 1);
        }

        // Gradiente por defeito (verde→amarelo→laranja→vermelho)
        if (CoresPorProgresso == null || CoresPorProgresso.colorKeys.Length == 0)
        {
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.10f,0.85f,0.25f), 1f),
                    new GradientColorKey(new Color(1.00f,0.90f,0.10f), 0.60f),
                    new GradientColorKey(new Color(1.00f,0.55f,0.00f), 0.30f),
                    new GradientColorKey(new Color(1.00f,0.20f,0.15f), 0f),
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f,0f), new GradientAlphaKey(1f,1f)
                }
            );
            CoresPorProgresso = g;
        }

        FitFillToRing(true);
    }

    void Start() => FitFillToRing(true);

    void OnRectTransformDimensionsChange()
    {
        if (!autoFitOnResize || _ringRT == null || _fillRT == null) return;
        var sz = _ringRT.rect.size;
        if (Mathf.Approximately(sz.x, _lastW) && Mathf.Approximately(sz.y, _lastH)) return;
        FitFillToRing();
    }

    void CopyLayoutFromRing()
    {
        if (_fillRT == null || _ringRT == null) return;
        _fillRT.anchorMin = _ringRT.anchorMin;
        _fillRT.anchorMax = _ringRT.anchorMax;
        _fillRT.pivot     = _ringRT.pivot;
        _fillRT.anchoredPosition = _ringRT.anchoredPosition;
        _fillRT.sizeDelta = _ringRT.sizeDelta;
        _fillRT.localScale = _ringRT.localScale;
        _fillRT.localRotation = _ringRT.localRotation;
    }

    void FitFillToRing(bool force = false)
    {
        if (_ringRT == null || _fillRT == null) return;
        var size = _ringRT.rect.size;
        _lastW = size.x; _lastH = size.y;

        float inset = Mathf.Clamp01(InnerInsetPercent);
        float k = Mathf.Clamp01(1f - inset * 2f);
        _fillRT.sizeDelta = new Vector2(size.x * k, size.y * k);
        _fillRT.anchoredPosition = _ringRT.anchoredPosition;

        if (force && (FillImage.sprite == null || FillImage.sprite.texture.width != GeneratedResolution))
            FillImage.sprite = CreateDisc(GeneratedResolution);
    }

    void Update()
    {
        var cj = ControladorJogo.Instancia;
        if (cj == null || FillImage == null) return;

        // 1) Fill radial (0..1) — mantém CCW porque fillAmount desce e fillClockwise = true
        float t = cj.Progresso01;     // 1 = cheio (cap), 0 = vazio
        FillImage.fillAmount = t;

        // 2) Cor acima do limiar → gradiente normal
        float tempo = cj.TempoRestanteF;
        float lim   = Mathf.Max(0.01f, LimiarPiscarSeg);
        if (tempo > lim)
        {
            var cor = CoresPorProgresso.Evaluate(t);
            FillImage.color = new Color(cor.r, cor.g, cor.b, 1f);
            return;
        }

        // 3) Abaixo do limiar → BLINK nítido (ON/OFF)
        // Urgência: 0 (acabou de entrar no limiar) → 1 (quase 0s)
        float u = 1f - Mathf.Clamp01(tempo / lim);

        // Frequência e duty OFF interpolados dentro de limites legíveis
        float freq = Mathf.Lerp(FreqPiscarMin, FreqPiscarMax, u);
        freq = Mathf.Clamp(freq, 0.2f, 6f); // hard clamp para segurança

        float offDuty = Mathf.Lerp(OffDutyMin, OffDutyMax, u);
        offDuty = Mathf.Clamp01(offDuty);

        float period = 1f / Mathf.Max(0.01f, freq);
        float now    = usarUnscaledTime ? Time.unscaledTime : Time.time;
        float phase  = Mathf.Repeat(now, period);     // 0..period
        float onTime = period * (1f - offDuty);       // tempo "aceso"

        bool on = (phase < onTime);

        // Cor base durante o blink
        Color baseColor = CongelarVermelhoDurantePiscar
            ? CoresPorProgresso.Evaluate(0f) // extremo vermelho do gradiente
            : CoresPorProgresso.Evaluate(t); // mantém gradiente

        float alpha = on ? AlphaOnBlink : AlphaOffBlink;
        FillImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
    }

    // Disco branco procedural para o fill
    Sprite CreateDisc(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float cx = (size - 1) * 0.5f, cy = (size - 1) * 0.5f;
        float r = Mathf.Min(cx, cy) - 1f;
        var col = new Color(1,1,1,1);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx, dy = y - cy;
            float d = Mathf.Sqrt(dx*dx + dy*dy);
            tex.SetPixel(x, y, d <= r ? col : Color.clear);
        }
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0,0,size,size), new Vector2(0.5f,0.5f), 100f);
    }
}
