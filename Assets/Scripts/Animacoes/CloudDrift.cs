using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class CloudDrift2D : MonoBehaviour
{
    public enum Direction { LeftToRight, RightToLeft }

    [Header("Câmara de referência")]
    public Camera TargetCamera;                 // se vazio, usa Camera.main (ortográfica)

    [Header("Movimento")]
    public Direction MoveDirection = Direction.RightToLeft;
    [Tooltip("Unidades por segundo (mundo). Mantém lento.")]
    [Range(0.05f, 2f)] public float Speed = 0.5f;
    public bool RandomizeSpeed = true;
    public Vector2 SpeedRange = new Vector2(0.25f, 0.65f); // todas lentas
    [Tooltip("Margem extra para considerar 'fora de ecrã' (unidades de mundo).")]
    public float OffscreenMargin = 0.5f;
    [Tooltip("Usa unscaledDeltaTime para continuar mesmo se o jogo pausar timeScale=0.")]
    public bool UseUnscaledTime = true;

    [Header("Balanço opcional (suave)")]
    public bool Bobbing = true;
    public float BobAmplitude = 0.15f;     // unidades de mundo
    public float BobFrequency = 0.25f;     // ciclos por segundo

    // internos
    SpriteRenderer _sr;
    float _halfWidth;          // metade da largura em mundo
    float _baseY;
    float _bobPhase;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (TargetCamera == null) TargetCamera = Camera.main;
        _baseY = transform.position.y;
        _bobPhase = Random.Range(0f, Mathf.PI * 2f);

        // calcular metade da largura real em mundo
        // Nota: bounds já em espaço de mundo e inclui scale.
        _halfWidth = _sr.bounds.extents.x;

        if (RandomizeSpeed)
            Speed = Random.Range(SpeedRange.x, SpeedRange.y);
    }

    void LateUpdate()
    {
        if (TargetCamera == null) return;

        float dt = UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Mover em X conforme direção
        float dir = (MoveDirection == Direction.LeftToRight) ? 1f : -1f;
        Vector3 pos = transform.position;
        pos.x += dir * Speed * dt;

        // Bobbing suave opcional (não altera X de loop)
        if (Bobbing && BobAmplitude > 0f && BobFrequency > 0f)
        {
            float t = UseUnscaledTime ? Time.unscaledTime : Time.time;
            pos.y = _baseY + Mathf.Sin((t * BobFrequency * Mathf.PI * 2f) + _bobPhase) * BobAmplitude;
        }

        // Repor quando sai do ecrã
        GetHorizontalBounds(TargetCamera, out float leftX, out float rightX);

        float leftLimit  = leftX  - OffscreenMargin - _halfWidth;
        float rightLimit = rightX + OffscreenMargin + _halfWidth;

        if (MoveDirection == Direction.RightToLeft)
        {
            // saiu pela ESQUERDA → volta a entrar pela DIREITA
            if (pos.x < leftLimit)
                pos.x = rightLimit;
        }
        else
        {
            // saiu pela DIREITA → volta a entrar pela ESQUERDA
            if (pos.x > rightLimit)
                pos.x = leftLimit;
        }

        transform.position = pos;
    }

    void GetHorizontalBounds(Camera cam, out float leftX, out float rightX)
    {
        if (cam.orthographic)
        {
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            float cx = cam.transform.position.x;
            leftX  = cx - halfW;
            rightX = cx + halfW;
        }
        else
        {
            // fallback para câmara perspetiva: sample às bordas do viewport no plano da nuvem
            float z = transform.position.z - cam.transform.position.z;
            Vector3 leftWorld  = cam.ViewportToWorldPoint(new Vector3(0f, 0.5f, z));
            Vector3 rightWorld = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, z));
            leftX  = leftWorld.x;
            rightX = rightWorld.x;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr) _halfWidth = sr.bounds.extents.x;
        }
        Camera cam = TargetCamera != null ? TargetCamera : Camera.main;
        if (cam == null) return;

        GetHorizontalBounds(cam, out float leftX, out float rightX);
        float leftLimit  = leftX  - OffscreenMargin - _halfWidth;
        float rightLimit = rightX + OffscreenMargin + _halfWidth;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(leftLimit,  transform.position.y - 0.1f, transform.position.z),
                        new Vector3(leftLimit,  transform.position.y + 0.1f, transform.position.z));
        Gizmos.DrawLine(new Vector3(rightLimit, transform.position.y - 0.1f, transform.position.z),
                        new Vector3(rightLimit, transform.position.y + 0.1f, transform.position.z));
    }
#endif
}
