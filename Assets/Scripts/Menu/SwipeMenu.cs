using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SwipeMenu : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public ScrollRect scrollRect;
    public RectTransform content;
    public float snapSpeed = 10f;

    public int CurrentPage { get; private set; }

    private float[] positions;
    private bool isDragging;

    void Start()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
        if (content == null) content = scrollRect.content;

        int pageCount = content.childCount;
        positions = new float[pageCount];

        float step = 1f / (pageCount - 1);
        for (int i = 0; i < pageCount; i++)
            positions[i] = step * i;
    }

    void Update()
    {
        if (!isDragging)
        {
            float nearest = FindNearest(scrollRect.horizontalNormalizedPosition);
            scrollRect.horizontalNormalizedPosition =
                Mathf.Lerp(scrollRect.horizontalNormalizedPosition, nearest, Time.deltaTime * snapSpeed);

            // Atualiza a página atual
            CurrentPage = GetNearestPage(nearest);
        }
    }

    float FindNearest(float current)
    {
        float nearest = positions[0];
        for (int i = 1; i < positions.Length; i++)
            if (Mathf.Abs(current - positions[i]) < Mathf.Abs(current - nearest))
                nearest = positions[i];
        return nearest;
    }

    int GetNearestPage(float pos)
    {
        int nearestPage = 0;
        for (int i = 1; i < positions.Length; i++)
            if (Mathf.Abs(pos - positions[i]) < Mathf.Abs(pos - positions[nearestPage]))
                nearestPage = i;
        return nearestPage;
    }

    public void OnBeginDrag(PointerEventData eventData) { isDragging = true; }
    public void OnEndDrag(PointerEventData eventData) { isDragging = false; }
}
