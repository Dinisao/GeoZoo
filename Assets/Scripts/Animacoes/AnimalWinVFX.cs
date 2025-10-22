using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class AnimalWinVFX : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Sprite da carta (a Frente usada no preview)")]
        public Sprite CardSprite;

        [Tooltip("Vídeo a tocar quando este animal é concluído (ideal: .mp4 H.264)")]
        public VideoClip Clip;
    }

    [Header("Mapeamento Carta → Vídeo")]
    public List<Entry> Entries = new List<Entry>();

    [Header("Referências (UI/Video)")]
    public DeckController Deck;
    public RawImage Target;
    public CanvasGroup Group;
    public VideoPlayer Player;

    [Header("Comportamento")]
    public float FadeIn = 0.12f, FadeOut = 0.20f;
    public bool AutoCreateRenderTexture = true;
    public RenderTexture SharedRT;

    [Header("Região (opcional)")]
    [Tooltip("RectTransform da grelha (ou container) sobre o qual o vídeo deve aparecer")]
    public RectTransform GridRegion;

    [Tooltip("Em vez de calcular posição/tamanho, muda temporariamente o parent do overlay para a GridRegion e estica-o a 100%. É o método mais robusto.")]
    public bool ReparentToRegion = true;

    [Tooltip("Se não reparentar, tenta dimensionar/posicionar o overlay para coincidir com a GridRegion antes de tocar")]
    public bool AutoSizeToRegion = false;

    [Tooltip("Se ativo e sem reparent, o overlay segue a GridRegion a cada frame enquanto toca")]
    public bool FollowRegionEveryFrame = false;

    [Tooltip("Padding extra (px) à volta da região")]
    public Vector2 RegionPadding = Vector2.zero;

    [Header("Fallback")]
    public VideoClip FallbackClip;

    [Header("Debug")]
    public bool debugLogs = true;

    Dictionary<Sprite, VideoClip> _mapByRef;
    Dictionary<string, VideoClip> _mapByName;
    bool _busy;
    bool _ownRT;

    Canvas _cachedCanvas;
    RectTransform _parentRT;

    // guardas para reparent
    Transform _origParent;
    int _origSibling;
    Vector2 _origAnchorMin, _origAnchorMax, _origPivot, _origSizeDelta, _origAnchoredPos;

    public bool IsPlaying => _busy;

    void Awake()
    {
        if (!Player) Player = GetComponent<VideoPlayer>();
        if (!Group)  Group  = GetComponent<CanvasGroup>();
        if (!Target) Target = GetComponent<RawImage>();

        _parentRT = transform.parent as RectTransform;
        _cachedCanvas = _parentRT ? _parentRT.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();

        if (Group)
        {
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }
        if (Target)
        {
            Target.enabled = false;
            Target.color = Color.white; // cor multiplica o vídeo
            var tr = Target.rectTransform; // garantir que o RawImage preenche o rect do overlay
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        SetupPlayer();
        BuildMap();
    }

    void OnEnable()
    {
        if (Group) { Group.alpha = 0f; Group.blocksRaycasts = false; }
        if (Target) Target.enabled = false;
    }

    void OnDestroy()
    {
        if (_ownRT && Player && Player.targetTexture)
        {
            var rt = Player.targetTexture;
            Player.targetTexture = null;
            rt.Release();
            Destroy(rt);
        }
    }

    void SetupPlayer()
    {
        if (!Player) return;

        Player.playOnAwake = false;
        Player.isLooping   = false;
        Player.waitForFirstFrame = true;
        Player.skipOnDrop  = true;
        Player.audioOutputMode = VideoAudioOutputMode.None;
        Player.renderMode  = VideoRenderMode.RenderTexture;
        Player.aspectRatio = VideoAspectRatio.FitInside;

        if (SharedRT)
        {
            Player.targetTexture = SharedRT;
            _ownRT = false;
        }
    }

    void BuildMap()
    {
        _mapByRef = new Dictionary<Sprite, VideoClip>();
        _mapByName = new Dictionary<string, VideoClip>();

        foreach (var e in Entries)
        {
            if (e == null || e.CardSprite == null || e.Clip == null) continue;
            _mapByRef[e.CardSprite] = e.Clip;
            _mapByName[e.CardSprite.name.ToLowerInvariant()] = e.Clip;
        }

        if (debugLogs)
        {
            var keys = string.Join(", ", _mapByName.Keys);
            Debug.Log($"[AnimalWinVFX] Entradas mapeadas: {_mapByRef.Count} ({keys})", this);
        }
    }

    public IEnumerator PlayForRoutine(AnimalPattern pattern)
    {
        Sprite cand1 = (pattern != null ? pattern.CardSprite : null);
        Sprite cand2 = (Deck != null ? Deck.CartaAtual : null);

        if (debugLogs)
        {
            string pName = (pattern != null ? pattern.name : "null");
            string s1 = (cand1 != null ? cand1.name : "null");
            string s2 = (cand2 != null ? cand2.name : "null");
            Debug.Log($"[AnimalWinVFX] Pedido de play. Pattern={pName}, CardSprite={s1}, CartaAtual={s2}", this);
        }

        var clip = ResolveClip(cand1, cand2);
        if (!clip)
        {
            if (debugLogs) Debug.LogWarning("[AnimalWinVFX] Nenhum clip encontrado (nem fallback).", this);
            yield break;
        }

        yield return PlayClipRoutine(clip);
    }

    public IEnumerator PlayForRoutine(Sprite cardSprite)
    {
        var clip = ResolveClip(cardSprite, Deck != null ? Deck.CartaAtual : null);
        if (!clip)
        {
            if (debugLogs) Debug.LogWarning("[AnimalWinVFX] Nenhum clip encontrado para cardSprite e sem fallback.", this);
            yield break;
        }
        yield return PlayClipRoutine(clip);
    }

    VideoClip ResolveClip(Sprite primary, Sprite secondary)
    {
        if (primary && _mapByRef != null && _mapByRef.TryGetValue(primary, out var clipRef))
        {
            if (debugLogs) Debug.Log($"[AnimalWinVFX] Match por referência: {primary.name}", this);
            return clipRef;
        }
        if (primary && _mapByName != null && _mapByName.TryGetValue(primary.name.ToLowerInvariant(), out var clipName))
        {
            if (debugLogs) Debug.Log($"[AnimalWinVFX] Match por nome: {primary.name}", this);
            return clipName;
        }
        if (secondary && _mapByRef != null && _mapByRef.TryGetValue(secondary, out var clipRef2))
        {
            if (debugLogs) Debug.Log($"[AnimalWinVFX] Match por referência (Deck): {secondary.name}", this);
            return clipRef2;
        }
        if (secondary && _mapByName != null && _mapByName.TryGetValue(secondary.name.ToLowerInvariant(), out var clipName2))
        {
            if (debugLogs) Debug.Log($"[AnimalWinVFX] Match por nome (Deck): {secondary.name}", this);
            return clipName2;
        }
        if (FallbackClip)
        {
            if (debugLogs) Debug.Log($"[AnimalWinVFX] Sem match → usar FallbackClip: {FallbackClip.name}", this);
            return FallbackClip;
        }
        return null;
    }

    IEnumerator PlayClipRoutine(VideoClip clip)
    {
        if (_busy || !Player || clip == null) yield break;
        _busy = true;

        // RT
        if (AutoCreateRenderTexture && !SharedRT)
        {
            int w = Mathf.Max((int)clip.width,  16);
            int h = Mathf.Max((int)clip.height, 16);

            if (Player.targetTexture == null || Player.targetTexture.width != w || Player.targetTexture.height != h)
            {
                if (Player.targetTexture != null)
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

        if (Target) Target.texture = Player.targetTexture;

        Player.clip = clip;
        Player.Prepare();
        while (!Player.isPrepared) yield return null;

        if (Target && Target.texture != Player.targetTexture)
            Target.texture = Player.targetTexture;

        // Aspect dentro da região
        if (Target)
        {
            var arf = Target.GetComponent<AspectRatioFitter>();
            if (!arf) arf = Target.gameObject.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = Mathf.Max(0.01f, (float)clip.width / Mathf.Max(1f, (float)clip.height));
        }

        if (debugLogs) Debug.Log($"[AnimalWinVFX] Preparado: {clip.name} ({clip.width}x{clip.height})", this);

        // ---- alinhar à região ----
        bool didReparent = false;
        if (GridRegion && ReparentToRegion)
            didReparent = BeginRegionAttach();
        else if (GridRegion && AutoSizeToRegion)
            MatchToRegionNow();
        // --------------------------

        // Fade in
        if (Group)
        {
            if (Target) Target.enabled = true;
            Group.blocksRaycasts = true;
            yield return FadeTo(1f, FadeIn);
        }
        else if (Target) Target.enabled = true;

        Player.Play();

        // Seguir região se não reparentaste
        if (!didReparent && GridRegion && AutoSizeToRegion && FollowRegionEveryFrame)
        {
            while (Player.isPlaying)
            {
                MatchToRegionNow();
                yield return null;
            }
        }
        else
        {
            while (Player.isPlaying) yield return null;
        }

        // Fade out
        if (Group)
        {
            yield return FadeTo(0f, FadeOut);
            Group.blocksRaycasts = false;
            if (Target) Target.enabled = false;
        }
        else if (Target) Target.enabled = false;

        // desfazer reparent
        if (didReparent) EndRegionAttach();

        _busy = false;
    }

    // ---------- Região: reparent seguro ----------
    bool BeginRegionAttach()
    {
        if (!GridRegion) return false;

        var rt = (RectTransform)transform;
        _origParent = rt.parent;
        _origSibling = rt.GetSiblingIndex();
        _origAnchorMin = rt.anchorMin;
        _origAnchorMax = rt.anchorMax;
        _origPivot     = rt.pivot;
        _origSizeDelta = rt.sizeDelta;
        _origAnchoredPos = rt.anchoredPosition;

        rt.SetParent(GridRegion, worldPositionStays: false);
        rt.SetAsLastSibling();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2( RegionPadding.x,  RegionPadding.y);
        rt.offsetMax = new Vector2(-RegionPadding.x, -RegionPadding.y);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        if (debugLogs) Debug.Log("[AnimalWinVFX] Reparent para GridRegion (stretch).", this);
        return true;
    }

    void EndRegionAttach()
    {
        var rt = (RectTransform)transform;
        rt.SetParent(_origParent, worldPositionStays: false);
        rt.SetSiblingIndex(_origSibling);
        rt.anchorMin = _origAnchorMin;
        rt.anchorMax = _origAnchorMax;
        rt.pivot     = _origPivot;
        rt.sizeDelta = _origSizeDelta;
        rt.anchoredPosition = _origAnchoredPos;

        if (debugLogs) Debug.Log("[AnimalWinVFX] Restaurado parent/anchors originais.", this);
    }
    // --------------------------------------------

    void MatchToRegionNow()
    {
        if (!GridRegion || !_parentRT) return;

        var rt = (RectTransform)transform;

        Camera cam = null;
        if (_cachedCanvas)
        {
            if (_cachedCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
                _cachedCanvas.renderMode == RenderMode.WorldSpace)
                cam = _cachedCanvas.worldCamera;
        }

        Vector3[] wc = new Vector3[4];
        GridRegion.GetWorldCorners(wc); // 0=BL,2=TR

        Vector2 bl, tr;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRT, RectTransformUtility.WorldToScreenPoint(cam, wc[0]), cam, out bl);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRT, RectTransformUtility.WorldToScreenPoint(cam, wc[2]), cam, out tr);

        Vector2 size = tr - bl;
        size += RegionPadding * 2f;
        Vector2 center = (bl + tr) * 0.5f;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = center;
    }

    IEnumerator FadeTo(float a, float dur)
    {
        if (!Group) yield break;
        if (dur <= 0f) { Group.alpha = a; yield break; }

        float start = Group.alpha, t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            Group.alpha = Mathf.Lerp(start, a, k);
            yield return null;
        }
        Group.alpha = a;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Fallback")]
    void _TestFallback()
    {
        if (FallbackClip && Application.isPlaying)
            StartCoroutine(PlayClipRoutine(FallbackClip));
    }
#endif
}
