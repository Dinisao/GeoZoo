using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Gestão de highscores (persistentes via PlayerPrefs).
/// </summary>
public class ScoreboardManager : MonoBehaviour
{
    public static ScoreboardManager Instancia { get; private set; }

    const string PLAYER_PREFS_KEY = "GeoZoo_Scoreboard_v1";

    [Serializable]
    public class ScoreEntry
    {
        public string nome;
        public int zoo;
        public int segundos;
        public bool comTutorial;
    }

    [Serializable]
    class ScoreboardData
    {
        public List<ScoreEntry> entries = new List<ScoreEntry>();
    }

    [SerializeField]
    List<ScoreEntry> _scores = new List<ScoreEntry>();

    void Awake()
    {
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject);
        Carregar();
    }

    // ---------- API pública ----------

    public void AdicionarScore(string nome, int zoo, int segundos, bool comTutorial)
    {
        if (string.IsNullOrWhiteSpace(nome))
            nome = "Player";

        var entry = new ScoreEntry
        {
            nome = nome.Trim(),
            zoo = Mathf.Max(0, zoo),
            segundos = Mathf.Max(0, segundos),
            comTutorial = comTutorial
        };

        _scores.Add(entry);
        Guardar();
    }

    /// <summary>Todas as entradas ordenadas por ZOO desc, depois tempo asc.</summary>
    public IEnumerable<ScoreEntry> TodosScoresOrdenados()
    {
        return _scores
            .OrderByDescending(s => s.zoo)
            .ThenBy(s => s.segundos);
    }

    public IEnumerable<ScoreEntry> ScoresComTutorial()
        => TodosScoresOrdenados().Where(s => s.comTutorial);

    public IEnumerable<ScoreEntry> ScoresSemTutorial()
        => TodosScoresOrdenados().Where(s => !s.comTutorial);

    public void Guardar()
    {
        var data = new ScoreboardData { entries = _scores };
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(PLAYER_PREFS_KEY, json);
        PlayerPrefs.Save();
    }

    public void Carregar()
    {
        _scores.Clear();

        if (!PlayerPrefs.HasKey(PLAYER_PREFS_KEY))
            return;

        string json = PlayerPrefs.GetString(PLAYER_PREFS_KEY, "");
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var data = JsonUtility.FromJson<ScoreboardData>(json);
            if (data != null && data.entries != null)
                _scores = data.entries;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ScoreboardManager] Falha a ler JSON: {e.Message}");
        }
    }
}
