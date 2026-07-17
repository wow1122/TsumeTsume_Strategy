using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵ユニット1体の思考（行動評価型）。
///
/// Phase 16 から「考える → 動かす」の2段構え：
///   Evaluate（考える）… 取りうる行動（どのマスに立って・誰を攻撃するか）を全部試して
///                        点数を付け、一番良い行動を ActionPlan（行動予定）として返す。
///                        この段階では盤面をまだ変えない。
///   Execute（動かす）… ActionPlan どおりに移動・攻撃を実行する。
///   思考ログ          … 1体につき1行、何を考えたかを日本語で出す
///                        （TurnManager の Inspector でON/OFF）。
///
/// 攻撃する行動の点数（ScoreAttack）：
///   合計ダメージ = CombatSystem.PredictTotalDamage（本体＋挟撃−無効化まで込みの共有予測）
///   点数        = 合計ダメージ ＋（その合計で相手を倒せるなら 撃破ボーナス）
///   ・反撃は無いので「攻撃する＝ノーリスク」。だから迷わず最大火力・撃破・挟撃を狙う。
///   ・点数が同じなら (1)地形防御の高いマス (2)移動が少ないマス の順で選ぶ（Phase 15）。
///
/// 射程・攻撃可否は CombatRules に任せる（プレイヤー操作と同じ判定）。
/// これにより後衛武器の敵（弓兵など）は自然に
///   「静止したままなら武器上限まで撃つ／隣接されたら離れたマスへ移動して撃つ」
/// という動きになる（動くと射程が2マスちょうどに縮むため）。
///
/// 攻撃できないときの接近（Phase 16 で直線距離 → 経路ベースに刷新）：
///   「どこかのプレイヤーを攻撃できる立ち位置」を目標にした距離マップ
///   （MovementCalculator.GetDistanceMap＝本当の道のり）を使い、残りの道のりが
///   一番縮むマスへ移動する。これで壁の向こうの敵も門へ回り込むようになる。
///   道が無いときの保険は3段構え（経路 → 敵を無視した経路 → 従来の直線距離）。
///
/// 性格ゲート（Phase 17）：
///   待ち伏せ型（EnemyAIProfile.Ambush）の敵は、挑発されるまで接近しない。
///   攻撃探索は通す＝攻撃できる相手がいれば普通に攻撃し、それ自体が挑発になる。
///   被弾（HPが減っている）でも挑発される。挑発は永続（Unit.IsProvoked）で、
///   以後は突撃型と同じ動きになる。連鎖挑発（近くの味方の起動で自分も起動）は今回は無し。
/// </summary>
public static class EnemyAI
{
    // ===== 点数の定数 =====
    // 点数は「予測ダメージ（1点=1ダメージ）」が土台。ボーナスの大小関係がそのまま優先順位になる。
    // Phase 18 以降、ここに定数が増えていく予定（挟撃のお膳立て・集中攻撃など）。

    /// <summary>倒せる行動を最優先にするための大きなボーナス（どんなダメージ値より大きく）。</summary>
    private const int KillBonus = 1000;

    /// <summary>思考ログを出すか。TurnManager が敵フェイズ開始時に Inspector の設定を書き込む。</summary>
    public static bool LogEnabled = true;

    public static void TakeAction(Unit enemy, GridManager grid)
    {
        if (enemy == null || !enemy.IsAlive) return;

        List<Unit> players = UnitRegistry.GetUnits(Faction.Player);
        if (players.Count == 0) return;

        // 挑発の判定その1（Phase 17）: 被弾していたら起動する。
        // その2「今すぐ攻撃できる」は、Evaluate が攻撃プランを返したかどうかで下で判定する。
        if (!enemy.IsProvoked && enemy.CurrentHP < enemy.MaxHP)
            enemy.MarkProvoked();

        ActionPlan plan = Evaluate(enemy, grid, players);

        // 挑発の判定その2（Phase 17）: 攻撃すること自体が挑発（以後ずっと突撃型として振る舞う）
        if (plan.kind == ActionKind.Attack)
            enemy.MarkProvoked();

        Log(enemy, plan);           // 先に思考を宣言してから
        Execute(enemy, grid, plan); // 実行する（攻撃・挟撃などの戦闘ログはこの後に続く）
    }

    // ===== 考える（盤面はまだ変えない）=====

    private static ActionPlan Evaluate(Unit enemy, GridManager grid, List<Unit> players)
    {
        // 立てる候補マス：移動できるマス ＋ 現在地（動かない選択）
        var candidates = new List<Vector2Int>(MovementCalculator.GetReachableCells(grid, enemy));
        candidates.Add(enemy.GridPosition);

        // 1) 攻撃できるなら、一番点数の高い攻撃を選ぶ
        //    （待ち伏せ型でも攻撃はする。「攻撃できる相手がいる」こと自体が挑発になる。Phase 17）
        if (TryFindBestAttack(enemy, grid, players, candidates, out ActionPlan attack))
            return attack;

        // 2) 待ち伏せ型で未挑発なら、一切動かない（Phase 17）。
        //    移動ゼロなだけで、立ち位置による受動的な挟撃ガードはそのまま効く
        if (enemy.AIProfile == EnemyAIProfile.Ambush && !enemy.IsProvoked)
        {
            return new ActionPlan
            {
                kind = ActionKind.Stay,
                standCell = enemy.GridPosition,
                reason = "待ち伏せ中（挑発されるまで動かない）",
            };
        }

        // 3) 攻撃できない：目標（攻撃できる立ち位置）へ近づく
        return PlanApproach(enemy, grid, players, candidates);
    }

