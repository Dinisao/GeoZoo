using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [Header("Refer�ncias UI")]
    public Button playButton;

    [Header("Configura��es")]
    [Tooltip("Nome da cena principal a carregar")]
    public string nomeCenaPrincipal = "CenaPrincipal";

    void Start()
    {
        // Adiciona o listener ao bot�o
        if (playButton != null)
        {
            playButton.onClick.AddListener(IniciarJogo);
        }
        else
        {
            Debug.LogError("Bot�o Play n�o foi atribu�do no Inspector!");
        }
    }

    // M�todo chamado quando o bot�o Play � clicado
    public void IniciarJogo()
    {
        // Carrega a cena principal
        SceneManager.LoadScene("GeoZoo");
    }

    // M�todo alternativo: carregar por �ndice da cena
    public void IniciarJogoPorIndice(int indiceCena)
    {
        SceneManager.LoadScene(indiceCena);
    }

    // M�todo para sair do jogo
    public void SairJogo()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}