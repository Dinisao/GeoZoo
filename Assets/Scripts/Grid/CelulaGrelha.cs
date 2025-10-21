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

    public void SetHover(bool on, bool permitido)
    {
        if (!Bg) return;
        Bg.color = on ? (permitido ? CorHover : CorBloq) : CorIdle;
    }
}
