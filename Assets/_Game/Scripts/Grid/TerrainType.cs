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
    Rampart,  // 城壁（屋外壁。地上は通行不可だが、飛行兵は通過・停止できる。Phase 14）
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

    [Tooltip("地上のユニットが通行できるか（壁・城壁は false。飛行兵は下の canFlyOver を見る）")]
    public bool isWalkable = true;

    [Tooltip("飛行兵が入れるか（屋内壁だけ false。飛行兵は地形を無視して飛ぶので、これ以外の項目は見ない）")]
    public bool canFlyOver = true;

    [Tooltip("このマスに入るのに必要な移動コスト")]
    public int moveCost = 1;

    [Tooltip("騎兵・輸送隊が通行できるか（飛行兵は関係しない）。\n" +
             "山を徒歩専用にする等（isWalkable が false なら地上の全員が通行不可なので、ここは見ない）。Phase 15")]
    public bool mountedWalkable = true;

    [Tooltip("騎兵・輸送隊の移動コスト（0なら moveCost と同じ。森=3 など騎兵だけ重くできる）。Phase 15")]
    public int mountedCost = 0;

    [Tooltip("このマスにいる防御側が得る地形防御（物理・魔法の両方に加算。飛行兵は受けない）")]
    public int defenseBonus = 0;

    [Tooltip("マスの表示色（仮素材）")]
    public Color color = new Color(0.85f, 0.85f, 0.85f);
}
