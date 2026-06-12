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

    /// <summary>
    /// このマスに入るのに必要な移動コスト。今は常に 1。
    /// 後のフェーズで地形（森=2 など）を入れるときに使います。
    /// </summary>
    public int MoveCost = 1;

    /// <summary>このマスにいるユニット（いなければ null）。</summary>
    public Unit Occupant;

    // ── 今後ここに追加していく予定 ──
    //  public TerrainData Terrain;   // 地形（移動コスト・防御補正）

    public TileData(Vector2Int gridPosition)
    {
        GridPosition = gridPosition;
    }
}
