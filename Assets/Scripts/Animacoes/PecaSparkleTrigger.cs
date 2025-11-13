using UnityEngine;

[DisallowMultipleComponent]
public class PecaSparkleTrigger : MonoBehaviour
{
    [SerializeField] private CellSparkleFx fx;
    [SerializeField] private bool contornarTileNaoCelula = false;
    [SerializeField] private bool umaVezPorRonda = true;

    private int rondaVista = -1;
    private static int rondaGlobal = 0;

    public static void NovaRonda()
    {
        rondaGlobal++;
        Debug.Log($"[Sparkle] NovaRonda → {rondaGlobal}");
    }

    private void Awake()
    {
        if (!fx) fx = FindAnyObjectByType<CellSparkleFx>(FindObjectsInactive.Exclude);
    }

    private void OnEnable() { rondaVista = -1; }

    private void OnTransformParentChanged()
    {
        if (!Application.isPlaying) return;

        var parent = transform.parent as RectTransform;
        if (!parent) return;

        // ✅ Em vez de depender do nome, procura o componente CelulaGrelha.
        bool eCelula = parent.GetComponent<CelulaGrelha>() != null;
        if (!eCelula) return;

        if (umaVezPorRonda && rondaVista == rondaGlobal)
        {
            Debug.Log("[Sparkle] Bloqueado (já tocado nesta ronda).");
            return;
        }

        rondaVista = rondaGlobal;
        RectTransform alvo = contornarTileNaoCelula ? (RectTransform)transform : parent;
        if (fx && alvo) fx.PlayOnce(alvo);
    }
}
