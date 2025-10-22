using UnityEditor;
using UnityEngine;

public static class PatternsTools
{
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
