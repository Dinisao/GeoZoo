using TMPro;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class ControladorJogo : MonoBehaviour
{
    public static ControladorJogo Instancia { get; private set; }

    // Evento dispara quando chega a 0 (envia ZOO atual)
    public event System.Action<int> OnTempoEsgotado;

    // === HUD (opcionais) ===
    [Header("HUD")]
    public TMP_Text TxtTempo;
    public TMP_Text TxtZoo;

    // === Configuração ===
    [Header("Configuração do Tempo")]
    [Tooltip("Tempo inicial do jogo em segundos (capado por MaxTempoSegundos).")]
    public int TempoInicialSeg = 120;

    [Tooltip("Tempo máximo permitido (cap).")]
    public int MaxTempoSegundos = 120;

    [Tooltip("Bónus por acerto (em segundos).")]
    public int BonusPorAcertoSeg = 20;

    [Tooltip("Se true, usa unscaledDeltaTime (ignora Time.timeScale).")]
    public bool usarUnscaledTime = false;

    [Header("Tutorial / Timer")]
    [Tooltip("Se true, a primeira carta comprada no deck não arranca o timer (modo tutorial).")]
    public bool PrimeiraCartaLivreComTutorial = false;

    // === Leitura pública / estado ===
    public bool InteracaoPermitida { get; private set; }
    public float TempoRestanteF => Mathf.Max(0f, _tempoRestanteF);
    public int   TempoRestante   => Mathf.CeilToInt(TempoRestanteF);
    public int   ZooAtual        => _zoo;

    /// <summary>
    /// 0..1 do tempo restante em relação ao CAP (útil para o círculo).
    /// 1.0 = cheio (cap), 0.0 = vazio (0s).
    /// </summary>
    public float Progresso01 => (MaxTempoSegundos > 0)
        ? Mathf.Clamp01(TempoRestanteF / MaxTempoSegundos)
        : 0f;

    // === NOVO: tempo real jogado (contando bónus, etc.) ===
    /// <summary>Tempo total de jogo (segundos em que o timer esteve a contar).</summary>
    public float TempoJogadoF => Mathf.Max(0f, _tempoJogadoF);
    public int   TempoJogadoInt => Mathf.RoundToInt(TempoJogadoF);

    /// <summary>
    /// True se esta run teve 1ª carta "livre" (modo tutorial ativo).
    /// Usado para separar scores "with tutorial" / "without tutorial".
    /// </summary>
    public bool FoiRunComTutorial { get; private set; }

    // Internos
    float _tempoRestanteF;
    float _tempoJogadoF;
    int   _zoo;
    bool  _timerAtivo;

    // controla se a primeira carta já foi usada (primeira compra do deck)
    bool _primeiraCartaJaUsada = false;

    // Throttle de HUD
    int _ultimoSegundoMostrado = -1;
    int _ultimoZooMostrado     = -1;

    float Delta => usarUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // ===== Ciclo de vida =====
    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;

        SanitizarConfig();

        _tempoRestanteF = TempoInicialSeg;
        _tempoJogadoF   = 0f;
        _zoo            = 0;
        _timerAtivo     = false;
        _primeiraCartaJaUsada = false;
        FoiRunComTutorial     = false;

        InteracaoPermitida = false;
        // O TutorialDeckHint é que liga isto quando for uma run "com tutorial".
        PrimeiraCartaLivreComTutorial = false;
    }

    void OnEnable()  => AtualizarHUD(force:true);

    void OnValidate()
    {
        SanitizarConfig();
        AtualizarHUD(force:true);
    }

    void Update()
    {
        if (!_timerAtivo) return;

        float dt = Delta;
        _tempoRestanteF -= dt;
        _tempoJogadoF   += dt;   // acumulamos tempo real de jogo

        if (_tempoRestanteF <= 0f)
        {
            _tempoRestanteF = 0f;
            _timerAtivo = false;
            AtualizarHUD(force:true);

            // Captura automática dos dados da run para o scoreboard
            LastRunData.Capturar(this);

            OnTempoEsgotado?.Invoke(_zoo);
            return;
        }

        AtualizarHUD(); // throttle interno
    }

    // ===== API pública =====
    // Chamado pelo Deck quando a carta fica no preview.
    // Nova lógica:
    //  - Se for a 1ª carta e PrimeiraCartaLivreComTutorial==true → não liga o timer (modo tutorial),
    //    mas marca a run como "com tutorial".
    //  - Caso contrário → liga o timer ao ser chamada.
    public void IniciarTimerSeAindaNao()
    {
        // Se já está a contar, não fazemos nada.
        if (_timerAtivo) return;

        // Garante que temos um tempo válido preparado
        if (_tempoRestanteF <= 0f)
            _tempoRestanteF = Mathf.Clamp(TempoInicialSeg, 1, MaxTempoSegundos);

        // Primeira carta do deck:
        if (!_primeiraCartaJaUsada)
        {
            _primeiraCartaJaUsada = true;

            if (PrimeiraCartaLivreComTutorial)
            {
                // 1ª carta é “livre” → run é marcada como "com tutorial",
                // mas ainda não ligamos o timer.
                FoiRunComTutorial = true;
                AtualizarHUD(force:true);
                return;
            }
            // Sem tutorial → cai para o bloco que liga o timer já nesta 1ª carta.
        }

        // A partir daqui, liga o timer (1ª carta sem tutorial, ou 2ª carta em diante).
        _timerAtivo = (_tempoRestanteF > 0f);
        AtualizarHUD(force:true);
    }

    /// <summary>
    /// Mantido para compatibilidade: marca explicitamente a primeira carta como "já usada".
    /// Actualmente não é chamado pelo Deck, mas pode ser útil em ajustes futuros.
    /// </summary>
    public void MarcarPrimeiraCartaComoUsada()
    {
        _primeiraCartaJaUsada = true;
    }

    public void PararTimer() => _timerAtivo = false;

    public void ReiniciarTimer()
    {
        _timerAtivo = false;
        _tempoRestanteF = Mathf.Clamp(TempoInicialSeg, 0, MaxTempoSegundos);
        _tempoJogadoF   = 0f;
        _zoo            = 0;

        _primeiraCartaJaUsada = false;
        PrimeiraCartaLivreComTutorial = false;
        FoiRunComTutorial = false;

        AtualizarHUD(force:true);
    }

    // Útil para efeitos/powerups/debug; respeita o CAP e não deixa negativo.
    public void AddTempo(float segundos)
    {
        _tempoRestanteF = Mathf.Clamp(_tempoRestanteF + segundos, 0f, MaxTempoSegundos);
        AtualizarHUD(force:true);
    }

    // “Teleporta” o tempo (debug).
    public void SetTempoAbsoluto(float segundos)
    {
        _tempoRestanteF = Mathf.Clamp(segundos, 0f, MaxTempoSegundos);
        AtualizarHUD(force:true);
    }

    public void DefinirInteracaoTiles(bool ativo) => InteracaoPermitida = ativo;

    // +1 ZOO e +20s, capado a MaxTempoSegundos
    public void RecompensaAcerto()
    {
        _zoo += 1;
        _tempoRestanteF = Mathf.Min(_tempoRestanteF + BonusPorAcertoSeg, MaxTempoSegundos);
        AtualizarHUD(force:true);
    }

    // ===== Helpers =====
    void AtualizarHUD(bool force = false)
    {
        // Só recalcula string quando o segundo inteiro muda ou o ZOO muda
        int segInt = TempoRestante;
        bool mudouSegundo = (segInt != _ultimoSegundoMostrado);
        bool mudouZoo     = (_zoo   != _ultimoZooMostrado);

        if (!force && !mudouSegundo && !mudouZoo) return;

        if (TxtZoo && (force || mudouZoo))
        {
            TxtZoo.text = $"ZOO: {_zoo}";
            _ultimoZooMostrado = _zoo;
        }
        if (TxtTempo && (force || mudouSegundo))
        {
            TxtTempo.text = Formatar(segInt);
            _ultimoSegundoMostrado = segInt;
        }
    }

    static string Formatar(int segundos)
    {
        if (segundos < 0) segundos = 0;
        int m = segundos / 60;
        int s = segundos % 60;
        return $"{m}:{s:00}";
    }

    void SanitizarConfig()
    {
        MaxTempoSegundos  = Mathf.Max(1, MaxTempoSegundos);
        TempoInicialSeg   = Mathf.Clamp(TempoInicialSeg, 0, MaxTempoSegundos);
        BonusPorAcertoSeg = Mathf.Max(0, BonusPorAcertoSeg);
    }
}
