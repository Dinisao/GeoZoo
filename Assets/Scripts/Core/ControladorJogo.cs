// ControladorJogo.cs — Gere HUD, temporizador e pontuação (+ gating de interação de tiles).
// Mantém um singleton simples para acesso global (Instancia), atualiza o UI (tempo/ZOO),
// e expõe chamadas para iniciar/parar/reiniciar o timer, recompensar acertos e ligar/desligar interação.
// Agora com evento OnTempoEsgotado(int zoo) e propriedade ZooAtual.

using TMPro;
using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(-1000)]
public class ControladorJogo : MonoBehaviour
{
    public static ControladorJogo Instancia { get; private set; }

    // === NOVO ===
    public event System.Action<int> OnTempoEsgotado; // notifica quando chega a 0 (envia ZOO)
    public int ZooAtual => _zoo;                     // leitura pública do total de “animais”

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

    // Setup do singleton + estado inicial do jogo (tempo e HUD).
    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;

        _tempoRestante = Mathf.Max(0, TempoInicialSeg);
        InteracaoPermitida = false;
        AtualizarHUD();
    }

    // Arranca o temporizador se ainda não estiver a contar.
    // Chamado quando a carta estaciona no preview (coordenado pelo DeckController).
    public void IniciarTimerSeAindaNao()
    {
        if (_timerAtivo) return;
        if (_tempoRestante <= 0) _tempoRestante = Mathf.Max(1, TempoInicialSeg); // evita começar a 0

        _timerAtivo = true;
        if (_tick != null) StopCoroutine(_tick);
        _tick = StartCoroutine(TickTimer());
    }

    // Pausa o temporizador (não reseta o tempo restante).
    public void PararTimer()
    {
        _timerAtivo = false;
        if (_tick != null) StopCoroutine(_tick);
        _tick = null;
    }

    // Para o temporizador e repõe o tempo no valor inicial.
    public void ReiniciarTimer()
    {
        PararTimer();
        _tempoRestante = Mathf.Max(0, TempoInicialSeg);
        AtualizarHUD();
    }

    // Liga/desliga a interação do jogador com os tiles (gating de jogabilidade).
    public void DefinirInteracaoTiles(bool ativo)
    {
        InteracaoPermitida = ativo;
    }

    // Aplica recompensa por padrão correto: +1 no ZOO e +20s no relógio.
    public void RecompensaAcerto()
    {
        _zoo += 1;
        _tempoRestante += 20; // +20s por acerto
        AtualizarHUD();
    }

    // Loop do temporizador: de segundo a segundo, reduz o tempo e atualiza o HUD.
    // Quando chega a 0, pára e dispara "Game Over".
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

            // 🔔 Dispara evento para UI de Game Over (ex.: GameOverUI)
            OnTempoEsgotado?.Invoke(_zoo);
        }
    }

    // Atualiza os textos do HUD com o ZOO e o tempo formatado (M:SS).
    void AtualizarHUD()
    {
        if (TxtZoo)   TxtZoo.text   = $"ZOO: {_zoo}";
        if (TxtTempo) TxtTempo.text = $"{Formatar(_tempoRestante)}";
    }

    // Converte segundos em "m:ss".
    string Formatar(int segundos)
    {
        int m = Mathf.Max(0, segundos) / 60;
        int s = Mathf.Max(0, segundos) % 60;
        return $"{m}:{s:00}";
    }
}
