using UnityEngine;

/// <summary>
/// Dados da última run — usado para passar info para a cena do Scoreboard.
/// </summary>
public static class LastRunData
{
    public static bool TemDados => _temDados;

    public static string NomeJogador = "";
    public static int Zoo;
    public static int SegundosJogados;
    public static bool FoiComTutorial;

    static bool _temDados = false;

    public static void Limpar()
    {
        _temDados = false;
        NomeJogador = "";
        Zoo = 0;
        SegundosJogados = 0;
        FoiComTutorial = false;
    }

    public static void Capturar(ControladorJogo cj)
    {
        if (cj == null)
        {
            Limpar();
            return;
        }

        Zoo = cj.ZooAtual;
        SegundosJogados = cj.TempoJogadoInt;   // tempo real em jogo
        FoiComTutorial = cj.FoiRunComTutorial;
        _temDados = true;
    }

    public static string FormatarTempo()
    {
        int segundos = Mathf.Max(0, SegundosJogados);
        int m = segundos / 60;
        int s = segundos % 60;
        return $"{m}:{s:00}";
    }
}
