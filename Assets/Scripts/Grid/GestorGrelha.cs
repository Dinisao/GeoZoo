// GestorGrelha.cs — Constrói e gere a grelha de jogo (UI).
// Define o GridLayout (tamanho, spacing, padding), cria/destrói células,
// faz auto-resize do container, spawna peças na mão (opcional) e
// expõe utilitários para redimensionar peças e recolhê-las para a mão.

using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class GestorGrelha : MonoBehaviour
{
    [Header("Referências")]
    public RectTransform GridRoot;
    public GridLayoutGroup GridLayout;
    public Transform MaoContainer;
    public RectTransform DragLayer;

    [Header("Prefabs")]
    public GameObject PF_Celula;   // opcional (se vazio, crio uma célula simples)
    public GameObject PF_Peca;     // obrigatório para spawn na mão

    [Header("Layout da grelha")]
    [Min(1)] public int Linhas = 3;
    [Min(1)] public int Colunas = 4;
    public Vector2 CellSize = new Vector2(220, 220);
    public Vector2 Spacing  = new Vector2(8, 8);
    public RectOffset Padding;

    [Header("Auto-resize")]
    public bool AutoResizeGridRoot = true;       // ajusta o Rect da grelha ao nº de células
    public bool AutoResizePecasNaGrelha = true;  // reescala peças já colocadas quando o cellSize muda

    [Header("Spawn inicial na mão")]
    public bool SpawnPecasNaMaoAoIniciar = false;
    public bool IgnorarSpawnSeJaExistiremPecasNaMao = true;

    [System.Serializable] public struct FacePair { public string Id; public Sprite Frente; public Sprite Verso; }
    public FacePair[] ParIniciais = new FacePair[4];

    [Header("Debug")]
    public bool DebugLogs = true;
    public Color CorCelula = new Color(1f, 1f, 1f, 0.06f);

    // Repõe valores sensatos no Inspector quando o componente é adicionado.
    void Reset()
    {
        if (Padding == null) Padding = new RectOffset(8, 8, 8, 8);
    }

    // Reflete alterações de Inspector imediatamente (layout, tamanho e ajuste de peças).
    void OnValidate()
    {
        if (Padding == null) Padding = new RectOffset(8, 8, 8, 8);
        if (GridLayout != null)
        {
            ConfigurarGridLayout();
            if (AutoResizeGridRoot) AjustarTamanhoGridRoot();
            if (AutoResizePecasNaGrelha) RedimensionarPecasNaGrelha();
        }
    }

    // Setup inicial: garante GridLayout, aplica configuração e constrói a grelha.
    void Awake()
    {
        if (Padding == null) Padding = new RectOffset(8, 8, 8, 8);
        if (!GridRoot)
        {
            Debug.LogError("[GestorGrelha] GridRoot não atribuído.", this);
            return;
        }

        GridLayout = GridLayout ? GridLayout : GridRoot.GetComponent<GridLayoutGroup>();
        if (!GridLayout) GridLayout = GridRoot.gameObject.AddComponent<GridLayoutGroup>();

        ConfigurarGridLayout();
        if (AutoResizeGridRoot) AjustarTamanhoGridRoot();

        CriarOuRecriarGrelha();

        if (SpawnPecasNaMaoAoIniciar) SpawnPecasIniciaisNaMao();
    }

    // Aplica os parâmetros de layout atuais ao GridLayoutGroup.
    void ConfigurarGridLayout()
    {
        GridLayout.cellSize       = CellSize;
        GridLayout.spacing        = Spacing;
        GridLayout.padding        = Padding ?? new RectOffset();
        GridLayout.childAlignment = TextAnchor.MiddleCenter;
        GridLayout.startAxis      = GridLayoutGroup.Axis.Horizontal;
        GridLayout.startCorner    = GridLayoutGroup.Corner.UpperLeft;
        GridLayout.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
        GridLayout.constraintCount = Colunas;
    }

    // Ajusta o Rect do GridRoot para comportar todas as células segundo CellSize/Spacing/Padding.
    void AjustarTamanhoGridRoot()
    {
        float w = Padding.left + Padding.right  + Colunas * CellSize.x + (Colunas - 1) * Spacing.x;
        float h = Padding.top  + Padding.bottom + Linhas  * CellSize.y + (Linhas  - 1) * Spacing.y;

        GridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        GridRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   h);
    }

    // Reconstrói a grelha completa: remove células antigas e cria novas com índices X/Y.
    public void CriarOuRecriarGrelha()
    {
        if (!GridRoot) return;

        var paraApagar = GridRoot.GetComponentsInChildren<CelulaGrelha>(true)
                                 .Select(c => c.gameObject).ToList();
        foreach (var go in paraApagar)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        ConfigurarGridLayout();
        if (AutoResizeGridRoot) AjustarTamanhoGridRoot();

        for (int y = 0; y < Linhas; y++)
        for (int x = 0; x < Colunas; x++)
        {
            GameObject go;
            if (PF_Celula != null)
            {
                go = Instantiate(PF_Celula, GridRoot);
            }
            else
            {
                go = new GameObject($"Celula_{x}_{y}", typeof(RectTransform), typeof(Image), typeof(CelulaGrelha));
                var img = go.GetComponent<Image>();
                img.color = CorCelula;
                img.raycastTarget = false;
                go.transform.SetParent(GridRoot, false);
            }

            var cel = go.GetComponent<CelulaGrelha>() ?? go.AddComponent<CelulaGrelha>();
            cel.Index = new Vector2Int(x, y);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = CellSize;
        }

        if (AutoResizePecasNaGrelha) RedimensionarPecasNaGrelha();

        if (DebugLogs)
            Debug.Log($"[GestorGrelha] Grelha criada: {Colunas}x{Linhas} ({Colunas * Linhas}).", this);
    }

    // Spawna peças iniciais na mão (respeita a flag para evitar duplicação).
    public void SpawnPecasIniciaisNaMao()
    {
        if (!MaoContainer || !PF_Peca)
        {
            Debug.LogWarning("[GestorGrelha] Sem MaoContainer ou PF_Peca para spawn inicial.", this);
            return;
        }

        if (IgnorarSpawnSeJaExistiremPecasNaMao &&
            MaoContainer.GetComponentsInChildren<Peca>(true).Any())
        {
            if (DebugLogs) Debug.Log("[GestorGrelha] Já existem peças na mão.", this);
            return;
        }

        int n = Mathf.Max(ParIniciais?.Length ?? 0, 4);
        for (int i = 0; i < n; i++)
        {
            var go = Instantiate(PF_Peca, MaoContainer, false);
            go.name = $"Peca_Mao_{i + 1}";

            var p = go.GetComponent<Peca>();
            if (p) p.ConfigurarContexto(GridRoot, GridLayout, (RectTransform)MaoContainer, DragLayer);

            var flip = go.GetComponent<PecaFlip>();
            if (flip)
            {
                if (ParIniciais != null && i < ParIniciais.Length)
                {
                    flip.Frente = ParIniciais[i].Frente;
                    flip.Verso  = ParIniciais[i].Verso;
                }
                flip.Aplicar(true);
            }
        }

        if (DebugLogs) Debug.Log($"[GestorGrelha] Spawn inicial concluído ({n}).", this);
    }

    // Reaplica tamanho/anchors das peças que já estão na grelha para casar com o cellSize atual.
    public void RedimensionarPecasNaGrelha()
    {
        foreach (Transform t in GridRoot)
        {
            var cel = t.GetComponent<CelulaGrelha>();
            if (!cel) continue;

            var peca = t.GetComponentInChildren<Peca>(false);
            if (!peca) continue;

            var rt = peca.transform as RectTransform;
            if (!rt) continue;

            rt.sizeDelta = GridLayout.cellSize;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    // -------- NOVO: utilitário para o pós-acerto --------
    // Percorre as células e pede a cada peça colocada que regresse à mão.
    public void RecolherTodasPecasParaMao()
    {
        if (!GridRoot) return;

        for (int i = 0; i < GridRoot.childCount; i++)
        {
            var cell = GridRoot.GetChild(i) as RectTransform;
            if (!cell) continue;
            if (!cell.GetComponent<CelulaGrelha>()) continue;

            var peca = cell.GetComponentInChildren<Peca>(includeInactive: false);
            if (peca != null) peca.VoltarParaMao();
        }
    }
}
