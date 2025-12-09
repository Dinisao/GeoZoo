using UnityEngine;
using UnityEngine.UI;

public class PageIndicators : MonoBehaviour
{
    public SwipeMenu swipe;
    public Image[] dots;

    void Update()
    {
        int page = swipe.CurrentPage;

        for (int i = 0; i < dots.Length; i++)
        {
            if (i == page)
                dots[i].color = Color.white;   // dot ativo
            else
                dots[i].color = Color.black;   // dot inativo
        }
    }
}
