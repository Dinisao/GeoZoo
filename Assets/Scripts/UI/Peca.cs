// Peca.cs — Peça arrastável/rodável da grelha.
// Lida com: drag & drop para células, rotação (Q/E) enquanto premida,
// flip no botão direito (via PecaFlip), highlight de célula sob o cursor,
// recolha para a mão (instantânea ou animada) e gating de interação com o jogo.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
[RequireComponent(typeof(LayoutElement))]
[DisallowMultipleComponent]
public class Peca : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerDownHandler, IPointerUpHandler
{
    public static event Action OnPecaStateChanged;

    // Refs internas (UI/RT/interaction)
    RectTransform _rt;
    Image _img;
    CanvasGroup _cg;
    LayoutElement _le;

    // Contexto (injetado pelo GestorGrelha)
    RectTransform _gridRoot;
    GridLayoutGroup _gridLayout;
    RectTransform _maoContainer;
    RectTransform _dragLayer;

    // Estado de input/drag/rotação
    bool _dragging;
    bool _leftHeld;                // rotação Q/E enquanto carregado
    Transform _paiOriginal;
    int _irmIndexOriginal;
    Vector2 _tamanhoMao;
    int _rotacoes90;               // 0..3
    RectTransform _hoverCell;      // highlight atual

    // Lido pelo GridValidator
    public int RotacaoGraus => (_rotacoes90 * 90);

