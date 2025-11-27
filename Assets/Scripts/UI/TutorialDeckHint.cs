using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TutorialDeckHint — tutorial multi-fase para o deck + coruja.
///
/// Fases:
///  1) Fase1_DeckHint   – seta / coruja a apontar para o deck ("Click on the deck…")
///  2) Fase2_Preview    – carta em preview ("This is the animal you must build…")
///  3) Fase3_DragHint   – arrastar peça para a grelha
///  4) Fase4_RotateHint – rodar peça com Q/E
///  5) Fase5_FlipHint   – flip com botão direito
///  6) Fase6_Final      – mensagem final ("Wonderful! Now, have a great time…")
///
/// DeckController chama:
///   - OnDeckClicked_IniciarTransicao()             quando se carrega no deck (para esconder fase 1)
///   - OnCartaChegouAoPreview(frente, posPreview)   quando a animação da carta acaba
/// </summary>
public class TutorialDeckHint : MonoBehaviour
{
    // Flag estática: controla se o próximo arranque da cena de jogo
    // deve mostrar o tutorial ou não (Retry = false, voltar do Menu = true).
    public static bool ProximoArranqueDeveMostrarTutorial = true;

    // ------------------------------------------------------
    // ESTADOS
    // ------------------------------------------------------
    enum TutorialFase
    {
        Fase1_DeckHint,
        Fase2_Preview,
        Fase3_DragHint,
        Fase4_RotateHint,
        Fase5_FlipHint,
        Fase6_Final
    }

    [Serializable]
    public class FaseUI
    {
        [Tooltip("Root desta fase (GameObject com coruja + balão + texto).")]
        public GameObject Root;

        [Tooltip("Texto desta fase (TMP_Text ou Text).")]
        public TMP_Text Texto;

        [Header("Sub-elementos opcionais")]
        [Tooltip("Coruja desta fase (RectTransform da coruja / personagem).")]
        public RectTransform CorujaRoot;

        [Tooltip("Balão + texto desta fase (RectTransform do balão).")]
        public RectTransform BalaoRoot;
    }

    [Header("Fases de UI")]
    public FaseUI Fase1;
    public FaseUI Fase2;
    public FaseUI Fase3;
    public FaseUI Fase4;
    public FaseUI Fase5;
    public FaseUI Fase6;

    [Header("Mensagens (podes substituir no Inspector)")]
    [TextArea] public string MsgDeck    = "Click on the deck to start!";
    [TextArea] public string MsgPreview = "This is the animal you must build.";
    [TextArea] public string MsgDrag    = "Try dragging a tile to the grid.";
    [TextArea] public string MsgRotate  = "Now rotate the tile using Q and E.";
    [TextArea] public string MsgFlip    = "Right click to flip the tile!";
    [TextArea] public string MsgFinal   = "Wonderful! Now, have a great time playing GEOZOO!";

    [Header("Escurecimento")]
    [Tooltip("CanvasGroup do escurecimento do fundo (FundoEscuro).")]
    public CanvasGroup Escurecedor;

    [Tooltip("Duração da animação de entrada/saída da coruja (Y).")]
    public float DuracaoSlide = 0.25f;

    [Tooltip("Offset vertical de entrada/saída da coruja (Y, em unidades de UI; negativo = para baixo).")]
    public float OffsetEntradaY = -400f;

    [Tooltip("Tempo antes de avançar automaticamente da Fase 2 para a 3.")]
    public float DelayFase3Segundos = 2.0f;

    [Header("Fase Final (auto-fecho)")]
    [Tooltip("Quanto tempo a mensagem final fica no ecrã antes do pop (segundos).")]
    public float Fase6_DelayAntesPop = 3f;

    [Tooltip("Duração da animação de 'pop' do balão/texto (segundos).")]
    public float Fase6_DuracaoPop = 0.25f;

    [Tooltip("Duração da descida da coruja abaixo do ecrã (segundos). Se <=0 usa DuracaoSlide.")]
    public float Fase6_DuracaoSlideCoruja = 0.25f;

    // ------------------------------------------------------
    // PULSE DO TEXTO
    // ------------------------------------------------------
    [Header("Pulse do Texto")]
    [Tooltip("Fator máximo de escala do texto (1.1 = cresce 10%).")]
    public float TextoPulseScale = 1.1f;

    [Tooltip("Velocidade do pulse em Hz (~2 = 2 ciclos/seg).")]
    public float TextoPulseSpeed = 2.0f;

    TMP_Text _textoAtual;
    Vector3  _textoBaseScale = Vector3.one;

