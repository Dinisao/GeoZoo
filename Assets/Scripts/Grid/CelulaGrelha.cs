// CelulaGrelha.cs — Uma célula da grelha de jogo.
// Mantém o índice (x,y) e pinta o fundo conforme estado: idle, hover permitido ou hover bloqueado.
// É “transparente” a cliques (raycastTarget = false) para não interferir com drag & drop.

using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CelulaGrelha : MonoBehaviour
{
    public Vector2Int Index;

    [Header("Skin")]
    public Image Bg;                          // auto-detecta se vazio
    public Sprite SpriteSliced;               // 9-sliced p/ cantos arredondados (opcional)
    public Color CorIdle  = new Color(1f, 1f, 1f, 0.06f);
    public Color CorHover = new Color(1f, 1f, 1f, 0.16f);         // célula livre sob cursor
    public Color CorBloq  = new Color(1f, 0.3f, 0.3f, 0.18f);     // célula ocupada sob cursor

    // Configura o fundo visual da célula (sprite 9-slice opcional, cor idle, sem bloquear cliques).
    void Awake()
    {
        if (!Bg) Bg = GetComponent<Image>();
        if (Bg)
        {
            if (SpriteSliced) { Bg.sprite = SpriteSliced; Bg.type = Image.Type.Sliced; }
            Bg.color = CorIdle;
            Bg.raycastTarget = false; // não bloqueia cliques
        }
    }

    // Atualiza a cor de hover: usa CorHover se permitido, CorBloq se ocupado; caso contrário volta a CorIdle.
    public void SetHover(bool on, bool permitido)
    {
        if (!Bg) return;
        Bg.color = on ? (permitido ? CorHover : CorBloq) : CorIdle;
    }
}