    // Cache de componentes essenciais; garante CanvasGroup presente.
    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _img = GetComponent<Image>();
        _le  = GetComponent<LayoutElement>();
        _cg  = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
    }

    // Inicializa “tamanho na mão” e elimina riscos de escala herdada.
    void Start()
    {
        _tamanhoMao = _rt.sizeDelta; // tamanho “na mão”
        // segurança: arranca com escala 1
        _rt.localScale = Vector3.one;
        var gfx0 = GetComponentInChildren<Image>(true);
        if (gfx0) gfx0.rectTransform.localScale = Vector3.one;
    }

    // Recebe as referências do contexto (grelha/mão/layer de drag) vindas do GestorGrelha.
    public void ConfigurarContexto(RectTransform gridRoot, GridLayoutGroup gridLayout, RectTransform maoContainer, RectTransform dragLayer)
    {
        _gridRoot     = gridRoot;
        _gridLayout   = gridLayout;
        _maoContainer = maoContainer;
        _dragLayer    = dragLayer;
    }

    // Enquanto a peça está clicada/dragging, permite rotação por Q/E (se interação for permitida).
    void Update()
    {
        if (!(_leftHeld || _dragging)) return;
        if (!PodeInteragir()) return;

        if (Input.GetKeyDown(KeyCode.Q)) Rodar(-1);
        if (Input.GetKeyDown(KeyCode.E)) Rodar(+1);
    }

    // Ajusta a rotação em incrementos de 90° e aplica no visual (filho Image, se existir).
    void Rodar(int dir)
    {
        _rotacoes90 = (_rotacoes90 + dir) % 4;
        if (_rotacoes90 < 0) _rotacoes90 += 4;

        float ang = _rotacoes90 * 90f;
        var gfx = GetComponentInChildren<Image>(true);
        (gfx ? gfx.rectTransform : _rt).localEulerAngles = new Vector3(0, 0, ang);

        OnPecaStateChanged?.Invoke();
    }

    // ---------- Clique/Press ----------

    // Início de clique: guarda estado do botão esquerdo para rotação; no botão direito pede flip (PecaFlip).
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!PodeInteragir()) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _leftHeld = true;
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            var flip = GetComponent<PecaFlip>();
            if (flip != null) flip.Alternar();
            OnPecaStateChanged?.Invoke();
        }
    }

    // Solta o “held” do botão esquerdo (para parar rotação por tecla).
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            _leftHeld = false;
    }

    // ---------- Drag & Drop ----------

    // Começa o drag: move para a dragLayer, desativa raycasts temporariamente e ajusta tamanho à célula.
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!PodeInteragir()) { eventData.pointerDrag = null; return; }
        _dragging = true;

        // segurança: zera escala antes de mexer
        _rt.localScale = Vector3.one;
        var gfx = GetComponentInChildren<Image>(true);
        if (gfx) gfx.rectTransform.localScale = Vector3.one;

        _paiOriginal = transform.parent;
        _irmIndexOriginal = transform.GetSiblingIndex();

        if (_dragLayer != null)
        {
            _rt.SetParent(_dragLayer, worldPositionStays: false);
            _rt.SetAsLastSibling();
        }

        _cg.alpha = 0.75f;
        _cg.blocksRaycasts = false;
        if (_img) _img.raycastTarget = false;

        if (_gridLayout != null) _rt.sizeDelta = _gridLayout.cellSize;

        SeguirRato(eventData);
    }

    // Durante o drag: segue o cursor e atualiza o highlight da célula sob o rato (permitida/bloqueada).
    public void OnDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        SeguirRato(eventData);

        var nova = CelulaDebaixoDoCursor(eventData.position, eventData.pressEventCamera);
        if (nova != _hoverCell)
        {
            if (_hoverCell)
            {
                var cgOld = _hoverCell.GetComponent<CelulaGrelha>();
                if (cgOld) cgOld.SetHover(false, true);
            }

            _hoverCell = nova;
            if (_hoverCell)
            {
                var ocupada = _hoverCell.GetComponentInChildren<Peca>(false) != null;
                var cg = _hoverCell.GetComponent<CelulaGrelha>();
                if (cg) cg.SetHover(true, !ocupada);
            }
        }
    }

    // Termina o drag: tenta “snap” na célula; se falhar, regressa à mão e repõe o estado visual completo.
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_dragging) return;
        _dragging = false;

        if (_hoverCell)
        {
            var cg = _hoverCell.GetComponent<CelulaGrelha>();
            if (cg) cg.SetHover(false, true);
            _hoverCell = null;
        }

        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
        if (_img) _img.raycastTarget = true;

        if (!TentarSnapNaGrelha(eventData))
        {
            if (_maoContainer != null) _rt.SetParent(_maoContainer, worldPositionStays: false);
            _rt.sizeDelta = _tamanhoMao;
            _rt.localEulerAngles = Vector3.zero;
            _rt.localScale = Vector3.one;                         // reset escala
            var gfx = GetComponentInChildren<Image>(true);
            if (gfx) gfx.rectTransform.localScale = Vector3.one;  // reset escala do filho
        }

        OnPecaStateChanged?.Invoke();
    }

    // Mantém a peça debaixo do cursor, usando o espaço do dragLayer para evitar saltos.
    void SeguirRato(PointerEventData eventData)
    {
        if (_dragLayer == null) return;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_dragLayer, eventData.position, eventData.pressEventCamera, out var world))
        {
            _rt.position = world; // sem offset
        }
    }

    // Devolve a célula (RectTransform) sob o cursor, filtrando só as que têm CelulaGrelha.
    RectTransform CelulaDebaixoDoCursor(Vector2 screenPos, Camera cam)
    {
        if (_gridRoot == null) return null;
        for (int i = 0; i < _gridRoot.childCount; i++)
        {
            var cell = _gridRoot.GetChild(i) as RectTransform;
            if (!cell || !cell.GetComponent<CelulaGrelha>()) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(cell, screenPos, cam))
                return cell;
        }
        return null;
    }

    // Tenta encaixar a peça na célula sob o cursor (se estiver livre).
    // Também normaliza anchors/posição/escala para casar com a grelha.
    bool TentarSnapNaGrelha(PointerEventData eventData)
    {
        if (_gridRoot == null || _gridLayout == null) return false;

        var alvo = CelulaDebaixoDoCursor(eventData.position, eventData.pressEventCamera);
        if (alvo == null) return false;

        var jaTemPeca = alvo.GetComponentInChildren<Peca>(includeInactive: false) != null;
        if (jaTemPeca) return false;

        _rt.SetParent(alvo, worldPositionStays: false);
        _rt.sizeDelta = _gridLayout.cellSize;
        _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.anchoredPosition = Vector2.zero;
        _rt.localScale = Vector3.one;                         // reset escala
        var gfx = GetComponentInChildren<Image>(true);
        if (gfx) gfx.rectTransform.localScale = Vector3.one;  // reset escala do filho
        return true;
    }

    // -------- NOVOS MÉTODOS DE RECOLHA --------

    // Anima a peça a regressar para a mão (suave e curto) e, no fim, chama VoltarParaMao() para consolidar.
    public IEnumerator AnimarVoltarParaMao(float dur = 0.25f)
    {
        if (_maoContainer == null || _dragLayer == null)
        {
            VoltarParaMao();
            yield break;
        }

        var canvas = GetComponentInParent<Canvas>();
        var cam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        // Ponto inicial e final (em coords locais do dragLayer)
        Vector2 p0 = WorldToLocal(_dragLayer, _rt.position, cam);
        Vector2 p1 = WorldToLocal(_dragLayer, _maoContainer.position, cam);

        // Coloca no dragLayer mantendo a posição visual
        _rt.SetParent(_dragLayer, worldPositionStays: true);
        _rt.SetAsLastSibling();

        // Aparência durante a animação
        float startAlpha = (_cg ? _cg.alpha : 1f);
        if (_cg) { _cg.blocksRaycasts = false; }
        if (_img) _img.raycastTarget = false;

        Vector3 s0 = _rt.localScale;
        Vector3 s1 = s0 * 0.85f;

        // Corre a animação (ease smoothstep)
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
            float k = Mathf.Clamp01(t);

            // ease
            float ke = k * k * (3f - 2f * k); // smoothstep

            _rt.anchoredPosition = Vector2.LerpUnclamped(p0, p1, ke);
            _rt.localScale = Vector3.LerpUnclamped(s0, s1, ke);
            if (_cg) _cg.alpha = Mathf.Lerp(startAlpha, 0f, ke);

            yield return null;
        }

        // Teleporta logicamente para a mão e RESTAURA COMPLETAMENTE
        VoltarParaMao();
    }

    // Regressa de imediato à mão (sem animação). Layout reposiciona no container.
    public void VoltarParaMao()
    {
        if (_maoContainer == null) return;

        _dragging = false;
        _leftHeld = false;

        _rt.SetParent(_maoContainer, worldPositionStays: false);
        _rt.sizeDelta = _tamanhoMao;
        _rt.localEulerAngles = Vector3.zero;
        _rt.localScale = Vector3.one;                         // reset escala
        var gfx = GetComponentInChildren<Image>(true);
        if (gfx) gfx.rectTransform.localScale = Vector3.one;  // reset escala do filho

        if (_cg != null) { _cg.alpha = 1f; _cg.blocksRaycasts = true; }
        if (_img != null) _img.raycastTarget = true;

        OnPecaStateChanged?.Invoke();
    }

    // Gate de interação: depende da flag global do ControladorJogo e de existir carta ativa no Deck.
    bool PodeInteragir()
    {
        if (ControladorJogo.Instancia && !ControladorJogo.Instancia.InteracaoPermitida) return false;

        var deck = FindAnyObjectByType<DeckController>();
        if (deck != null && !deck.TemCartaAtual) return false;

        return true;
    }

    // Utilitário para converter world→local num RectTransform (respeitando a câmara do Canvas).
    static Vector2 WorldToLocal(RectTransform parent, Vector3 worldPos, Camera cam)
    {
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, cam, out var local);
        return local;
    }
}
