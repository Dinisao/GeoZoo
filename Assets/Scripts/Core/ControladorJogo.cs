// ControladorJogo.cs — Timer com cap a 120s, HUD otimizado e API estável.
// Drop-in: mantém os métodos e propriedades que já usas no Deck/Grid.

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

    // Internos
    float _tempoRestanteF;
    int   _zoo;
    bool  _timerAtivo;

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
        InteracaoPermitida = false;
    }

    void OnEnable()  => AtualizarHUD(force:true);
    void OnValidate(){ SanitizarConfig(); AtualizarHUD(force:true); }

    void Update()
    {
        if (!_timerAtivo) return;

        _tempoRestanteF -= Delta;
        if (_tempoRestanteF <= 0f)
        {
            _tempoRestanteF = 0f;
            _timerAtivo = false;
            AtualizarHUD(force:true);
            OnTempoEsgotado?.Invoke(_zoo);
            return;
        }

        AtualizarHUD(); // throttle interno
    }

    // ===== API pública =====
    // Chamado pelo Deck quando a carta fica no preview.
    public void IniciarTimerSeAindaNao()
    {
        if (_timerAtivo) return;
        if (_tempoRestanteF <= 0f)
            _tempoRestanteF = Mathf.Clamp(TempoInicialSeg, 1, MaxTempoSegundos);

        _timerAtivo = (_tempoRestanteF > 0f);
        AtualizarHUD(force:true);
    }

    public void PararTimer() => _timerAtivo = false;

    public void ReiniciarTimer()
    {
        _timerAtivo = false;
        _tempoRestanteF = Mathf.Clamp(TempoInicialSeg, 0, MaxTempoSegundos);
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
