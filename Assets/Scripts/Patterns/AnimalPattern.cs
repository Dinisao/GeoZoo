// AnimalPattern.cs — Define o “molde” que a grelha deve reproduzir para um animal.
// Contém: sprite da carta (preview), as células relativas que formam o padrão,
// rotação esperada por célula (opcional) e regra Eye/NoEye por célula (opcional).
// As listas alinham por índice: cellsRelatives[i] corresponde a cellsRotations[i] e cellsEyeReq[i] se existirem.
// As flags em “Regras” controlam o quão estrita é a validação.

using System.Collections.Generic;
using UnityEngine;

public enum EyeRequirement { None, Eye, NoEye }

[CreateAssetMenu(menuName = "GeoZoo/Animal Pattern", fileName = "ANM_Novo")]
public class AnimalPattern : ScriptableObject
{
    [Header("Carta (sprite do preview)")]
    public Sprite CardSprite; // Sprite mostrado no preview quando este padrão fica ativo.

    [Header("Padrão (coordenadas relativas em grelha)")]
    public List<Vector2Int> cellsRelatives = new List<Vector2Int>();          // Posições relativas (ex.: (0,0),(1,0),(0,1),(1,1))
    public List<int>        cellsRotations  = new List<int>();                // Rotação esperada por célula (0/90/180/270). Pode ficar vazio.
    public List<EyeRequirement> cellsEyeReq = new List<EyeRequirement>();     // Regra Eye/NoEye/None por célula. Pode ficar vazio.

    [Header("Regras")]
    // Estrito por omissão:
    public bool ExigirRotacao = true;                 // Se true, a rotação da peça tem de bater com a esperada (salvo exceções definidas abaixo).
    public bool PermitirRotacoesGlobais = true;       // Se true, o padrão casa mesmo que o conjunto esteja rodado (0/90/180/270) como um todo.
    public bool PermitirFlipH = false;                // Não usado (reservado).
    public bool PermitirFlipV = false;                // Não usado (reservado).

    // STRICT: o olho tem rotação que conta (fica false)
    public bool IgnorarRotacaoNaCelulaEye = false;    // Se true, ignora a rotação especificamente nas células marcadas como Eye.

    // STRICT: não aceitar equivalência 0↔180 / 90↔270 (fica false)
    public bool AceitarMeiaVolta = false;             // Se true, aceita 0≈180 e 90≈270 como equivalentes para rotação local.
}