    /// <summary>
    /// 一番点数の高い「マスに立って攻撃する」行動を探す。攻撃できる相手がいなければ false。
    /// 点数・同点規則は Phase 15 までと同じ（点数 → 地形防御 → 移動の少なさ）。
    /// </summary>
    private static bool TryFindBestAttack(
        Unit enemy, GridManager grid, List<Unit> players, List<Vector2Int> candidates, out ActionPlan plan)
    {
        plan = default;

        Vector2Int bestCell = enemy.GridPosition;
        Unit bestTarget = null;
        int bestScore = int.MinValue;
        int bestDefense = int.MinValue;  // 同点なら地形防御の高いマスを選ぶ（Phase 15）
        int bestMoveDist = int.MaxValue; // それも同じなら移動が少ない方を選ぶ

        foreach (Vector2Int cell in candidates)
        {
            // そのマスに立ったとき「移動した扱い」か（後衛武器は移動すると射程が縮む）
            bool hasMoved = cell != enemy.GridPosition;

            foreach (Unit target in players)
            {
                if (!CombatRules.CanAttack(enemy, cell, target, hasMoved)) continue; // 射程外・武装無し

                int score = ScoreAttack(enemy, cell, target, grid);
                int defense = TileDefense(enemy, cell, grid);
                int moveDist = CombatRules.Manhattan(enemy.GridPosition, cell);

                // 点数が高い方を採用。同点なら (1)地形防御が高い (2)移動が少ない の順（Phase 15）。
                bool better = score > bestScore
                    || (score == bestScore && defense > bestDefense)
                    || (score == bestScore && defense == bestDefense && moveDist < bestMoveDist);
                if (better)
                {
                    bestScore = score;
                    bestDefense = defense;
                    bestMoveDist = moveDist;
                    bestCell = cell;
                    bestTarget = target;
                }
            }
        }

        if (bestTarget == null) return false;

        int damage = CombatSystem.PredictTotalDamage(enemy, bestCell, bestTarget, grid);
        plan = new ActionPlan
        {
            kind = ActionKind.Attack,
            standCell = bestCell,
            target = bestTarget,
            score = bestScore,
            reason = BuildAttackReason(enemy, bestCell, bestTarget, grid, damage),
        };
        return true;
    }

    /// <summary>
    /// 攻撃できないときの接近先を決める。保険を含めた3段構え（Phase 16）：
    ///   第1段: 経路ベース（プレイヤーのマスは通れない前提の、本当の道のり）
    ///   第2段: 道がプレイヤーに塞がれて全滅なら、通せんぼを無視した道のりで測り直す
    ///           （門に栓をされても門前へ整列し、射程に入れば塞いだ相手を攻撃する）
    ///   第3段: それでも道が無い（壁で完全に仕切られている・交戦できる相手がいない）なら
    ///           従来どおり直線距離で近づく（Phase 15 までの挙動。凍結しないための保険）
    /// </summary>
    private static ActionPlan PlanApproach(
        Unit enemy, GridManager grid, List<Unit> players, List<Vector2Int> candidates)
    {
        // 目標マス＝「どこかのプレイヤーを攻撃できる立ち位置」全部。
        // hasMoved:true で見積もる（そこへ着く頃には移動しているはずなので、
        // 後衛武器は最小射程ちょうどの控えめな目標にしておく）
        var goals = new List<Vector2Int>();
        foreach (Unit p in players)
            goals.AddRange(CombatRules.GetAttackFromCells(enemy, p, grid, hasMoved: true));

        if (goals.Count > 0)
        {
            // 第1段: 本当の道のり
            var map = MovementCalculator.GetDistanceMap(grid, enemy, goals, ignoreEnemyUnits: false);
            if (TryPickByDistanceMap(enemy, grid, candidates, map, "経路", out ActionPlan plan))
                return plan;

            // 第2段: 敵（プレイヤー）の通せんぼを無視して測り直し
            map = MovementCalculator.GetDistanceMap(grid, enemy, goals, ignoreEnemyUnits: true);
            if (TryPickByDistanceMap(enemy, grid, candidates, map, "経路(敵を無視)", out plan))
                return plan;
        }

        // 第3段: 従来の直線距離
        return PlanApproachByManhattan(enemy, grid, players, candidates);
    }

