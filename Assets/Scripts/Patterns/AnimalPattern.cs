using System.Collections.Generic;
using UnityEngine;

public enum EyeRequirement { None, Eye, NoEye }

[CreateAssetMenu(menuName = "GeoZoo/Animal Pattern", fileName = "ANM_Novo")]
public class AnimalPattern : ScriptableObject
{
    [Header("Carta (sprite do preview)")]
    public Sprite CardSprite;

    [Header("Padrão (coordenadas relativas em grelha)")]
    public List<Vector2Int> cellsRelatives = new List<Vector2Int>();          // ex.: (0,0),(1,0),(0,1),(1,1)
    public List<int>        cellsRotations  = new List<int>();                // 0/90/180/270 (relativas; pode ficar vazio)
    public List<EyeRequirement> cellsEyeReq = new List<EyeRequirement>();     // None/Eye/NoEye (pode ficar vazio)

    [Header("Regras")]
    // Estrito por omissão:
    public bool ExigirRotacao = true;
    public bool PermitirRotacoesGlobais = true;
    public bool PermitirFlipH = false;      // não usar
    public bool PermitirFlipV = false;      // não usar

    // STRICT: o olho tem rotação que conta (fica false)
    public bool IgnorarRotacaoNaCelulaEye = false;

    // STRICT: não aceitar equivalência 0↔180 / 90↔270 (fica false)
    public bool AceitarMeiaVolta = false;
}
