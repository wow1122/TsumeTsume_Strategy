using System.Collections;
using UnityEngine;

/// <summary>戦闘のフェイズ（どちらの軍の番か）。</summary>
public enum TurnPhase { Player, Enemy }

/// <summary>
/// ターンとフェイズを管理する。
///  ・自軍フェイズ開始時に、味方全員の「行動済み」をリセット（飛翔の残りターンもここで減る）
///  ・味方が全員行動したら自動で敵フェイズへ。敵フェイズは EnemyAI が1体ずつ行動する
///  ・勝敗判定：敵全滅で勝利／味方全滅・味方輸送隊の死亡で敗北。
///    ターン制限（Phase 15）：StageData で指定されたターン数を使い切っても
///    決着していなければ敗北（0なら無制限）
///  ・画面左上にターン数・フェイズ・「ターン終了」ボタンを表示（簡易UI）
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
        foreach (Unit u in UnitRegistry.GetUnits(Faction.Player))
        {
            u.TickFlight();    // 飛翔の残りターンを1減らす（0になったら自動着地。Phase 14）
            u.SetActed(false); // 全味方を「未行動」に戻す
        }
        Debug.Log($"── ターン {TurnNumber}：自軍フェイズ 開始 ──");
    }

    /// <summary>自軍フェイズを終了して敵フェイズへ（ボタン or 全員行動で呼ばれる）。</summary>
    public void EndPlayerPhase()
    {
        if (IsGameOver) return;
        if (CurrentPhase != TurnPhase.Player) return;

        CurrentPhase = TurnPhase.Enemy;
        foreach (Unit u in UnitRegistry.GetUnits(Faction.Enemy))
            u.TickFlight(); // 敵側の飛翔もフェイズ開始で数える（現状は「開始時から飛翔」の敵のみ該当）
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
    /// </summary>
    public void NotifyUnitActed()
    {
        CheckGameEnd();
        if (IsGameOver) return;

        if (AllActed(Faction.Player))
        {
            Debug.Log("全味方が行動済み → 自動でターン終了");
            EndPlayerPhase();
        }
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

    // ===== ユニットの問い合わせ =====

    private bool AllActed(Faction faction)
    {
        foreach (Unit u in UnitRegistry.GetUnits(faction))
            if (!u.HasActed) return false;
        return true;
    }

    // ===== 簡易UI（Canvas不要・コードで画面に直接描画）=====

    void OnGUI()
    {
        string phaseLabel = (CurrentPhase == TurnPhase.Player) ? "自軍フェイズ" : "敵フェイズ";

        // ターン制限があるステージでは「ターン 3／15」のように残りが分かる表示にする（Phase 15）
        string turnLabel = (turnLimit > 0) ? $"ターン {TurnNumber}／{turnLimit}" : $"ターン {TurnNumber}";

        var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        GUI.Label(new Rect(12, 10, 360, 28), $"{turnLabel} ／ {phaseLabel}", style);

        // 決着後：画面中央に結果を大きく表示（ボタンは出さない）
        if (IsGameOver)
        {
            var bigStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            bigStyle.normal.textColor = (Result == GameResult.Win) ? Color.yellow : Color.red;

            string text = (Result == GameResult.Win) ? "勝利！" : "敗北…";
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 80), text, bigStyle);
            return;
        }

        if (CurrentPhase == TurnPhase.Player)
        {
            if (GUI.Button(new Rect(12, 44, 130, 32), "ターン終了"))
                EndPlayerPhase();
        }
    }
}