    /// <summary>
    /// 距離マップを使って、候補マスから「残りの道のりが一番短いマス」を選ぶ。
    /// どの候補からも目標へ届かない（マップに載っていない）なら false（次の段へ）。
    /// 同点規則は従来の接近と同じ（地形防御 → 移動の少なさ。現在地は移動0なので
    /// 「動いても良くならないなら動かない」が自然に守られる）。
    /// </summary>
    private static bool TryPickByDistanceMap(
        Unit enemy, GridManager grid, List<Vector2Int> candidates,
        Dictionary<Vector2Int, int> map, string tierName, out ActionPlan plan)
    {
        plan = default;

        bool found = false;
        Vector2Int bestCell = enemy.GridPosition;
        int bestDist = int.MaxValue;
        int bestDefense = int.MinValue;
        int bestMoveDist = int.MaxValue;

        foreach (Vector2Int cell in candidates) // 候補には現在地も含まれている
        {
            if (!map.TryGetValue(cell, out int dist)) continue; // そのマスからは目標へ届かない

            found = true;
            int defense = TileDefense(enemy, cell, grid);
            int moveDist = CombatRules.Manhattan(enemy.GridPosition, cell);

            bool better = dist < bestDist
                || (dist == bestDist && defense > bestDefense)
                || (dist == bestDist && defense == bestDefense && moveDist < bestMoveDist);
            if (better)
            {
                bestDist = dist;
                bestDefense = defense;
                bestMoveDist = moveDist;
                bestCell = cell;
            }
        }

        if (!found) return false;

        bool stay = bestCell == enemy.GridPosition;
        plan = new ActionPlan
        {
            kind = stay ? ActionKind.Stay : ActionKind.Approach,
            standCell = bestCell,
            reason = stay
                ? $"攻撃できる相手なし → その場で待機（{tierName}・目標まで残り{bestDist}）"
                : $"攻撃できる相手なし → ({bestCell.x},{bestCell.y})へ接近（{tierName}・目標まで残り{bestDist}）",
        };
        return true;
    }

    /// <summary>第3段の保険：一番近いプレイヤーへの直線距離で近づく（Phase 15 までの挙動）。</summary>
    private static ActionPlan PlanApproachByManhattan(
        Unit enemy, GridManager grid, List<Unit> players, List<Vector2Int> candidates)
    {
        Vector2Int approachCell = enemy.GridPosition;
        int bestDist = NearestPlayerDistance(enemy.GridPosition, players);
        int bestDefense = TileDefense(enemy, enemy.GridPosition, grid);

        foreach (Vector2Int cell in candidates)
        {
            int d = NearestPlayerDistance(cell, players);
            int defense = TileDefense(enemy, cell, grid);

            // より近づけるマスを選ぶ。同じ近さなら地形防御の高いマスで待つ（Phase 15）
            if (d < bestDist || (d == bestDist && defense > bestDefense))
            {
                bestDist = d;
                bestDefense = defense;
                approachCell = cell;
            }
        }

        bool stay = approachCell == enemy.GridPosition;
        return new ActionPlan
        {
            kind = stay ? ActionKind.Stay : ActionKind.Approach,
            standCell = approachCell,
            reason = stay
                ? "攻撃できる相手なし → その場で待機（直線距離・道なし）"
                : $"攻撃できる相手なし → ({approachCell.x},{approachCell.y})へ接近（直線距離・道なし）",
        };
    }

    // ===== 動かす（Evaluate の結果を実行する）=====

    private static void Execute(Unit enemy, GridManager grid, ActionPlan plan)
    {
        if (plan.standCell != enemy.GridPosition)
            enemy.MoveTo(grid, plan.standCell);

        if (plan.kind == ActionKind.Attack && plan.target != null)
            CombatSystem.ResolveAttack(enemy, plan.target, grid);
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

    // ===== 思考ログ =====

    private static void Log(Unit enemy, ActionPlan plan)
    {
        if (!LogEnabled) return;
        Debug.Log($"AI思考: {enemy.Data.unitName}({enemy.GridPosition.x},{enemy.GridPosition.y}) {plan.reason}");
    }

    /// <summary>攻撃プランの説明文（ログ用）。挟撃・撃破の見込みも添える。</summary>
    private static string BuildAttackReason(Unit enemy, Vector2Int cell, Unit target, GridManager grid, int damage)
    {
        string move = cell == enemy.GridPosition ? "その場から" : $"({cell.x},{cell.y})へ移動して";

        string note = "";
        Unit ally = CombatRules.FindPincerAlly(cell, enemy, target, grid);
        if (ally != null && !CombatRules.IsPincerNegated(target, grid)) note += "・挟撃込み";
        if (damage >= target.CurrentHP) note += "・撃破";

        return $"{move} {target.Data.unitName} を攻撃（予測{damage}{note}）";
    }

    // ===== 補助 =====

    /// <summary>そのマスに立ったとき受ける地形防御（飛翔中は地形の恩恵が無いので常に0）。</summary>
    private static int TileDefense(Unit unit, Vector2Int cell, GridManager grid)
    {
        if (unit.IsFlying) return 0;
        TileData tile = grid.GetTile(cell);
        return tile != null ? tile.DefenseBonus : 0;
    }

    private static int NearestPlayerDistance(Vector2Int cell, List<Unit> players)
    {
        int min = int.MaxValue;
        foreach (Unit p in players)
            min = Mathf.Min(min, CombatRules.Manhattan(cell, p.GridPosition));
        return min;
    }
}
