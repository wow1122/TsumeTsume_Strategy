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
///
/// 挟撃の組み立て（Phase 18）：
///   挟撃を「完成させる」側は元から最適に動く（PredictTotalDamage が挟撃込みで評価するので、
///   先に立っている味方の反対側マスを自然に選ぶ）。このフェーズで足したのはその前段の
///   「お膳立て」— あとで動く未行動・前衛の味方が反対側マスに立てるなら、そのマスを
///   わずかに優先する（PincerSetupBonus +2。ほぼ同点のマス選びでだけ効く）。
///   もう1つは「ガード役潰し」— 一撃で倒せる相手が複数いるとき、誰かのガード役をしている
///   相手を優先して倒す（GuardBreakKillBonus +50。撃破同士の並べ替え専用）。
///
/// ガードの活用＝守り配置（Phase 19）：
///   攻撃できる相手がいない「前衛武器の歩兵」は、次のプレイヤーフェイズに挟撃されそうな
///   味方（ThreatMap.FindPincerThreatenedAllies）の隣に立って挟撃を無効化する。
///   攻撃探索が先＝攻撃優先（守り位置を離れて攻撃に行くこともある。作者合意）。
///   防げるダメージが1以上なら接近よりガード。マスは 防止量→地形防御→移動の少なさ で選ぶ。
///   未挑発の待ち伏せ型はガード移動もしない（性格ゲートが先。受動的なガードは従来どおり効く）。
///
/// 飛翔の活用＝離陸・巡航・着陸（Phase 20）：
///   敵の飛行兵が自分の判断で飛翔コマンドを使う。プレイヤー側の飛翔の仕様・挙動は無変更。
///   仮定の評価は probe（お試し）方式 — 評価のあいだだけ実際に StartFlight / CancelFlight で
///   状態を切り替えて測り、try/finally で必ず元に戻す（作者合意。共有コードは変えない）。
///   ・離陸（地上で攻撃できる相手がいないとき）… 次のいずれかで飛ぶ：
///       (1) 離陸すれば今すぐ空対空攻撃できる（飛翔は行動を消費しないので離陸→攻撃が1行動）
///       (2) 空中でしか攻撃できない相手（飛翔中のプレイヤー）がいる
///       (3) 地上では目標へ到達できないが空中なら可能
///       (4) 空路のほうが速い：空路ターン数+1 ≤ 陸路ターン数（+1は着陸行動の分。同数なら空路優先）
///   ・巡航（空中で攻撃できる相手がいないとき）… 対空射程マス（ThreatMap.GetAirThreatCells）を
///     「1マス分の遠回りまでなら」避けつつ、目標への空路が一番縮むマスへ飛ぶ
///   ・着陸 … 残り飛翔2ターン以上で「着陸マスから次のターンに攻撃位置へ届く」なら
///     今すぐ着陸して行動終了（自動着地を待つより1ターン早く攻撃に移れる）。
///     残り1ターンなら空中のまま待つのが得（プレイヤーフェイズを近接無敵で過ごし、
///     次フェイズ開始にタダで自動着地→そのフェイズは普通に行動できる）
///   ・着陸後に攻撃する立ち位置は必ず「地上の状態」で計算する — 飛翔中は CanEngage が
///     地上の敵に false を返すため（本フェーズ唯一の非自明点。probe その1参照）
/// </summary>
public static class EnemyAI
{
    // ===== 点数の定数 =====
    // 点数は「予測ダメージ（1点=1ダメージ）」が土台。ボーナスの大小関係がそのまま優先順位になる。
    // 大きい順: KillBonus 1000 ＞（Phase 21 予定: 集中攻撃 200）＞ GuardBreakKillBonus 50
    //          ＞ PincerSetupBonus 2。この階層を崩さないように値を選んでいる。

    /// <summary>倒せる行動を最優先にするための大きなボーナス（どんなダメージ値より大きく）。</summary>
    private const int KillBonus = 1000;

    /// <summary>
    /// ガード役潰し（Phase 18）：一撃で倒せる相手が複数いるとき、誰かの挟撃ガード役をしている
    /// 相手を優先して倒すための加点。ガードはHPが残っている限り有効なので、倒し切れない攻撃に
    /// 付けても意味が無い＝KillBonus が付くときにだけ加える（撃破候補同士の並べ替え専用）。
    /// </summary>
    private const int GuardBreakKillBonus = 50;

