#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Patterns Builder "como no jogo":
/// - Mão com 4 tiles (F1..F4 / B1..B4) arrastáveis para a grelha (e vice-versa).
/// - Clique: selecionar; Q/E: rodar; Botão direito: virar; Drop numa célula ocupada = SWAP.
/// - Guarda para AnimalPattern (coords/rotações/eye). Mostra contador de tiles no tabuleiro.
/// - Botão para renomear o asset atual para "ANM_<Carta>".
///
/// Nota geral: esta janela tenta replicar o fluxo do jogo para montar rapidamente um AnimalPattern.
/// O foco está em UX: arrastar/rodar/virar, normalizar posições e converter rotações UI↔Math ao guardar/carregar.
public class PatternsBuilderWindow : EditorWindow
{
    [MenuItem("Tools/GeoZoo/Patterns Builder")]
    public static void Open()
    {
        // Abre a janela com um tamanho mínimo confortável para grelha + colunas.
        var w = GetWindow<PatternsBuilderWindow>("Patterns Builder");
        w.minSize = new Vector2(1100, 640);
        w.Show();
    }

    // --------- Cartas (deck) ----------
    DeckController[] _decks;
    List<Sprite> _deckSprites = new List<Sprite>();
    int _selectedSpriteIndex = -1;

    // --------- Tiles / sprites ----------
    string _frontFolder = "Assets/Art/Tiles/Front";
    string _backFolder  = "Assets/Art/Tiles/Back";

    Sprite[] _front = new Sprite[4];   // F1..F4
    Sprite[] _back  = new Sprite[4];   // B1..B4
    bool[] _backHasEye = new bool[4] { true, true, false, false }; // B1,B2 têm olho

    // --------- Mão (4 instâncias) + grelha ----------
    struct TileInst
    {
        public int id;           // 0..3
        public bool onBoard;     // true = está numa célula
        public Vector2Int cell;  // se onBoard, a posição da célula
        public int rot;          // 0/90/180/270 (UI; clockwise)
        public bool back;        // true = a mostrar verso (B)
    }
    TileInst[] _tiles = new TileInst[4];

    // seleção & drag
    int _sel = -1;                // índice do tile selecionado (0..3) ou -1
    bool _dragging = false;
    Vector2 _dragStartMouse;
    TileInst _dragStartState;
    Vector2 _dragVisualPos;

    // grelha
    Vector2Int _gridSize = new Vector2Int(4, 3);
    Dictionary<Vector2Int, Rect> _gridRects = new Dictionary<Vector2Int, Rect>();
    Rect[] _handRects = new Rect[4];

    // pattern asset
    string _savePath = "Assets/AnimalPattern";
    AnimalPattern _current;

    // scrolls
    Vector2 _scrollCards, _scrollBoard;

    // ---------- Setup ----------
    void OnEnable()
    {
        // Carrega sprites de cartas existentes na(s) cena(s) e repõe mão/tabuleiro.
        RefreshDecks();
        ResetHand();
    }

    void RefreshDecks()
    {
        // Varrimento tolerante de Decks na(s) cena(s) para extrair as frentes únicas das cartas.
#if UNITY_2023_1_OR_NEWER
        _decks = UnityEngine.Object.FindObjectsByType<DeckController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        _decks = UnityEngine.Object.FindObjectsOfType<DeckController>(true);
#endif
        var set = new HashSet<Sprite>();
        _deckSprites.Clear();
        foreach (var d in _decks)
        {
            if (!d || d.Cartas == null) continue;
            foreach (var fp in d.Cartas)
                if (fp.Frente && set.Add(fp.Frente)) _deckSprites.Add(fp.Frente);
        }
        _deckSprites = _deckSprites.OrderBy(s => s.name).ToList();
        if (_deckSprites.Count == 0) _selectedSpriteIndex = -1;
        else if (_selectedSpriteIndex < 0 || _selectedSpriteIndex >= _deckSprites.Count) _selectedSpriteIndex = 0;
    }

    void ResetHand()
    {
        // Repõe a mão (todos os 4 tiles “fora do tabuleiro”).
        for (int i = 0; i < 4; i++)
            _tiles[i] = new TileInst { id = i, onBoard = false, cell = default, rot = 0, back = false };

        _sel = -1;
        _dragging = false;
        Repaint();
    }

