using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>戦闘のフェイズ（どちらの軍の番か）。</summary>
public enum TurnPhase { Player, Enemy }

/// <summary>
/// ターンとフェイズを管理する。
///  ・自軍フェイズ開始時に、味方全員の「行動済み」をリセット（飛翔の残りターンもここで減る）。
///    メモリの自然増加（フェーズ28）もここで行う（フェーズ32の輸送隊からの補給は 2026-10-24 に撤廃）
///  ・味方が全員行動したら自動で敵フェイズへ。敵フェイズは EnemyAI が1体ずつ行動する
///  ・勝敗判定：敵全滅で勝利／味方全滅・味方輸送隊の死亡で敗北。
///    ターン制限（Phase 15）：StageData で指定されたターン数を使い切っても
///    決着していなければ敗北（0なら無制限）
///  ・画面に出すのは決着時の「勝利！／敗北…」だけ。ターン数・フェイズ・クリア条件は
///    コマンド一覧の「状況」（SystemInfoPanel）から見る。ターンを終えるのも
///    コマンド一覧の「終了」から（2026-08-15 の UI 刷新）
/// </summary>
public class TurnManager : MonoBehaviour
{
    public TurnPhase CurrentPhase { get; private set; } = TurnPhase.Player;
    public int TurnNumber { get; private set; } = 1;

    /// <summary>勝敗の結果。</summary>
    public enum GameResult { None, Win, Lose }
    public GameResult Result { get; private set; } = GameResult.None;

    /// <summary>決着がついたか。</summary>
    public bool IsGameOver => Result != GameResult.None;

    [Tooltip("敵フェイズに入ってから最初の行動までの待ち時間(秒)")]
    public float enemyPhaseDelay = 0.6f;
    [Tooltip("敵1体ごとの行動の間隔(秒)。動きが見やすくなる")]
    public float enemyActionDelay = 0.4f;

    [Tooltip("敵AIの思考ログを Console に出す（Phase 16）")]
    public bool enemyAILog = true;

    private GridManager grid;
    private int turnLimit; // ステージのターン制限（0なら無制限。StageData から読む。Phase 15）

    /// <summary>このステージのターン制限（0なら無制限）。「状況」の表示に使う。</summary>
    public int TurnLimit => turnLimit;

    void Start()
    {
        grid = FindAnyObjectByType<GridManager>();

        // ステージのターン制限を読む（Phase 15）
        BattleSetup setup = FindAnyObjectByType<BattleSetup>();
        if (setup != null && setup.stage != null) turnLimit = setup.stage.turnLimit;

        StartPlayerPhase();
    }

    // ===== フェイズ進行 =====

    private void StartPlayerPhase()
    {
        CurrentPhase = TurnPhase.Player;
        GaugeRules.ResetChain(); // 連続撃破の連鎖はフェイズをまたがない（フェーズ28）

        foreach (Unit u in UnitRegistry.GetUnits(Faction.Player))
        {
            u.TickFlight();      // 飛翔の残りターンを1減らす（0になったら自動着地。Phase 14）
            u.TickFlightBlock(); // 撃墜による飛翔禁止の残りを1減らす（2026-08-05）
            u.SetActed(false);   // 全味方を「未行動」に戻す
            u.ResetRefresh();    // 再行動の「1ターン1回」を戻す（フェーズ30）
        }

        // メモリの自然増加（フェーズ28）。ターン1は初期値のまま
        // （BattleSetup と TurnManager のどちらの Start が先に走るかを Unity は保証しないため、
        //   「ターン2以降に増える」と明示的に決めて、配置直後の値がぶれないようにしている）
        if (TurnNumber > 1) GaugeRules.TickMemory(Faction.Player);

        Debug.Log($"── ターン {TurnNumber}：自軍フェイズ 開始 ──");
    }

