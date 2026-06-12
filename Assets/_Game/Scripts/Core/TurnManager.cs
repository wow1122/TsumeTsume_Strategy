using System.Collections;
using System.Collections.Generic;
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
        foreach (Unit u in GetUnits(Faction.Player))
            u.SetActed(false); // 全味方を「未行動」に戻す
        Debug.Log($"── ターン {TurnNumber}：自軍フェイズ 開始 ──");
    }

    /// <summary>自軍フェイズを終了して敵フェイズへ（ボタン or 全員行動で呼ばれる）。</summary>
    public void EndPlayerPhase()
    {
        if (CurrentPhase != TurnPhase.Player) return;

        CurrentPhase = TurnPhase.Enemy;
        Debug.Log($"ターン {TurnNumber}：敵フェイズ 開始");
        StartCoroutine(EnemyPhaseRoutine());
    }

    /// <summary>敵を1体ずつ、少し間を置いて行動させる。</summary>
    private IEnumerator EnemyPhaseRoutine()
    {
        yield return new WaitForSeconds(enemyPhaseDelay);

        foreach (Unit enemy in GetUnits(Faction.Enemy))
        {
            if (enemy == null || !enemy.IsAlive) continue;
            EnemyAI.TakeAction(enemy, grid);
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
        if (AllActed(Faction.Player))
        {
            Debug.Log("全味方が行動済み → 自動でターン終了");
            EndPlayerPhase();
        }
    }

    // ===== ユニットの問い合わせ =====

    private bool AllActed(Faction faction)
    {
        foreach (Unit u in GetUnits(faction))
            if (!u.HasActed) return false;
        return true;
    }

    private List<Unit> GetUnits(Faction faction)
    {
        var result = new List<Unit>();
        foreach (Unit u in FindObjectsByType<Unit>(FindObjectsSortMode.None))
            if (u.Faction == faction) result.Add(u);
        return result;
    }

    // ===== 簡易UI（Canvas不要・コードで画面に直接描画）=====

    void OnGUI()
    {
        string phaseLabel = (CurrentPhase == TurnPhase.Player) ? "自軍フェイズ" : "敵フェイズ";

        var style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        GUI.Label(new Rect(12, 10, 360, 28), $"ターン {TurnNumber} ／ {phaseLabel}", style);

        if (CurrentPhase == TurnPhase.Player)
        {
            if (GUI.Button(new Rect(12, 44, 130, 32), "ターン終了"))
                EndPlayerPhase();
        }
    }
}
