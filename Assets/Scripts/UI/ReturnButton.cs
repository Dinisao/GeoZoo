using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script utilit�rio para carregar uma cena espec�fica e gerir o popup de confirma��o.
/// </summary>
public class ReturnButton : MonoBehaviour
{
    [Tooltip("O nome exato da cena de destino (ex: Menu).")]
    public string nomeDaCenaDeMenu = "Menu";

    // NOVO: Refer�ncia ao Painel de Confirma��o
    [Header("Confirma��o UI")]
    [Tooltip("O GameOject do painel pop-up de confirma��o. Deve estar inicialmente inativo.")]
    public GameObject PainelConfirma;

    // --- M�todos de Confirma��o ---

    /// <summary>
    /// Chamado pelo bot�o principal de "Return". Mostra o painel de confirma��o.
    /// </summary>
    public void PedirConfirmacao()
    {
        if (PainelConfirma != null)
        {
            PainelConfirma.SetActive(true);
            // Time.timeScale = 0f; // Descomente para pausar o jogo durante a confirma��o
        }
    }

    /// <summary>
    /// Chamado pelo bot�o "N�o" (Cancela). Esconde o painel e continua o jogo.
    /// </summary>
    public void CancelarVoltar()
    {
        if (PainelConfirma != null)
        {
            PainelConfirma.SetActive(false);
            // Time.timeScale = 1f; // Descomente para retomar o jogo
        }
    }

    // --- M�todo Principal de Transi��o ---

    /// <summary>
    /// Chamado pelo bot�o "Sim" (Confirma). Carrega o menu.
    /// </summary>
    public void VoltarParaMenu()
{
    // Se voltas ao Menu, no próximo arranque de jogo queremos mostrar o tutorial.
    TutorialDeckHint.ProximoArranqueDeveMostrarTutorial = true;

    // Time.timeScale = 1f; 

    SceneManager.LoadScene(nomeDaCenaDeMenu);
}

}