    // ------------------------------------------------------
    // HIGHLIGHT DO DECK (overlay + seta)
    // ------------------------------------------------------
    [Header("Highlight do Deck (Fase 1)")]
    [Tooltip("Imagem base do highlight do deck (DeckHighlightBase).")]
    public Image DeckHighlightBase;

    [Tooltip("Glow do deck (DeckHighlightGlow).")]
    public Image DeckHighlightGlow;

    [Tooltip("Seta principal a apontar para o deck (SetaBase).")]
    public RectTransform SetaBase;

    [Tooltip("Glow da seta (SetaGlow).")]
    public RectTransform SetaGlow;

    [Tooltip("Velocidade do piscar do highlight do deck.")]
    public float DeckHighlightSpeed = 2.5f;

    [Range(0f, 1f)]
    [Tooltip("Alpha mínimo do deck highlight.")]
    public float DeckHighlightMinAlpha = 0.15f;

    [Range(0f, 1f)]
    [Tooltip("Alpha máximo do deck highlight.")]
    public float DeckHighlightMaxAlpha = 0.55f;

    [Header("Movimento da Seta (deck)")]
    [Tooltip("Amplitude do movimento vertical da seta (em píxeis).")]
    public float SetaBobAmplitude = 8f;

    [Tooltip("Velocidade do movimento vertical da seta.")]
    public float SetaBobSpeed = 2.5f;

    [Tooltip("Amplitude de rotação da seta em graus.")]
    public float SetaRotAmplitude = 5f;

    [Tooltip("Velocidade de rotação da seta.")]
    public float SetaRotSpeed = 3f;

    // Estado interno do highlight do deck
    bool    _deckHighlightAtivo;
    float   _deckBaseAlphaBase = 1f;
    float   _deckBaseAlphaGlow = 1f;
    Vector2 _setaBasePos;
    Vector2 _setaGlowPos;
    float   _setaBaseRotZ;
    float   _setaGlowRotZ;

    // ------------------------------------------------------
    // HIGHLIGHT DO PREVIEW (DOG) – SÓ NA FASE 2
    // ------------------------------------------------------
    [Header("Highlight do Preview (Fase 2)")]
    [Tooltip("Intensidade do pulse de escala (0.1 = cresce ~10%).")]
    [Range(0f, 1f)]
    public float PreviewHighlightScaleIntensity = 0.12f;

    [Tooltip("Velocidade do pulse/piscar da carta em preview.")]
    public float PreviewHighlightSpeed = 2.5f;

    [Tooltip("Intensidade do brilho extra na carta (0.4 = até ~40% mais clara).")]
    [Range(0f, 1f)]
    public float PreviewHighlightIntensity = 0.4f;

    bool          _previewHighlightAtivo;
    Image         _previewTargetImage;
    Color         _previewBaseColor = Color.white;
    RectTransform _previewTargetRect;
    Vector3       _previewBaseScale = Vector3.one;

    // ------------------------------------------------------
    // FASE 3 – MÃO DRAG/DROP
    // ------------------------------------------------------
    [Header("Fase 3 – Mão Drag/Drop")]
    [Tooltip("Root da mão de drag (sprite que se move dos tiles para a grelha).")]
    public RectTransform MaoDragRoot;

    [Tooltip("Root da mão de drop (sprite parado no centro da grelha).")]
    public RectTransform MaoDropRoot;

    [Tooltip("Distância em Y que a mão de drag percorre (positivo = para cima, negativo = para baixo).")]
    public float MaoDrag_DistY = 440f;

    [Tooltip("Duração do movimento de drag (segundos).")]
    public float MaoDrag_Duracao = 0.8f;

    [Tooltip("Pausa no fim do drag, enquanto a mão de drop pisca (segundos).")]
    public float MaoDrag_PausaNoFim = 0.8f;

    [Tooltip("Escala máxima do pulse da mão de drop (1.1 = cresce 10%).")]
    public float MaoDrop_PulseScale = 1.1f;

    [Tooltip("Velocidade do pulse da mão de drop (Hz).")]
    public float MaoDrop_PulseSpeed = 3.0f;

    // ------------------------------------------------------
    // ESTADO GERAL
    // ------------------------------------------------------
    TutorialFase _faseAtual = TutorialFase.Fase1_DeckHint;
    bool _iniciado;

    GameObject _ultimoRootAtivo;

    public bool TutorialConcluido { get; private set; }

    // Estado interno da animação da mão (Fase 3)
    Vector2   _maoDragStartPos;
    Vector3   _maoDropBaseScale = Vector3.one;
    Coroutine _coMaoFase3;

