// CellSparkleFx.cs — Efeito de “contorno elétrico” a varrer o perímetro de uma célula UI.
// • Um único CellSparkleFx na cena (ex.: GameObject "FX_Sparkle" filho do Canvas).
// • O PecaSparkleTrigger chama PlayOnce(cellRect) para disparar o efeito nessa célula.
// • Varre o retângulo começando num canto (configurável) e seguindo CW/CCW.
// • Mantém pooling de “traços” (rects finos) para evitar GC.
//
// Dicas visuais:
//   - Para linha mais grossa: aumentar dashEspessuraPx, reduzir soproPxPorSeg.
//   - Para ficar menos “piquinhos” e mais faixa contínua: aumentar spawnPerSecond,
//     aumentar dashVida, reduzir dashTamanhoPx.x/y e soproPxPorSeg (até 0).

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CellSparkleFx : MonoBehaviour
{
    public enum Modo { BurstCentro, ContornarCelula }

    public enum StartCorner
    {
        TopLeft,
        TopRight,
        BottomRight,
        BottomLeft
    }

    // Nome do layer (GameObject UI) onde os traços são desenhados
    private static readonly string FX_LAYER_NAME = "FX_Sparkle";

    // ————— PARÂMETROS ——————————————————————————————————————————————————————

    [Header("Modo")]
    [SerializeField] private Modo modo = Modo.ContornarCelula;

    [Header("Ligação (auto/inspector)")]
    [SerializeField] private Canvas canvas;                // auto se vazio
    [SerializeField] private RectTransform fxLayer;        // container no Canvas (auto cria se não existir)
    [SerializeField] private Sprite sparkSprite;           // sprite de cada traço
    [SerializeField] private bool additiveMaterial = false;

    [Header("Percurso do contorno")]
    [SerializeField] private StartCorner startCorner = StartCorner.TopLeft;
    [SerializeField] private bool clockwise = true;        // true = sentido horário

    [Header("Sweep (perímetro)")]
    [SerializeField, Tooltip("Segundos para a cabeça dar uma volta completa ao perímetro")]
    private float sweepDuration = 0.70f;

    [SerializeField, Tooltip("Percentagem do perímetro que fica acesa atrás da cabeça (0..1)")]
    private float trailLenNormalized = 0.18f;

    [SerializeField, Tooltip("Quantos traços novos por segundo emitir (densidade da faixa)")]
    private int spawnPerSecond = 160;

    [Header("Traços (aparência)")]
    [SerializeField] private Vector2 dashTamanhoPx = new(8, 16);   // comprimento min/max do traço
    [SerializeField] private Vector2 dashEspessuraPx = new(3, 6);  // “grossura” (altura/espessura)
    [SerializeField] private Vector2 dashVida = new(0.40f, 0.75f); // vida dos traços
    [SerializeField, Tooltip("Velocidade de afastamento (na normal, px/s). 0 = colado à borda")]
    private float soproPxPorSeg = 12f;

    [Header("Burst (modo opcional)")]
    [SerializeField] private int quantidade = 12;
    [SerializeField] private Vector2 vidaSeg = new(0.35f, 0.7f);
    [SerializeField] private Vector2 vel = new(80f, 160f);
    [SerializeField] private Vector2 tamanhoPx = new(4f, 10f);
    [SerializeField] private float rotVel = 180f;

    [Header("Cor")]
    [SerializeField] private Color cor = Color.white;

    // ————— ESTADO/POOL ————————————————————————————————————————————————————

    private struct Part
    {
        public RectTransform rt;
        public Image img;
        public Vector2 v;
        public float t, life, rot;
    }

    private readonly List<Part> ativos = new();
    private readonly Stack<Part> pool = new();
    private Material matAdd;
    private Coroutine sweepCo;

    // ————— CICLO DE VIDA ———————————————————————————————————————————————————

    private void Awake()
    {
        if (!Application.isPlaying) return;

        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!fxLayer) fxLayer = EnsureFxLayer(canvas ? canvas.transform : transform.root);

        if (!sparkSprite)
            sparkSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        if (additiveMaterial)
        {
            // Usa um shader UI transparente como base; se tiveres um Shader Graph Additive, coloca-o aqui:
            var sh = Shader.Find("UI/Unlit/Transparent");
            if (sh) matAdd = new Material(sh);
        }
    }

    private RectTransform EnsureFxLayer(Transform parent)
    {
        var t = parent.Find(FX_LAYER_NAME) as RectTransform;
        if (!t)
        {
            var go = new GameObject(FX_LAYER_NAME, typeof(RectTransform));
            t = go.GetComponent<RectTransform>();
            t.SetParent(parent, false);
            t.anchorMin = Vector2.zero;
            t.anchorMax = Vector2.one;
            t.offsetMin = Vector2.zero;
            t.offsetMax = Vector2.zero;

            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }
        return t;
    }

    private void Update()
    {
        if (ativos.Count == 0) return;

        float dt = Time.unscaledDeltaTime;

        for (int i = ativos.Count - 1; i >= 0; i--)
        {
            var p = ativos[i];
            p.t += dt;

            p.rt.anchoredPosition += p.v * dt;
            if (p.rot != 0f) p.rt.Rotate(0, 0, p.rot * dt);

            float a = 1f - Mathf.SmoothStep(0.7f, 1f, p.t / p.life);
            var c = p.img.color; c.a = a; p.img.color = c;

            if (p.t >= p.life)
            {
                p.img.enabled = false;
                p.rt.SetParent(fxLayer, false);
                pool.Push(p);
                ativos.RemoveAt(i);
            }
            else ativos[i] = p;
        }
    }

    // ————— API ————————————————————————————————————————————————————————————

    public void PlayOnce(RectTransform alvo)
    {
        if (!Application.isPlaying || !alvo) return;

        if (modo == Modo.ContornarCelula)
        {
            if (sweepCo != null) StopCoroutine(sweepCo);
            sweepCo = StartCoroutine(CoSweepPerimeter(alvo));
        }
        else
        {
            SpawnCentro(alvo);
        }
    }

    // ————— IMPLEMENTAÇÃO ————————————————————————————————————————————————

    private IEnumerator CoSweepPerimeter(RectTransform alvo)
    {
        // 1) Canto do alvo em espaço do fxLayer
        Vector3[] wc = new Vector3[4];
        alvo.GetWorldCorners(wc); // ordem: 0=BL, 1=TL, 2=TR, 3=BR

        for (int i = 0; i < 4; i++) wc[i] = fxLayer.InverseTransformPoint(wc[i]);

        Vector2 bl = wc[0];
        Vector2 tl = wc[1];
        Vector2 tr = wc[2];
        Vector2 br = wc[3];

        // 2) Construir sequência de cantos segundo startCorner + sentido
        Vector2[] seqCW = new Vector2[4] { tl, tr, br, bl }; // sequência CW a partir do TL
        Vector2[] seq = BuildCornerSequence(seqCW, startCorner, clockwise);

        // 3) Pré-calcular comprimentos dos 4 lados (em px) e perímetro total
        float[] edgeLen = new float[4];
        float perim = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = seq[i];
            Vector2 b = seq[(i + 1) & 3];
            float L = Vector2.Distance(a, b);
            edgeLen[i] = L;
            perim += L;
        }

        // 4) Varre o perímetro
        float t = 0f;
        float emitAcc = 0f;

        // Densidade por frame = spawnPerSecond * dt
        while (t < sweepDuration)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;

            float head = Mathf.Clamp01(t / sweepDuration) * perim;  // distância ao longo do perímetro
            emitAcc += spawnPerSecond * dt;

            // “Janela” de trailing atrás da cabeça
            float trailLen = Mathf.Clamp01(trailLenNormalized) * perim;

            while (emitAcc >= 1f)
            {
                emitAcc -= 1f;

                // u ∈ [0, trailLen], amostra aleatória ao longo da janela
                float u = Random.value * trailLen;
                float L = head - u;
                // wrap para [0, perim)
                if (L < 0f) L += perim;

                // Posição/ângulos nesse comprimento L
                EvalPerimeterAt(seq, edgeLen, perim, L, out Vector2 pos, out Vector2 tan, out Vector2 nrm, out float ang);

                var p = GetPart();
                p.rt.anchoredPosition = pos;

                // Escolhe orientação do traço (paralelo ao lado) e dimensões
                float comp = Random.Range(dashTamanhoPx.x, dashTamanhoPx.y);
                float esp  = Random.Range(dashEspessuraPx.x, dashEspessuraPx.y);

                // O traço é “comprido” ao longo do tangente e “espesso” na normal
                // Para simplificar, rodamos o rect para alinhar com o ângulo do lado.
                p.rt.localEulerAngles = new Vector3(0, 0, ang);
                p.rt.sizeDelta = new Vector2(comp, esp);

                // “Sopro” ligeiro para dentro/fora (usa normal)
                p.v    = nrm.normalized * soproPxPorSeg;
                p.rot  = 0f;
                p.life = Random.Range(dashVida.x, dashVida.y);
                p.t    = 0f;

                var c = cor; c.a = 1f; p.img.color = c;
                p.img.enabled = true;

                ativos.Add(p);
            }

            yield return null;
        }

        sweepCo = null;
    }

    // Constrói a sequência de cantos a partir de TL→TR→BR→BL (CW),
    // aplicando o canto inicial e o sentido (CW/CCW).
    private Vector2[] BuildCornerSequence(Vector2[] seqCW, StartCorner start, bool cw)
    {
        // Índice do canto inicial na sequência CW
        int startIdxCW = 0; // TopLeft
        switch (start)
        {
            case StartCorner.TopLeft:     startIdxCW = 0; break;
            case StartCorner.TopRight:    startIdxCW = 1; break;
            case StartCorner.BottomRight: startIdxCW = 2; break;
            case StartCorner.BottomLeft:  startIdxCW = 3; break;
        }

        // Se CCW, invertimos a sequência base (CW)
        if (!cw)
        {
            // seqCCW = TL, BL, BR, TR
            var seqCCW = new Vector2[4] { seqCW[0], seqCW[3], seqCW[2], seqCW[1] };
            // mapear startIdx para a sequência CCW
            int[] map = new int[4] { 0, 3, 2, 1 }; // TL->0, TR->3, BR->2, BL->1
            int s = map[startIdxCW];
            return Rotate4(seqCCW, s);
        }
        else
        {
            return Rotate4(seqCW, startIdxCW);
        }
    }

    // Roda um array de 4 elementos para começar em 'start'
    private Vector2[] Rotate4(Vector2[] a, int start)
    {
        var r = new Vector2[4];
        for (int i = 0; i < 4; i++) r[i] = a[(start + i) & 3];
        return r;
    }

    // Dado um comprimento L ao longo do perímetro, calcula posição, tangente, normal e ângulo (graus).
    private void EvalPerimeterAt(Vector2[] seq, float[] edgeLen, float perim,
                                 float L,
                                 out Vector2 pos, out Vector2 tan, out Vector2 nrm, out float angDeg)
    {
        // Descobre em qual lado L cai
        float acc = 0f;
        for (int i = 0; i < 4; i++)
        {
            float nextAcc = acc + edgeLen[i];
            if (L <= nextAcc)
            {
                float local = Mathf.Clamp01((L - acc) / Mathf.Max(1e-4f, edgeLen[i]));
                Vector2 a = seq[i];
                Vector2 b = seq[(i + 1) & 3];

                pos = Vector2.LerpUnclamped(a, b, local);

                tan = (b - a);
                float len = tan.magnitude;
                tan = (len > 1e-4f) ? tan / len : new Vector2(1, 0);

                // Normal “para fora”: rotacionar tangente -90° (x, y) -> (y, -x)
                nrm = new Vector2(tan.y, -tan.x);

                angDeg = Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg;
                return;
            }
            acc = nextAcc;
        }

        // Fallback (pouco provável): fim do último lado
        pos = seq[3];
        Vector2 tb = seq[0] - seq[3];
        float l = tb.magnitude;
        tan = (l > 1e-4f) ? tb / l : new Vector2(1, 0);
        nrm = new Vector2(tan.y, -tan.x);
        angDeg = Mathf.Atan2(tan.y, tan.x) * Mathf.Rad2Deg;
    }

    // ——— Burst central (opcional) ———
    private void SpawnCentro(RectTransform alvo)
    {
        Vector2 centro = WorldToAnchored(alvo, fxLayer);
        for (int i = 0; i < quantidade; i++)
        {
            var p = GetPart();
            p.rt.anchoredPosition = centro;

            float ang = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float spd = Random.Range(vel.x, vel.y);
            p.v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd;

            float sz = Random.Range(tamanhoPx.x, tamanhoPx.y);
            p.rt.sizeDelta = new Vector2(sz * 0.25f, sz);
            p.rt.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 360f));
            p.rot = Random.Range(-rotVel, rotVel);

            p.life = Random.Range(vidaSeg.x, vidaSeg.y);
            p.t = 0f;

            var c = cor; c.a = 1f; p.img.color = c;
            p.img.enabled = true;
            ativos.Add(p);
        }
    }

    // ——— Pool ———
    private Part GetPart()
    {
        if (pool.Count > 0)
        {
            var p = pool.Pop();
            p.rt.SetParent(fxLayer, false);
            return p;
        }

        var go = new GameObject("spark", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(fxLayer, false);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = sparkSprite;
        if (matAdd) img.material = matAdd;

        return new Part { rt = rt, img = img };
    }

    // ——— Utils ———
    private static Vector2 WorldToAnchored(RectTransform child, RectTransform parent)
    {
        Vector2 world = child.TransformPoint(child.rect.center);
        Vector2 local = parent.InverseTransformPoint(world);
        return local;
    }
}