    /// <summary>
    /// 挟撃のお膳立て（Phase 18）：あとで動く前衛の味方が反対側マスに立てる攻撃位置を
    /// 選ぶための加点。お膳立ては投機的（その味方はもっと良い獲物を選ぶかもしれない）なので、
    /// 実ダメージ差1点までしか覆さない控えめな値にしてある（撃破や火力は犠牲にしない）。
    /// </summary>
    private const int PincerSetupBonus = 2;

    /// <summary>
    /// 対空回避（Phase 20）：空中巡航の停止マス選びで、対空射程マス（次のプレイヤーフェイズに
    /// 空中の自分を撃たれうるマス）に付ける減点。距離1マス相当＝「1マス分の遠回りまでなら
    /// 撃たれないマスを選ぶ」控えめな値（回避のために大きく後退はしない）。
    /// </summary>
    private const int AirThreatPenalty = 1;

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

        // 挑発の判定その2（Phase 17）: 攻撃すること自体が挑発（以後ずっと突撃型として振る舞う）。
        // 離陸からの空対空攻撃（TakeOff で target あり。Phase 20）も攻撃として数える
        if (plan.kind == ActionKind.Attack || (plan.kind == ActionKind.TakeOff && plan.target != null))
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

        // 2) 待ち伏せ型で未挑発なら、一切動かない（Phase 17。ガード移動もしない＝Phase 19 作者合意）。
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

        // 3) ガード（Phase 19）：攻撃できる相手がいない前衛歩兵は、
        //    挟撃されそうな味方を守りに行く（防げるダメージが正なら接近より優先。作者合意）
        if (TryPlanGuard(enemy, grid, candidates, out ActionPlan guardPlan))
            return guardPlan;

        // 4) 飛翔の活用（Phase 20）：
        //    空中にいる … 着陸か巡航かをここで決める（地上向けの接近は空中では機能しない）
        //    地上の飛行兵 … 離陸したほうが得なら飛ぶ。そうでなければ普通に接近へ
        if (enemy.IsFlying)
            return PlanFlying(enemy, grid, players, candidates);
        if (TryPlanTakeOff(enemy, grid, players, out ActionPlan flightPlan))
            return flightPlan;

        // 5) 攻撃できない・守る相手もいない：目標（攻撃できる立ち位置）へ近づく
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

        // 挟撃のお膳立て用：後続（未行動・前衛）の味方と、その到達マス集合（Phase 18）。
        // この TakeAction の間は盤面が変わらないので、最初に1回だけ計算して使い回す
        List<PincerHelper> helpers = BuildPincerHelpers(enemy, grid);

        Vector2Int bestCell = enemy.GridPosition;
        Unit bestTarget = null;
        int bestScore = int.MinValue;
        int bestDefense = int.MinValue;  // 同点なら地形防御の高いマスを選ぶ（Phase 15）
        int bestMoveDist = int.MaxValue; // それも同じなら移動が少ない方を選ぶ
        bool bestPincerSetup = false;    // ログ用（Phase 18）
        bool bestGuardBreak = false;

        foreach (Vector2Int cell in candidates)
        {
            // そのマスに立ったとき「移動した扱い」か（後衛武器は移動すると射程が縮む）
            bool hasMoved = cell != enemy.GridPosition;

            foreach (Unit target in players)
            {
                if (!CombatRules.CanAttack(enemy, cell, target, hasMoved)) continue; // 射程外・武装無し

                int score = ScoreAttack(enemy, cell, target, grid, helpers,
                    out bool pincerSetup, out bool guardBreak);
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
                    bestPincerSetup = pincerSetup;
                    bestGuardBreak = guardBreak;
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
            reason = BuildAttackReason(enemy, bestCell, bestTarget, grid, damage,
                bestPincerSetup, bestGuardBreak),
        };
        return true;
    }

    // ===== ガードの活用＝守り配置（Phase 19）=====

