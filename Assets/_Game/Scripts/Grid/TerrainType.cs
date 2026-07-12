using UnityEngine;

/// <summary>
/// 地形の種類。マス1つにつき1つ割り当てる。
/// 数値（移動コスト・防御など）は enum ではなく TerrainDef（TerrainTable アセット）が持つので、
/// 調整はアセット編集だけで済む。
/// </summary>
public enum TerrainType
{
    Plain,    // 平地
    Forest,   // 森
    Mountain, // 山
    Fort,     // 砦
    Wall,     // 壁（屋内壁。通行不可・飛行でも通過不可）
    Rampart,  // 城壁（屋外壁。地上は通行不可だが、飛翔中は通過・停止できる。Phase 14）
}

/// <summary>
/// 地形1種類分の定義（表示名・記号・通行可否・移動コスト・防御・色）。
/// TerrainTable アセットのリストに並べて使う。
/// </summary>
[System.Serializable]
public class TerrainDef
{
    [Tooltip("地形の種類")]
    public TerrainType type = TerrainType.Plain;

    [Tooltip("画面表示に使う名前（例: 森）")]
    public string displayName = "平地";

    [Tooltip("ステージの文字マップ（terrainRows）で使う記号1文字（例: F）")]
    public string symbol = ".";

    [Tooltip("通行できるか（壁・城壁は false）")]
    public bool isWalkable = true;

    [Tooltip("飛翔状態のユニットが入れるか（屋内壁だけ false。地上ユニットには関係しない）")]
    public bool canFlyOver = true;

    [Tooltip("このマスに入るのに必要な移動コスト")]
    public int moveCost = 1;

    [Tooltip("このマスにいる防御側が得る地形防御（物理・魔法の両方に加算）")]
    public int defenseBonus = 0;

    [Tooltip("マスの表示色（仮素材）")]
    public Color color = new Color(0.85f, 0.85f, 0.85f);
}
