using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cria automaticamente um "tapete" por baixo da grelha:
/// - Vidro fumado (centro escuro, bordas a desaparecer)
/// - Cantos arredondados
/// - Faixa de highlight no topo (efeito vidro)
/// Tudo gerado por código, NÃO mexe na lógica da grelha.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class GridMatBackground : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Root da grelha (o mesmo GridRoot do GestorGrelha).")]
    public RectTransform GridRoot;

    [Tooltip("Image usada para o tapete. Se vazio, é criada como irmã do GridRoot.")]
    public Image MatImage;

    [Header("Aparência do tapete")]
    [Min(32)]
    public int Resolution = 512;

    [Tooltip("Cor no centro do tapete (vidro fumado).")]
    // 51,59,69,217 em 0..1
    public Color CentroCor = new Color(0.20f, 0.2313726f, 0.2705883f, 0.8509804f);

    [Tooltip("Cor nas bordas (normalmente a mesma cor mas com alpha 0).")]
    public Color BordaAzulCor = new Color(0.20f, 0.2313726f, 0.2705883f, 0f);

    [Tooltip("Largura da zona de borda em relação ao tamanho (0 = nada, 0.5 = tudo).")]
    [Range(0.01f, 0.5f)]
    public float LarguraBorda = 0.20f;

    [Tooltip("Multiplicador de intensidade geral (1 = tal como as cores).")]
    [Range(0.3f, 1.2f)]
    public float IntensidadeGlobal = 1.0f;

    [Header("Cantos arredondados")]
    [Tooltip("Raio dos cantos em coordenadas normalizadas (0 = quadrado, 0.5 = círculo).")]
    [Range(0f, 0.5f)]
    public float CornerRadius = 0.18f;

    [Tooltip("Suavização da borda dos cantos (em fração do tamanho).")]
    [Range(0f, 0.05f)]
    public float CornerFeather = 0.01f;

    [Header("Highlight de vidro (faixa no topo)")]
    [Tooltip("Cor do brilho no topo (quase branco, ligeiramente azulado).")]
    public Color TopHighlightColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("Força do highlight do topo.")]
    [Range(0f, 1f)]
    public float TopHighlightStrength = 0.35f;

    [Tooltip("Altura da faixa de highlight (0 = fininha, 1 = ocupa o tapete todo).")]
    [Range(0.02f, 0.5f)]
    public float TopHighlightHeight = 0.22f;

    [Tooltip("Posição vertical do centro do highlight (0 = fundo, 1 = topo).")]
    [Range(0f, 1f)]
    public float TopHighlightPosY = 0.82f;

    Sprite _generatedSprite;

    void Reset()
    {
        // Tenta auto-ligar o GridRoot a partir do GestorGrelha
        var gg = GetComponent<GestorGrelha>();
        if (gg != null)
            GridRoot = gg.GridRoot;
    }

    void Awake()
    {
        ActualizarTudo();
    }

    void OnEnable()
    {
        ActualizarTudo();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                ActualizarTudo();
            };
        }
        else
        {
            ActualizarTudo();
        }
    }
