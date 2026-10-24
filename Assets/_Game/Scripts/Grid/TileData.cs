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
    /// このマスの地形定義（Phase 13）。GridManager.ApplyTerrain が設定する。
    /// null のときは「平地相当」（通行可・コスト1・防御0）として扱う。
    /// </summary>
    public TerrainDef Terrain;

    /// <summary>地上のユニットが通行可能かどうか（壁・城壁は false）。</summary>
    public bool IsWalkable => Terrain == null || Terrain.isWalkable;

    /// <summary>飛行兵が入れるか（屋内壁だけ false。城壁も入れる。Phase 14 の地形・2026-10-24 から飛行兵は常にこちら）。</summary>
    public bool CanFlyOver => Terrain == null || Terrain.canFlyOver;

    /// <summary>このマスに入るのに必要な移動コスト（平地1・森2など）。</summary>
    public int MoveCost => Terrain != null ? Terrain.moveCost : 1;

    /// <summary>
    /// この兵種が通行できるか（Phase 15）。移動・配置・降ろす先・死亡時の解放先がすべてここを見る。
    /// 飛行兵は地形を無視して飛ぶ＝飛行で入れるマスならどこでも可（屋内壁だけ不可。2026-10-24 作者仕様）。
    /// 騎兵・輸送隊は騎乗通行可否（mountedWalkable）も見る（山は通れない等）。
    /// </summary>
    public bool IsWalkableFor(UnitClass unitClass)
    {
        if (unitClass.IsFlier()) return CanFlyOver;
        if (!IsWalkable) return false;
        if (unitClass.IsMounted() && Terrain != null && !Terrain.mountedWalkable) return false;
        return true;
    }

    /// <summary>
    /// この兵種がこのマスに入る移動コスト（Phase 15）。
    /// 飛行兵は地形に関係なく常に1（2026-10-24 作者仕様）。
    /// 騎兵・輸送隊は騎乗用のコスト指定（mountedCost）があればそちらを使う。
    /// </summary>
    public int MoveCostFor(UnitClass unitClass)
    {
        if (unitClass.IsFlier()) return 1;
        if (unitClass.IsMounted() && Terrain != null && Terrain.mountedCost > 0)
            return Terrain.mountedCost;
        return MoveCost;
    }

    /// <summary>このマスにいる防御側が得る防御ボーナス（地形効果）。</summary>
    public int DefenseBonus => Terrain != null ? Terrain.defenseBonus : 0;

    /// <summary>このマスにいるユニット（いなければ null）。</summary>
    public Unit Occupant;

    public TileData(Vector2Int gridPosition)
    {
        GridPosition = gridPosition;
    }
}
