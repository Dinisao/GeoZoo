// AnimalWinVFX.cs — Overlay de vídeo quando um animal é concluído. (fix: no stale frames)
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
        public Sprite CardSprite;
        public VideoClip Clip;
    }

    [Header("Mapeamento Carta → Vídeo")]
    public List<Entry> Entries = new List<Entry>();

    [Header("Shuffle extra (opcional)")]
    [Tooltip("Se definido, podes chamar PlayShuffleExtraOnly() para tocar um shuffle do deck.")]
    public DeckShuffleVFX ShuffleVFX;
    [Tooltip("Se desligado, PlayShuffleExtraOnly() não faz nada.")]
    public bool ShuffleDepoisDoAnimal = true;

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
    public RectTransform GridRegion;
    public bool ReparentToRegion = true;
    public bool AutoSizeToRegion = false;
    public bool FollowRegionEveryFrame = false;
    public Vector2 RegionPadding = Vector2.zero;

    [Header("Fallback")]
    public VideoClip FallbackClip;

    [Header("Debug")]
    public bool debugLogs = true;

    Dictionary<Sprite, VideoClip> _mapByRef;
    Dictionary<string, VideoClip> _mapByName;

    bool _playing;
    Coroutine _playCo;

    bool _ownRT;
    Canvas _cachedCanvas;
    RectTransform _parentRT;

    Transform _origParent;
    int _origSibling;
    Vector2 _origAnchorMin, _origAnchorMax, _origPivot, _origSizeDelta, _origAnchoredPos;

    public bool IsPlaying => _playing;

    void Awake()
    {
        if (!Player) Player = GetComponent<VideoPlayer>();
        if (!Group)  Group  = GetComponent<CanvasGroup>();
        if (!Target) Target = GetComponent<RawImage>();

        _parentRT = transform.parent as RectTransform;
        _cachedCanvas = _parentRT
            ? _parentRT.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();

        if (Group)
        {
            Group.alpha = 0f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
        }

        if (Target)
        {
            Target.enabled = false;
            Target.color = Color.white;
            var tr = Target.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            Target.texture = null; // evita mostrar lixo no arranque
        }

        SetupPlayer();
        BuildMap();
    }

    void OnEnable()
    {
        if (Group)
        {
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
        }
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

        Player.playOnAwake       = false;
        Player.isLooping         = false;
        Player.waitForFirstFrame = true;
        Player.skipOnDrop        = true;
        Player.audioOutputMode   = VideoAudioOutputMode.None;
        Player.renderMode        = VideoRenderMode.RenderTexture;
        Player.aspectRatio       = VideoAspectRatio.FitInside;

        if (SharedRT)
        {
            Player.targetTexture = SharedRT;
            _ownRT = false;
        }
    }

    void BuildMap()
    {
        _mapByRef  = new Dictionary<Sprite, VideoClip>();
        _mapByName = new Dictionary<string, VideoClip>();

        foreach (var e in Entries)
        {
            if (e == null || e.CardSprite == null || e.Clip == null)
                continue;

            _mapByRef[e.CardSprite] = e.Clip;
            _mapByName[e.CardSprite.name.ToLowerInvariant()] = e.Clip;
        }

        if (debugLogs)
        {
            string keyList = string.Join(", ", _mapByName.Keys);
            Debug.Log("[AnimalWinVFX] Entradas mapeadas: " + _mapByRef.Count + " (" + keyList + ")", this);
        }
    }

    public void PlayForOnce(AnimalPattern pattern)
    {
        if (_playing || _playCo != null) return;
        _playCo = StartCoroutine(PlayForRoutine(pattern));
    }

    public void PlayForOnce(Sprite cardSprite)
    {
        if (_playing || _playCo != null) return;
        _playCo = StartCoroutine(PlayForRoutine(cardSprite));
    }

    public IEnumerator PlayForRoutine(AnimalPattern pattern)
    {
        if (_playing) yield break;
        _playing = true;

        Sprite cand1 = pattern != null ? pattern.CardSprite : null;
        Sprite cand2 = Deck != null ? Deck.CartaAtual : null;

        if (debugLogs)
        {
            string pName = pattern != null ? pattern.name : "null";
            string s1 = cand1 != null ? cand1.name : "null";
            string s2 = cand2 != null ? cand2.name : "null";
            Debug.Log("[AnimalWinVFX] Pedido de play. Pattern=" + pName +
                      ", CardSprite=" + s1 + ", CartaAtual=" + s2, this);
        }

        var clip = ResolveClip(cand1, cand2);
        if (!clip)
        {
            if (debugLogs)
                Debug.LogWarning("[AnimalWinVFX] Nenhum clip encontrado (nem fallback).", this);

            _playing = false;
            _playCo = null;
            yield break;
        }

        yield return PlayClipRoutine(clip);

        _playing = false;
        _playCo = null;
    }

    public IEnumerator PlayForRoutine(Sprite cardSprite)
    {
        if (_playing) yield break;
        _playing = true;

        var clip = ResolveClip(cardSprite, Deck != null ? Deck.CartaAtual : null);
        if (!clip)
        {
            if (debugLogs)
                Debug.LogWarning("[AnimalWinVFX] Nenhum clip encontrado para cardSprite e sem fallback.", this);

            _playing = false;
            _playCo = null;
            yield break;
        }

        yield return PlayClipRoutine(clip);

        _playing = false;
        _playCo = null;
    }

    VideoClip ResolveClip(Sprite primary, Sprite secondary)
    {
        if (primary && _mapByRef != null &&
            _mapByRef.TryGetValue(primary, out var clipRef))
        {
            if (debugLogs)
                Debug.Log("[AnimalWinVFX] Match por referência: " + primary.name, this);
            return clipRef;
        }

        if (primary && _mapByName != null &&
            _mapByName.TryGetValue(primary.name.ToLowerInvariant(), out var clipName))
        {
            if (debugLogs)
                Debug.Log("[AnimalWinVFX] Match por nome: " + primary.name, this);
            return clipName;
        }

        if (secondary && _mapByRef != null &&
            _mapByRef.TryGetValue(secondary, out var clipRef2))
        {
            if (debugLogs)
                Debug.Log("[AnimalWinVFX] Match por referência (Deck): " + secondary.name, this);
            return clipRef2;
        }

        if (secondary && _mapByName != null &&
            _mapByName.TryGetValue(secondary.name.ToLowerInvariant(), out var clipName2))
        {
            if (debugLogs)
                Debug.Log("[AnimalWinVFX] Match por nome (Deck): " + secondary.name, this);
            return clipName2;
        }

        if (FallbackClip)
        {
            if (debugLogs)
                Debug.Log("[AnimalWinVFX] Sem match → usar FallbackClip: " + FallbackClip.name, this);
            return FallbackClip;
        }

        return null;
    }

    // --- utilitário: limpar RT para transparente para não “brilhar” frame antigo
    static void ClearRT(RenderTexture rt)
    {
        if (!rt) return;
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = prev;
    }

    IEnumerator PlayClipRoutine(VideoClip clip)
    {
        if (!Player || clip == null) yield break;

        // Garante RT correta
        if (AutoCreateRenderTexture && !SharedRT)
        {
            int w = Mathf.Max((int)clip.width, 16);
            int h = Mathf.Max((int)clip.height, 16);

            if (Player.targetTexture == null ||
                Player.targetTexture.width  != w ||
                Player.targetTexture.height != h)
            {
                if (Player.targetTexture != null)
                {
                    Player.targetTexture.Release();
                    Object.Destroy(Player.targetTexture);
                }

                var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                rt.Create();
                Player.targetTexture = rt;
                _ownRT = true;
            }
        }

        // Zera qualquer resíduo do clip anterior
        if (Player.isPlaying) Player.Stop();
        if (Player.targetTexture) ClearRT(Player.targetTexture);

        if (Target)
        {
            Target.enabled = false;
            Target.texture = Player.targetTexture;
        }

        if (Group)
        {
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
        }

        // Preparar o novo clip
        Player.clip = clip;
        Player.Prepare();
        while (!Player.isPrepared)
            yield return null;

        if (Target && Target.texture != Player.targetTexture)
            Target.texture = Player.targetTexture;

        // Ajuste de proporção
        if (Target)
        {
            var arf = Target.GetComponent<AspectRatioFitter>();
            if (!arf) arf = Target.gameObject.AddComponent<AspectRatioFitter>();

            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = Mathf.Max(
                0.01f,
                (float)clip.width / Mathf.Max(1f, (float)clip.height)
            );
        }

        if (debugLogs)
            Debug.Log("[AnimalWinVFX] Preparado: " + clip.name +
                      " (" + clip.width + "x" + clip.height + ")", this);

        bool didReparent = false;
        if (GridRegion && ReparentToRegion)
            didReparent = BeginRegionAttach();
        else if (GridRegion && AutoSizeToRegion)
            MatchToRegionNow();

        // ► Toca já, mas NÃO mostres ainda: espera o 1º frame
        Player.Play();

        // Espera pelo 1º frame visível (frame>0 ou time>0)
        double safety = 0;
        while (Player.isPlaying && Player.frame <= 0 && Player.time <= 0)
        {
            safety += Time.unscaledDeltaTime;
            if (safety > 1.0) break; // segurança de 1s

            if (GridRegion && AutoSizeToRegion && FollowRegionEveryFrame)
                MatchToRegionNow();

            yield return null;
        }

        // Agora sim: mostrar e fazer fade-in sobre o frame correto
        if (Group)
        {
            if (Target) Target.enabled = true;
            Group.blocksRaycasts = true;
            yield return FadeTo(1f, FadeIn);
        }
        else if (Target) Target.enabled = true;

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
            while (Player.isPlaying)
                yield return null;
        }

        // Fade-out e esconder
        if (Group)
        {
            yield return FadeTo(0f, FadeOut);
            Group.blocksRaycasts = false;
            if (Target) Target.enabled = false;
        }
        else if (Target) Target.enabled = false;

        if (didReparent)
            EndRegionAttach();

        // NOTA: já não chamamos o shuffle aqui.
        // O GridValidator é que decide quando tocar o DeckShuffleVFX.
    }

    /// <summary>
    /// Toca apenas o vídeo de shuffle extra (se configurado).
    /// Usado pelo GridValidator depois da recolha das peças.
    /// </summary>
    public IEnumerator PlayShuffleExtraOnly()
    {
        if (ShuffleVFX == null || !ShuffleDepoisDoAnimal)
            yield break;

        if (debugLogs)
            Debug.Log("[AnimalWinVFX] A reproduzir shuffle extra (isolado).", this);

        yield return ShuffleVFX.PlayRoutine();
    }

    bool BeginRegionAttach()
    {
        if (!GridRegion) return false;

        var rt = (RectTransform)transform;
        _origParent      = rt.parent;
        _origSibling     = rt.GetSiblingIndex();
        _origAnchorMin   = rt.anchorMin;
        _origAnchorMax   = rt.anchorMax;
        _origPivot       = rt.pivot;
        _origSizeDelta   = rt.sizeDelta;
        _origAnchoredPos = rt.anchoredPosition;

        rt.SetParent(GridRegion, false);
        rt.SetAsLastSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2( RegionPadding.x,  RegionPadding.y);
        rt.offsetMax = new Vector2(-RegionPadding.x, -RegionPadding.y);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = Vector2.zero;

        if (debugLogs)
            Debug.Log("[AnimalWinVFX] Reparent para GridRegion (stretch).", this);

        return true;
    }

    void EndRegionAttach()
    {
        var rt = (RectTransform)transform;
        rt.SetParent(_origParent, false);
        rt.SetSiblingIndex(_origSibling);
        rt.anchorMin        = _origAnchorMin;
        rt.anchorMax        = _origAnchorMax;
        rt.pivot            = _origPivot;
        rt.sizeDelta        = _origSizeDelta;
        rt.anchoredPosition = _origAnchoredPos;

        if (debugLogs)
            Debug.Log("[AnimalWinVFX] Restaurado parent/anchors originais.", this);
    }

    void MatchToRegionNow()
    {
        if (!GridRegion || !_parentRT) return;

        var rt = (RectTransform)transform;
        Camera cam = null;

        if (_cachedCanvas &&
            (_cachedCanvas.renderMode == RenderMode.ScreenSpaceCamera ||
             _cachedCanvas.renderMode == RenderMode.WorldSpace))
        {
            cam = _cachedCanvas.worldCamera;
        }

        Vector3[] wc = new Vector3[4];
        GridRegion.GetWorldCorners(wc);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRT,
            RectTransformUtility.WorldToScreenPoint(cam, wc[0]),
            cam,
            out var bl
        );

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parentRT,
            RectTransformUtility.WorldToScreenPoint(cam, wc[2]),
            cam,
            out var tr
        );

        Vector2 size   = (tr - bl) + RegionPadding * 2f;
        Vector2 center = (bl + tr) * 0.5f;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = center;
    }

    IEnumerator FadeTo(float a, float dur)
    {
        if (!Group) yield break;
        if (dur <= 0f)
        {
            Group.alpha = a;
            yield break;
        }

        float start = Group.alpha;
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            Group.alpha = Mathf.Lerp(start, a, Mathf.Clamp01(t / dur));
            yield return null;
        }

        Group.alpha = a;
    }

#if UNITY_EDITOR
    [ContextMenu("Test Fallback")]
    void _TestFallback()
    {
        if (FallbackClip && Application.isPlaying && !_playing)
        {
            PlayForOnce((Sprite)null);
            StartCoroutine(PlayClipRoutine(FallbackClip));
        }
    }
#endif
}
