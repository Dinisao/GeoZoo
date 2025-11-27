using UnityEngine;

/// <summary>
/// Liga ou desliga o tutorial desta run, com base em GameRunConfig.UseTutorial.
/// Deve estar no MESMO GameObject que o TutorialDeckHint (UI_Tutorial_Deck).
/// Também configura o ControladorJogo para 1ª carta "livre" quando há tutorial.
/// </summary>
[DefaultExecutionOrder(-900)]
public class TutorialRunToggle : MonoBehaviour
{
    void Awake()
    {
        var cj = ControladorJogo.Instancia;

        if (!GameRunConfig.UseTutorial)
        {
            Debug.Log("[TutorialRunToggle] Tutorial OFF nesta run → desativar UI_Tutorial_Deck.", this);

            if (cj != null)
            {
                // run normal: a 1ª carta já NÃO é “livre”
                cj.PrimeiraCartaLivreComTutorial = false;
            }

            // Desligar completamente o tutorial desta cena
            gameObject.SetActive(false);
            return;
        }

        Debug.Log("[TutorialRunToggle] Tutorial ON nesta run → 1ª carta LIVRE (modo tutorial).", this);

        if (cj != null)
        {
            // Com tutorial: 1ª carta não liga o timer
            cj.PrimeiraCartaLivreComTutorial = true;
            // FoiRunComTutorial continua a ser gerido internamente pelo ControladorJogo
            // quando a primeira carta “livre” chega ao preview.
        }
    }
}
