using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ユニットが移動できるマスの集合を計算する（経路探索）。
/// Phase 13 からマスごとに移動コストが違う（森=2 など）ので、
/// 「累計コストが小さいマスから順に調べる」ダイクストラ法を使います。
/// これで「森を突っ切るより平地の遠回りが安い」経路も正しく見つかります。
/// </summary>
public static class MovementCalculator
{
    // 上下左右の4方向（斜めは無し）
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>
    /// unit がこのマスに入るときの移動コスト。
    /// 地上は地形のコスト（騎乗ユニットは騎乗用コストの指定があればそちら。Phase 15）。
    /// 飛翔中は常に 1（地形の影響なし。Phase 14）。
    /// </summary>
    public static int GetMoveCost(Unit unit, TileData tile)
    {
        if (unit.IsFlying) return 1;
        return tile.MoveCostFor(unit.Class);
    }

    /// <summary>
    /// unit が移動できるマスの集合を返す（出発マスは含めない）。
    /// ・盤外、通行不可(IsWalkable=false)、敵ユニットがいるマスには入れない
    /// ・味方がいるマスは「通過」できるが、移動先（停止位置）には選べない
    /// ・累計移動コストが unit の移動力以下のマスだけ到達可能
    /// 飛翔中の特例（Phase 14・作者仕様 2026-07-12 改訂）：
    /// ・「飛行で入れるか」で判定（屋内壁だけ不可。城壁は入れる）。コストは常に1
    /// ・敵のマスをすり抜けられる。ただし「飛翔状態の敵」と「対空武器（弓・魔法）装備の敵」の
    ///   マスはすり抜けられない（空を見張っている相手は素通りできない）
    /// ・逆に、地上のユニットは「飛翔中の敵」のマスをすり抜けられる（頭上を飛んでいるだけなので）
    /// ・止まれるのは誰もいないマスだけ（着地衝突を仕組みで排除）
    /// </summary>
    public static HashSet<Vector2Int> GetReachableCells(GridManager grid, Unit unit)
    {
        return GetReachableCells(grid, unit, unit.Move, null);
    }

    /// <summary>
    /// 移動予算（moveBudget）を指定する版。救出後の再移動は
    /// 「移動力 − すでに使った移動コスト」を予算にして呼ぶ（Phase 11）。
    /// costOut を渡すと、到達できる各マスまでの最短移動コストを書き込んで返す
    /// （呼び出し側が「どこまで移動したらコストいくつ使ったか」を記録できる）。
    /// </summary>
    public static HashSet<Vector2Int> GetReachableCells(
        GridManager grid, Unit unit, int moveBudget, Dictionary<Vector2Int, int> costOut)
    {
        var reachable = new HashSet<Vector2Int>();
        var costSoFar = new Dictionary<Vector2Int, int>();
        var open = new List<Vector2Int>();      // まだ展開していないマス（探索候補）
        var done = new HashSet<Vector2Int>();   // 最短コストが確定したマス

        Vector2Int start = unit.GridPosition;
        costSoFar[start] = 0;
        open.Add(start);

        while (open.Count > 0)
        {
            // 候補の中から累計コストが最小のマスを取り出す（ダイクストラ法の核心）。
            // 15マス四方程度の盤面なので、リストの全走査で十分速い。
            int best = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (costSoFar[open[i]] < costSoFar[open[best]]) best = i;
            }
            Vector2Int current = open[best];
            open.RemoveAt(best);

            // 同じマスが複数回 open に入ることがあるので、確定済みなら飛ばす
            if (!done.Add(current)) continue;

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = current + dir;
                if (done.Contains(next)) continue;     // すでに最短が確定

                TileData tile = grid.GetTile(next);
                if (tile == null) continue;            // 盤外

                // 入れる地形か：地上は兵種ごとの通行可否（壁・城壁は全員不可、山を歩兵専用にする等。
                // Phase 15）、飛翔中は飛行可否（屋内壁だけ不可）
                bool canEnter = unit.IsFlying ? tile.CanFlyOver : tile.IsWalkableFor(unit.Class);
                if (!canEnter) continue;

                // ユニットがいるマスの扱い（味方のマスは常に通過できる。敵のマスだけ通せんぼがあり得る）：
                //   地上ユニットの移動 … 地上の敵は通せんぼ。飛翔中の敵の下はすり抜けられる
                //   飛翔中の移動      … 「飛翔状態の敵」と「対空武器（弓・魔法）装備の敵」は
                //                        すり抜け不可（作者仕様 2026-07-12）。それ以外の敵の上は通過できる
                bool occupied = tile.Occupant != null;
                if (occupied && tile.Occupant.Faction != unit.Faction)
                {
                    Unit blocker = tile.Occupant;
                    bool blocked = unit.IsFlying
                        ? (blocker.IsFlying
                           || (blocker.Weapon != null && blocker.Weapon.category == WeaponCategory.Ranged))
                        : !blocker.IsFlying;
                    if (blocked) continue;
                }

                int newCost = costSoFar[current] + GetMoveCost(unit, tile);
                if (newCost > moveBudget) continue; // 移動力（予算）オーバー

                // 未到達、またはより安い経路が見つかったら更新
                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost;
                    open.Add(next);
                    if (!occupied) reachable.Add(next); // 誰かがいるマスには止まれない
                }
            }
        }

        if (costOut != null)
        {
            costOut.Clear();
            foreach (Vector2Int cell in reachable)
                costOut[cell] = costSoFar[cell];
        }

        return reachable;
    }
}
