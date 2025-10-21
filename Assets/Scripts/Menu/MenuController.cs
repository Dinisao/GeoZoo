using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [Header("Referências UI")]
    public Button playButton;

    [Header("Configurações")]
    [Tooltip("Nome da cena principal a carregar")]
    public string nomeCenaPrincipal = "CenaPrincipal";

    void Start()
    {
        // Adiciona o listener ao botão
        if (playButton != null)
        {
            playButton.onClick.AddListener(IniciarJogo);
        }
        else
        {
            Debug.LogError("Botão Play não foi atribuído no Inspector!");
        }
    }

    // Método chamado quando o botão Play é clicado
    public void IniciarJogo()
    {
        // Carrega a cena principal
        SceneManager.LoadScene("GeoZoo");
    }

    // Método alternativo: carregar por índice da cena
    public void IniciarJogoPorIndice(int indiceCena)
    {
        SceneManager.LoadScene(indiceCena);
    }

    // Método para sair do jogo
    public void SairJogo()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}