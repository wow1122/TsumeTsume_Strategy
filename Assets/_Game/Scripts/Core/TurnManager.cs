using System.Collections;
using UnityEngine;

/// <summary>戦闘のフェイズ（どちらの軍の番か）。</summary>
public enum TurnPhase { Player, Enemy }

/// <summary>
/// ターンとフェイズを管理する。
///  ・自軍フェイズ開始時に、味方全員の「行動済み」をリセット
///  ・味方が全員行動したら自動で敵フェイズへ
///  ・敵フェイズは今は「行動なし」で、少し待って自軍フェイズに戻る（AIは Phase 6）
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

    private GridManager grid;

    void Start()
    {
        grid = FindAnyObjectByType<GridManager>();
        StartPlayerPhase();
    }

    // ===== フェイズ進行 =====

    private void StartPlayerPhase()
    {
        CurrentPhase = TurnPhase.Player;
        foreach (Unit u in UnitRegistry.GetUnits(Faction.Player))
            u.SetActed(false); // 全味方を「未行動」に戻す
        Debug.Log($"── ターン {TurnNumber}：自軍フェイズ 開始 ──");
    }

    /// <summary>自軍フェイズを終了して敵フェイズへ（ボタン or 全員行動で呼ばれる）。</summary>
    public void EndPlayerPhase()
    {
        if (IsGameOver) return;
        if (CurrentPhase != TurnPhase.Player) return;

        CurrentPhase = TurnPhase.Enemy;
        Debug.Log($"ターン {TurnNumber}：敵フェイズ 開始");
        StartCoroutine(EnemyPhaseRoutine());
    }

    /// <summary>敵を1体ずつ、少し間を置いて行動させる。</summary>
    private IEnumerator EnemyPhaseRoutine()
    {
        yield return new WaitForSeconds(enemyPhaseDelay);

        foreach (Unit enemy in UnitRegistry.GetUnits(Faction.Enemy))
        {
            if (enemy == null || !enemy.IsAlive) continue;
            EnemyAI.TakeAction(enemy, grid);

            CheckGameEnd();
            if (IsGameOver) yield break; // 決着がついたら敵フェイズを止める

            yield return new WaitForSeconds(enemyActionDelay);
        }

        EndEnemyPhase();
    }

    private void EndEnemyPhase()
    {
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

    /// <summary>敵全滅で勝ち、味方全滅で負け。決着済みなら何もしない。</summary>
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

        var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        GUI.Label(new Rect(12, 10, 360, 28), $"ターン {TurnNumber} ／ {phaseLabel}", style);

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
