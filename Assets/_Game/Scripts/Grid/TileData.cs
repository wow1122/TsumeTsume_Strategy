using UnityEngine;

/// <summary>
/// マップの1マス分の「内部データ」を表すクラス。
/// 見た目（スプライト）とは別に、ゲームの判定はこのデータを使って行います。
/// ※ MonoBehaviour ではない普通の C# クラスなので、GameObject には付きません。
/// </summary>
public class TileData
{
    /// <summary>このマスのグリッド座標（左下が 0,0）。</summary>
    public Vector2Int GridPosition { get; private set; }

    /// <summary>
    /// 通行可能かどうか。今は常に true。
    /// 後のフェーズで「壁」などを表現するときに使います。
    /// </summary>
    public bool IsWalkable = true;

    // ── 今後ここに追加していく予定 ──
    //  public TerrainData Terrain;   // 地形（移動コスト・防御補正）
    //  public Unit Occupant;         // このマスにいるユニット（いなければ null）

    public TileData(Vector2Int gridPosition)
    {
        GridPosition = gridPosition;
    }
}
