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
///
/// Phase 12（輸送隊）の追加合意（2026-07-11）：
///  ・輸送隊（容量4）は騎乗ユニットも救出・引き受けできる（対象が輸送隊のときは不可）
///  ・誰も輸送隊を救出できない（乗り込みでの格納も不可）
///  ・入れ子救出は禁止：貨物を持っているユニットは、救出・乗り込みの対象にならない
///  ・「乗り込む」＝隣接する輸送隊へ自分から格納される（自分は行動終了。輸送隊の行動は消費しない）
///  ・引き受けで騎乗ユニット（輸送隊以外）が受け取れるのは歩兵の貨物のみ。輸送隊は何でも受け取れる
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
            // 行動済みかどうかは問わない（合意(h)）。
            if (neighbor.Faction != user.Faction) continue;
            if (!CanBeStored(neighbor)) continue; // 輸送隊・貨物持ちは格納不可（入れ子禁止）

            // 通常の騎乗ユニットの救出対象は歩兵のみ。輸送隊は騎乗ユニットも救出できる
            if (user.Class != UnitClass.Transporter && neighbor.Class != UnitClass.Infantry) continue;

            result.Add(neighbor);
        }
        return result;
    }

    // ===== 乗り込む（Phase 12）=====

    /// <summary>
    /// user がいまの位置から「乗り込む」ことができる、隣接する輸送隊の一覧。
    /// 乗り込む＝自分から輸送隊に格納される（自分は行動終了。輸送隊の行動は消費しない）。
    /// </summary>
    public static List<Unit> FindBoardTransporters(Unit user, GridManager grid)
    {
        var result = new List<Unit>();
        if (!CanBeStored(user)) return result; // 輸送隊自身・貨物持ちは乗り込めない

        foreach (Unit neighbor in GetAdjacentUnits(user.GridPosition, grid))
        {
            if (neighbor.Faction == user.Faction
                && neighbor.Class == UnitClass.Transporter
                && CanCarryMore(neighbor))
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
            if (neighbor.Faction == user.Faction && neighbor.IsRescuing
                && GetTakeOverCargoes(user, neighbor).Count > 0)
                result.Add(neighbor);
        }
        return result;
    }

    /// <summary>
    /// receiver が carrier から「引き受け」できる貨物の一覧。
    /// 輸送隊は何でも受け取れるが、通常の騎乗ユニットが受け取れるのは歩兵の貨物のみ（Phase 12）。
    /// </summary>
    public static List<Unit> GetTakeOverCargoes(Unit receiver, Unit carrier)
    {
        var result = new List<Unit>();
        foreach (Unit cargo in carrier.Carried)
        {
            if (receiver.Class != UnitClass.Transporter && cargo.Class != UnitClass.Infantry) continue;
            result.Add(cargo);
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
            if (GetProxyDropCargoes(user, neighbor).Count == 0) continue;
            if (GetDropCellsAround(neighbor.GridPosition, grid).Count == 0) continue;

            result.Add(neighbor);
        }
        return result;
    }

    /// <summary>
    /// 歩兵 user が carrier から「代わりに降ろす」ことができる貨物の一覧。
    /// 条件：貨物が歩兵で、貨物の移動力 ≧ user の移動力（合意(g)）。
    /// 輸送隊が複数の貨物を持っていても、条件を満たすものだけが対象になる。
    /// </summary>
    public static List<Unit> GetProxyDropCargoes(Unit user, Unit carrier)
    {
        var result = new List<Unit>();
        foreach (Unit cargo in carrier.Carried)
        {
            if (cargo.Class != UnitClass.Infantry) continue;   // 降ろせるのは歩兵貨物のみ
            if (cargo.Move < user.Move) continue;              // 貨物.移動力 ≧ 自分.移動力（合意(g)）
            result.Add(cargo);
        }
        return result;
    }

    // ===== 補助 =====

    /// <summary>あと1体でも格納できるか（歩兵は容量0なので常に false）。</summary>
    public static bool CanCarryMore(Unit unit)
    {
        return unit != null && unit.Carried.Count < unit.CarryCapacity;
    }

    /// <summary>
    /// このユニットは「格納される側」になれるか（救出・乗り込みの共通条件。Phase 12）。
    /// 輸送隊は誰にも救出されない。貨物を持っているユニットも不可（入れ子救出の禁止）。
    /// </summary>
    public static bool CanBeStored(Unit unit)
    {
        return unit != null
            && unit.Class != UnitClass.Transporter
            && !unit.IsRescuing;
    }

    /// <summary>
    /// center（死亡マス）から近い順に、ユニットを配置できる空きマスを count 個まで集める（Phase 12・合意(b)）。
    /// 幅優先探索で盤面を近い順にたどる（探索の通過は占有マスも可。配置先は空きマスのみ）。
    /// 運び手が倒れた直後は center 自身も空いているので、先頭は必ず死亡マスになる。
    /// </summary>
    public static List<Vector2Int> FindReleaseCells(Vector2Int center, int count, GridManager grid)
    {
        var result = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int> { center };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(center);

        while (queue.Count > 0 && result.Count < count)
        {
            Vector2Int cell = queue.Dequeue();
            TileData tile = grid.GetTile(cell);
            if (tile == null) continue; // 盤外

            if (tile.IsWalkable && tile.Occupant == null)
                result.Add(cell);

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = cell + dir;
                if (visited.Add(next)) queue.Enqueue(next);
            }
        }
        return result;
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