    /// <summary>自軍フェイズを終了して敵フェイズへ（ボタン or 全員行動で呼ばれる）。</summary>
    public void EndPlayerPhase()
    {
        if (IsGameOver) return;
        if (CurrentPhase != TurnPhase.Player) return;

        CurrentPhase = TurnPhase.Enemy;
        GaugeRules.ResetChain(); // 自軍フェイズの連鎖を持ち越さない（フェーズ28）

        foreach (Unit u in UnitRegistry.GetUnits(Faction.Enemy))
        {
            u.TickFlight();      // 敵側の飛翔もフェイズ開始で数える（Phase 20 から敵AIも自分で離陸する）
            u.TickFlightBlock(); // 撃墜による飛翔禁止も敵味方対称に数える（2026-08-05）
            u.ResetRefresh();    // 敵は再行動を使わないが、状態は味方と対称に保つ
        }

        // 敵側のメモリも自軍と対称に増やす（敵AIは当面使わないが、蓄積は陣営を区別しない）
        if (TurnNumber > 1) GaugeRules.TickMemory(Faction.Enemy);

        Debug.Log($"ターン {TurnNumber}：敵フェイズ 開始");
        StartCoroutine(EnemyPhaseRoutine());
    }

    /// <summary>敵を1体ずつ、少し間を置いて行動させる。</summary>
    private IEnumerator EnemyPhaseRoutine()
    {
        EnemyAI.LogEnabled = enemyAILog; // Inspector の設定を反映（Play中の切り替えは次のフェイズから効く）

        // 敵全員を「未行動」に戻してから始める（Phase 16）。
        // 行動した敵から行動済みにしていくことで、「まだ動いていない味方がいるか」を
        // 後のフェーズ（挟撃のお膳立て等）で判断材料に使える。見た目にも行動済みの敵から暗くなる。
        foreach (Unit u in UnitRegistry.GetUnits(Faction.Enemy))
            u.SetActed(false);

        // 集中攻撃（Phase 21）: 「皆で倒し切れる相手」を1体選んでから行動を始める。
        // 全員を未行動に戻した直後に呼ぶ＝「このフェイズ動ける敵」の火力で見積もるため
        EnemyAI.BeginEnemyPhase(grid);

        yield return new WaitForSeconds(enemyPhaseDelay);

        foreach (Unit enemy in UnitRegistry.GetUnits(Faction.Enemy))
        {
            if (enemy == null || !enemy.IsAlive) continue;
            EnemyAI.TakeAction(enemy, grid);
            enemy.SetActed(true); // 行動済み（暗く表示）

            CheckGameEnd();
            if (IsGameOver) yield break; // 決着がついたら敵フェイズを止める

            yield return new WaitForSeconds(enemyActionDelay);
        }

        EndEnemyPhase();
    }

    private void EndEnemyPhase()
    {
        // 敵の「行動済み」表示を元に戻す（プレイヤーフェイズ中に敵が暗いままにならないように）
        foreach (Unit u in UnitRegistry.GetUnits(Faction.Enemy))
            u.SetActed(false);

        // ターン制限（Phase 15）：最終ターンの敵フェイズが終わっても決着していなければ敗北
        if (turnLimit > 0 && TurnNumber >= turnLimit && !IsGameOver)
        {
            Result = GameResult.Lose;
            Debug.Log($"× 敗北… {turnLimit} ターン以内に勝利できなかった ×");
            return;
        }

        TurnNumber++;
        StartPlayerPhase();
    }

    /// <summary>
    /// 味方が1体行動を終えたら呼ぶ。全員行動済みなら自動でフェイズを終える。
    /// ただし気勢で再行動できる味方が残っているあいだは自動で終わらせない（フェーズ30）。
    /// </summary>
    public void NotifyUnitActed()
    {
        CheckGameEnd();
        if (IsGameOver) return;

        if (!AllActed(Faction.Player)) return;

        // 再行動（気勢1本）は行動済みのユニットを選び直して使うので、
        // 最後の1体が行動した瞬間に自動でターンが終わると使う隙が無くなってしまう。
        // 使うかどうかはプレイヤーが決めることなので、ここでは終わらせず
        // コマンド一覧の「終了」で明示的に終えてもらう
        if (GaugeRules.AnyoneCanRefresh(Faction.Player))
        {
            Debug.Log("全味方が行動済み。ただし再行動できる味方がいるので、"
                      + "ターンを終えるときは空きマスをクリックして「終了」を選んでください");
            return;
        }

        Debug.Log("全味方が行動済み → 自動でターン終了");
        EndPlayerPhase();
    }