#endif

    void ActualizarTudo()
    {
        if (!GridRoot)
        {
            var gg = GetComponent<GestorGrelha>();
            if (gg != null) GridRoot = gg.GridRoot;
        }
        if (!GridRoot) return;

        GarantirMatImage();
        CopiarLayoutDoGrid();
        GerarSpriteTapete();
    }

    void GarantirMatImage()
    {
        if (MatImage != null) return;

        var parent = GridRoot.parent as RectTransform;
        if (!parent) return;

        Transform existente = parent.Find("GridMat");
        RectTransform matRT;

        if (existente != null)
        {
            MatImage = existente.GetComponent<Image>();
            matRT = MatImage.rectTransform;
        }
        else
        {
            GameObject go = new GameObject("GridMat",
                                           typeof(RectTransform),
                                           typeof(CanvasRenderer),
                                           typeof(Image));
            matRT = go.GetComponent<RectTransform>();
            matRT.SetParent(parent, false);
            MatImage = go.GetComponent<Image>();
        }

        // Tapete atrás da grelha
        if (matRT.parent == GridRoot.parent)
        {
            int gridIndex = GridRoot.GetSiblingIndex();
            matRT.SetSiblingIndex(gridIndex);
            GridRoot.SetSiblingIndex(gridIndex + 1);
        }

        MatImage.raycastTarget = false;
        MatImage.type = Image.Type.Simple;
    }

    void CopiarLayoutDoGrid()
    {
        if (!MatImage || !GridRoot) return;

        var matRT = MatImage.rectTransform;
        matRT.anchorMin        = GridRoot.anchorMin;
        matRT.anchorMax        = GridRoot.anchorMax;
        matRT.pivot            = GridRoot.pivot;
        matRT.anchoredPosition = GridRoot.anchoredPosition;
        matRT.sizeDelta        = GridRoot.sizeDelta;
        matRT.localRotation    = GridRoot.localRotation;
        matRT.localScale       = GridRoot.localScale;
    }

    void GerarSpriteTapete()
    {
        if (!MatImage) return;

        int size = Mathf.Clamp(Resolution, 32, 2048);
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.name = "GridMatGenerated";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float larg    = Mathf.Clamp01(LarguraBorda);
        float rNorm   = Mathf.Clamp01(CornerRadius);
        float feather = Mathf.Clamp(CornerFeather, 0f, 0.1f);

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / (size - 1);      // 0 = fundo, 1 = topo
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / (size - 1);  // 0 = esquerda, 1 = direita

                // ---------- Máscara de rounded-rect ----------
                Vector2 uv = new Vector2(u - 0.5f, v - 0.5f);
                float r = rNorm * 0.5f;
                Vector2 box = new Vector2(0.5f - r, 0.5f - r);

                Vector2 absC = new Vector2(Mathf.Abs(uv.x), Mathf.Abs(uv.y));
                bool insideCoreRect = (absC.x <= box.x && absC.y <= box.y);
                float alphaMask;

                if (r <= 0.0001f)
                {
                    alphaMask = 1f;
                }
                else
                {
                    if (insideCoreRect)
                    {
                        alphaMask = 1f;
                    }
                    else
                    {
                        float cx = Mathf.Clamp(uv.x, -box.x, box.x);
                        float cy = Mathf.Clamp(uv.y, -box.y, box.y);
                        float dx = uv.x - cx;
                        float dy = uv.y - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        float edge = r;
                        float inner = r - feather;

                        if (dist > edge)
                            alphaMask = 0f;
                        else if (dist < inner || feather <= 0f)
                            alphaMask = 1f;
                        else
                        {
                            float t = Mathf.InverseLerp(edge, inner, dist);
                            alphaMask = t;
                        }
                    }
                }

                if (alphaMask <= 0f)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                // ---------- Gradiente tipo moldura (NÃO radial) ----------
                float distEsq   = u;
                float distDir   = 1f - u;
                float distBaixo = v;
                float distCima  = 1f - v;

                float distMin = Mathf.Min(distEsq, distDir, distBaixo, distCima);
                // 0 junto à borda, 1 no interior
                float interior01 = Mathf.InverseLerp(0f, larg, distMin);
                interior01 = Mathf.Clamp01(interior01);

                Color col = Color.Lerp(BordaAzulCor, CentroCor, interior01);
                col *= IntensidadeGlobal;

                // ---------- Highlight de vidro no topo ----------
                float dyTop = Mathf.Abs(v - TopHighlightPosY);
                float normY = Mathf.Clamp01(1f - dyTop / Mathf.Max(TopHighlightHeight, 0.0001f));
                // curva suave (tipo sino)
                float band = normY * normY;

                if (TopHighlightStrength > 0f && band > 0f)
                {
                    col = Color.Lerp(col, TopHighlightColor, TopHighlightStrength * band);
                }

                col.a *= alphaMask;
                tex.SetPixel(x, y, col);
            }
        }

        tex.Apply(false, false);

        if (_generatedSprite != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_generatedSprite);
            else
                Destroy(_generatedSprite);
#else
            Destroy(_generatedSprite);
#endif
        }

        _generatedSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        MatImage.sprite = _generatedSprite;
        MatImage.type   = Image.Type.Simple;
    }
}
