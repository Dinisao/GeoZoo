// PatternsTools.cs — Utilitários de Editor para inspecionar e corrigir patterns.
// Menu "GeoZoo/Patterns":
//  • Diagnóstico Rotacoes Globais: faz scan aos AnimalPattern e avisa os que têm a flag OFF.
//  • Fixar Rotacoes Globais (forçar ON): liga a flag em todos os patterns e grava alterações.

using UnityEditor;
using UnityEngine;

public static class PatternsTools
{
    // Percorre todos os ScriptableObjects AnimalPattern do projeto e
    // escreve um aviso para cada um que tenha PermitirRotacoesGlobais = false.
    [MenuItem("GeoZoo/Patterns/Diagnóstico Rotacoes Globais")]
    public static void Diagnostico()
    {
        var guids = AssetDatabase.FindAssets("t:AnimalPattern");
        int off = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var ap = AssetDatabase.LoadAssetAtPath<AnimalPattern>(path);
            if (ap && !ap.PermitirRotacoesGlobais)
            {
                off++;
                Debug.LogWarning($"[Patterns] {ap.name} com PermitirRotacoesGlobais = FALSE", ap);
            }
        }
        Debug.Log($"[Patterns] Diagnóstico concluído. Patterns com flag OFF: {off}");
    }

    // Força PermitirRotacoesGlobais = true em todos os AnimalPattern encontrados,
    // marca-os como “dirty” e faz SaveAssets no fim.
    [MenuItem("GeoZoo/Patterns/Fixar Rotacoes Globais (forçar ON)")]
    public static void FixarRotacoesGlobais()
    {
        var guids = AssetDatabase.FindAssets("t:AnimalPattern");
        int changed = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var ap = AssetDatabase.LoadAssetAtPath<AnimalPattern>(path);
            if (ap && !ap.PermitirRotacoesGlobais)
            {
                ap.PermitirRotacoesGlobais = true;
                EditorUtility.SetDirty(ap);
                changed++;
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Patterns] Atualizados: {changed}. (PermitirRotacoesGlobais = TRUE)");
    }
}
