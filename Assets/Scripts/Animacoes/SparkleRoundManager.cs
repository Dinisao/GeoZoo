// SparkleRoundManager.cs — Contabiliza rondas para o sparkle "1x por ronda".
// Incrementa quando o GridValidator assinala um estado válido+completo (acerto).
// Só precisa de UMA instância na cena.

using UnityEngine;

[DefaultExecutionOrder(-50)]
public class SparkleRoundManager : MonoBehaviour
{
    public static int CurrentRound { get; private set; } = 0;

    [Tooltip("Se vazio, é descoberto automaticamente.")]
    public GridValidator Validator;

    bool _prevOk = false;

    void Awake()
    {
        if (Validator == null)
        {
#if UNITY_2023_1_OR_NEWER
            Validator = Object.FindFirstObjectByType<GridValidator>(FindObjectsInactive.Include);
#else
            Validator = Object.FindObjectOfType<GridValidator>(true);
#endif
        }
    }

    void Update()
    {
        if (!Validator) return;

        bool ok = (Validator.EstadoValido && Validator.EstadoCompleto);
        // quando muda de falso → verdadeiro, consideramos fim de ronda
        if (ok && !_prevOk)
        {
            CurrentRound++;
            // Opcional: Debug.Log($"[SparkleRoundManager] Nova ronda: {CurrentRound}");
        }
        _prevOk = ok;
    }
}