    /// <summary>
    /// 挟撃されそうな味方の隣に立って、挟撃を無効化する行動を探す（Phase 19）。
    /// ここへ来るのは攻撃探索が空振りしたときだけ＝攻撃優先（作者合意）。
    /// ガード役になれるのは「前衛武器の歩兵」のみ（WouldGuardAt と同じ条件）。
    /// 防げるダメージが1以上のマスがあれば接近よりガードを選ぶ。マスの選び方は
    ///   防止量の合計 → 地形防御 → 移動の少なさ
    /// の順（1マスで複数の味方を同時に守れたら防止量は合算）。
    /// 候補には現在地も含まれるので「その場で守り続ける」も自然に出る。
    /// ※ガード役自身が挟撃される可能性までは v1 では読まない（思考ログで分かる。作者合意）
    /// </summary>
    private static bool TryPlanGuard(
        Unit enemy, GridManager grid, List<Vector2Int> candidates, out ActionPlan plan)
    {
        plan = default;

        // ガード役の資格チェック（歩兵・前衛武器・地上）。資格が無ければ計算せず素通り
        if (enemy.IsFlying) return false;
        if (enemy.Class != UnitClass.Infantry) return false;
        if (!CombatRules.IsPincerCapable(enemy)) return false;

        // 「挟撃されそうな味方 → 防げる最大ダメージ」を取得（自分は盤面に居ない扱いで判定）
        Dictionary<Unit, int> threats = ThreatMap.FindPincerThreatenedAllies(grid, enemy);
        if (threats.Count == 0) return false;

        Vector2Int bestCell = enemy.GridPosition;
        int bestValue = 0; // 防止量が1以上のマスだけ採用する（0なら守る意味が無い）
        int bestDefense = int.MinValue;
        int bestMoveDist = int.MaxValue;
        List<Unit> bestProtected = null;

        foreach (Vector2Int cell in candidates)
        {
            // このマスに立ったとき守れる味方と、防げるダメージの合計
            int value = 0;
            List<Unit> protectedAllies = null;
            foreach (KeyValuePair<Unit, int> threat in threats)
            {
                if (!CombatRules.WouldGuardAt(enemy, cell, threat.Key)) continue;

                value += threat.Value;
                if (protectedAllies == null) protectedAllies = new List<Unit>();
                protectedAllies.Add(threat.Key);
            }
            if (value <= 0) continue;

            int defense = TileDefense(enemy, cell, grid);
            int moveDist = CombatRules.Manhattan(enemy.GridPosition, cell);

            bool better = value > bestValue
                || (value == bestValue && defense > bestDefense)
                || (value == bestValue && defense == bestDefense && moveDist < bestMoveDist);
            if (better)
            {
                bestValue = value;
                bestDefense = defense;
                bestMoveDist = moveDist;
                bestCell = cell;
                bestProtected = protectedAllies;
            }
        }

        if (bestProtected == null) return false;

        var names = new List<string>();
        foreach (Unit ally in bestProtected) names.Add(ally.Data.unitName);
        string who = string.Join("、", names);

        bool stay = bestCell == enemy.GridPosition;
        plan = new ActionPlan
        {
            kind = ActionKind.Guard,
            standCell = bestCell,
            score = bestValue,
            reason = stay
                ? $"その場で {who} への挟撃を防ぎ続ける（ガード・最大{bestValue}ダメージ防止）"
                : $"({bestCell.x},{bestCell.y})へ移動して {who} への挟撃を無効化（ガード・最大{bestValue}ダメージ防止）",
        };
        return true;
    }

    // ===== 飛翔の活用＝離陸・巡航・着陸（Phase 20）=====

    /// <summary>
    /// 着陸後の攻撃を見積もるための「地上の情報」ひとまとめ。
    /// goals = 地上の状態で攻撃できる立ち位置（全プレイヤー分）、
    /// map   = 各マスから最寄りの goals までの地上の道のり（プレイヤーの通せんぼ込み）。
    /// </summary>
    private struct GroundInfo
    {
        public List<Vector2Int> goals;
        public Dictionary<Vector2Int, int> map;
    }

