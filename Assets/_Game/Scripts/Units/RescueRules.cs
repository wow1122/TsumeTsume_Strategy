using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 救出システムの「できる・できない」判定を集めた共有ルール集（Phase 11〜）。
/// CombatRules と同じ発想で、UIとAI（将来敵が救出を使うとき）が同じ関数を呼ぶ。
///
/// 作者と合意済みのルール（2026-07-11）：
///  ・救出できるのは騎乗ユニットのみ。対象は隣接する同陣営の歩兵（騎乗・輸送隊は対象外）
///  ・行動済みの味方も救出できる（合意(h)）
///  ・救出中のペナルティなし・攻撃も可能（合意(c)(d)）
///  ・引き受け＝隣の救出中ユニットから貨物をもらう。空きがある騎乗ユニットのみ
///  ・降ろす＝隣接する「空きで歩ける」マスに再配置
///  ・代わりに降ろす＝歩兵専用。隣の救出中ユニットの貨物（歩兵）を、
///    「貨物の移動力 ≧ 自分の移動力」のときだけ降ろせる（合意(g)、仕様どおり）
/// </summary>
public static class RescueRules
{
    /// <summary>隣接4方向（上下左右）。</summary>
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    };

    // ===== 救出 =====

    /// <summary>user がいまの位置から「救出」できる隣接ユニットの一覧。</summary>
    public static List<Unit> FindRescueTargets(Unit user, GridManager grid)
    {
        var result = new List<Unit>();
        if (!CanCarryMore(user)) return result;

        foreach (Unit neighbor in GetAdjacentUnits(user.GridPosition, grid))
        {
            // 同陣営の歩兵だけが救出対象（騎乗・輸送隊は仕様で対象外）。
            // 行動済みかどうかは問わない（合意(h)）。
            if (neighbor.Faction == user.Faction && neighbor.Class == UnitClass.Infantry)
                result.Add(neighbor);
        }
        return result;
    }

    // ===== 引き受け =====

    /// <summary>user がいまの位置から「引き受け」できる、隣接する救出中ユニットの一覧。</summary>
    public static List<Unit> FindTakeOverCarriers(Unit user, GridManager grid)
    {
        var result = new List<Unit>();
        if (!CanCarryMore(user)) return result;

        foreach (Unit neighbor in GetAdjacentUnits(user.GridPosition, grid))
        {
            if (neighbor.Faction == user.Faction && neighbor.IsRescuing)
                result.Add(neighbor);
        }
        return result;
    }

    // ===== 降ろす =====

    /// <summary>carrier の隣で貨物を降ろせるマス（空きで歩ける）の一覧。</summary>
    public static List<Vector2Int> GetDropCells(Unit carrier, GridManager grid)
    {
        return GetDropCellsAround(carrier.GridPosition, grid);
    }

    /// <summary>指定マスの隣接4方向のうち、ユニットを配置できるマスの一覧。</summary>
    public static List<Vector2Int> GetDropCellsAround(Vector2Int center, GridManager grid)
    {
        var result = new List<Vector2Int>();
        foreach (Vector2Int dir in Directions)
        {
            TileData tile = grid.GetTile(center + dir);
            if (tile != null && tile.IsWalkable && tile.Occupant == null)
                result.Add(center + dir);
        }
        return result;
    }

    // ===== 代わりに降ろす =====

    /// <summary>
    /// 歩兵 user が「代わりに降ろす」を使える、隣接する救出中ユニットの一覧。
    /// 条件：貨物が歩兵で、貨物の移動力 ≧ user の移動力、かつ降ろせる空きマスがある。
    /// （移動力の低いユニットほど多くの味方を降ろせる、という仕様の意図どおり）
    /// </summary>
    public static List<Unit> FindProxyDropCarriers(Unit user, GridManager grid)
    {
        var result = new List<Unit>();
        if (user.Class != UnitClass.Infantry) return result; // 歩兵専用コマンド

        foreach (Unit neighbor in GetAdjacentUnits(user.GridPosition, grid))
        {
            if (neighbor.Faction != user.Faction || !neighbor.IsRescuing) continue;

            Unit cargo = neighbor.Carried[0];
            if (cargo.Class != UnitClass.Infantry) continue;   // 降ろせるのは歩兵貨物のみ
            if (cargo.Move < user.Move) continue;              // 貨物.移動力 ≧ 自分.移動力（合意(g)）
            if (GetDropCellsAround(neighbor.GridPosition, grid).Count == 0) continue;

            result.Add(neighbor);
        }
        return result;
    }

    // ===== 補助 =====

    /// <summary>あと1体でも格納できるか（歩兵は容量0なので常に false）。</summary>
    public static bool CanCarryMore(Unit unit)
    {
        return unit != null && unit.Carried.Count < unit.CarryCapacity;
    }

    /// <summary>指定マスの上下左右にいる生存ユニットを列挙する。</summary>
    private static IEnumerable<Unit> GetAdjacentUnits(Vector2Int center, GridManager grid)
    {
        foreach (Vector2Int dir in Directions)
        {
            TileData tile = grid.GetTile(center + dir);
            Unit occupant = tile != null ? tile.Occupant : null;
            if (occupant != null && occupant.IsAlive)
                yield return occupant;
        }
    }
}
