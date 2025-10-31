using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Script utilitário para carregar uma cena específica e gerir o popup de confirmação.
/// </summary>
public class ReturnButton : MonoBehaviour
{
    [Tooltip("O nome exato da cena de destino (ex: Menu).")]
    public string nomeDaCenaDeMenu = "Menu";

    // NOVO: Referência ao Painel de Confirmação
    [Header("Confirmação UI")]
    [Tooltip("O GameOject do painel pop-up de confirmação. Deve estar inicialmente inativo.")]
    public GameObject PainelConfirma;

    // --- Métodos de Confirmação ---

    /// <summary>
    /// Chamado pelo botão principal de "Return". Mostra o painel de confirmação.
    /// </summary>
    public void PedirConfirmacao()
    {
        if (PainelConfirma != null)
        {
            PainelConfirma.SetActive(true);
            // Time.timeScale = 0f; // Descomente para pausar o jogo durante a confirmação
        }
    }

    /// <summary>
    /// Chamado pelo botão "Não" (Cancela). Esconde o painel e continua o jogo.
    /// </summary>
    public void CancelarVoltar()
    {
        if (PainelConfirma != null)
        {
            PainelConfirma.SetActive(false);
            // Time.timeScale = 1f; // Descomente para retomar o jogo
        }
    }

    // --- Método Principal de Transição ---

    /// <summary>
    /// Chamado pelo botão "Sim" (Confirma). Carrega o menu.
    /// </summary>
    public void VoltarParaMenu()
    {
        // Certifica-se que a TimeScale está normalizada se tiver sido pausada
        // Time.timeScale = 1f; 

        // Usa a variável pública definida no Inspector
        SceneManager.LoadScene(nomeDaCenaDeMenu);

        // NOTA: O seu código anterior usava SceneManager.LoadScene("Menu");.
        // Mudei para SceneManager.LoadScene(nomeDaCenaDeMenu); para usar a variável pública,
        // o que é a melhor prática.
    }
}