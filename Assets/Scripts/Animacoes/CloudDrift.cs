using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(Image))]
public class CloudDriftUI : MonoBehaviour
{
    public enum Direction { LeftToRight, RightToLeft }

    [Header("Área de movimento")]
    public RectTransform TrackArea;                 // se vazio, usa o pai (CloudsLayer)

    [Header("Movimento")]
    public Direction MoveDirection = Direction.RightToLeft;
    [Tooltip("Pixels por segundo (UI). Mantém lento.")]
    [Range(5f, 120f)] public float Speed = 24f;
    public bool RandomizeSpeed = true;
    public Vector2 SpeedRange = new Vector2(18f, 36f); // todas lentas
    [Tooltip("Quanto deve sair do ecrã antes de reaparecer (px).")]
    public float Margin = 60f;
    [Tooltip("Continua a mexer mesmo se timeScale=0 (ex.: Game Over).")]
    public bool UseUnscaledTime = true;

    [Header("Balanço opcional")]
    public bool Bobbing = true;
    public float BobAmplitude = 6f;   // px
    public float BobSpeed = 0.25f;    // ciclos/seg

    // internos
    RectTransform _rt;
    float _halfWidth;
    float _baseY;
    float _phase;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (TrackArea == null) TrackArea = _rt.parent as RectTransform;

        // UI: estas nuvens não precisam receber cliques
        var img = GetComponent<Image>();
        if (img) img.raycastTarget = false;

        _baseY = _rt.anchoredPosition.y;
        _phase = Random.Range(0f, Mathf.PI * 2f);
        _halfWidth = Mathf.Abs(_rt.rect.width) * 0.5f;

        if (RandomizeSpeed)
            Speed = Random.Range(SpeedRange.x, SpeedRange.y);
    }

    void OnRectTransformDimensionsChange()
    {
        if (_rt != null) _halfWidth = Mathf.Abs(_rt.rect.width) * 0.5f;
    }

    void LateUpdate()
    {
        if (TrackArea == null) return;

        float dt = UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // mover em X
        float dir = (MoveDirection == Direction.LeftToRight) ? 1f : -1f;
        var pos = _rt.anchoredPosition;
        pos.x += dir * Speed * dt;

        // bobbing (suave)
        if (Bobbing && BobAmplitude > 0f && BobSpeed > 0f)
        {
            float t = UseUnscaledTime ? Time.unscaledTime : Time.time;
            pos.y = _baseY + Mathf.Sin(_phase + t * BobSpeed * 2f * Mathf.PI) * BobAmplitude;
        }

        // limites horizontais com base no TrackArea (pivot central)
        float halfW = TrackArea.rect.width * 0.5f;
        float leftLimit  = -halfW - Margin - _halfWidth;
        float rightLimit =  halfW + Margin + _halfWidth;

        if (MoveDirection == Direction.RightToLeft)
        {
            if (pos.x < leftLimit) pos.x = rightLimit;   // saiu à esquerda → entra pela direita
        }
        else
        {
            if (pos.x > rightLimit) pos.x = leftLimit;   // saiu à direita → entra pela esquerda
        }

        _rt.anchoredPosition = pos;
    }
}