    // ===== 勝敗判定 =====

    /// <summary>
    /// 敵全滅で勝ち、味方全滅で負け。味方の輸送隊が倒されても負け（Phase 12・作者合意）。
    /// 決着済みなら何もしない。
    /// </summary>
    private void CheckGameEnd()
    {
        if (IsGameOver) return;

        // 名簿（UnitRegistry）で数える。格納中（非アクティブ）でも生きていれば数に入る。
        int players = UnitRegistry.CountAlive(Faction.Player);
        int enemies = UnitRegistry.CountAlive(Faction.Enemy);

        if (enemies == 0)
        {
            Result = GameResult.Win;
            Debug.Log("★ 勝利！ 敵を全滅させた ★");
        }
        else if (UnitRegistry.PlayerTransporterLost)
        {
            Result = GameResult.Lose;
            Debug.Log("× 敗北… 輸送隊が倒された ×");
        }
        else if (players == 0)
        {
            Result = GameResult.Lose;
            Debug.Log("× 敗北… 味方が全滅した ×");
        }
    }

    /// <summary>
    /// いまの勝敗ルールを説明する行の一覧を作る（コマンド一覧の「状況」で表示する。2026-08-15）。
    /// 文言はステージのデータではなく、実際の判定（CheckGameEnd とターン制限）から組み立てる。
    /// <b>CheckGameEnd を変えたら、ここも必ず直すこと</b>（判定と説明が食い違わないよう、
    /// あえて判定の真下に置いている）。
    /// </summary>
    public List<string> BuildClearConditionLines()
    {
        var lines = new List<string>
        {
            "勝利：敵ユニットの全滅",
            "敗北：味方ユニットの全滅",
        };

        // 輸送隊がいるステージでだけ出す（いなければ起こりえない敗北条件なので）
        if (HasPlayerTransporter())
            lines.Add("　　　味方輸送隊の死亡");

        if (turnLimit > 0)
            lines.Add($"　　　{turnLimit} ターン以内に勝利できない");

        return lines;
    }

    /// <summary>味方に輸送隊がいるか（クリア条件の表示に使う）。</summary>
    private static bool HasPlayerTransporter()
    {
        foreach (Unit u in UnitRegistry.GetUnits(Faction.Player))
            if (u.IsAlive && u.Class == UnitClass.Transporter) return true;
        return false;
    }

    // ===== ユニットの問い合わせ =====

    private bool AllActed(Faction faction)
    {
        foreach (Unit u in UnitRegistry.GetUnits(faction))
            if (!u.HasActed) return false;
        return true;
    }

    // ===== 簡易UI（Canvas不要・コードで画面に直接描画）=====
    // 2026-08-15 の UI 刷新で、常時表示していたターン数・フェイズと「ターン終了」ボタンは撤去した。
    // ターン数・フェイズ・クリア条件・両軍の数は「状況」（SystemInfoPanel）へ、
    // ターンを終える操作はコマンド一覧の「終了」へ移した。ここに残すのは決着の表示だけ。

    void OnGUI()
    {
        if (!IsGameOver) return;

        // 決着後：画面中央に結果を大きく表示
        var bigStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 48,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        bigStyle.normal.textColor = (Result == GameResult.Win) ? Color.yellow : Color.red;

        string text = (Result == GameResult.Win) ? "勝利！" : "敗北…";
        GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 80), text, bigStyle);
    }
}
