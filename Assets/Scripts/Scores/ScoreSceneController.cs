using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ScoreSceneController : MonoBehaviour
{
    [Header("Header")]
    public TMP_Text TitleText;
    public TMP_Text SubtitleText;

    [Header("Resumo última run (por coluna)")]
    public TMP_Text LastRun_WT;
    public TMP_Text LastRun_NT;

    [Header("Nome + botão (linhas de input)")]
    public GameObject NameRow_WT;
    public TMP_InputField InputName_WT;
    public TMP_Text BtnGuardarLabel_WT;

    public GameObject NameRow_NT;
    public TMP_InputField InputName_NT;
    public TMP_Text BtnGuardarLabel_NT;

    [Header("Listas de scores")]
    public Transform ListContent_WT;   // ListContent_WT
    public Transform ListContent_NT;   // ListContent_NT
    public GameObject LinhaPrefab;     // prefab de uma linha "#  Name  Animals  Time"

    [Header("Botões / Navegação")]
    public string NomeCenaMenu = "Menu";

    TMP_InputField _inputAtivo;
    TMP_Text _btnLabelAtivo;
    bool _scoreJaGuardado;

    void Start()
    {
        ConfigurarHeaderEColunas();
        RedesenharScoreboard();
    }

    void ConfigurarHeaderEColunas()
    {
        if (TitleText != null)
            TitleText.text = "GeoZoo – Scoreboard";

        if (SubtitleText != null)
            SubtitleText.text = "Best zoos built so far";

        if (!LastRunData.TemDados)
        {
            if (LastRun_WT) LastRun_WT.text = "No recent run with tutorial.";
            if (LastRun_NT) LastRun_NT.text = "No recent run without tutorial.";

            if (NameRow_WT) NameRow_WT.SetActive(false);
            if (NameRow_NT) NameRow_NT.SetActive(false);
            return;
        }

        string tempo = LastRunData.FormatarTempo();
        string resumo = $"Last run: {LastRunData.Zoo} animal{(LastRunData.Zoo == 1 ? "" : "s")} in {tempo}";
        bool comTut = LastRunData.FoiComTutorial;

        if (comTut)
        {
            if (LastRun_WT) LastRun_WT.text = resumo;
            if (LastRun_NT) LastRun_NT.text = "No recent run without tutorial.";

            if (NameRow_WT) NameRow_WT.SetActive(true);
            if (NameRow_NT) NameRow_NT.SetActive(false);

            _inputAtivo = InputName_WT;
            _btnLabelAtivo = BtnGuardarLabel_WT;
        }
        else
        {
            if (LastRun_WT) LastRun_WT.text = "No recent run with tutorial.";
            if (LastRun_NT) LastRun_NT.text = resumo;

            if (NameRow_WT) NameRow_WT.SetActive(false);
            if (NameRow_NT) NameRow_NT.SetActive(true);

            _inputAtivo = InputName_NT;
            _btnLabelAtivo = BtnGuardarLabel_NT;
        }

        if (_btnLabelAtivo != null)
            _btnLabelAtivo.text = "Save score";
    }

    // Chamado pelos botões Save (WT ou NT)
    public void OnGuardarScore()
    {
        if (_scoreJaGuardado) return;

        if (!LastRunData.TemDados || ScoreboardManager.Instancia == null)
        {
            VoltarAoMenu();
            return;
        }

        string nome = _inputAtivo != null ? _inputAtivo.text : "";

        ScoreboardManager.Instancia.AdicionarScore(
            nome,
            LastRunData.Zoo,
            LastRunData.SegundosJogados,
            LastRunData.FoiComTutorial
        );

        _scoreJaGuardado = true;

        if (_btnLabelAtivo != null)
            _btnLabelAtivo.text = "Saved!";

        RedesenharScoreboard();
    }

    // Botão “Back to Menu”
    public void OnBackToMenu()
    {
        VoltarAoMenu();
    }

    void VoltarAoMenu()
    {
        if (!string.IsNullOrEmpty(NomeCenaMenu))
            SceneManager.LoadScene(NomeCenaMenu);
    }

    void RedesenharScoreboard()
    {
        if (ScoreboardManager.Instancia == null || LinhaPrefab == null)
            return;

        LimparFilhos(ListContent_WT);
        LimparFilhos(ListContent_NT);

        int rankWT = 1;
        foreach (var entry in ScoreboardManager.Instancia.ScoresComTutorial())
            CriarLinha(entry, ListContent_WT, rankWT++);

        int rankNT = 1;
        foreach (var entry in ScoreboardManager.Instancia.ScoresSemTutorial())
            CriarLinha(entry, ListContent_NT, rankNT++);
    }

    void LimparFilhos(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    void CriarLinha(ScoreboardManager.ScoreEntry entry, Transform parent, int rank)
    {
        if (parent == null || LinhaPrefab == null || entry == null) return;

        var go = Instantiate(LinhaPrefab, parent);
        var textos = go.GetComponentsInChildren<TMP_Text>();

        // 0 = posição, 1 = nome, 2 = animais, 3 = tempo
        if (textos.Length > 0)
            textos[0].text = rank.ToString();

        if (textos.Length > 1)
            textos[1].text = entry.nome;

        if (textos.Length > 2)
            textos[2].text = entry.zoo.ToString();

        if (textos.Length > 3)
        {
            int s = Mathf.Max(0, entry.segundos);
            int m = s / 60;
            int sec = s % 60;
            textos[3].text = $"{m}:{sec:00}";
        }
    }
}
