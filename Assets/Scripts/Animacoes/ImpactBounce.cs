using System.Collections;
using UnityEngine;

/// Bounce global "cartoon": antecipação (squash) + mola contínua + shake perlin.
/// Dispara com: ImpactBounce.Instancia?.Play(intensity);
[DisallowMultipleComponent]
public class ImpactBounce : MonoBehaviour
{
    public static ImpactBounce Instancia { get; private set; }

    [Header("Alvo (UI)")]
    [Tooltip("RectTransform que contém TODA a UI a abanar (UI_RootWrapper).")]
    public RectTransform Wrapper;

    [Header("Fase 0 — Antecipação (squash)")]
    [Tooltip("Duração da antecipação antes do bounce principal (s).")]
    [Range(0.00f, 0.25f)] public float PreDuration = 0.08f;
    [Tooltip("Intensidade do squash prévio (0..1). 0.06 ≈ 6%")]
    [Range(0.00f, 0.25f)] public float PreSquash = 0.06f;
    [Tooltip("Deslocamento ligeiro para baixo na antecipação (px UI).")]
    [Range(0f, 20f)] public float PreDipPx = 6f;

    [Header("Fase 1 — Mola contínua")]
    [Tooltip("Duração do bounce principal (s).")]
    [Range(0.20f, 1.20f)] public float MainDuration = 0.55f;
    [Tooltip("Oscilações total (2.5–3.8 dá bounce 'funny').")]
    [Range(1.0f, 5.0f)] public float Cycles = 3.2f;
    [Tooltip("Amortecimento da mola (1.4–2.2 é bouncy sem morrer logo).")]
    [Range(0.5f, 4.0f)] public float Damping = 1.9f;
    [Tooltip("Amplitude base de escala (0.08 = +8%).")]
    [Range(0.00f, 0.30f)] public float ScaleAmp = 0.09f;
    [Tooltip("Peso do squash&stretch (0=off, 1=forte).")]
    [Range(0.0f, 1.5f)] public float Squash = 1.05f;
    [Tooltip("Rotação leve (graus).")]
    [Range(0.0f, 8.0f)] public float RotDeg = 1.2f;

    [Header("Shake 'fofinho' (Perlin)")]
    [Tooltip("Amplitude do shake (px UI).")]
    [Range(0f, 8f)] public float ShakePx = 1.4f;
    [Tooltip("Frequência do Perlin (Hz aprox.).")]
    [Range(0.5f, 10f)] public float ShakeHz = 6f;

    [Header("Auto-find (opcional)")]
    public bool AutoFindByName = true;
    public string WrapperName = "UI_RootWrapper";

    [Header("Debug")]
    public bool DebugLogs = false;

    // Estado
    Vector3 _origScale;
    Vector3 _origPos3D;
    float   _origRotZ;
    bool    _hasOrig;
    Coroutine _run;

    void Awake()
    {
        if (Instancia && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
    }

    void Start()
    {
        if (!Wrapper && AutoFindByName && !string.IsNullOrEmpty(WrapperName))
        {
            var go = GameObject.Find(WrapperName);
            if (go) Wrapper = go.GetComponent<RectTransform>();
            if (DebugLogs) Debug.Log($"[ImpactBounce] AutoFind '{WrapperName}': {(Wrapper ? "OK" : "N/A")}", this);
        }
        if (!Wrapper && DebugLogs) Debug.LogWarning("[ImpactBounce] Wrapper não atribuído!", this);
    }

    /// <summary>Dispara o efeito. intensity=1 é default (escala vários parâmetros).</summary>
    public void Play(float intensity = 1f)
    {
        if (!isActiveAndEnabled || !Wrapper) { if (DebugLogs) Debug.LogWarning("[ImpactBounce] Sem Wrapper/ativo.", this); return; }
        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(CoPlay(Mathf.Max(0.01f, intensity)));
    }

    [ContextMenu("Test Bounce")]
    void TestBounceContext() => Play(1f);

    IEnumerator CoPlay(float intensity)
    {
        // snapshot
        _origScale = Wrapper.localScale;
        _origPos3D = Wrapper.anchoredPosition3D;
        _origRotZ  = Wrapper.localEulerAngles.z;
        _hasOrig   = true;

        // Fase 0: antecipação (pequeno squash + dip para baixo)
        if (PreDuration > 0f && PreSquash > 0f)
        {
            float t = 0f, dur = PreDuration;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = EaseInQuad(k); // entra rápido, ajuda "anticipation"
                float sX = 1f + PreSquash * intensity * e; // alarga X
                float sY = 1f - PreSquash * intensity * e; // comprime Y
                float dip = PreDipPx * intensity * e;

                Wrapper.localScale = new Vector3(_origScale.x * sX, _origScale.y * sY, _origScale.z);
                Wrapper.anchoredPosition3D = _origPos3D + new Vector3(0f, -dip, 0f);
                yield return null;
            }
        }

        // Fase 1: mola contínua (sem "pancadas")
        float time = 0f;
        float durMain = Mathf.Max(0.05f, MainDuration);
        float omega = Cycles * 2f * Mathf.PI / durMain; // frequência angular
        float seed = Random.value * 1000f;              // para o perlin

        while (time < durMain)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / durMain);

            // amortecimento "lento" → bouncy/funny
            float dec = Mathf.Exp(-Damping * k);

            // seno principal
            float sin = Mathf.Sin(omega * time);
            float amp = ScaleAmp * intensity;

            // escala isotrópica + squash&stretch a 90° de fase
            float baseS = 1f + amp * dec * sin;
            float ssTerm = Squash * amp * dec * Mathf.Cos(omega * time);

            // limites mínimos para não "achatar" demais
            float sx = Mathf.Max(0.82f, baseS + ssTerm);
            float sy = Mathf.Max(0.82f, baseS - ssTerm);

            // rotação suave (fase desviada)
            float rz = _origRotZ + RotDeg * intensity * dec * Mathf.Sin(omega * time + Mathf.PI * 0.35f);

            // shake com Perlin (suave, sem estalos)
            float n1 = Mathf.PerlinNoise(seed + time * ShakeHz, 0f) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(seed + 77f + time * (ShakeHz * 0.87f), 0f) * 2f - 1f;
            float offX = (ShakePx * intensity) * dec * n1;
            float offY = (ShakePx * intensity) * dec * n2;

            // aplicar
            Wrapper.localScale = new Vector3(_origScale.x * sx, _origScale.y * sy, _origScale.z);
            var eul = Wrapper.localEulerAngles; eul.z = rz; Wrapper.localEulerAngles = eul;
            Wrapper.anchoredPosition3D = _origPos3D + new Vector3(offX, offY, 0f);

            yield return null;
        }

        // restore exato
        if (_hasOrig)
        {
            Wrapper.localScale = _origScale;
            var eul = Wrapper.localEulerAngles; eul.z = _origRotZ; Wrapper.localEulerAngles = eul;
            Wrapper.anchoredPosition3D = _origPos3D;
        }

        _run = null;
    }

    // Eases simples
    static float EaseInQuad(float x) => x * x;
}
