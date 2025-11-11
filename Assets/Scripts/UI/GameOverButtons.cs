using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameOverButtons : MonoBehaviour
{
    [Header("Referências")]
    public Button BtnRetry;              // botão com a imagem RETRY (filho do ImgFundo)
    public Button BtnQuit;               // botão com a imagem QUIT  (filho do ImgFundo)
    public string NomeCenaMenu = "Menu";

    void Awake()
    {
        if (BtnRetry) BtnRetry.onClick.AddListener(() =>
        {
            var s = SceneManager.GetActiveScene();
            SceneManager.LoadScene(s.name);          // Retry → recarrega a cena atual
        });

        if (BtnQuit) BtnQuit.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(NomeCenaMenu))
                SceneManager.LoadScene(NomeCenaMenu); // Quit → vai para "Menu"
        });
    }
}
