using System.Collections.Generic;
using UnityEngine;

public class PatternRegistry : MonoBehaviour
{
    public List<AnimalPattern> Patterns = new List<AnimalPattern>();

    public AnimalPattern EscolherAleatorio()
    {
        if (Patterns == null || Patterns.Count == 0) return null;
        return Patterns[Random.Range(0, Patterns.Count)];
    }
}