    /// <summary>
    /// 【probe その1：一時着地】地上の目標（攻撃できる立ち位置）と道のりを計算する。
    /// 飛翔中は CanEngage が地上の敵に false を返すため、「着陸後に攻撃する立ち位置」は
    /// 必ず地上の状態で計算しなければならない（Phase 20 唯一の非自明点）。
    /// そこで飛翔中なら CancelFlight で一時的に着地して計算し、finally で必ず
    /// 元の飛翔状態（残りターン数まで）に戻す。地上のユニットが呼んだ場合はそのまま計算する。
    /// </summary>
    private static GroundInfo ComputeGroundInfo(Unit enemy, GridManager grid, List<Unit> players)
    {
        bool wasFlying = enemy.IsFlying;
        int savedTurns = enemy.FlightTurnsLeft;
        if (wasFlying) enemy.CancelFlight(); // お試し着地（評価専用。盤面には何も起きない）
        try
        {
            var info = new GroundInfo { goals = new List<Vector2Int>() };
            foreach (Unit p in players)
                info.goals.AddRange(CombatRules.GetAttackFromCells(enemy, p, grid, hasMoved: true));

            info.map = MovementCalculator.GetDistanceMap(grid, enemy, info.goals, ignoreEnemyUnits: false);
            return info;
        }
        finally
        {
            if (wasFlying) enemy.StartFlight(savedTurns); // 必ず元の飛翔状態に戻す
        }
    }

    /// <summary>
    /// 飛翔中の敵の行動を決める（攻撃探索が空振りしたとき）。
    ///   1) 着陸判断 … 残り飛翔2ターン以上で「着陸マスから次のターンに攻撃位置へ届く」なら
    ///      今すぐ着陸して行動終了。着陸コマンドは行動を消費するが、次のフェイズには攻撃できる
    ///      ＝自動着地（行動を消費しないが着地がフェイズ開始になる）を待つより1ターン早い。
    ///      残り1ターンなら着陸しない：空中のままプレイヤーフェイズを近接無敵で過ごし、
    ///      次フェイズ開始にタダで自動着地して、そのフェイズは普通に行動するのが得。
    ///   2) 空中巡航 … 目標への空路が一番縮むマスへ（対空射程マスは1マス分の遠回りまで回避）
    ///   3) 空路でも届かないときの保険 … 従来の直線距離接近
    /// </summary>
    private static ActionPlan PlanFlying(Unit enemy, GridManager grid, List<Unit> players, List<Vector2Int> candidates)
    {
        GroundInfo ground = ComputeGroundInfo(enemy, grid, players); // probe その1（一時着地）

        // 1) 着陸判断
        if (enemy.FlightTurnsLeft >= 2 && TryPlanLanding(enemy, grid, candidates, ground, out ActionPlan landing))
            return landing;

        // 2) 空中巡航
        if (TryPickCruiseCell(enemy, grid, players, candidates, ground,
                out Vector2Int cell, out int dist, out _, out string threatNote))
        {
            bool stay = cell == enemy.GridPosition;
            string turnsNote = enemy.FlightTurnsLeft == 1
                ? "・残り1ターンは空中待機が得（次フェイズ開始に自動着地→即行動）"
                : $"・残り飛翔{enemy.FlightTurnsLeft}ターン";

            return new ActionPlan
            {
                kind = stay ? ActionKind.Stay : ActionKind.Approach,
                standCell = cell,
                reason = stay
                    ? $"空中で待機（目標まで残り{dist}{threatNote}{turnsNote}）"
                    : $"({cell.x},{cell.y})へ巡航（空路・目標まで残り{dist}{threatNote}{turnsNote}）",
            };
        }

        // 3) 保険：空路でも目標へ届かない（交戦できる相手がいない等）
        return PlanApproachByManhattan(enemy, grid, players, candidates);
    }

