using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

/// <summary>
/// プレイヤーの操作を受け持つ司令塔。操作は次の流れ：
///  1) 未行動の味方をクリック → 選択（移動範囲を水色表示）
///  2) 水色マスをクリック → 移動／自分のマスをクリック → その場に留まる
///  3) 攻撃できる敵が射程内にいれば赤く表示 → 敵をクリックで攻撃／別の所で待機
/// 攻撃後・待機後はそのユニットを「行動済み」にして TurnManager に通知する。
/// </summary>
public class BattleController : MonoBehaviour
{
    [Tooltip("グリッド（GridManager）への参照")]
    public GridManager grid;

    [Tooltip("ターン管理（TurnManager）への参照")]
    public TurnManager turnManager;

    [Header("ハイライト色")]
    [Tooltip("選択中ユニットのマス")]
    public Color selectColor = new Color(1f, 0.9f, 0.4f);    // 黄
    [Tooltip("移動できるマス")]
    public Color moveRangeColor = new Color(0.5f, 0.8f, 1f);  // 水色
    [Tooltip("攻撃できる敵のマス")]
    public Color attackColor = new Color(1f, 0.45f, 0.45f);   // 赤

    // 操作の状態
    private enum State { Idle, AwaitingMove, AwaitingAttack }
    private State state = State.Idle;

    private Unit selectedUnit;
    private HashSet<Vector2Int> reachableCells;
    private List<Unit> attackTargets;

    void Update()
    {
        if (grid == null || turnManager == null) return;

        // 決着後は操作不可。
        if (turnManager.IsGameOver)
        {
            if (state != State.Idle) CancelSelection();
            return;
        }

        // 自軍フェイズ以外は操作不可。選択が残っていれば消す。
        if (turnManager.CurrentPhase != TurnPhase.Player)
        {
            if (state != State.Idle) CancelSelection();
            return;
        }

        if (Mouse.current == null || Camera.main == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

            if (grid.TryWorldToCell(worldPos, out Vector2Int cell))
                OnCellClicked(cell);
            else if (state != State.AwaitingAttack)
                CancelSelection(); // 盤外クリックで選択解除（攻撃待ち中は無視）
        }
    }

    private void OnCellClicked(Vector2Int cell)
    {
        // 座標確認用：クリックしたマスを常にログに出す（左下が(0,0)、右へX+、上へY+）
        Debug.Log($"クリック: {cell}");

        TileData tile = grid.GetTile(cell);
        Unit clickedUnit = tile != null ? tile.Occupant : null;

        switch (state)
        {
            case State.Idle:
                // 未行動の味方をクリックで選択
                if (IsSelectablePlayer(clickedUnit))
                    BeginSelection(clickedUnit);
                break;

            case State.AwaitingMove:
                if (cell == selectedUnit.GridPosition)
                {
                    // 動かずその場で（攻撃 or 待機へ）。
                    // 静止＝hasMoved false → 後衛武器（弓・魔法）は武器上限まで射程が伸びる
                    BeginAttackPhase(hasMoved: false);
                }
                else if (reachableCells.Contains(cell))
                {
                    selectedUnit.MoveTo(grid, cell);
                    BeginAttackPhase(hasMoved: true); // 移動後 → 後衛武器は基本射程（2マスちょうど）のみ
                }
                else if (IsSelectablePlayer(clickedUnit))
                {
                    BeginSelection(clickedUnit); // 別の味方へ選択切替
                }
                else
                {
                    CancelSelection();
                }
                break;

            case State.AwaitingAttack:
                if (clickedUnit != null && attackTargets.Contains(clickedUnit))
                {
                    CombatSystem.ResolveAttack(selectedUnit, clickedUnit, grid);
                    FinishAction();
                }
                else
                {
                    FinishAction(); // 攻撃せず待機（行動は終了）
                }
                break;
        }
    }

    // ===== 状態遷移 =====

    /// <summary>ユニットを選択し、移動範囲を表示する。</summary>
    private void BeginSelection(Unit unit)
    {
        grid.ResetAllHighlights();

        selectedUnit = unit;
        reachableCells = MovementCalculator.GetReachableCells(grid, unit);

        grid.SetHighlight(unit.GridPosition, selectColor);
        foreach (Vector2Int c in reachableCells)
            grid.SetHighlight(c, moveRangeColor);

        state = State.AwaitingMove;
        Debug.Log($"選択: {unit.Data.unitName}（移動できるマス {reachableCells.Count} 個）");
    }

    /// <summary>移動後（または据え置き後）、攻撃できる敵を探して表示する。</summary>
    private void BeginAttackPhase(bool hasMoved)
    {
        grid.ResetAllHighlights();
        grid.SetHighlight(selectedUnit.GridPosition, selectColor);

        attackTargets = FindAttackTargets(selectedUnit, hasMoved);

        if (attackTargets.Count == 0)
        {
            FinishAction(); // 攻撃対象なし → 行動終了
            return;
        }

        foreach (Unit enemy in attackTargets)
            grid.SetHighlight(enemy.GridPosition, attackColor);

        state = State.AwaitingAttack;
        Debug.Log($"攻撃対象を選択（{attackTargets.Count} 体）。敵以外をクリックで待機。");
    }

    /// <summary>行動を終え、ユニットを行動済みにして通知する。</summary>
    private void FinishAction()
    {
        Unit acted = selectedUnit;
        grid.ResetAllHighlights();
        selectedUnit = null;
        reachableCells = null;
        attackTargets = null;
        state = State.Idle;

        if (acted != null) acted.SetActed(true);
        turnManager.NotifyUnitActed();
    }

    /// <summary>選択を取り消す（行動は消費しない）。</summary>
    private void CancelSelection()
    {
        grid.ResetAllHighlights();
        selectedUnit = null;
        reachableCells = null;
        attackTargets = null;
        state = State.Idle;
    }

    // ===== 補助 =====

    private bool IsSelectablePlayer(Unit u)
    {
        return u != null && u.Faction == Faction.Player && !u.HasActed;
    }

    /// <summary>
    /// unit がいまの位置から攻撃できる敵ユニットの一覧を返す。
    /// 射程の判定は CombatRules に任せる（移動有無で後衛武器の射程が変わる。敵AIと共通ルール）。
    /// </summary>
    private List<Unit> FindAttackTargets(Unit unit, bool hasMoved)
    {
        var targets = new List<Unit>();

        // 相手陣営の盤上ユニット（名簿）から、攻撃可能なものを探す
        Faction enemyFaction = (unit.Faction == Faction.Player) ? Faction.Enemy : Faction.Player;
        foreach (Unit other in UnitRegistry.GetUnits(enemyFaction))
        {
            if (CombatRules.CanAttack(unit, unit.GridPosition, other, hasMoved))
                targets.Add(other);
        }
        return targets;
    }
}