    // ------------------------------------------------------
    // LIFECYCLE
    // ------------------------------------------------------
    void Awake()
    {
        Debug.Log("[TutorialDeckHint] Awake em " + gameObject.name, this);

        if (DeckHighlightBase != null) _deckBaseAlphaBase = DeckHighlightBase.color.a;
        if (DeckHighlightGlow != null) _deckBaseAlphaGlow = DeckHighlightGlow.color.a;

        if (SetaBase != null)
        {
            _setaBasePos  = SetaBase.anchoredPosition;
            _setaBaseRotZ = SetaBase.localEulerAngles.z;
        }
        if (SetaGlow != null)
        {
            _setaGlowPos  = SetaGlow.anchoredPosition;
            _setaGlowRotZ = SetaGlow.localEulerAngles.z;
        }

        if (DeckHighlightBase != null) DeckHighlightBase.raycastTarget = false;
        if (DeckHighlightGlow != null) DeckHighlightGlow.raycastTarget = false;

        // Guardar posição/escala base das mãos (se existirem)
        if (MaoDragRoot != null)
        {
            _maoDragStartPos = MaoDragRoot.anchoredPosition;
            MaoDragRoot.gameObject.SetActive(false);
        }

        if (MaoDropRoot != null)
        {
            _maoDropBaseScale = MaoDropRoot.localScale;
            MaoDropRoot.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        Debug.Log("[TutorialDeckHint] OnEnable", this);

        // Se este arranque foi marcado para NÃO mostrar o tutorial (Retry),
        // desligamos imediatamente este GameObject.
        if (!ProximoArranqueDeveMostrarTutorial)
        {
            gameObject.SetActive(false);
            return;
        }

        TutorialConcluido = false;

        Peca.OnPecaColocadaNaGrid += OnPecaColocadaNaGrid;
        Peca.OnPecaRodada         += OnPecaRodada;
        PecaFlipEvents.OnPecaFlipada += OnPecaFlipada;

        IniciarTutorial();
    }

    void OnDisable()
    {
        Debug.Log("[TutorialDeckHint] OnDisable", this);

        Peca.OnPecaColocadaNaGrid -= OnPecaColocadaNaGrid;
        Peca.OnPecaRodada         -= OnPecaRodada;
        PecaFlipEvents.OnPecaFlipada -= OnPecaFlipada;

        PararMaoDragDropFase3();
        ResetHighlights();
        ResetTextoPulse();
    }

    void Update()
    {
        ActualizarTextoPulse();
        ActualizarDeckHighlight();
        ActualizarPreviewHighlight();
    }

    // ------------------------------------------------------
    // ARRANQUE
    // ------------------------------------------------------
    void IniciarTutorial()
    {
        _iniciado  = true;
        _faseAtual = TutorialFase.Fase1_DeckHint;

        // Modo tutorial activo → primeira carta do deck é “livre” (não arranca o timer)
        if (ControladorJogo.Instancia != null)
            ControladorJogo.Instancia.PrimeiraCartaLivreComTutorial = true;

        if (Fase1 != null && Fase1.Texto != null && !string.IsNullOrEmpty(MsgDeck))
            Fase1.Texto.text = MsgDeck;

        // Garante que a seta começa visível quando o tutorial arranca
        if (SetaBase != null) SetaBase.gameObject.SetActive(true);
        if (SetaGlow != null) SetaGlow.gameObject.SetActive(true);

        AtivarApenasFase(TutorialFase.Fase1_DeckHint, animarEntrada: true);

        if (Escurecedor != null)
        {
            Escurecedor.alpha = 0.85f;
            Escurecedor.blocksRaycasts = true;
        }

        _deckHighlightAtivo     = true;
        _previewHighlightAtivo  = false;
        _previewTargetImage     = null;
        _previewTargetRect      = null;
    }

    // ------------------------------------------------------
    // CLIQUE NO DECK
    // ------------------------------------------------------
    public void OnDeckClicked_IniciarTransicao()
    {
        if (!_iniciado) return;
        if (TutorialConcluido) return;

        // FASE 1 → esconder seta + balão + texto imediatamente
        if (_faseAtual == TutorialFase.Fase1_DeckHint)
        {
            Debug.Log("[TutorialDeckHint] Deck click na Fase1 → esconder seta/balão/texto.", this);

            // Esconde coruja + balão + texto da fase 1
            DesativarRoot(Fase1);
            _textoAtual = null;

            // Desactiva completamente a seta
            if (SetaBase != null) SetaBase.gameObject.SetActive(false);
            if (SetaGlow != null) SetaGlow.gameObject.SetActive(false);

            // Para o highlight do deck (mas não mexe no preview)
            _deckHighlightAtivo = false;
            return;
        }
    }

    // ------------------------------------------------------
    // CARTA CHEGOU AO PREVIEW (DOG)
    // ------------------------------------------------------
    public void OnCartaChegouAoPreview(Sprite frente, RectTransform posPreview)
    {
        if (!_iniciado) return;
        if (_faseAtual != TutorialFase.Fase1_DeckHint && _faseAtual != TutorialFase.Fase2_Preview)
            return;

        _previewTargetImage = null;
        _previewTargetRect  = null;
        _previewBaseColor   = Color.white;
        _previewBaseScale   = Vector3.one;

        if (posPreview != null)
        {
            _previewTargetImage = posPreview.GetComponentInChildren<Image>();
            if (_previewTargetImage != null)
            {
                _previewBaseColor  = _previewTargetImage.color;
                _previewTargetRect = _previewTargetImage.rectTransform;
                _previewBaseScale  = _previewTargetRect.localScale;
            }
        }

        IniciarFase2_Preview();
    }

    // ------------------------------------------------------
    // EVENTOS DAS PEÇAS
    // ------------------------------------------------------
    void OnPecaColocadaNaGrid(Peca peca, CelulaGrelha celula)
    {
        if (!_iniciado) return;

        Debug.Log($"[TutorialDeckHint] OnPecaColocadaNaGrid na fase {_faseAtual}", this);

        if (_faseAtual == TutorialFase.Fase3_DragHint)
        {
            Debug.Log("[TutorialDeckHint] Fase3 -> Fase4 (drag concluído).", this);

            // Parar animação da mão ao sair da fase 3
            PararMaoDragDropFase3();

            IniciarFase4_RotateHint();
        }
    }

    void OnPecaRodada(Peca peca)
    {
        if (!_iniciado) return;

        Debug.Log($"[TutorialDeckHint] OnPecaRodada na fase {_faseAtual}", this);

        if (_faseAtual == TutorialFase.Fase4_RotateHint)
        {
            Debug.Log("[TutorialDeckHint] Fase4 -> Fase5 (rotacao da peca).", this);
            IniciarFase5_FlipHint();
        }
    }

    void OnPecaFlipada(Peca peca)
    {
        if (!_iniciado) return;

        Debug.Log($"[TutorialDeckHint] OnPecaFlipada na fase {_faseAtual}", this);

        if (_faseAtual == TutorialFase.Fase5_FlipHint)
        {
            Debug.Log("[TutorialDeckHint] Fase5 -> Fase6 (flip da peca).", this);
            IniciarFase6_Final();
        }
    }

    // ------------------------------------------------------
    // FASE 2: PREVIEW (DOG)
    // ------------------------------------------------------
    void IniciarFase2_Preview()
    {
        _faseAtual = TutorialFase.Fase2_Preview;

        if (Fase2 != null && Fase2.Texto != null && !string.IsNullOrEmpty(MsgPreview))
            Fase2.Texto.text = MsgPreview;

        AtivarApenasFase(TutorialFase.Fase2_Preview, animarEntrada: true);

        if (Escurecedor != null)
        {
            Escurecedor.alpha = 0.85f;
            Escurecedor.blocksRaycasts = true;
        }

        _deckHighlightAtivo    = false;
        _previewHighlightAtivo = true;

        StartCoroutine(CoFase2_AutoParaFase3());
    }

    IEnumerator CoFase2_AutoParaFase3()
    {
        float t = 0f;
        while (t < DelayFase3Segundos)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_faseAtual == TutorialFase.Fase2_Preview)
        {
            IniciarFase3_DragHint();
        }
    }