    /// <summary>
    /// 「今すぐ着陸して行動終了」する着陸マスを探す（Phase 20）。
    /// 条件：飛行移動で立てるマス（＝空きマス。現在地も可）のうち、
    ///   ・地上の兵種として歩いて立てる地形（城壁の上などへ手動着陸はしない。
    ///     飛翔切れの自動着地で城壁に乗るのは従来どおり合法）
    ///   ・そこから次のターンに攻撃位置へ届く（地上の道のり ≤ 移動力）
    /// マスの選び方は 目標までの地上の道のり → 地形防御（着陸後は地上なので効く）→ 移動の少なさ。
    /// </summary>
    private static bool TryPlanLanding(
        Unit enemy, GridManager grid, List<Vector2Int> candidates, GroundInfo ground, out ActionPlan plan)
    {
        plan = default;

        bool found = false;
        Vector2Int bestCell = enemy.GridPosition;
        int bestDist = int.MaxValue;
        int bestDefense = int.MinValue;
        int bestMoveDist = int.MaxValue;

        foreach (Vector2Int cell in candidates)
        {
            TileData tile = grid.GetTile(cell);
            if (tile == null || !tile.IsWalkableFor(enemy.Class)) continue;      // 歩けない地形に手動着陸はしない
            if (!ground.map.TryGetValue(cell, out int dist)) continue;           // そこからは目標へ地上路が無い
            if (dist > enemy.Move) continue;                                     // 次のターンに攻撃位置まで届かない

            int defense = tile.DefenseBonus; // 着陸後は地上なので地形防御が効く
            int moveDist = CombatRules.Manhattan(enemy.GridPosition, cell);

            bool better = dist < bestDist
                || (dist == bestDist && defense > bestDefense)
                || (dist == bestDist && defense == bestDefense && moveDist < bestMoveDist);
            if (better)
            {
                found = true;
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
            kind = ActionKind.LandAndWait,
            standCell = bestCell,
            reason = stay
                ? "その場に着陸（次ターン攻撃圏内・着陸で行動終了）"
                : $"({bestCell.x},{bestCell.y})へ着陸（次ターン攻撃圏内・着陸で行動終了）",
        };
        return true;
    }

    /// <summary>
    /// 空中巡航の停止マスを選ぶ（Phase 20）。目標は「地上で攻撃できる立ち位置（着陸後用）」＋
    /// 「空対空の立ち位置（飛翔中の相手がいるときだけ返る）」。
    /// マスの評価は 空路の残り道のり ＋ 対空射程マスなら AirThreatPenalty（1マス分の遠回りまで回避）。
    /// 同点なら移動の少ないマス（飛翔中は地形防御が無いので防御の同点規則は無い）。
    /// 空路が塞がれて全滅なら、通せんぼを無視して測り直す（地上の接近と同じ保険。Phase 16）。
    /// distFromHere には現在地からの空路の道のりを返す（離陸判断のターン数計算用）。
    /// threatNote はログ用（"・対空圏を回避"／"・対空圏内（回避先なし）"／脅威と無関係なら空文字）。
    /// </summary>
    private static bool TryPickCruiseCell(
        Unit enemy, GridManager grid, List<Unit> players, List<Vector2Int> candidates, GroundInfo ground,
        out Vector2Int bestCell, out int bestDist, out int distFromHere, out string threatNote)
    {
        bestCell = enemy.GridPosition;
        bestDist = 0;
        distFromHere = 0;
        threatNote = "";

        // 目標マス：着陸後に攻撃する地上の立ち位置 ＋ 空対空の立ち位置
        //（GetAttackFromCells は今の飛翔状態で判定するので、飛翔中の相手の分だけが加わる）
        var goals = new List<Vector2Int>(ground.goals);
        foreach (Unit p in players)
            goals.AddRange(CombatRules.GetAttackFromCells(enemy, p, grid, hasMoved: true));
        if (goals.Count == 0) return false;

        HashSet<Vector2Int> threat = ThreatMap.GetAirThreatCells(grid, enemy);

        var map = MovementCalculator.GetDistanceMap(grid, enemy, goals, ignoreEnemyUnits: false);
        if (PickCruiseFromMap(enemy, candidates, map, threat, ref bestCell, ref bestDist, ref distFromHere, ref threatNote))
            return true;

        map = MovementCalculator.GetDistanceMap(grid, enemy, goals, ignoreEnemyUnits: true);
        return PickCruiseFromMap(enemy, candidates, map, threat, ref bestCell, ref bestDist, ref distFromHere, ref threatNote);
    }

    /// <summary>TryPickCruiseCell の1段分：距離マップから巡航マスを選ぶ。候補が全滅なら false。</summary>
    private static bool PickCruiseFromMap(
        Unit enemy, List<Vector2Int> candidates, Dictionary<Vector2Int, int> map, HashSet<Vector2Int> threat,
        ref Vector2Int bestCell, ref int bestDist, ref int distFromHere, ref string threatNote)
    {
        bool found = false;
        int bestCost = int.MaxValue;
        int bestMoveDist = int.MaxValue;
        int minRawDist = int.MaxValue; // 対空を気にしない場合の最短（回避したかどうかのログ用）
        Vector2Int pick = enemy.GridPosition;
        int pickDist = 0;

        foreach (Vector2Int cell in candidates)
        {
            if (!map.TryGetValue(cell, out int dist)) continue; // そのマスからは目標へ届かない

            found = true;
            if (dist < minRawDist) minRawDist = dist;

            int cost = dist + (threat.Contains(cell) ? AirThreatPenalty : 0);
            int moveDist = CombatRules.Manhattan(enemy.GridPosition, cell);

            if (cost < bestCost || (cost == bestCost && moveDist < bestMoveDist))
            {
                bestCost = cost;
                bestMoveDist = moveDist;
                pick = cell;
                pickDist = dist;
            }
        }

        if (!found) return false;

        bestCell = pick;
        bestDist = pickDist;
        if (pickDist > minRawDist)
            threatNote = "・対空圏を回避";           // 遠回りしてでも対空射程マスの外を選んだ
        else if (threat.Contains(pick))
            threatNote = "・対空圏内（回避先なし）"; // 1マスの遠回りでは逃げ場が無く、撃たれうるマスに停止
        else
            threatNote = "";
        map.TryGetValue(enemy.GridPosition, out distFromHere); // 現在地からの道のり（離陸判断用）
        return true;
    }

    /// <summary>
    /// 地上の飛行兵が「離陸したほうが得か」を判断する（Phase 20・攻撃探索とガードが空振りしたとき）。
    /// 離陸する条件（いずれか）：
    ///   (1) 離陸すれば今すぐ空対空攻撃できる（飛翔は行動を消費しない＝離陸→移動→攻撃が1行動）
    ///   (2) 地上では目標へ到達できないが、空中なら行き先がある
    ///   (3) 空路のほうが速い：空路ターン数+1 ≤ 陸路ターン数
    ///       （+1 は着陸行動の分。同数なら空路優先＝門を味方に譲れて、移動中に近接に絡まれない。作者合意）
    ///   ※(2)(3)のとき「空中でしか攻撃できない相手（飛翔中のプレイヤー）」も目標に含まれている
    /// 【probe その2：お試し飛翔】空中の評価は StartFlight で実際に飛翔状態にして行い、
    /// 採用・不採用にかかわらず finally で必ず CancelFlight で地上に戻す
    /// （採用したときの本当の離陸は Execute が行う。評価では盤面を変えない約束を守る）。
    /// </summary>
    private static bool TryPlanTakeOff(Unit enemy, GridManager grid, List<Unit> players, out ActionPlan plan)
    {
        plan = default;
        if (enemy.Class != UnitClass.Flier) return false; // 飛翔は飛行兵専用
        if (enemy.Weapon == null) return false;           // 武装無しは攻撃目標を定義できない

        // 陸路の見積もり（今は地上なのでそのまま計算）。
        // 門を塞がれているだけなら、通せんぼを無視した測り直しで「道はある」とみなす（接近の第2段と同じ）
        GroundInfo ground = ComputeGroundInfo(enemy, grid, players);
        bool groundReachable = ground.map.TryGetValue(enemy.GridPosition, out int groundDist);
        if (!groundReachable)
        {
            var retry = MovementCalculator.GetDistanceMap(grid, enemy, ground.goals, ignoreEnemyUnits: true);
            groundReachable = retry.TryGetValue(enemy.GridPosition, out groundDist);
        }

        // 空中でしか攻撃できない相手（飛翔中のプレイヤー）がいるか。今は地上なので CanEngage が false になる相手
        bool airOnlyTarget = false;
        foreach (Unit p in players)
        {
            if (p.IsFlying && !CombatRules.CanEngage(enemy, p))
            {
                airOnlyTarget = true;
                break;
            }
        }

        // ── probe その2：お試し飛翔で空中の行動を評価 ──
        bool attackFound = false;
        ActionPlan airAttack = default;
        bool cruiseFound = false;
        Vector2Int cruiseCell = default;
        int distFromHere = 0;
        string threatNote = "";

        enemy.StartFlight(); // お試し飛翔（評価専用）
        try
        {
            var airCandidates = new List<Vector2Int>(MovementCalculator.GetReachableCells(grid, enemy));
            airCandidates.Add(enemy.GridPosition);

            // (1) 離陸して今すぐ空対空攻撃できるか
            attackFound = TryFindBestAttack(enemy, grid, players, airCandidates, out airAttack);

            // (2)(3)用：空路での巡航先と、現在地からの空路の道のり
            if (!attackFound)
            {
                cruiseFound = TryPickCruiseCell(enemy, grid, players, airCandidates, ground,
                    out cruiseCell, out _, out distFromHere, out threatNote);
            }
        }
        finally
        {
            enemy.CancelFlight(); // 必ず地上に戻す（採用時の本当の離陸は Execute が行う）
        }

        // (1) 空対空攻撃：離陸してそのまま攻撃する
        if (attackFound)
        {
            airAttack.kind = ActionKind.TakeOff;
            airAttack.reason = "離陸して空中戦 → " + airAttack.reason;
            plan = airAttack;
            return true;
        }

        if (!cruiseFound) return false; // 空中にも行き先が無いなら離陸しない

        // (2)(3) 陸路と空路のターン数を比べる（ターン数 = 道のり ÷ 移動力 の切り上げ）
        int move = Mathf.Max(1, enemy.Move);
        int airTurns = (distFromHere + move - 1) / move;

        string reason;
        if (!groundReachable)
        {
            reason = $"地上では目標へ到達できない → 離陸して({cruiseCell.x},{cruiseCell.y})へ（空路{airTurns}ターン）";
        }
        else
        {
            int groundTurns = (groundDist + move - 1) / move;
            bool airFaster = airTurns + 1 <= groundTurns; // +1 は着陸行動の分。同数なら空路優先（作者合意）
            if (airFaster)
                reason = $"離陸して({cruiseCell.x},{cruiseCell.y})へ巡航（空{airTurns}ターン+着陸1 vs 陸{groundTurns}ターン）";
            else if (airOnlyTarget)
                reason = $"飛翔中の相手を追って離陸 → ({cruiseCell.x},{cruiseCell.y})へ巡航";
            else
                return false; // 陸路のほうが速く、空中限定の相手もいない → 歩いたほうが得
        }
        reason += threatNote;

        plan = new ActionPlan
        {
            kind = ActionKind.TakeOff,
            standCell = cruiseCell,
            reason = reason,
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
        // 離陸（Phase 20）：飛翔は行動を消費しないコマンド（プレイヤーと同じ）。
        // 移動より先に発動する（移動の可否評価も飛翔状態で行った）
        if (plan.kind == ActionKind.TakeOff)
            enemy.StartFlight();

        if (plan.standCell != enemy.GridPosition)
            enemy.MoveTo(grid, plan.standCell);

        // 離陸からの空対空攻撃もここで実行される（target が入っているのは攻撃プランだけ）
        if (plan.target != null && (plan.kind == ActionKind.Attack || plan.kind == ActionKind.TakeOff))
            CombatSystem.ResolveAttack(enemy, plan.target, grid);

        // 着陸（Phase 20）：プレイヤーの着陸コマンドと同じ「飛翔解除＋行動終了」の意味論。
        // 行動終了は、TurnManager が TakeAction の直後に SetActed(true) することで満たされる
        if (plan.kind == ActionKind.LandAndWait)
            enemy.CancelFlight();
    }

    // ===== 行動の点数付け =====

    /// <summary>
    /// 「cell に立って target を攻撃する」行動の点数。
    /// 合計ダメージの予測は CombatSystem.PredictTotalDamage に一元化されている
    /// （挟撃の成立・ガードによる無効化も織り込み済み。実際の戦闘結果と必ず一致する）。
    /// Phase 18 の加点2つ（お膳立て・ガード役潰し）はここで足す。out の2つはログ用のしるし。
    /// </summary>
    private static int ScoreAttack(
        Unit enemy, Vector2Int cell, Unit target, GridManager grid,
        List<PincerHelper> helpers, out bool pincerSetup, out bool guardBreak)
    {
        int damage = CombatSystem.PredictTotalDamage(enemy, cell, target, grid);
        bool kill = damage >= target.CurrentHP;

        int score = damage;
        if (kill) score += KillBonus; // この一手で倒せるなら最優先

        // ガード役潰し（Phase 18）：倒せる相手の中では、誰かのガード役をしている相手を優先
        guardBreak = kill && CombatRules.IsGuardingSomeone(target, grid);
        if (guardBreak) score += GuardBreakKillBonus;

        // 挟撃のお膳立て（Phase 18）：ほぼ同点のマス選びでだけ効く控えめな加点
        pincerSetup = HasPincerSetup(cell, target, grid, helpers);
        if (pincerSetup) score += PincerSetupBonus;

        return score;
    }

    // ===== 挟撃のお膳立て（Phase 18）=====

    /// <summary>お膳立ての判定に使う「後続の味方」（未行動・前衛）と、その到達マス集合のペア。</summary>
    private struct PincerHelper
    {
        public Unit unit;
        public HashSet<Vector2Int> reach;
    }

    /// <summary>
    /// 自分のあとに動く「挟み役の候補」＝ 未行動・前衛武器・自分以外 の味方一覧と、
    /// それぞれが移動で立てるマスの集合。自分が前衛でなければ挟撃自体が無いので空を返す。
    /// ※待ち伏せ型で未挑発の味方も候補に含めてよい：反対側マスに立って攻撃すること自体が
    ///   挑発になるので、射程内なら普通に挟みに来る（攻撃探索は性格ゲートより先。Phase 17）。
    /// </summary>
    private static List<PincerHelper> BuildPincerHelpers(Unit enemy, GridManager grid)
    {
        var helpers = new List<PincerHelper>();
        if (!CombatRules.IsPincerCapable(enemy)) return helpers;

        foreach (Unit ally in UnitRegistry.GetUnits(enemy.Faction))
        {
            if (ally == enemy || ally.HasActed) continue;
            if (!CombatRules.IsPincerCapable(ally)) continue;

            helpers.Add(new PincerHelper
            {
                unit = ally,
                reach = MovementCalculator.GetReachableCells(grid, ally),
            });
        }
        return helpers;
    }

    /// <summary>
    /// 「cell に立って target を攻撃する」が挟撃のお膳立てになるか（Phase 18）。
    /// すべて満たすとき true：
    ///   ・上下左右の隣接攻撃である（反対側マスが定義できる。前衛かどうかは helpers 側で確認済み）
    ///   ・target がガードされていない（プレイヤーのガード役は敵フェイズ中に増えないので判定が安定）
    ///   ・反対側マスが盤内で、誰もいない
    ///   ・後続の味方の誰かがそこへ到達でき、target と交戦できる（飛翔の制限。Phase 14）
    /// ※反対側マスに既に前衛の味方が立っている場合は「完成した挟撃」なので、
    ///   ダメージ予測（PredictTotalDamage）の側に織り込まれていて、ここでは扱わない。
    /// </summary>
    private static bool HasPincerSetup(
        Vector2Int cell, Unit target, GridManager grid, List<PincerHelper> helpers)
    {
        if (helpers.Count == 0) return false;

        Vector2Int? opposite = CombatRules.GetPincerOppositeCell(cell, target.GridPosition);
        if (opposite == null) return false;                          // 遠距離攻撃に挟撃は無い
        if (CombatRules.IsPincerNegated(target, grid)) return false; // ガード中の相手には無駄

        TileData tile = grid.GetTile(opposite.Value);
        if (tile == null || tile.Occupant != null) return false;     // 盤外・埋まっている

        foreach (PincerHelper helper in helpers)
        {
            if (!CombatRules.CanEngage(helper.unit, target)) continue; // 飛翔の制限（Phase 14）
            if (helper.reach.Contains(opposite.Value)) return true;
        }
        return false;
    }

    // ===== 思考ログ =====

    private static void Log(Unit enemy, ActionPlan plan)
    {
        if (!LogEnabled) return;
        Debug.Log($"AI思考: {enemy.Data.unitName}({enemy.GridPosition.x},{enemy.GridPosition.y}) {plan.reason}");
    }

    /// <summary>攻撃プランの説明文（ログ用）。挟撃・撃破・Phase 18 の加点の見込みも添える。</summary>
    private static string BuildAttackReason(
        Unit enemy, Vector2Int cell, Unit target, GridManager grid, int damage,
        bool pincerSetup, bool guardBreak)
    {
        string move = cell == enemy.GridPosition ? "その場から" : $"({cell.x},{cell.y})へ移動して";

        string note = "";
        Unit ally = CombatRules.FindPincerAlly(cell, enemy, target, grid);
        if (ally != null && !CombatRules.IsPincerNegated(target, grid)) note += "・挟撃込み";
        if (damage >= target.CurrentHP) note += "・撃破";
        if (guardBreak) note += "・ガード役潰し";
        if (pincerSetup) note += "・挟撃のお膳立て";

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
