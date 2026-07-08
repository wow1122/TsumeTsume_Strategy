using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ユニットが移動できるマスの集合を計算する（経路探索）。
/// 幅優先探索（BFS）で、移動力の範囲内に到達できるマスを調べます。
/// 移動コストは各マスの MoveCost を使うので、後で地形(森=2など)を入れても動きます。
/// </summary>
public static class MovementCalculator
{
    // 上下左右の4方向（斜めは無し）
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    /// <summary>
    /// unit が移動できるマスの集合を返す（出発マスは含めない）。
    /// ・盤外、通行不可(IsWalkable=false)、敵ユニットがいるマスには入れない
    /// ・味方がいるマスは「通過」できるが、移動先（停止位置）には選べない
    /// ・累計移動コストが unit の移動力以下のマスだけ到達可能
    /// </summary>
    public static HashSet<Vector2Int> GetReachableCells(GridManager grid, Unit unit)
    {
        var reachable = new HashSet<Vector2Int>();
        var costSoFar = new Dictionary<Vector2Int, int>();
        var frontier = new Queue<Vector2Int>();

        Vector2Int start = unit.GridPosition;
        costSoFar[start] = 0;
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            Vector2Int current = frontier.Dequeue();

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int next = current + dir;

                TileData tile = grid.GetTile(next);
                if (tile == null) continue;            // 盤外
                if (!tile.IsWalkable) continue;        // 壁など

                // 敵ユニットのマスは侵入も通過も不可。味方のマスは「通過だけ」できる。
                bool occupiedByAlly = false;
                if (tile.Occupant != null)
                {
                    if (tile.Occupant.Faction != unit.Faction) continue; // 敵は通せんぼ
                    occupiedByAlly = true;
                }

                int newCost = costSoFar[current] + tile.MoveCost;
                if (newCost > unit.Move) continue; // 移動力オーバー

                // 未到達、またはより安い経路が見つかったら更新
                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost;
                    frontier.Enqueue(next);
                    if (!occupiedByAlly) reachable.Add(next); // 味方マスには止まれない
                }
            }
        }

        return reachable;
    }
}