    // ------------------------------------------------------
    // FASE 3: DRAG
    // ------------------------------------------------------
    void IniciarFase3_DragHint()
    {
        _faseAtual = TutorialFase.Fase3_DragHint;

        if (Fase3 != null && Fase3.Texto != null && !string.IsNullOrEmpty(MsgDrag))
            Fase3.Texto.text = MsgDrag;

        AtivarApenasFase(TutorialFase.Fase3_DragHint, animarEntrada: true);

        if (Escurecedor != null)
        {
            Escurecedor.alpha = 0.0f;
            Escurecedor.blocksRaycasts = false;
        }

        _deckHighlightAtivo    = false;
        _previewHighlightAtivo = false;

        Debug.Log("[TutorialDeckHint] Entrar na Fase 3 (DragHint)", this);

        // Iniciar loop da mão Drag/Drop
        IniciarMaoDragDropFase3();
    }

    // ------------------------------------------------------
    // FASE 4: ROTATE
    // ------------------------------------------------------
    void IniciarFase4_RotateHint()
    {
        _faseAtual = TutorialFase.Fase4_RotateHint;

        if (Fase4 != null && Fase4.Texto != null && !string.IsNullOrEmpty(MsgRotate))
            Fase4.Texto.text = MsgRotate;

        AtivarApenasFase(TutorialFase.Fase4_RotateHint, animarEntrada: true);

        _deckHighlightAtivo    = false;
        _previewHighlightAtivo = false;

        Debug.Log("[TutorialDeckHint] Entrar na Fase 4 (RotateHint).", this);
    }

