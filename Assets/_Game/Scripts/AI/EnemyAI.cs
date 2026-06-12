using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵ユニット1体の思考（最小実装）。
///  ・移動できる範囲の中に「そこに立てば味方を攻撃できるマス」があれば、
///    一番近いそのマスへ移動して攻撃する
///  ・無ければ、最も近い味方に少しでも近づくよう移動する
/// </summary>
public static class EnemyAI
{
    public static void TakeAction(Unit enemy, GridManager grid)
    {
        if (enemy == null || !enemy.IsAlive) return;

        List<Unit> players = GetUnits(Faction.Player);
        if (players.Count == 0) return;

        WeaponData weapon = enemy.Data.weapon;

        // 立てる候補マス：移動可能マス ＋ 現在地（動かない選択）
        var candidates = new List<Vector2Int>(MovementCalculator.GetReachableCells(grid, enemy));
        candidates.Add(enemy.GridPosition);

        // 1) 攻撃できるマスを探す（そこに立つと味方が武器射程に入る）
        //    移動距離が最も小さいマスを選ぶ
        Vector2Int bestAttackCell = enemy.GridPosition;
        Unit bestTarget = null;
        int bestMoveDist = int.MaxValue;

        if (weapon != null)
        {
            foreach (Vector2Int c in candidates)
            {
                foreach (Unit p in players)
                {
                    int range = Manhattan(c, p.GridPosition);
                    if (range < weapon.minRange || range > weapon.maxRange) continue;

                    int moveDist = Manhattan(enemy.GridPosition, c);
                    if (moveDist < bestMoveDist)
                    {
                        bestMoveDist = moveDist;
                        bestAttackCell = c;
                        bestTarget = p;
                    }
                }
            }
        }

        if (bestTarget != null)
        {
            if (bestAttackCell != enemy.GridPosition)
                enemy.MoveTo(grid, bestAttackCell);
            CombatSystem.ResolveAttack(enemy, bestTarget, grid);
            return;
        }

        // 2) 攻撃できない：最も近い味方に近づくマスへ移動
        Vector2Int bestMoveCell = enemy.GridPosition;
        int bestDist = NearestPlayerDistance(enemy.GridPosition, players);

        foreach (Vector2Int c in candidates)
        {
            int d = NearestPlayerDistance(c, players);
            if (d < bestDist)
            {
                bestDist = d;
                bestMoveCell = c;
            }
        }

        if (bestMoveCell != enemy.GridPosition)
            enemy.MoveTo(grid, bestMoveCell);
    }

    // ===== 補助 =====

    private static int NearestPlayerDistance(Vector2Int cell, List<Unit> players)
    {
        int min = int.MaxValue;
        foreach (Unit p in players)
            min = Mathf.Min(min, Manhattan(cell, p.GridPosition));
        return min;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static List<Unit> GetUnits(Faction faction)
    {
        var result = new List<Unit>();
        foreach (Unit u in Object.FindObjectsByType<Unit>(FindObjectsSortMode.None))
            if (u.Faction == faction) result.Add(u);
        return result;
    }
}
