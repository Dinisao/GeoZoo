using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class DeckShuffleVFX : MonoBehaviour
{
    [Header("Clip de Shuffle")]
    public VideoClip ShuffleClip;

    [Header("Referências (UI/Video)")]
    public RawImage Target;
    public CanvasGroup Group;
    public VideoPlayer Player;

    [Header("Deck / Posicionamento")]
    [Tooltip("DeckController da cena (opcional, só para auto-descobrir a imagem do deck).")]
    public DeckController Deck;

    [Tooltip("Se definido, usa esta imagem como referência do deck (IMG_DeckBack).")]
    public Image DeckImageOverride;

    [Tooltip("Padding extra à volta do rect do deck (em px).")]
    public Vector2 ExtraPadding = new Vector2(10f, 10f);

    [Header("Comportamento")]
    public float FadeIn  = 0.12f;
    public float FadeOut = 0.20f;

    [Tooltip("Se true e não houver SharedRT, cria uma RenderTexture à medida do clip.")]
    public bool AutoCreateRenderTexture = true;

    [Tooltip("Opcional: RT partilhada com outros efeitos.")]
    public RenderTexture SharedRT;

    [Header("Debug")]
    public bool DebugLogs = true;

    // ---- estado interno ----
    RectTransform _rt;
    Canvas _canvas;
    RectTransform _canvasRT;
    bool _ownRT;
    bool _playing;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (!Target) Target = GetComponent<RawImage>();
        if (!Group)  Group  = GetComponent<CanvasGroup>();
        if (!Player) Player = GetComponent<VideoPlayer>();

        _canvas   = GetComponentInParent<Canvas>();
        _canvasRT = _canvas ? _canvas.GetComponent<RectTransform>() : null;

        if (Group)
        {
            Group.alpha = 0f;
            Group.interactable   = false;
            Group.blocksRaycasts = false;
        }

        if (Target)
        {
            Target.enabled = false;
            Target.color   = Color.white;
            Target.raycastTarget = false;
        }

        SetupPlayer();
    }

    void SetupPlayer()
    {
        if (!Player) return;

        Player.playOnAwake      = false;
        Player.isLooping        = false;
        Player.waitForFirstFrame = true;
        Player.skipOnDrop       = true;
        Player.renderMode       = VideoRenderMode.RenderTexture;
        Player.aspectRatio      = VideoAspectRatio.FitInside;
        Player.audioOutputMode  = VideoAudioOutputMode.None;

        if (SharedRT)
        {
            Player.targetTexture = SharedRT;
            _ownRT = false;
        }
    }

    // Conveniência: se algum dia quiseres chamar directamente no Inspector via botão/context menu.
    [ContextMenu("Test Shuffle")]
    public void PlayShuffleOnce()
    {
        if (!_playing && isActiveAndEnabled)
            StartCoroutine(PlayRoutine());
    }

    /// <summary>
    /// Chamado pelo AnimalWinVFX: yield return ShuffleVFX.PlayRoutine();
    /// </summary>
    public IEnumerator PlayRoutine()
    {
        if (_playing) yield break;
        _playing = true;

        if (!ShuffleClip || !Player || !Target)
        {
            if (DebugLogs)
                Debug.LogWarning("[DeckShuffleVFX] Falta ShuffleClip / Player / Target.", this);
            _playing = false;
            yield break;
        }

        // 1) Garantir que este GO está NO TOPO do Canvas (para não ficar atrás do UI_RootWrapper).
        if (_rt) _rt.SetAsLastSibling();

        // 2) Descobrir a imagem do deck
        Image deckImg = DeckImageOverride;
        if (!deckImg && Deck)
            deckImg = Deck.imgDeckBack;

        if (!deckImg)
        {
            if (DebugLogs)
                Debug.LogWarning("[DeckShuffleVFX] Não encontrei imagem do deck (DeckImageOverride + Deck.imgDeckBack são null).", this);
            _playing = false;
            yield break;
        }

        RectTransform deckRT = deckImg.rectTransform;

        // 3) Posicionar o FX exactamente por cima do deck
        if (_canvasRT && deckRT)
        {
            Camera cam = null;
            if (_canvas &&
                (_canvas.renderMode == RenderMode.ScreenSpaceCamera ||
                 _canvas.renderMode == RenderMode.WorldSpace))
            {
                cam = _canvas.worldCamera;
            }

            Vector3[] wc = new Vector3[4];
            deckRT.GetWorldCorners(wc);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT,
                RectTransformUtility.WorldToScreenPoint(cam, wc[0]),
                cam,
                out var bl);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT,
                RectTransformUtility.WorldToScreenPoint(cam, wc[2]),
                cam,
                out var tr);

            Vector2 size   = (tr - bl) + ExtraPadding * 2f;
            Vector2 center = (bl + tr) * 0.5f;

            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot     = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = size;
            _rt.anchoredPosition = center;
        }

        // 4) Preparar RenderTexture
        if (AutoCreateRenderTexture && !SharedRT)
        {
            int w = Mathf.Max((int)ShuffleClip.width, 16);
            int h = Mathf.Max((int)ShuffleClip.height, 16);

            if (!Player.targetTexture ||
                Player.targetTexture.width  != w ||
                Player.targetTexture.height != h)
            {
                if (Player.targetTexture != null && _ownRT)
                {
                    Player.targetTexture.Release();
                    Destroy(Player.targetTexture);
                }

                var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                rt.Create();
                Player.targetTexture = rt;
                _ownRT = true;
            }
        }

        if (!Player.targetTexture)
        {
            if (DebugLogs)
                Debug.LogWarning("[DeckShuffleVFX] targetTexture NULL, não consigo desenhar o shuffle.", this);
            _playing = false;
            yield break;
        }

        ClearRT(Player.targetTexture);
        Target.texture = Player.targetTexture;

        if (DebugLogs)
            Debug.Log($"[DeckShuffleVFX] A preparar shuffle: {ShuffleClip.name} ({ShuffleClip.width}x{ShuffleClip.height})", this);

        Player.clip = ShuffleClip;
        Player.Prepare();
        while (!Player.isPrepared) yield return null;

        // 5) Esconder a imagem do deck
        bool prevDeckEnabled = deckImg.enabled;
        deckImg.enabled = false;

        // 6) Mostrar o overlay e tocar o vídeo
        if (Group)
        {
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
        }
        if (Target) Target.enabled = true;

        Player.Play();

        if (Group)
            yield return FadeTo(1f, FadeIn);

        // Espera até acabar
        while (Player.isPlaying)
            yield return null;

        // 7) Fade-out e esconder
        if (Group)
        {
            yield return FadeTo(0f, FadeOut);
            Group.blocksRaycasts = false;
        }
        if (Target) Target.enabled = false;

        // 8) Restaurar imagem do deck
        deckImg.enabled = prevDeckEnabled;

        _playing = false;
    }

    static void ClearRT(RenderTexture rt)
    {
        if (!rt) return;
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = prev;
    }

    IEnumerator FadeTo(float alvo, float dur)
    {
        if (!Group) yield break;
        if (dur <= 0f) { Group.alpha = alvo; yield break; }

        float start = Group.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            Group.alpha = Mathf.Lerp(start, alvo, k);
            yield return null;
        }
        Group.alpha = alvo;
    }
}
