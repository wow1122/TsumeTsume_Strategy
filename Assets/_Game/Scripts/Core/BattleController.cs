using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

/// <summary>
/// プレイヤーの操作を受け持つ司令塔。Phase 10 からはFE型の
/// 「移動 → コマンドメニュー → 対象選択」の流れで、途中の取り消しができる：
///  1) 未行動の味方をクリック → 選択（移動範囲を水色表示）【MoveSelect】
///  2) 水色マス（または自分のマス）をクリック → 行動メニュー表示【CommandMenu】
///  3) 「攻撃」→ 対象の敵が赤表示 → クリックで戦闘【TargetSelect】／「待機」→ 行動終了
/// キャンセル（右クリック / ESC）で1段階ずつ戻る：
///  対象選択 → メニュー（移動位置は保持）／メニュー → 移動を取り消して移動選択へ／移動選択 → 選択解除
/// </summary>
public class BattleController : MonoBehaviour
{
    [Tooltip("グリッド（GridManager）への参照")]
    public GridManager grid;

    [Tooltip("ターン管理（TurnManager）への参照")]
    public TurnManager turnManager;

    // 操作の状態（ハイライト色の設定は GridManager に集約した）
    private enum State { Idle, MoveSelect, CommandMenu, TargetSelect }
    private State state = State.Idle;

    private ActionContext context;              // いま行動中のユニットの文脈（移動取り消し用）
    private HashSet<Vector2Int> reachableCells; // 移動できるマス
    private List<Unit> attackTargets;           // いまの位置から攻撃できる敵
    private ActionMenu menu;                    // 行動メニュー（IMGUI）

    void Awake()
    {
        // メニューは自分と同じ GameObject に自動で用意する（シーンへの追加作業を不要にするため）
        menu = GetComponent<ActionMenu>();
        if (menu == null) menu = gameObject.AddComponent<ActionMenu>();
    }

