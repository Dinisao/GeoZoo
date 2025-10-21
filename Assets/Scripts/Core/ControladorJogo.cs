using TMPro;
using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(-1000)]
public class ControladorJogo : MonoBehaviour
{
    public static ControladorJogo Instancia { get; private set; }

    [Header("HUD")]
    public TMP_Text TxtTempo;
    public TMP_Text TxtZoo;

    [Header("Tempo")]
    public int TempoInicialSeg = 120;

    public bool InteracaoPermitida { get; private set; }

    int _tempoRestante;
    int _zoo;
    bool _timerAtivo;
    Coroutine _tick;

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;

        _tempoRestante = Mathf.Max(0, TempoInicialSeg);
        InteracaoPermitida = false;
        AtualizarHUD();
    }

    // Chama isto quando a carta estaciona no preview (já está no DeckController)
    public void IniciarTimerSeAindaNao()
    {
        if (_timerAtivo) return;
        if (_tempoRestante <= 0) _tempoRestante = Mathf.Max(1, TempoInicialSeg); // evita começar a 0

        _timerAtivo = true;
        if (_tick != null) StopCoroutine(_tick);
        _tick = StartCoroutine(TickTimer());
    }

    public void PararTimer()
    {
        _timerAtivo = false;
        if (_tick != null) StopCoroutine(_tick);
        _tick = null;
    }

    public void ReiniciarTimer()
    {
        PararTimer();
        _tempoRestante = Mathf.Max(0, TempoInicialSeg);
        AtualizarHUD();
    }

    public void DefinirInteracaoTiles(bool ativo)
    {
        InteracaoPermitida = ativo;
    }

    public void RecompensaAcerto()
    {
        _zoo += 1;
        _tempoRestante += 20; // +20s por acerto
        AtualizarHUD();
    }

    IEnumerator TickTimer()
    {
        while (_timerAtivo && _tempoRestante > 0)
        {
            yield return new WaitForSeconds(1f); // 1 segundo real por tick
            _tempoRestante--;
            AtualizarHUD();
        }

        if (_tempoRestante <= 0)
        {
            _tempoRestante = 0;
            _timerAtivo = false;
            AtualizarHUD();
            // TODO: aqui podes disparar "Game Over"
        }
    }

    void AtualizarHUD()
    {
        if (TxtZoo)   TxtZoo.text   = $"ZOO: {_zoo}";
        if (TxtTempo) TxtTempo.text = $"TIME: {Formatar(_tempoRestante)}";
    }

    string Formatar(int segundos)
    {
        int m = Mathf.Max(0, segundos) / 60;
        int s = Mathf.Max(0, segundos) % 60;
        return $"{m}:{s:00}";
    }
}
