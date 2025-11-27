using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverButtons : MonoBehaviour
{
    [Header("Referências UI")]
    [Tooltip("Botão Retry (filho de UI_GameOver).")]
    public Button BtnRetry;

    [Tooltip("Botão Quit (filho de UI_GameOver).")]
    public Button BtnQuit;

    [Header("Cenas")]
    [Tooltip("Nome da cena principal de jogo.")]
    public string NomeCenaJogo = "GeoZoo";

    [Tooltip("Nome da cena de scoreboard.")]
    public string NomeCenaScore = "Scores";

    void Awake()
    {
        // Auto-ligar os eventos de clique, se as referências estiverem atribuídas
        if (BtnRetry != null)
        {
            BtnRetry.onClick.RemoveAllListeners();
            BtnRetry.onClick.AddListener(OnRetry);
        }
        else
        {
            Debug.LogError("[GameOverButtons] BtnRetry não atribuído no Inspector.");
        }

        if (BtnQuit != null)
        {
            BtnQuit.onClick.RemoveAllListeners();
            BtnQuit.onClick.AddListener(OnQuitToScores);
        }
        else
        {
            Debug.LogError("[GameOverButtons] BtnQuit não atribuído no Inspector.");
        }
    }

    /// <summary>
    /// Voltar a jogar desde o início (sem guardar score).
    /// </summary>
    public void OnRetry()
    {
        Debug.Log("[GameOverButtons] Retry clicado → carregar cena de jogo (SEM tutorial).");

        Time.timeScale = 1f;
        LastRunData.Limpar();                  // não queremos levar dados antigos

        // Próxima run: SEM tutorial
        GameRunConfig.UseTutorial = false;

        SceneManager.LoadScene(NomeCenaJogo);
    }

    /// <summary>
    /// Sair para o ecrã de scores (onde o jogador pode guardar ou não).
    /// </summary>
    public void OnQuitToScores()
    {
        Debug.Log("[GameOverButtons] Quit clicado → capturar dados e ir para Scores.");

        Time.timeScale = 1f;

        var cj = ControladorJogo.Instancia;
        LastRunData.Capturar(cj);              // guarda Zoo, tempo, se foi com tutorial, etc.

        SceneManager.LoadScene(NomeCenaScore);
    }
}