    void Update()
    {
        if (grid == null || turnManager == null) return;

        // 決着後・敵フェイズ中は操作不可。行動の途中なら移動も取り消して選択を解除する
        if (turnManager.IsGameOver || turnManager.CurrentPhase != TurnPhase.Player)
        {
            if (state != State.Idle) CancelAll();
            return;
        }

        // キャンセル操作（右クリック / ESC）：1段階ずつ戻る
        bool cancelPressed =
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
        if (cancelPressed)
        {
            OnCancelPressed();
            return;
        }

        if (Mouse.current == null || Camera.main == null) return;

        // メニュー表示中のセルクリックは無効。ボタンの処理は ActionMenu（IMGUI）側が
        // 行うので、ここで盤面まで反応すると二重処理になるのを防ぐ
        if (state == State.CommandMenu) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

            if (grid.TryWorldToCell(worldPos, out Vector2Int cell))
                OnCellClicked(cell);
            else if (state == State.MoveSelect)
                CancelAll(); // 盤外クリックで選択解除（対象選択中は無視）
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
                    EnterMoveSelect(clickedUnit);
                break;

            case State.MoveSelect:
                if (cell == context.unit.GridPosition)
                {
                    // その場で静止（hasMoved=false のまま）
                    // → 後衛武器（弓・魔法）は武器上限まで射程が伸びる
                    OpenCommandMenu();
                }
                else if (reachableCells.Contains(cell))
                {
                    context.unit.MoveTo(grid, cell);
                    context.hasMoved = true; // 移動後 → 後衛武器は基本射程（2マスちょうど）のみ
                    OpenCommandMenu();
                }
                else if (IsSelectablePlayer(clickedUnit))
                {
                    EnterMoveSelect(clickedUnit); // 別の味方へ選択切替
                }
                else
                {
                    CancelAll();
                }
                break;

            case State.TargetSelect:
                if (clickedUnit != null && attackTargets.Contains(clickedUnit))
                {
                    CombatSystem.ResolveAttack(context.unit, clickedUnit, grid);
                    FinishAction();
                }
                // 対象以外のクリックは無視（待機はメニューの「待機」から。誤クリックでの行動消費を防ぐ）
                break;
        }
    }

    // ===== キャンセル系統 =====

    /// <summary>右クリック / ESC が押されたとき、いまの状態から1段階だけ戻る。</summary>
    private void OnCancelPressed()
    {
        switch (state)
        {
            case State.TargetSelect:
                // 対象選択 → メニューへ戻る（移動した位置はそのまま保持）
                OpenCommandMenu();
                break;

            case State.CommandMenu:
                // メニュー → 移動を取り消して、元の位置から移動選択をやり直す
                Unit unit = context.unit;
                menu.Hide();
                context.RevertMove(grid);
                EnterMoveSelect(unit);
                break;

            case State.MoveSelect:
                CancelAll(); // 選択解除（まだ何もしていないので取り消すものは無い）
                break;
        }
    }

    // ===== 状態遷移 =====

    /// <summary>ユニットを選択し、移動範囲を表示する。</summary>
    private void EnterMoveSelect(Unit unit)
    {
        menu.Hide();
        context = new ActionContext(unit);
        reachableCells = MovementCalculator.GetReachableCells(grid, unit);

        grid.ResetAllHighlights();
        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);
        foreach (Vector2Int c in reachableCells)
            grid.AddHighlight(c, HighlightKind.MoveRange);

        state = State.MoveSelect;
        Debug.Log($"選択: {unit.Data.unitName}（移動できるマス {reachableCells.Count} 個）");
    }

    /// <summary>行動メニューを開く（移動直後・静止選択時・対象選択からの戻りで呼ばれる）。</summary>
    private void OpenCommandMenu()
    {
        Unit unit = context.unit;

        // 移動範囲・対象候補の表示を消し、選択マークだけ現在位置に付け直す
        grid.ResetAllHighlights();
        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);

        menu.Show(grid.CellToWorld(unit.GridPosition), grid.cellSize, BuildCommands());
        state = State.CommandMenu;
    }

    /// <summary>
    /// いまの文脈（context）で実行できるコマンドの一覧を作る。
    /// Phase 11 以降のコマンド（救出・飛翔など）はここに項目を足していく。
    /// </summary>
    private List<ActionMenu.Entry> BuildCommands()
    {
        var commands = new List<ActionMenu.Entry>();

        attackTargets = FindAttackTargets(context.unit, context.hasMoved);
        if (attackTargets.Count > 0)
            commands.Add(new ActionMenu.Entry("攻撃", EnterTargetSelect));

        commands.Add(new ActionMenu.Entry("待機", FinishAction));
        return commands;
    }

    /// <summary>「攻撃」が選ばれた：対象の敵を赤表示して、クリックを待つ。</summary>
    private void EnterTargetSelect()
    {
        menu.Hide();

        foreach (Unit enemy in attackTargets)
            grid.AddHighlight(enemy.GridPosition, HighlightKind.TargetChoice);

        state = State.TargetSelect;
        Debug.Log($"攻撃対象を選択（{attackTargets.Count} 体）。右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>行動を確定して終える。ここまで来たら移動の取り消しはもうできない。</summary>
    private void FinishAction()
    {
        Unit acted = context != null ? context.unit : null;
        ClearSelection();

        if (acted != null) acted.SetActed(true);
        turnManager.NotifyUnitActed();
    }

    /// <summary>行動を中断して選択を解除する（移動していれば元の位置へ戻す）。</summary>
    private void CancelAll()
    {
        if (context != null) context.RevertMove(grid);
        ClearSelection();
    }

    /// <summary>選択に関わる表示と内部状態をすべて片付ける（移動の取り消しはしない）。</summary>
    private void ClearSelection()
    {
        menu.Hide();
        grid.ResetAllHighlights();
        context = null;
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
