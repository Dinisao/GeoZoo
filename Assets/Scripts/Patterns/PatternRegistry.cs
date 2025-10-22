// PatternRegistry.cs — Lista central de AnimalPattern disponíveis na cena.
// Serve como “catálogo” para lookup ou seleção aleatória de padrões.

using System.Collections.Generic;
using UnityEngine;

public class PatternRegistry : MonoBehaviour
{
    public List<AnimalPattern> Patterns = new List<AnimalPattern>(); // Preenche no Inspector com os ScriptableObjects dos padrões

    // Devolve um padrão aleatório da lista (ou null se estiver vazia).
    public AnimalPattern EscolherAleatorio()
    {
        if (Patterns == null || Patterns.Count == 0) return null;
        return Patterns[Random.Range(0, Patterns.Count)];
    }
}