    // ------------------------------------------------------
    // FASE 5: FLIP
    // ------------------------------------------------------
    void IniciarFase5_FlipHint()
    {
        _faseAtual = TutorialFase.Fase5_FlipHint;

        if (Fase5 != null && Fase5.Texto != null && !string.IsNullOrEmpty(MsgFlip))
            Fase5.Texto.text = MsgFlip;

        AtivarApenasFase(TutorialFase.Fase5_FlipHint, animarEntrada: true);

        _deckHighlightAtivo    = false;
        _previewHighlightAtivo = false;

        Debug.Log("[TutorialDeckHint] Entrar na Fase 5 (FlipHint).", this);
    }

    // ------------------------------------------------------
    // FASE 6: FINAL (auto-fecho)
    // ------------------------------------------------------
    void IniciarFase6_Final()
    {
        _faseAtual = TutorialFase.Fase6_Final;

        if (Fase6 != null && Fase6.Texto != null && !string.IsNullOrEmpty(MsgFinal))
            Fase6.Texto.text = MsgFinal;

        AtivarApenasFase(TutorialFase.Fase6_Final, animarEntrada: false, permitirReusoRoot: true);

        // NA FASE 6 NÃO ESCURECEMOS O ECRÃ
        if (Escurecedor != null)
        {
            Escurecedor.alpha = 0f;
            Escurecedor.blocksRaycasts = false;
        }

        _deckHighlightAtivo    = false;
        _previewHighlightAtivo = false;

        Debug.Log("[TutorialDeckHint] Entrar na Fase 6 (Final).", this);

        // A partir daqui, para lógica de jogo (timer, etc.), consideramos o tutorial concluído.
        TutorialConcluido = true;

        // Auto-fecho: 3s de mensagem → pop do balão/texto → coruja desce → fecha hint
        StartCoroutine(CoFase6_AutoFechar());
    }

    // ------------------------------------------------------
    // FASE 3 – CORROTINAS DA MÃO DRAG/DROP
    // ------------------------------------------------------
    void IniciarMaoDragDropFase3()
    {
        if (MaoDragRoot == null || MaoDropRoot == null)
            return;

        PararMaoDragDropFase3();
        _coMaoFase3 = StartCoroutine(CoMaoDragDrop_Fase3());
    }

    void PararMaoDragDropFase3()
    {
        if (_coMaoFase3 != null)
        {
            StopCoroutine(_coMaoFase3);
            _coMaoFase3 = null;
        }

        if (MaoDragRoot != null)
            MaoDragRoot.gameObject.SetActive(false);

        if (MaoDropRoot != null)
        {
            MaoDropRoot.localScale = _maoDropBaseScale;
            MaoDropRoot.gameObject.SetActive(false);
        }
    }