    // ---------- GUI ----------
    void OnGUI()
    {
        // Layout: Toolbar topo + 3 colunas (cartas, mão, tabuleiro).
        DrawToolbar();

        EditorGUILayout.BeginHorizontal();

        DrawCardsColumn(GUILayout.Width(220));
        DrawHandColumn(GUILayout.Width(160));
        DrawBoardPane();

        EditorGUILayout.EndHorizontal();

        HandleKeyboard();
    }

    void DrawToolbar()
    {
        // Ações rápidas e dimensões da grelha (Cols/Rows) com sliders.
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            if (GUILayout.Button("Recarregar Decks", GUILayout.Height(24))) RefreshDecks();
            GUILayout.Space(6);
            _savePath = EditorGUILayout.TextField("Guardar em:", _savePath, GUILayout.MaxWidth(360));
            GUILayout.FlexibleSpace();
            _gridSize.x = EditorGUILayout.IntSlider("Cols", _gridSize.x, 2, 10, GUILayout.MaxWidth(260));
            _gridSize.y = EditorGUILayout.IntSlider("Rows", _gridSize.y, 2, 10, GUILayout.MaxWidth(260));
        }
    }

    // ----- Coluna Cartas -----
    void DrawCardsColumn(params GUILayoutOption[] opts)
    {
        // Lista de sprites do deck detectados; clicar seleciona e tenta carregar pattern existente.
        EditorGUILayout.BeginVertical(opts);

        _scrollCards = EditorGUILayout.BeginScrollView(_scrollCards);
        if (_deckSprites.Count == 0)
        {
            EditorGUILayout.HelpBox("Não encontrei sprites de cartas na(s) cena(s). Adiciona um DeckController com Cartas preenchidas.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < _deckSprites.Count; i++)
            {
                var s = _deckSprites[i];
                var rect = GUILayoutUtility.GetRect(200, 60, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(rect, i == _selectedSpriteIndex ? new Color(0.2f,0.6f,1f,0.25f) : new Color(1,1,1,0.06f));
                var tex = AssetPreview.GetAssetPreview(s) ?? s?.texture;
                if (tex) GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                var lab = new Rect(rect.x+4, rect.yMax-16, rect.width-8, 14);
                EditorGUI.LabelField(lab, s ? s.name : "(null)", EditorStyles.miniLabel);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    _selectedSpriteIndex = i;
                    var ap = FindPatternByCardSprite(_deckSprites[i]);
                    if (ap) LoadPattern(ap);
                }
            }
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(6);
        GUI.enabled = _selectedSpriteIndex >= 0;
        if (GUILayout.Button("Criar/Selecionar Pattern desta Carta", GUILayout.Height(24)))
            CreateOrSelectPatternForSprite(_deckSprites[_selectedSpriteIndex]);
        GUI.enabled = true;

        EditorGUILayout.EndVertical();
    }

    // ----- Coluna Mão -----
    void DrawHandColumn(params GUILayoutOption[] opts)
    {
        // Zona de configuração rápida dos 4 tiles (frentes/versos) e drag a partir da mão.
        EditorGUILayout.BeginVertical(opts);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Tiles (mão)", EditorStyles.boldLabel);

            _frontFolder = EditorGUILayout.TextField("Frente (pasta)", _frontFolder);
            _backFolder  = EditorGUILayout.TextField("Verso  (pasta)", _backFolder);
            if (GUILayout.Button("Auto-carregar F1..F4 / B1..B4")) AutoLoadTiles();

            GUILayout.Space(6);
            for (int i = 0; i < 4; i++)
            {
                var r = GUILayoutUtility.GetRect(130, 80, GUILayout.ExpandWidth(true));
                _handRects[i] = r;

                EditorGUI.DrawRect(r, new Color(1,1,1,0.06f));

                var inst = _tiles[i];
                var sp = inst.back ? _back[i] : _front[i];
                var tex = sp ? (AssetPreview.GetAssetPreview(sp) ?? sp.texture) : null;

                if (_sel == i)
                    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 3), new Color(0.2f,0.6f,1f,0.9f));

                if (tex) GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);

                var tag = inst.back ? $"B{i+1}" : $"F{i+1}";
                var lab = new Rect(r.x+4, r.y+4, r.width-8, 16);
                EditorGUI.LabelField(lab, tag + (inst.back ? (_backHasEye[i] ? " (Eye)" : " (NoEye)") : ""), EditorStyles.miniBoldLabel);

                // Drag a partir da mão
                if (!inst.onBoard && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                { _sel = i; BeginDrag(i, Event.current.mousePosition); Event.current.Use(); }
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Repor Mão (retira do tabuleiro)", GUILayout.Height(22)))
                ResetHand();
        }

        EditorGUILayout.EndVertical();
    }

    void AutoLoadTiles()
    {
        // Procura F1..F4 e B1..B4 nas pastas indicadas; marca B1/B2 como Eye por convenção.
        _front[0] = FindSprite(_frontFolder, "F1");
        _front[1] = FindSprite(_frontFolder, "F2");
        _front[2] = FindSprite(_frontFolder, "F3");
        _front[3] = FindSprite(_frontFolder, "F4");

        _back[0]  = FindSprite(_backFolder,  "B1");
        _back[1]  = FindSprite(_backFolder,  "B2");
        _back[2]  = FindSprite(_backFolder,  "B3");
        _back[3]  = FindSprite(_backFolder,  "B4");

        _backHasEye[0] = true; _backHasEye[1] = true;
        _backHasEye[2] = false; _backHasEye[3] = false;

        Repaint();
    }

    static Sprite FindSprite(string folder, string name)
    {
        // Busca um sprite pelo nome exacto dentro da pasta indicada.
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder)) return null;
        var guids = AssetDatabase.FindAssets($"{name} t:Sprite", new[] { folder });
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s && s.name == name) return s;
        }
        return null;
    }

    // ----- Tabuleiro / grelha -----
    void DrawBoardPane()
    {
        // Painel principal do tabuleiro + barra inferior com ações (guardar/renomear).
        EditorGUILayout.BeginVertical();

        using (new EditorGUILayout.HorizontalScope())
        {
            var curName = _current ? _current.name : "Sem Pattern Selecionado";
            EditorGUILayout.LabelField($"Pattern: {curName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
        }

        _scrollBoard = EditorGUILayout.BeginScrollView(_scrollBoard);
        var gridRect = GUILayoutUtility.GetRect(position.width-420, position.height-220, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawBoard(gridRect);
        EditorGUILayout.EndScrollView();

        // Barra inferior com contador + ações
        using (new EditorGUILayout.HorizontalScope())
        {
            int onBoardCount = CountOnBoard();
            EditorGUILayout.LabelField($"Tiles no tabuleiro: {onBoardCount}", GUILayout.Width(180));

            GUI.enabled = _current != null;
            if (GUILayout.Button("Guardar no Pattern", GUILayout.Height(26)))
            {
                if (onBoardCount == 0)
                    EditorUtility.DisplayDialog("Patterns Builder", "Não há tiles no tabuleiro. Coloca pelo menos um tile e tenta de novo.", "Ok");
                else
                    SaveToPattern(_current);
            }

            if (GUILayout.Button("Renomear para sugerido (ANM_<Carta>)", GUILayout.Height(26)))
                RenameCurrentToSuggested();
            GUI.enabled = true;
        }

        EditorGUILayout.EndVertical();
    }

    void DrawBoard(Rect rect)
    {
        // Desenha grelha, tiles no tabuleiro, arraste visual e lida com clique/drag/soltar (inclui SWAP).
        if (Event.current.type == EventType.Repaint)
            EditorGUI.DrawRect(rect, new Color(0.1f,0.1f,0.1f,0.08f));

        _gridRects.Clear();

        int cols = Mathf.Max(2, _gridSize.x);
        int rows = Mathf.Max(2, _gridSize.y);
        float gap = 6f;
        float cw = (rect.width  - (cols + 1) * gap) / cols;
        float ch = (rect.height - (rows + 1) * gap) / rows;
        var mouse = Event.current.mousePosition;

        // células (UI referencial: (0,0) canto sup-esquerdo; Y cresce para baixo)
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < cols; x++)
        {
            var r = new Rect(rect.x + gap + x*(cw+gap), rect.y + gap + y*(ch+gap), cw, ch);
            _gridRects[new Vector2Int(x,y)] = r;
            EditorGUI.DrawRect(r, new Color(1,1,1,0.06f));
        }

        // tiles no tabuleiro
        for (int i = 0; i < 4; i++)
        {
            var t = _tiles[i];
            if (!t.onBoard) continue;
            if (!_gridRects.TryGetValue(t.cell, out var cr)) continue;

            var sp = t.back ? _back[i] : _front[i];
            var tex = sp ? (AssetPreview.GetAssetPreview(sp) ?? sp.texture) : null;

            if (_sel == i)
                EditorGUI.DrawRect(new Rect(cr.x, cr.y, cr.width, 3), new Color(0.2f,0.6f,1f,0.9f));

            if (tex)
            {
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(t.rot, cr.center);     // UI: positivo = clockwise
                GUI.DrawTexture(cr, tex, ScaleMode.ScaleToFit);
                GUI.matrix = old;
            }

            if (cr.Contains(mouse))
            {
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && !_dragging)
                { _sel = i; BeginDrag(i, mouse); Event.current.Use(); }
                else if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && !_dragging)
                { _sel = i; t.back = !t.back; _tiles[i] = t; Event.current.Use(); Repaint(); }
            }
        }

        // drag visual
        if (_dragging && _sel >= 0 && _sel < 4)
        {
            var t = _tiles[_sel];
            var sp = t.back ? _back[_sel] : _front[_sel];
            var tex = sp ? (AssetPreview.GetAssetPreview(sp) ?? sp.texture) : null;

            var drawR = new Rect(_dragVisualPos.x-48, _dragVisualPos.y-48, 96, 96);
            EditorGUI.DrawRect(drawR, new Color(0,0,0,0.1f));
            if (tex)
            {
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(t.rot, drawR.center);
                GUI.DrawTexture(drawR, tex, ScaleMode.ScaleToFit);
                GUI.matrix = old;
            }

            if (Event.current.type == EventType.MouseDrag)
            { _dragVisualPos = mouse; Repaint(); }
        }

        // MouseUp global no tabuleiro
        if (_dragging && Event.current.type == EventType.MouseUp)
        { EndDrag(mouse); Event.current.Use(); }

        var help = "Arrastar: mover • Clique num tile: selecionar • Q/E: rodar • Botão direito: virar • SWAP ao largar numa célula ocupada";
        var helpRect = new Rect(rect.x+6, rect.yMax-20, rect.width-12, 18);
        EditorGUI.LabelField(helpRect, help, EditorStyles.miniLabel);
    }

    // ---------- Drag helpers ----------
    void BeginDrag(int idx, Vector2 mouse)
    {
        // Marca início do arraste e guarda estado para possível reversão.
        _dragging = true;
        _dragStartMouse = mouse;
        _dragStartState = _tiles[idx];
        _dragVisualPos = mouse;
    }

    int FindTileIndexAtCell(Vector2Int cell)
    {
        // Procura qual tile está numa célula (para SWAP).
        for (int i = 0; i < 4; i++)
            if (_tiles[i].onBoard && _tiles[i].cell == cell) return i;
        return -1;
    }

    void EndDrag(Vector2 mouse)
    {
        // Lógica de soltar: coloca, troca (SWAP) ou reverte. Também aceita “voltar à mão” sobre o slot.
        if (_sel < 0 || _sel >= 4) { _dragging = false; return; }

        var t = _tiles[_sel];
        var startWasOnBoard = _dragStartState.onBoard;

        var cellOver = CellUnderMouse(mouse);
        if (cellOver.HasValue)
        {
            var occ = FindTileIndexAtCell(cellOver.Value);
            if (occ < 0)
            {
                t.onBoard = true; t.cell = cellOver.Value; _tiles[_sel] = t;
            }
            else
            {
                if (startWasOnBoard)
                {
                    // SWAP
                    var other = _tiles[occ];
                    var srcCell = _dragStartState.cell;
                    other.cell = srcCell; other.onBoard = true;
                    t.cell = cellOver.Value; t.onBoard = true;
                    _tiles[occ] = other; _tiles[_sel] = t;
                }
                else
                {
                    // veio da mão -> ocupa destino e o ocupante volta à mão
                    var other = _tiles[occ];
                    other.onBoard = false; _tiles[occ] = other;
                    t.onBoard = true; t.cell = cellOver.Value; _tiles[_sel] = t;
                }
            }
        }
        else
        {
            int? handSlot = HandSlotUnderMouse(mouse);
            if (handSlot.HasValue && handSlot.Value == _sel)
            { t.onBoard = false; _tiles[_sel] = t; }
            else
            { _tiles[_sel] = _dragStartState; } // reverte
        }

        _dragging = false;
        Repaint();
    }

    Vector2Int? CellUnderMouse(Vector2 mouse)
    {
        foreach (var kv in _gridRects) if (kv.Value.Contains(mouse)) return kv.Key;
        return null;
    }
    int? HandSlotUnderMouse(Vector2 mouse)
    {
        for (int i = 0; i < 4; i++) if (_handRects[i].Contains(mouse)) return i;
        return null;
    }

    // ---------- Teclado ----------
    void HandleKeyboard()
    {
        // Rotação Q/E do tile selecionado (clockwise/anticlockwise) — ignorado se estiver a arrastar.
        var e = Event.current;
        if (e.type != EventType.KeyDown || _dragging) return;

        if (_sel >= 0 && _sel < 4)
        {
            var t = _tiles[_sel];
            if (e.keyCode == KeyCode.Q) { t.rot = NormRot(t.rot - 90); _tiles[_sel] = t; e.Use(); Repaint(); }
            if (e.keyCode == KeyCode.E) { t.rot = NormRot(t.rot + 90); _tiles[_sel] = t; e.Use(); Repaint(); }
        }
    }

    // ---------- Guardar / Renomear ----------
    int CountOnBoard()
    {
        int n = 0; for (int i = 0; i < 4; i++) if (_tiles[i].onBoard) n++; return n;
    }

    void RenameCurrentToSuggested()
    {
        // Renomeia o asset atual para “ANM_<Carta>”, mantendo a pasta e evitando conflitos.
        if (!_current) return;
        var suggested = SuggestedAssetName();
        var path = AssetDatabase.GetAssetPath(_current);
        var folder = System.IO.Path.GetDirectoryName(path).Replace('\\','/');
        if (string.IsNullOrEmpty(folder)) folder = _savePath;
        var newPath = folder + "/" + suggested + ".asset";
        if (newPath == path) return;

        if (AssetDatabase.LoadAssetAtPath<AnimalPattern>(newPath) != null)
            newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
        var err = AssetDatabase.MoveAsset(path, newPath);
        if (!string.IsNullOrEmpty(err))
            EditorUtility.DisplayDialog("Renomear", "Falhou: " + err, "Ok");
        else
            ShowNotification(new GUIContent("Renomeado para " + System.IO.Path.GetFileNameWithoutExtension(newPath)));
    }

    string SuggestedAssetName()
    {
        // Sugestão baseada na carta selecionada; fallback para nome atual do asset.
        string baseName = (_selectedSpriteIndex >= 0 && _selectedSpriteIndex < _deckSprites.Count && _deckSprites[_selectedSpriteIndex])
            ? _deckSprites[_selectedSpriteIndex].name
            : (_current ? _current.name.Replace("ANM_","") : "Novo");
        return "ANM_" + MakeSafeFileName(baseName);
    }

    void CreateOrSelectPatternForSprite(Sprite s)
    {
        // Cria um novo AnimalPattern para a sprite (se não existir) com flags default,
        // ou seleciona o existente; depois carrega-o na janela.
        var ap = FindPatternByCardSprite(s);
        if (ap == null)
        {
            EnsureFolder(_savePath);
            string safe = MakeSafeFileName("ANM_" + (s ? s.name : "Novo"));
            string path = AssetDatabase.GenerateUniqueAssetPath(_savePath + "/" + safe + ".asset");
            ap = ScriptableObject.CreateInstance<AnimalPattern>();
            ap.CardSprite = s;

            // STRICT por omissão (alinha com GridValidator)
            ap.ExigirRotacao = true;
            ap.PermitirRotacoesGlobais = true;
            ap.PermitirFlipH = false;
            ap.PermitirFlipV = false;
            ap.IgnorarRotacaoNaCelulaEye = false;
            ap.AceitarMeiaVolta = false;

            AssetDatabase.CreateAsset(ap, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        LoadPattern(ap);
    }

    AnimalPattern FindPatternByCardSprite(Sprite s)
    {
        // Procura em todos os AnimalPattern por referência direta ao CardSprite.
        var guids = AssetDatabase.FindAssets("t:AnimalPattern");
        foreach (var g in guids)
        {
            var p = AssetDatabase.LoadAssetAtPath<AnimalPattern>(AssetDatabase.GUIDToAssetPath(g));
            if (p && p.CardSprite == s) return p;
        }
        return null;
    }

    void LoadPattern(AnimalPattern ap)
    {
        // Carrega o asset para a janela: recria o layout de tiles conforme o pattern.
        _current = ap;
        ResetHand();

        if (!_current) return;

        var rel  = _current.cellsRelatives ?? new List<Vector2Int>();
        var rots = _current.cellsRotations ?? new List<int>();
        var eyes = _current.cellsEyeReq    ?? new List<EyeRequirement>();

        for (int i = 0; i < Mathf.Min(rel.Count, 4); i++)
        {
            var t = _tiles[i];
            t.onBoard = true;
            t.cell = rel[i];
            // IMPORTANTE: rotações guardadas no asset estão em "math/CCW".
            // Converte para "UI/CW" para desenhar corretamente aqui.
            t.rot = (i < rots.Count ? MathToUi(rots[i]) : 0);
            var eye = (i < eyes.Count ? eyes[i] : EyeRequirement.None);
            t.back = (eye != EyeRequirement.None);
            _tiles[i] = t;
        }

        if (rel.Count > 0)
        {
            // Normaliza para que o conjunto fique “encostado” ao topo-esquerdo do tabuleiro.
            int minX = rel.Min(v => v.x);
            int minY = rel.Min(v => v.y);
            for (int i = 0; i < 4; i++)
            {
                if (_tiles[i].onBoard)
                {
                    var t = _tiles[i];
                    t.cell = new Vector2Int(t.cell.x - minX, t.cell.y - minY);
                    _tiles[i] = t;
                }
            }
        }

        Repaint();
    }

    void SaveToPattern(AnimalPattern ap)
    {
        // Extrai os tiles no tabuleiro, normaliza posições (UI) e converte rotações para "math/CCW" antes de guardar.
        var onBoard = new List<TileInst>();
        for (int i = 0; i < 4; i++) if (_tiles[i].onBoard) onBoard.Add(_tiles[i]);

        if (onBoard.Count == 0)
        {
            EditorUtility.DisplayDialog("Patterns Builder", "Não há tiles no tabuleiro. Coloca pelo menos um tile e tenta outra vez.", "Ok");
            return;
        }

        // Normalizar (UI): (0,0) no topo-esquerdo do conjunto
        int minX = onBoard.Min(t => t.cell.x);
        int minY = onBoard.Min(t => t.cell.y);
        var ord = onBoard.OrderBy(t => t.cell.y).ThenBy(t => t.cell.x).ToList();

        ap.cellsRelatives = ord.Select(t => new Vector2Int(t.cell.x - minX, t.cell.y - minY)).ToList();

        // IMPORTANTE: converter para "math/CCW" antes de guardar no asset.
        ap.cellsRotations = ord.Select(t => UiToMath(t.rot)).ToList();

        ap.cellsEyeReq    = ord.Select(t =>
        {
            if (!t.back) return EyeRequirement.None;
            return _backHasEye[Mathf.Clamp(t.id,0,3)] ? EyeRequirement.Eye : EyeRequirement.NoEye;
        }).ToList();

        // Flags por omissão (alteráveis no inspector depois)
        ap.ExigirRotacao = true;
        ap.PermitirRotacoesGlobais = true;
        ap.PermitirFlipH = false;
        ap.PermitirFlipV = false;
        ap.IgnorarRotacaoNaCelulaEye = false;
        ap.AceitarMeiaVolta = false;

        if (_selectedSpriteIndex >= 0 && _selectedSpriteIndex < _deckSprites.Count && _deckSprites[_selectedSpriteIndex])
            ap.CardSprite = _deckSprites[_selectedSpriteIndex];

        Undo.RecordObject(ap, "Salvar AnimalPattern");
        EditorUtility.SetDirty(ap);
        AssetDatabase.SaveAssets();

        ShowNotification(new GUIContent("Pattern guardado ✓"));
    }

    // ---------- Utils ----------
    static int NormRot(int deg)
    {
        // “Arredonda” graus para múltiplos de 90 em [0, 360).
        int g = deg % 360; if (g < 0) g += 360;
        int q = Mathf.RoundToInt(g / 90f) * 90;
        q = (q % 360 + 360) % 360;
        return q;
    }

    // Conversões entre convenções de rotação
    // UI (GUIUtility.RotateAroundPivot): positivo = sentido dos ponteiros (CW)
    // Math/RectTransform: positivo = anti-horário (CCW)
    static int UiToMath(int ui) => (360 - NormRot(ui)) % 360;
    static int MathToUi(int m)  => (360 - NormRot(m))  % 360;

    static void EnsureFolder(string folderPath)
    {
        // Garante que a pasta de destino existe (cria intermedárias se necessário).
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        var parts = folderPath.Split('/');
        string cur = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
    static string MakeSafeFileName(string s)
    {
        // Remove caracteres inválidos para nomes de ficheiro no SO.
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
#endif
