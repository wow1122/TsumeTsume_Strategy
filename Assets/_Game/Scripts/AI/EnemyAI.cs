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
///   合計ダメージ = CombatSystem.PredictTotalDamage（本体＋挟撃−無効化まで込みの共有予測）
///   点数        = 合計ダメージ ＋（その合計で相手を倒せるなら 撃破ボーナス）
///   ・反撃は無いので「攻撃する＝ノーリスク」。だから迷わず最大火力・撃破・挟撃を狙う。
///   ・点数が同じなら、移動が少ない（あまり前に出ない）マスを選ぶ。
///
/// 射程・攻撃可否は CombatRules に任せる（プレイヤー操作と同じ判定）。
/// これにより後衛武器の敵（弓兵など）は自然に
///   「静止したままなら武器上限まで撃つ／隣接されたら離れたマスへ移動して撃つ」
/// という動きになる（動くと射程が2マスちょうどに縮むため）。
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

        foreach (Vector2Int cell in candidates)
        {
            // そのマスに立ったとき「移動した扱い」か（後衛武器は移動すると射程が縮む）
            bool hasMoved = cell != enemy.GridPosition;

            foreach (Unit target in players)
            {
                if (!CombatRules.CanAttack(enemy, cell, target, hasMoved)) continue; // 射程外・武装無し

                int score = ScoreAttack(enemy, cell, target, grid);
                int moveDist = CombatRules.Manhattan(enemy.GridPosition, cell);

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
    /// 合計ダメージの予測は CombatSystem.PredictTotalDamage に一元化されている
    /// （挟撃の成立・ガードによる無効化も織り込み済み。実際の戦闘結果と必ず一致する）。
    /// </summary>
    private static int ScoreAttack(Unit enemy, Vector2Int cell, Unit target, GridManager grid)
    {
        int damage = CombatSystem.PredictTotalDamage(enemy, cell, target, grid);

        int score = damage;
        if (damage >= target.CurrentHP) score += KillBonus; // この一手で倒せるなら最優先
        return score;
    }

    // ===== 補助 =====

    private static int NearestPlayerDistance(Vector2Int cell, List<Unit> players)
    {
        int min = int.MaxValue;
        foreach (Unit p in players)
            min = Mathf.Min(min, CombatRules.Manhattan(cell, p.GridPosition));
        return min;
    }
}
