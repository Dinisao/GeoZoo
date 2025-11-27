using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [Header("Referências UI")]
    public Button playButton;

    [Header("Configurações")]
    [Tooltip("Nome da cena principal a carregar (ex: GeoZoo)")]
    public string nomeCenaPrincipal = "GeoZoo";

    void Start()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(IniciarJogo);
        }
        else
        {
            Debug.LogError("[MenuController] Botão Play não foi atribuído no Inspector!");
        }
    }

    public void IniciarJogo()
    {
        string cena = string.IsNullOrEmpty(nomeCenaPrincipal)
            ? "GeoZoo"
            : nomeCenaPrincipal;

        // Sempre que vens do menu → run COM tutorial
        GameRunConfig.UseTutorial = true;

        SceneManager.LoadScene(cena);
    }

    // (mantive estes dois métodos caso queiras usar mais tarde)
    public void IniciarJogoPorIndice(int indiceCena)
    {
        SceneManager.LoadScene(indiceCena);
    }

    public void SairJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
