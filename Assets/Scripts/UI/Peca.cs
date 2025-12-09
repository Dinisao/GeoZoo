// Peca.cs — Peça arrastável/rodável da grelha.
// Lida com: drag & drop para células, rotação (clique/toque),
// flip (duplo clique/toque), highlight de célula sob o cursor,
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
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler // Adicionado IPointerClickHandler para clique simples/duplo
{
    public static event Action OnPecaStateChanged;

    // Eventos para o tutorial / outros sistemas
    public static event Action<Peca> OnPecaRodada; 			    // dispara sempre que a peça roda
    public static event Action<Peca, CelulaGrelha> OnPecaColocadaNaGrid; // dispara quando a peça é pousada numa célula da grid

    // Evento disparado quando a peça é flipada
    public static event Action<Peca> OnPecaFlipada;

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
    bool _leftHeld; 				// mantido no toque (para drag/rotação imediata)
    Transform _paiOriginal;
    int _irmIndexOriginal;
    Vector2 _tamanhoMao;
    int _rotacoes90; 				// 0..3
    RectTransform _hoverCell; 		// highlight atual

    // Variáveis para deteção de Duplo Clique/Toque para Flip
    const float DoubleClickTime = 0.3f; // Tempo máximo entre cliques para ser considerado duplo clique
    float _lastClickTime;

    // Lido pelo GridValidator
    public int RotacaoGraus => (_rotacoes90 * 90);

    // Exposto para o tutorial/validator
    public bool IsDragging => _dragging;
    public bool IsHeld => _leftHeld;

    // Cache de componentes essenciais; garante CanvasGroup presente.
    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _img = GetComponent<Image>();
        _le = GetComponent<LayoutElement>();
        _cg = GetComponent<CanvasGroup>();
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
        _gridRoot = gridRoot;
        _gridLayout = gridLayout;
        _maoContainer = maoContainer;
        _dragLayer = dragLayer;
    }

    // Remove o Update() original que lia Q/E, já não é necessário
    /*
    void Update()
    {
        if (!(_leftHeld || _dragging)) return;
        if (!PodeInteragir()) return;

        if (Input.GetKeyDown(KeyCode.Q)) Rodar(-1);
        if (Input.GetKeyDown(KeyCode.E)) Rodar(+1);
    }
    */

    // Ajusta a rotação em incrementos de 90° e aplica no visual (filho Image, se existir).
    void Rodar(int dir)
    {
        _rotacoes90 = (_rotacoes90 + dir) % 4;
        if (_rotacoes90 < 0) _rotacoes90 += 4;

        float ang = _rotacoes90 * 90f;
        var gfx = GetComponentInChildren<Image>(true);
        (gfx ? gfx.rectTransform : _rt).localEulerAngles = new Vector3(0, 0, ang);

        OnPecaStateChanged?.Invoke();

        // avisa quem quiser (tutorial, etc.) que esta peça acabou de rodar
        OnPecaRodada?.Invoke(this);
    }

    // ---------- Clique/Press/Toque ----------

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!PodeInteragir()) return;

        // Apenas processa o clique esquerdo (toque)
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _leftHeld = true;
        }

        // **Removido:** a lógica do botão direito (flip) é agora no OnPointerClick (duplo clique)
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            _leftHeld = false;
            OnPecaStateChanged?.Invoke();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!PodeInteragir() || eventData.button != PointerEventData.InputButton.Left) return;

        // 1. Lógica de Duplo Clique (Flip)
        if (Time.time - _lastClickTime < DoubleClickTime)
        {
            // Duplo clique detectado -> Flip
            var flip = GetComponent<PecaFlip>();
            if (flip != null) flip.Alternar();
            OnPecaStateChanged?.Invoke();

            // notificar que houve flip nesta peça
            OnPecaFlipada?.Invoke(this);

            // "Consome" o clique para evitar a rotação
            _lastClickTime = 0;
            return;
        }

        // 2. Lógica de Clique Simples (Rotação)
        // A rotação só deve ocorrer se NÃO for o início de um Drag
        if (!_dragging)
        {
            // Clique simples detectado -> Rodar 
            // Rodamos apenas se o tempo for suficiente para excluir o duplo clique
            // e se não estivermos a arrastar.
            Rodar(+1);
        }

        // Atualiza o tempo do último clique
        _lastClickTime = Time.time;
    }


    // ---------- Drag & Drop ----------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!PodeInteragir() || eventData.button != PointerEventData.InputButton.Left) { eventData.pointerDrag = null; return; }

        // Se começar a arrastar, ignoramos a lógica do clique simples/duplo por agora
        _lastClickTime = 0;

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
        OnPecaStateChanged?.Invoke();
    }

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

        // Saber se pousou mesmo na grid e em que célula
        CelulaGrelha celDestino;
        bool pousouNaGrid = TentarSnapNaGrelha(eventData, out celDestino);

        if (!pousouNaGrid)
        {
            if (_maoContainer != null) _rt.SetParent(_maoContainer, worldPositionStays: false);
            _rt.sizeDelta = _tamanhoMao;
            _rt.localEulerAngles = Vector3.zero;
            _rt.localScale = Vector3.one;
            var gfx = GetComponentInChildren<Image>(true);
            if (gfx) gfx.rectTransform.localScale = Vector3.one;
        }
        else
        {
            // peça acabou de ser colocada numa célula da grid
            OnPecaColocadaNaGrid?.Invoke(this, celDestino);
        }

        OnPecaStateChanged?.Invoke();
    }

    void SeguirRato(PointerEventData eventData)
    {
        if (_dragLayer == null) return;

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_dragLayer, eventData.position, eventData.pressEventCamera, out var world))
        {
            _rt.position = world;
        }
    }

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

    // Agora devolve também a célula alvo via out CelulaGrelha
    bool TentarSnapNaGrelha(PointerEventData eventData, out CelulaGrelha celulaDestino)
    {
        celulaDestino = null;

        if (_gridRoot == null || _gridLayout == null) return false;

        var alvo = CelulaDebaixoDoCursor(eventData.position, eventData.pressEventCamera);
        if (alvo == null) return false;

        var cel = alvo.GetComponent<CelulaGrelha>();
        if (cel == null) return false;

        var jaTemPeca = alvo.GetComponentInChildren<Peca>(includeInactive: false) != null;
        if (jaTemPeca) return false;

        _rt.SetParent(alvo, worldPositionStays: false);
        _rt.sizeDelta = _gridLayout.cellSize;
        _rt.anchorMin = _rt.anchorMax = _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.anchoredPosition = Vector2.zero;
        _rt.localScale = Vector3.one;
        var gfx = GetComponentInChildren<Image>(true);
        if (gfx) gfx.rectTransform.localScale = Vector3.one;

        celulaDestino = cel;
        return true;
    }

    // -------- MÉTODOS DE RECOLHA --------

    public IEnumerator AnimarVoltarParaMao(float dur = 0.25f)
    {
        if (_maoContainer == null || _dragLayer == null)
        {
            VoltarParaMao();
            yield break;
        }

        var canvas = GetComponentInParent<Canvas>();
        var cam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector2 p0 = WorldToLocal(_dragLayer, _rt.position, cam);
        Vector2 p1 = WorldToLocal(_dragLayer, _maoContainer.position, cam);

        _rt.SetParent(_dragLayer, worldPositionStays: true);
        _rt.SetAsLastSibling();

        float startAlpha = (_cg ? _cg.alpha : 1f);
        if (_cg) { _cg.blocksRaycasts = false; }
        if (_img) _img.raycastTarget = false;

        Vector3 s0 = _rt.localScale;
        Vector3 s1 = s0 * 0.85f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, dur);
            float k = Mathf.Clamp01(t);
            float ke = k * k * (3f - 2f * k); // smoothstep

            _rt.anchoredPosition = Vector2.LerpUnclamped(p0, p1, ke);
            _rt.localScale = Vector3.LerpUnclamped(s0, s1, ke);
            if (_cg) _cg.alpha = Mathf.Lerp(startAlpha, 0f, ke);

            yield return null;
        }

        VoltarParaMao();
    }

    public void VoltarParaMao()
    {
        if (_maoContainer == null) return;

        _dragging = false;
        _leftHeld = false;

        _rt.SetParent(_maoContainer, worldPositionStays: false);
        _rt.sizeDelta = _tamanhoMao;
        _rt.localEulerAngles = Vector3.zero;
        _rt.localScale = Vector3.one;
        var gfx = GetComponentInChildren<Image>(true);
        if (gfx) gfx.rectTransform.localScale = Vector3.one;

        if (_cg != null) { _cg.alpha = 1f; _cg.blocksRaycasts = true; }
        if (_img != null) _img.raycastTarget = true;

        OnPecaStateChanged?.Invoke();
    }

    bool PodeInteragir()
    {
        if (ControladorJogo.Instancia && !ControladorJogo.Instancia.InteracaoPermitida) return false;

        var deck = FindAnyObjectByType<DeckController>();
        if (deck != null && !deck.TemCartaAtual) return false;

        return true;
    }

    static Vector2 WorldToLocal(RectTransform parent, Vector3 worldPos, Camera cam)
    {
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, cam, out var local);
        return local;
    }
}