    IEnumerator CoMaoDragDrop_Fase3()
    {
        // Loop enquanto estivermos na Fase 3 e o tutorial estiver activo
        while (_iniciado && _faseAtual == TutorialFase.Fase3_DragHint)
        {
            // Garantir estado inicial
            if (MaoDropRoot != null)
            {
                MaoDropRoot.localScale = _maoDropBaseScale;
                MaoDropRoot.gameObject.SetActive(false);
            }

            if (MaoDragRoot != null)
            {
                MaoDragRoot.gameObject.SetActive(true);
                MaoDragRoot.anchoredPosition = _maoDragStartPos;
            }

            // 1) Movimento da mão de drag (dos tiles até à grelha)
            float t = 0f;
            Vector2 start = _maoDragStartPos;
            Vector2 end   = start + new Vector2(0f, MaoDrag_DistY); // DistY > 0 = sobe

            while (t < MaoDrag_Duracao && _iniciado && _faseAtual == TutorialFase.Fase3_DragHint)
            {
                t += Time.unscaledDeltaTime;
                float k  = Mathf.Clamp01(t / MaoDrag_Duracao);
                float ke = k * k * (3f - 2f * k); // smoothstep

                if (MaoDragRoot != null)
                    MaoDragRoot.anchoredPosition = Vector2.LerpUnclamped(start, end, ke);

                yield return null;
            }

            if (!_iniciado || _faseAtual != TutorialFase.Fase3_DragHint)
                break;

            // Esconder mão de drag quando chega ao destino
            if (MaoDragRoot != null)
                MaoDragRoot.gameObject.SetActive(false);

            // 2) Mostrar mão de drop no centro (com pulse)
            if (MaoDropRoot != null)
            {
                MaoDropRoot.gameObject.SetActive(true);
                MaoDropRoot.localScale = _maoDropBaseScale;

                t = 0f;
                while (t < MaoDrag_PausaNoFim && _iniciado && _faseAtual == TutorialFase.Fase3_DragHint)
                {
                    t += Time.unscaledDeltaTime;

                    // Pulse suave (seno em Hz)
                    float osc = (Mathf.Sin(t * MaoDrop_PulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 0..1
                    float scale = Mathf.Lerp(1f, MaoDrop_PulseScale, osc);

                    MaoDropRoot.localScale = _maoDropBaseScale * scale;

                    yield return null;
                }

                // Restaurar escala e esconder
                MaoDropRoot.localScale = _maoDropBaseScale;
                MaoDropRoot.gameObject.SetActive(false);
            }

            if (!_iniciado || _faseAtual != TutorialFase.Fase3_DragHint)
                break;
        }

        _coMaoFase3 = null;
    }

    // ------------------------------------------------------
    // FASE 6: AUTO-FECHO
    // ------------------------------------------------------
    IEnumerator CoFase6_AutoFechar()
    {
        // 1) Espera com a mensagem final visível
        float t = 0f;
        while (t < Fase6_DelayAntesPop)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // 2) POP do balão + texto
        RectTransform balRT = null;
        CanvasGroup cgBal = null;

        if (Fase6 != null)
        {
            balRT = Fase6.BalaoRoot;
            if (balRT == null && Fase6.Texto != null)
                balRT = Fase6.Texto.rectTransform;
        }

        if (balRT != null)
        {
            cgBal = balRT.GetComponent<CanvasGroup>();
            if (cgBal == null) cgBal = balRT.gameObject.AddComponent<CanvasGroup>();

            Vector3 startScale = balRT.localScale;
            Vector3 endScale   = startScale * 1.15f;
            float durPop = Mathf.Max(0.01f, Fase6_DuracaoPop);

            t = 0f;
            while (t < durPop)
            {
                t += Time.unscaledDeltaTime;
                float k  = Mathf.Clamp01(t / durPop);
                float ke = k * k * (3f - 2f * k); // smoothstep

                balRT.localScale = Vector3.LerpUnclamped(startScale, endScale, ke);
                cgBal.alpha      = 1f - ke;

                yield return null;
            }

            cgBal.alpha = 0f;
            balRT.gameObject.SetActive(false);
        }

        // 3) CORUJA desce para fora do ecrã
        RectTransform corujaRT = null;
        if (Fase6 != null)
        {
            corujaRT = Fase6.CorujaRoot;
            if (corujaRT == null && Fase6.Root != null)
                corujaRT = Fase6.Root.GetComponent<RectTransform>();
        }

        if (corujaRT != null)
        {
            Vector2 startPos = corujaRT.anchoredPosition;
            float durSlide = Fase6_DuracaoSlideCoruja > 0f ? Fase6_DuracaoSlideCoruja : DuracaoSlide;
            Vector2 endPos = startPos + new Vector2(0f, OffsetEntradaY);

            t = 0f;
            while (t < durSlide)
            {
                t += Time.unscaledDeltaTime;
                float k  = Mathf.Clamp01(t / durSlide);
                float ke = k * k * (3f - 2f * k);

                corujaRT.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, ke);
                yield return null;
            }

            corujaRT.anchoredPosition = endPos;
        }

        // Garante que o fundo está limpo
        if (Escurecedor != null)
        {
            Escurecedor.alpha = 0f;
            Escurecedor.blocksRaycasts = false;
        }

        FecharHint();
    }

    // ------------------------------------------------------
    // FECHAR TUTORIAL (usado no auto-fecho)
    // ------------------------------------------------------
    void FecharHint()
    {
        DesativarRoot(Fase1);
        DesativarRoot(Fase2);
        DesativarRoot(Fase3);
        DesativarRoot(Fase4);
        DesativarRoot(Fase5);
        DesativarRoot(Fase6);

        ResetHighlights();
        ResetTextoPulse();

        _iniciado = false;

        gameObject.SetActive(false);
    }

    // ------------------------------------------------------
    // UTILITÁRIOS DE UI
    // ------------------------------------------------------
    FaseUI ObterUI(TutorialFase fase)
    {
        switch (fase)
        {
            case TutorialFase.Fase1_DeckHint:   return Fase1;
            case TutorialFase.Fase2_Preview:    return Fase2;
            case TutorialFase.Fase3_DragHint:   return Fase3;
            case TutorialFase.Fase4_RotateHint: return Fase4;
            case TutorialFase.Fase5_FlipHint:   return Fase5;
            case TutorialFase.Fase6_Final:      return Fase6;
            default:                            return null;
        }
    }

    void AtivarApenasFase(TutorialFase fase, bool animarEntrada, bool permitirReusoRoot = false)
    {
        var ui = ObterUI(fase);
        GameObject rootAlvo = ui != null ? ui.Root : null;

        if (rootAlvo == null && permitirReusoRoot && _ultimoRootAtivo != null)
            rootAlvo = _ultimoRootAtivo;

        DesativarRootSeDiferente(Fase1, rootAlvo);
        DesativarRootSeDiferente(Fase2, rootAlvo);
        DesativarRootSeDiferente(Fase3, rootAlvo);
        DesativarRootSeDiferente(Fase4, rootAlvo);
        DesativarRootSeDiferente(Fase5, rootAlvo);
        DesativarRootSeDiferente(Fase6, rootAlvo);

        _textoAtual = null;

        if (rootAlvo != null)
        {
            rootAlvo.SetActive(true);
            _ultimoRootAtivo = rootAlvo;

            if (animarEntrada)
                StartCoroutine(CoSlideIn(rootAlvo));

            if (ui != null && ui.Texto != null)
            {
                _textoAtual = ui.Texto;
                if (_textoAtual != null)
                {
                    var rt = _textoAtual.rectTransform;
                    _textoBaseScale = rt.localScale;
                }
            }
        }
    }

    IEnumerator CoSlideIn(GameObject root)
    {
        var rt = root.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 endPos   = rt.anchoredPosition;
        Vector2 startPos = endPos + new Vector2(0f, OffsetEntradaY);
        rt.anchoredPosition = startPos;

        float t = 0f;
        while (t < DuracaoSlide)
        {
            t += Time.unscaledDeltaTime;
            float k  = Mathf.Clamp01(t / DuracaoSlide);
            float ke = k * k * (3f - 2f * k);
            rt.anchoredPosition = Vector2.LerpUnclamped(startPos, endPos, ke);
            yield return null;
        }

        rt.anchoredPosition = endPos;
    }

    void DesativarRoot(FaseUI fase)
    {
        if (fase != null && fase.Root != null)
            fase.Root.SetActive(false);
    }

    void DesativarRootSeDiferente(FaseUI fase, GameObject excecao)
    {
        if (fase == null || fase.Root == null) return;
        if (fase.Root == excecao) return;
        fase.Root.SetActive(false);
    }

    // ------------------------------------------------------
    // TEXTO PULSE
    // ------------------------------------------------------
    void ActualizarTextoPulse()
    {
        if (_textoAtual == null) return;

        float t = Time.unscaledTime * TextoPulseSpeed;
        float osc = (Mathf.Sin(t) + 1f) * 0.5f;
        float scaleFactor = Mathf.Lerp(1f, TextoPulseScale, osc);

        var rt = _textoAtual.rectTransform;
        rt.localScale = _textoBaseScale * scaleFactor;
    }

    void ResetTextoPulse()
    {
        if (_textoAtual != null)
        {
            var rt = _textoAtual.rectTransform;
            rt.localScale = _textoBaseScale;
        }
    }

    // ------------------------------------------------------
    // HIGHLIGHT DO DECK
    // ------------------------------------------------------
    void ActualizarDeckHighlight()
    {
        bool ativo = _iniciado && _deckHighlightAtivo && _faseAtual == TutorialFase.Fase1_DeckHint;

        float osc = (Mathf.Sin(Time.unscaledTime * DeckHighlightSpeed) + 1f) * 0.5f;
        float alphaBase = ativo ? Mathf.Lerp(DeckHighlightMinAlpha, DeckHighlightMaxAlpha, osc) : 0f;
        float alphaGlow = ativo ? Mathf.Lerp(DeckHighlightMinAlpha, DeckHighlightMaxAlpha, osc) : 0f;

        if (DeckHighlightBase != null)
        {
            var c = DeckHighlightBase.color;
            c.a = _deckBaseAlphaBase * alphaBase;
            DeckHighlightBase.color = c;
        }

        if (DeckHighlightGlow != null)
        {
            var c = DeckHighlightGlow.color;
            c.a = _deckBaseAlphaGlow * alphaGlow;
            DeckHighlightGlow.color = c;
        }

        float bob  = ativo ? Mathf.Sin(Time.unscaledTime * SetaBobSpeed) * SetaBobAmplitude : 0f;
        float rotZ = ativo ? Mathf.Sin(Time.unscaledTime * SetaRotSpeed) * SetaRotAmplitude  : 0f;

        if (SetaBase != null)
        {
            SetaBase.anchoredPosition = _setaBasePos + new Vector2(0f, bob);
            Vector3 e = SetaBase.localEulerAngles;
            e.z = _setaBaseRotZ + rotZ;
            SetaBase.localEulerAngles = e;
        }

        if (SetaGlow != null)
        {
            SetaGlow.anchoredPosition = _setaGlowPos + new Vector2(0f, bob);
            Vector3 e = SetaGlow.localEulerAngles;
            e.z = _setaGlowRotZ + rotZ;
            SetaGlow.localEulerAngles = e;
        }
    }

    // ------------------------------------------------------
    // HIGHLIGHT DO PREVIEW (DOG) – ESCALA + BRILHO
    // ------------------------------------------------------
    void ActualizarPreviewHighlight()
    {
        bool ativo = _iniciado && _previewHighlightAtivo && _faseAtual == TutorialFase.Fase2_Preview;

        // Se não está activo, voltar tudo ao normal
        if (!ativo)
        {
            if (_previewTargetRect != null)
                _previewTargetRect.localScale = _previewBaseScale;

            if (_previewTargetImage != null)
                _previewTargetImage.color = _previewBaseColor;

            return;
        }

        float t = Time.unscaledTime * PreviewHighlightSpeed;
        float osc = (Mathf.Sin(t) + 1f) * 0.5f; // 0..1

        // Pulse de escala (cresce / encolhe)
        if (_previewTargetRect != null)
        {
            float scaleFactor = Mathf.Lerp(1f, 1f + PreviewHighlightScaleIntensity, osc);
            _previewTargetRect.localScale = _previewBaseScale * scaleFactor;
        }

        // Brilho na própria carta (só fica mais clara, nunca mais escura)
        if (_previewTargetImage != null)
        {
            float brilhoExtra = osc * PreviewHighlightIntensity; // 0..Intensity
            float mult = 1f + brilhoExtra;                       // 1..1+Intensity

            Color c = _previewBaseColor;
            c.r = Mathf.Clamp01(c.r * mult);
            c.g = Mathf.Clamp01(c.g * mult);
            c.b = Mathf.Clamp01(c.b * mult);
            _previewTargetImage.color = c;
        }
    }

    void ResetHighlights()
    {
        if (DeckHighlightBase != null)
        {
            var c = DeckHighlightBase.color;
            c.a = 0f;
            DeckHighlightBase.color = c;
        }
        if (DeckHighlightGlow != null)
        {
            var c = DeckHighlightGlow.color;
            c.a = 0f;
            DeckHighlightGlow.color = c;
        }

        if (_previewTargetImage != null)
            _previewTargetImage.color = _previewBaseColor;

        if (_previewTargetRect != null)
            _previewTargetRect.localScale = _previewBaseScale;

        _deckHighlightAtivo    = false;
        _previewHighlightAtivo = false;

        if (SetaBase != null)
        {
            SetaBase.anchoredPosition = _setaBasePos;
            Vector3 e = SetaBase.localEulerAngles;
            e.z = _setaBaseRotZ;
            SetaBase.localEulerAngles = e;
        }
        if (SetaGlow != null)
        {
            SetaGlow.anchoredPosition = _setaGlowPos;
            Vector3 e = SetaGlow.localEulerAngles;
            e.z = _setaGlowRotZ;
            SetaGlow.localEulerAngles = e;
        }
    }
}

/// <summary>
/// Helper estático para emitir o evento de "Peca flippada"
/// a partir do script PecaFlip (chamar PecaFlipEvents.EmitPecaFlipada(thisPeca)).
/// </summary>
public static class PecaFlipEvents
{
    public static event Action<Peca> OnPecaFlipada;

    public static void EmitPecaFlipada(Peca p)
    {
        OnPecaFlipada?.Invoke(p);
    }
}
