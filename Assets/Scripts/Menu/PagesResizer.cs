using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class PagesResizer : MonoBehaviour
{
    public RectTransform viewport;
    private ScrollRect scrollRect;
    private bool hasSetInitialPosition = false;

    void Update()
    {
        if (viewport == null) return;

        float width = viewport.rect.width;
        float height = viewport.rect.height;

        // Ajusta o tamanho do Content para caber todas as páginas
        RectTransform contentRect = GetComponent<RectTransform>();
        int childCount = transform.childCount;
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * childCount);
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        // Ajusta cada página
        int index = 0;
        foreach (RectTransform child in transform)
        {
            // Define o tamanho
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            // Configura os anchors e pivot no centro
            child.anchorMin = new Vector2(0, 0.5f);
            child.anchorMax = new Vector2(0, 0.5f);
            child.pivot = new Vector2(0.5f, 0.5f);

            // Posiciona horizontalmente (lado a lado) - ajustado para pivot central
            child.anchoredPosition = new Vector2((width * index) + (width * 0.5f), 0);

            index++;
        }

        // Forçar começar na Page1 apenas em Play Mode
        if (Application.isPlaying && !hasSetInitialPosition)
        {
            ResetToPage1();
            hasSetInitialPosition = true;
        }
    }

    void ResetToPage1()
    {
        if (scrollRect == null)
        {
            // Procura o ScrollRect no parent (PagesScroll)
            scrollRect = GetComponentInParent<ScrollRect>();
        }

        if (scrollRect != null)
        {
            // Reseta a posição horizontal para 0 (primeira página)
            scrollRect.horizontalNormalizedPosition = 0f;
        }
    }

    // Método público caso queira chamar de outro script
    public void GoToPage1()
    {
        ResetToPage1();
    }
}