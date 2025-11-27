using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TimerDialRingUI : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Image do ARO (o PNG com os animais, por cima do fill).")]
    public Image RingImage;   // Aro com os animais

    [Tooltip("Image do preenchimento interior (criado ou reaproveitado como irmão atrás do aro).")]
    public Image FillImage;   // Círculo interior atrás do aro

    [Header("Layout do Fill")]
    [Range(0f, 0.5f)]
    public float InnerInsetPercent = 0.16f;

    [Min(64)]
    public int GeneratedResolution = 512;

    [Tooltip("Se true, NÃO altero o RectTransform do Fill por código (usas o layout manual do Inspector).")]
    public bool UsarLayoutManualDoFill = false;

    [Header("Direção do radial")]
    [Tooltip("Quando true, a borda radial do Fill é Clockwise.")]
    public bool fillClockwise = false;

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
    [Range(0f, 1f)]
    public float OffDutyMin = 0.30f;

    [Tooltip("Percentagem do tempo 'OFF' por ciclo perto do 0s (0..1).")]
    [Range(0f, 1f)]
    public float OffDutyMax = 0.55f;

    [Tooltip("Opacidade quando 'OFF' (0 = invisível, 0.1 = semi).")]
    [Range(0f, 1f)]
    public float AlphaOffBlink = 0.0f;

    [Tooltip("Opacidade quando 'ON'.")]
    [Range(0f, 1f)]
    public float AlphaOnBlink = 1.0f;

    [Tooltip("Quando a piscar, força a cor a ser o extremo vermelho do gradiente (mais claro visualmente).")]
    public bool CongelarVermelhoDurantePiscar = true;

    [Header("Timing/Responsividade")]
    public bool usarUnscaledTime = true;
    public bool autoFitOnResize = true;

    // -----------------------------
    // PONTEIRO
    // -----------------------------
    [Header("Ponteiro (opcional)")]
    [Tooltip("RectTransform da imagem do ponteiro (TimerPointer).")]
    public RectTransform Ponteiro;

    [Tooltip("RectTransform que VAI RODAR (TimerPointerPivot). Se vazio, roda o próprio Ponteiro.")]
    public RectTransform PonteiroPivot;

    [Tooltip("Offset extra em graus aplicado ao ponteiro (caso precises de um ajuste fino).")]
    [Range(-180f, 180f)]
    public float PonteiroOffsetGraus = 0f;

    [Tooltip("Se ligado, o ponteiro roda no sentido contrário ao default.")]
    public bool InverterSentidoPonteiro = false;

    // ============================
    //  Campos internos
    // ============================

    RectTransform _ringRT;
    RectTransform _fillRT;
    RectTransform _parentRT;
    float _lastW;
    float _lastH;

    // ângulo base do rect que vamos rodar (pivot ou ponteiro) tal como está NA SCENE
    RectTransform _rtQueRoda;
    float _ponteiroBaseAngulo;

    float TempoAgora => usarUnscaledTime ? Time.unscaledTime : Time.time;

    // ============================
    //  Ciclo de vida
    // ============================

    void Reset()
    {
        RingImage = GetComponent<Image>();
    }

    void Awake()
    {
        if (!RingImage)
            RingImage = GetComponent<Image>();

        if (!RingImage)
        {
            Debug.LogError("TimerDialRingUI: coloca este script no GO do aro (com Image).");
            enabled = false;
            return;
        }

        _ringRT   = RingImage.rectTransform;
        _parentRT = _ringRT.parent as RectTransform;

        // -------------------------
        // FILL
        // -------------------------
        if (FillImage != null && UsarLayoutManualDoFill)
        {
            // Usa exatamente o layout que tens no Inspector
            _fillRT = FillImage.rectTransform;
        }
        else
        {
            // Se não vier nada ligado no inspector, tenta encontrar um irmão chamado 'TimerFill'
            if (!FillImage && _parentRT)
            {
                Transform t = _parentRT.Find("TimerFill");
                if (t)
                    FillImage = t.GetComponent<Image>();
            }

            // Criar ou configurar o Fill como IRMÃO atrás do Ring
            if (!FillImage)
            {
                GameObject go = new GameObject("TimerFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                _fillRT = go.GetComponent<RectTransform>();
                _fillRT.SetParent(_parentRT ?? _ringRT.parent, false);

                CopyLayoutFromRing();

                FillImage = go.GetComponent<Image>();
                FillImage.sprite = CreateDisc(GeneratedResolution);
            }
            else
            {
                _fillRT = FillImage.rectTransform;

                // Se o Fill estiver como filho do Ring, sobe um nível para ser irmão
                if (_fillRT.parent == _ringRT && _parentRT != null)
                {
                    _fillRT.SetParent(_parentRT, false);
                    CopyLayoutFromRing();
                }
            }
        }

        // Se vier sem sprite (caso de layout manual), cria o disco branco
        if (FillImage != null && FillImage.sprite == null)
        {
            FillImage.sprite = CreateDisc(GeneratedResolution);
        }

        ConfigurarFillImage();

        // 🎯 ATENÇÃO: REMOVEMOS O CÓDIGO QUE MEXIA NA HIERARQUIA
        // (nada de SetSiblingIndex aqui)

        // Gradiente default se não houver (verde → amarelo → laranja → vermelho)
        if (CoresPorProgresso == null || CoresPorProgresso.colorKeys == null || CoresPorProgresso.colorKeys.Length == 0)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.10f, 0.85f, 0.25f), 1f),   // verde
                    new GradientColorKey(new Color(1.00f, 0.90f, 0.10f), 0.6f), // amarelo
                    new GradientColorKey(new Color(1.00f, 0.55f, 0.00f), 0.3f), // laranja
                    new GradientColorKey(new Color(1.00f, 0.20f, 0.15f), 0f),   // vermelho
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            CoresPorProgresso = g;
        }

        // Só auto-ajusta o Fill se não estiveres em modo manual
        if (!UsarLayoutManualDoFill)
        {
            FitFillToRing(true);
        }

        // ------------------------------
        // Guardar a referência de quem vai rodar (pivot se existir)
        // ------------------------------
        _rtQueRoda = PonteiroPivot != null ? PonteiroPivot : Ponteiro;

        if (_rtQueRoda != null)
        {
            _ponteiroBaseAngulo = _rtQueRoda.localEulerAngles.z;
        }
    }

    void OnRectTransformDimensionsChange()
    {
        if (!autoFitOnResize || UsarLayoutManualDoFill || _ringRT == null || _fillRT == null)
            return;

        Rect r = _ringRT.rect;
        if (!Mathf.Approximately(r.width, _lastW) || !Mathf.Approximately(r.height, _lastH))
        {
            FitFillToRing(false);
        }
    }

    void Update()
    {
        if (ControladorJogo.Instancia == null || FillImage == null)
            return;

        // Progresso01 = fração de tempo RESTANTE (1 no início, 0 no fim)
        float progresso     = ControladorJogo.Instancia.Progresso01;
        float tempoRestante = ControladorJogo.Instancia.TempoRestanteF;

        AtualizarFillVisual(progresso, tempoRestante);
        AtualizarPonteiro(progresso);
    }

    void OnValidate()
    {
#if UNITY_EDITOR
        if (FillImage != null)
        {
            ConfigurarFillImage();
        }
#endif
    }

    // ============================
    //  Layout helpers
    // ============================

    void CopyLayoutFromRing()
    {
        if (_ringRT == null || _fillRT == null)
            return;

        _fillRT.anchorMin        = _ringRT.anchorMin;
        _fillRT.anchorMax        = _ringRT.anchorMax;
        _fillRT.pivot            = _ringRT.pivot;
        _fillRT.anchoredPosition = _ringRT.anchoredPosition;
        _fillRT.sizeDelta        = _ringRT.sizeDelta;
        _fillRT.localRotation    = Quaternion.identity;
        _fillRT.localScale       = Vector3.one;
    }

    void FitFillToRing(bool regenSprite)
    {
        if (_ringRT == null || FillImage == null)
            return;

        if (_fillRT == null)
            _fillRT = FillImage.rectTransform;

        Rect rect    = _ringRT.rect;
        float minSide = Mathf.Min(rect.width, rect.height);

        // Diâmetro interior com margem definida por InnerInsetPercent
        float innerDiameter = minSide * (1f - 2f * Mathf.Clamp01(InnerInsetPercent));

        // Pequeno shrink extra para evitar borda branca
        innerDiameter = Mathf.Max(0f, innerDiameter - 2f);

        // Aplicar tamanho ao fill
        _fillRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerDiameter);
        _fillRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   innerDiameter);
        _fillRT.anchoredPosition = _ringRT.anchoredPosition;

        if (regenSprite)
        {
            FillImage.sprite = CreateDisc(GeneratedResolution);
        }

        ConfigurarFillImage();

        _lastW = rect.width;
        _lastH = rect.height;
    }

    void ConfigurarFillImage()
    {
        if (FillImage == null)
            return;

        FillImage.type          = Image.Type.Filled;
        FillImage.fillMethod    = Image.FillMethod.Radial360;
        FillImage.fillOrigin    = (int)Image.Origin360.Top;
        FillImage.fillClockwise = fillClockwise;
        FillImage.raycastTarget = false;
    }

    // ============================
    //  Visual do fill
    // ============================

    void AtualizarFillVisual(float progresso, float tempoRestante)
    {
        progresso = Mathf.Clamp01(progresso);

        // Fração de tempo JÁ CONSUMIDA (0 no início, 1 no fim)
        float fracConsumida = 1f - progresso;

        // Fill = RASTO do ponteiro (vai enchendo com o tempo).
        FillImage.fillAmount = fracConsumida;

        // A cor continua baseada no tempo RESTANTE:
        //  início (progresso≈1) → verde ; fim (progresso≈0) → vermelho
        Color cor = (CoresPorProgresso != null)
            ? CoresPorProgresso.Evaluate(progresso)
            : Color.white;

        float alphaExtra = 1f;

        // Modo piscar
        if (LimiarPiscarSeg > 0f && tempoRestante <= LimiarPiscarSeg)
        {
            float t       = Mathf.InverseLerp(LimiarPiscarSeg, 0f, tempoRestante); // 0..1
            float freq    = Mathf.Lerp(FreqPiscarMin, FreqPiscarMax, t);
            float dutyOff = Mathf.Lerp(OffDutyMin,      OffDutyMax, t);

            float periodo = 1f / Mathf.Max(0.0001f, freq);
            float fase    = (TempoAgora % periodo) / periodo; // 0..1

            bool emOff    = fase < dutyOff;

            alphaExtra    = emOff ? AlphaOffBlink : AlphaOnBlink;

            if (CongelarVermelhoDurantePiscar && CoresPorProgresso != null)
            {
                // Força a cor para o extremo vermelho (tempo ~0)
                cor = CoresPorProgresso.Evaluate(0f);
            }
        }

        cor.a *= alphaExtra;
        FillImage.color = cor;
    }

    // ============================
    //  Ponteiro
    // ============================

    void AtualizarPonteiro(float progresso)
    {
        if (_rtQueRoda == null)
            return;

        progresso = Mathf.Clamp01(progresso);

        // MESMA fração consumida que o Fill usa
        float fracConsumida = 1f - progresso;

        // Em UI, rotação Z positiva é CCW.
        // Agora o ponteiro segue a MESMA convenção do Fill:
        //  fillClockwise = false  -> CCW
        //  fillClockwise = true   -> CW
        float dir = fillClockwise ? -1f : 1f;

        if (InverterSentidoPonteiro)
            dir *= -1f;

        // Usa a rotação de base que tu definiste na Scene + offset opcional
        float ang = _ponteiroBaseAngulo + PonteiroOffsetGraus + fracConsumida * 360f * dir;

        Vector3 e = _rtQueRoda.localEulerAngles;
        e.z = ang;
        _rtQueRoda.localEulerAngles = e;
    }

    // ============================
    //  Sprite utilitário (disco simples branco)
    // ============================

    Sprite CreateDisc(int size)
    {
        size = Mathf.Clamp(size, 8, 4096);

        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.name      = "TimerDisc_" + size;
        tex.wrapMode  = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float r  = Mathf.Min(cx, cy) - 1f;
        Color col = new Color(1f, 1f, 1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, d <= r ? col : Color.clear);
            }
        }

        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
