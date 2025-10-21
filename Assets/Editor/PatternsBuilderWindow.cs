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
public class PatternsBuilderWindow : EditorWindow
{
    [MenuItem("Tools/GeoZoo/Patterns Builder")]
    public static void Open()
    {
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
        RefreshDecks();
        ResetHand();
    }

    void RefreshDecks()
    {
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
        for (int i = 0; i < 4; i++)
            _tiles[i] = new TileInst { id = i, onBoard = false, cell = default, rot = 0, back = false };

        _sel = -1;
        _dragging = false;
        Repaint();
    }

    // ---------- GUI ----------
    void OnGUI()
    {
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
        _dragging = true;
        _dragStartMouse = mouse;
        _dragStartState = _tiles[idx];
        _dragVisualPos = mouse;
    }

    int FindTileIndexAtCell(Vector2Int cell)
    {
        for (int i = 0; i < 4; i++)
            if (_tiles[i].onBoard && _tiles[i].cell == cell) return i;
        return -1;
    }

    void EndDrag(Vector2 mouse)
    {
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
        string baseName = (_selectedSpriteIndex >= 0 && _selectedSpriteIndex < _deckSprites.Count && _deckSprites[_selectedSpriteIndex])
            ? _deckSprites[_selectedSpriteIndex].name
            : (_current ? _current.name.Replace("ANM_","") : "Novo");
        return "ANM_" + MakeSafeFileName(baseName);
    }

    void CreateOrSelectPatternForSprite(Sprite s)
    {
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
        foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
#endif
