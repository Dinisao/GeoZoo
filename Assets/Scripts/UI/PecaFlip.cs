// PecaFlip.cs — Anima o “flip” visual entre Frente e Verso de uma peça.
// Funcionamento: encolhe no eixo X até 0 (fecha), troca o sprite, volta a crescer (abre).
// Usa Time.unscaledDeltaTime (independente de pausas/escala de tempo) e uma ease-out cúbica suave.

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[DisallowMultipleComponent]
public class PecaFlip : MonoBehaviour
{
    public Sprite Frente;
    public Sprite Verso;

    [Tooltip("Duração total do flip (fechar + abrir)")]
    public float Duracao = 0.18f;

    [Tooltip("Image alvo (auto se vazio)")]
    public Image Alvo;

    public bool EstaNaFrente { get; private set; } = true;

    bool _animando;

    // Auto-descobre a Image alvo se não estiver atribuída e aplica o estado inicial sem animação.
    void Awake()
    {
        if (!Alvo) Alvo = GetComponentInChildren<Image>(true);
        Aplicar(EstaNaFrente, true);
    }

    // Aplica diretamente o sprite (sem animação). Útil em setup/reset.
    // 'forcar' chama SetAllDirty para garantir refresh imediato no UI.
    public void Aplicar(bool frente, bool forcar = false)
    {
        EstaNaFrente = frente;
        if (!Alvo) return;
        Alvo.sprite = frente ? Frente : Verso;
        if (forcar) Alvo.SetAllDirty();
    }

    // Dispara o flip animado se não houver um em curso.
    public void Alternar()
    {
        if (_animando) return;
        StartCoroutine(CoFlip());
    }

    // Curva de easing usada na transição (mais suave no final).
    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    // Rotina do flip: fecha (scale.x→0), troca sprite, abre (scale.x 0→1).
    IEnumerator CoFlip()
    {
        if (!Alvo) yield break;

        _animando = true;
        var rt = Alvo.rectTransform;
        float half = Mathf.Max(0.03f, Duracao * 0.5f);

        // 1) Fecha (scale.x → 0)
        float t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - EaseOutCubic(Mathf.Clamp01(t / half)); // 1→0
            rt.localScale = new Vector3(k, 1f, 1f);
            yield return null;
        }
        rt.localScale = new Vector3(0f, 1f, 1f);

        // 2) Troca sprite
        EstaNaFrente = !EstaNaFrente;
        Alvo.sprite = EstaNaFrente ? Frente : Verso;

        // 3) Abre (scale.x 0→1)
        t = 0f;
        while (t < half)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutCubic(Mathf.Clamp01(t / half)); // 0→1
            rt.localScale = new Vector3(k, 1f, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;

        // Avisar o sistema de tutorial que esta peça foi flipada
        var peca = GetComponentInParent<Peca>();
        if (peca != null)
        {
            PecaFlipEvents.EmitPecaFlipada(peca);
        }

        _animando = false;
    }
}
