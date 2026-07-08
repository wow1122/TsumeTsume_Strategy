using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵ユニット1体の思考（行動評価型）。
///
/// 基本の考え方：
///   「取りうる行動（どのマスに立って・誰を攻撃するか）」を全部試し、
///   それぞれに点数を付けて、一番点数の高い行動を実行する。
///
/// 攻撃する行動の点数（ScoreAttack）：
///   合計ダメージ = 自分の攻撃ダメージ ＋（挟撃が成立するなら）味方の追撃ダメージ
///   点数        = 合計ダメージ ＋（その合計で相手を倒せるなら 撃破ボーナス）
///   ・反撃は無いので「攻撃する＝ノーリスク」。だから迷わず最大火力・撃破・挟撃を狙う。
///   ・点数が同じなら、移動が少ない（あまり前に出ない）マスを選ぶ。
///
/// 射程に入る相手が一人もいなければ、一番近い味方へ近づくだけ移動する。
/// </summary>
public static class EnemyAI
{
    /// <summary>倒せる行動を最優先にするための大きなボーナス（どんなダメージ値より大きく）。</summary>
    private const int KillBonus = 1000;

    public static void TakeAction(Unit enemy, GridManager grid)
    {
        if (enemy == null || !enemy.IsAlive) return;

        List<Unit> players = UnitRegistry.GetUnits(Faction.Player);
        if (players.Count == 0) return;

        // 立てる候補マス：移動できるマス ＋ 現在地（動かない選択）
        var candidates = new List<Vector2Int>(MovementCalculator.GetReachableCells(grid, enemy));
        candidates.Add(enemy.GridPosition);

        // ===== 1) 一番良い攻撃を探す =====
        Vector2Int bestCell = enemy.GridPosition;
        Unit bestTarget = null;
        int bestScore = int.MinValue;
        int bestMoveDist = int.MaxValue; // 同点なら移動が少ない方を選ぶための比較用

        WeaponData weapon = enemy.Weapon;
        if (weapon != null)
        {
            foreach (Vector2Int cell in candidates)
            {
                foreach (Unit target in players)
                {
                    int range = Manhattan(cell, target.GridPosition);
                    if (range < weapon.minRange || range > weapon.maxRange) continue; // 射程外

                    int score = ScoreAttack(enemy, cell, target, grid);
                    int moveDist = Manhattan(enemy.GridPosition, cell);

                    // 点数が高い方を採用。同点なら移動が少ない方を採用。
                    if (score > bestScore || (score == bestScore && moveDist < bestMoveDist))
                    {
                        bestScore = score;
                        bestMoveDist = moveDist;
                        bestCell = cell;
                        bestTarget = target;
                    }
                }
            }
        }

        if (bestTarget != null)
        {
            if (bestCell != enemy.GridPosition)
                enemy.MoveTo(grid, bestCell);
            CombatSystem.ResolveAttack(enemy, bestTarget, grid);
            return;
        }

        // ===== 2) 攻撃できない：一番近い味方へ近づく =====
        Vector2Int approachCell = enemy.GridPosition;
        int bestDist = NearestPlayerDistance(enemy.GridPosition, players);

        foreach (Vector2Int cell in candidates)
        {
            int d = NearestPlayerDistance(cell, players);
            if (d < bestDist)
            {
                bestDist = d;
                approachCell = cell;
            }
        }

        if (approachCell != enemy.GridPosition)
            enemy.MoveTo(grid, approachCell);
    }

    // ===== 行動の点数付け =====

    /// <summary>
    /// 「cell に立って target を攻撃する」行動の点数。
    /// 合計ダメージ（自分＋挟撃の追撃）に、倒せるなら撃破ボーナスを足す。
    /// </summary>
    private static int ScoreAttack(Unit enemy, Vector2Int cell, Unit target, GridManager grid)
    {
        int damage = DamageCalculator.Calculate(enemy, target, grid);
        damage += PredictPincerDamage(enemy, cell, target, grid);

        int score = damage;
        if (damage >= target.CurrentHP) score += KillBonus; // この一手で倒せるなら最優先
        return score;
    }

    /// <summary>
    /// cell から target を近接攻撃したとき、挟撃で増える追撃ダメージを予測する。
    /// 判定は CombatSystem.TryPincer と同じ：
    ///   ・cell が target の上下左右に隣接していること（近接攻撃）
    ///   ・target を挟んだ反対側のマスに、自分以外の味方（敵陣営）がいること
    /// 成立しなければ 0 を返す。
    /// </summary>
    private static int PredictPincerDamage(Unit enemy, Vector2Int cell, Unit target, GridManager grid)
    {
        Vector2Int diff = target.GridPosition - cell;
        if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) != 1) return 0; // 隣接でない＝遠距離なので挟撃なし

        Vector2Int oppositeCell = target.GridPosition + diff;
        TileData tile = grid.GetTile(oppositeCell);
        Unit ally = tile != null ? tile.Occupant : null;

        if (ally == null || ally == enemy || ally.Faction != enemy.Faction) return 0;

        return DamageCalculator.Calculate(ally, target, grid);
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
}
