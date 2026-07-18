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
///
/// Phase 11（救出）で追加された流れ：
///  「救出」  → 対象の味方を選択【UnitTargetSelect】→ 格納 → 残り移動力で再移動 → 待機
///  「降ろす」→ 隣の空きマスを選択【TileSelect】→ 再配置して行動終了
///  「引き受け」→ 隣の救出中の味方を選択 → 貨物をもらう → 降ろす/待機のみ
///  「代わりに降ろす」→ 救出中の味方を選択 → その隣の空きマスを選択 → 行動終了（歩兵専用）
///  救出・引き受けを実行すると「取り消し不能」になり、以後は選択解除できない
///  （選択解除できると移動力を使い直せてしまうため）。
///
/// Phase 12（輸送隊）で追加された流れ：
///  「乗り込む」→ 隣接する輸送隊を選択 → 自分が格納されて行動終了（輸送隊の行動は消費しない）
///  貨物が複数のとき、「降ろす」「引き受け」「代わりに降ろす」は貨物リスト【CargoSelect】から
///  対象を選ぶ（1体だけなら自動選択でリストは出ない）。
///
/// Phase 14（飛翔・作者仕様 2026-07-12 改訂）で追加された流れ：
///  「飛翔」→ 飛行兵が非飛翔中ならいつでも選べる（移動前・移動後・救出後も）。行動は消費せず、
///  飛翔状態のまま「残り移動力」で移動選択へ戻る（飛翔→移動→攻撃/待機まで1行動でできる）。
///  取り消しは1段階ずつ：メニュー →（飛翔後の再移動を取り消し＝飛翔した位置へ）→ 飛翔解除 →
///  （飛翔前の移動を取り消し）→ 選択解除。行動を確定する前に別のマス・別の味方をクリックした
///  ときも飛翔は取り消される（選択解除できる状況なら解除も同時に。救出後など取り消し不能の
///  ときは飛翔の取り消しだけが起こり、選択は残る）。確定するには待機・攻撃などで行動を終える。
///  「着陸」→ 飛翔中の飛行兵が使える（飛翔を発動したターンは不可）。飛翔を解除して即行動終了。
///  空中で「引き受け」た貨物は降ろせない（降ろすは地上でのみ表示）。
///
/// Phase 15 の追加：
///  攻撃の対象選択中、対象にマウスを重ねると戦闘予測（BattleForecast）が画面右下に出る。
///  「降ろす」系の対象・マスは貨物の兵種で判定する（騎乗の貨物は山に降ろせない等）。
/// </summary>
public class BattleController : MonoBehaviour
{
    [Tooltip("グリッド（GridManager）への参照")]
    public GridManager grid;

    [Tooltip("ターン管理（TurnManager）への参照")]
    public TurnManager turnManager;

    // 操作の状態（ハイライト色の設定は GridManager に集約した）
    private enum State { Idle, MoveSelect, CommandMenu, TargetSelect, UnitTargetSelect, TileSelect, CargoSelect }
    private State state = State.Idle;

    // ユニット選択（UnitTargetSelect）・マス選択（TileSelect）・貨物選択（CargoSelect）が「どのコマンドのためか」
    private enum UnitSelectPurpose { Rescue, TakeOver, ProxyDropCarrier, Board }
    private enum TileSelectPurpose { Drop, ProxyDrop }
    private enum CargoSelectPurpose { Drop, TakeOver, ProxyDrop }
    private UnitSelectPurpose unitSelectPurpose;
    private TileSelectPurpose tileSelectPurpose;
    private CargoSelectPurpose cargoSelectPurpose;

    private ActionContext context;              // いま行動中のユニットの文脈（移動取り消し用）
    private HashSet<Vector2Int> reachableCells; // 移動できるマス
    private Dictionary<Vector2Int, int> moveCosts = new Dictionary<Vector2Int, int>(); // 各マスまでの移動コスト（再移動の予算計算用）
    private List<Unit> attackTargets;           // いまの位置から攻撃できる敵
    private List<Unit> unitCandidates;          // 救出・引き受け・代わりに降ろすの相手候補
    private List<Vector2Int> tileCandidates;    // 降ろす先のマス候補
    private Unit proxyCarrier;                  // 「代わりに降ろす」で選んだ運び手
    private Unit takeOverCarrier;               // 「引き受け」で選んだ運び手（貨物選択のあいだ保持）
    private Unit selectedCargo;                 // 貨物リストで選んだ（または自動選択された）貨物
    private ActionMenu menu;                    // 行動メニュー（IMGUI）
    private CargoListMenu cargoMenu;            // 貨物リストメニュー（IMGUI・Phase 12）
    private BattleForecast forecast;            // 戦闘予測パネル（IMGUI・Phase 15）

    void Awake()
    {
        // メニューは自分と同じ GameObject に自動で用意する（シーンへの追加作業を不要にするため）
        menu = GetComponent<ActionMenu>();
        if (menu == null) menu = gameObject.AddComponent<ActionMenu>();
        cargoMenu = GetComponent<CargoListMenu>();
        if (cargoMenu == null) cargoMenu = gameObject.AddComponent<CargoListMenu>();
        forecast = GetComponent<BattleForecast>();
        if (forecast == null) forecast = gameObject.AddComponent<BattleForecast>();

        // デバッグ用のユニット情報パネル（クリックで能力値・状態を表示）。
        // 自分でクリックを読む独立部品なので、生成するだけでよい（呼び出しは不要）
        if (GetComponent<DebugUnitPanel>() == null) gameObject.AddComponent<DebugUnitPanel>();
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

        // メニュー表示中のセルクリックは無効。ボタンの処理は ActionMenu / CargoListMenu（IMGUI）側が
        // 行うので、ここで盤面まで反応すると二重処理になるのを防ぐ
        if (state == State.CommandMenu || state == State.CargoSelect) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

            if (grid.TryWorldToCell(worldPos, out Vector2Int cell))
                OnCellClicked(cell);
            else if (state == State.MoveSelect && !context.committed)
                CancelAll(); // 盤外クリックで選択解除（取り消し不能後・対象選択中は無視）
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
                    context.moveCostUsed += moveCosts[cell]; // 再移動の予算計算のため消費を記録
                    context.unit.MoveTo(grid, cell);
                    context.hasMoved = true; // 移動後 → 後衛武器は基本射程（2マスちょうど）のみ
                    OpenCommandMenu();
                }
                else if (!context.committed && IsSelectablePlayer(clickedUnit))
                {
                    CancelAll(); // 未確定の移動・飛翔を巻き戻してから
                    EnterMoveSelect(clickedUnit); // 別の味方へ選択切替
                }
                else if (!context.committed)
                {
                    CancelAll(); // 範囲外クリック＝選択解除（未確定の飛翔もここで取り消される）
                }
                else if (context.usedFlight)
                {
                    // 取り消し不能（救出済みなど）でも、確定前の飛翔だけは範囲外クリックで
                    // 取り消せる（作者仕様 2026-07-12）。選択解除はされず、飛翔前の状態に戻る
                    CancelFlightAndReturn();
                }
                // 取り消し不能で飛翔もしていないときは範囲外クリックを無視
                break;

            case State.TargetSelect:
                if (clickedUnit != null && attackTargets.Contains(clickedUnit))
                {
                    CombatSystem.ResolveAttack(context.unit, clickedUnit, grid);
                    FinishAction();
                }
                // 対象以外のクリックは無視（待機はメニューの「待機」から。誤クリックでの行動消費を防ぐ）
                break;

            case State.UnitTargetSelect:
                if (clickedUnit != null && unitCandidates.Contains(clickedUnit))
                {
                    switch (unitSelectPurpose)
                    {
                        case UnitSelectPurpose.Rescue: ExecuteRescue(clickedUnit); break;
                        case UnitSelectPurpose.TakeOver:
                            takeOverCarrier = clickedUnit;
                            EnterTakeOverCargoSelect();
                            break;
                        case UnitSelectPurpose.ProxyDropCarrier:
                            proxyCarrier = clickedUnit;
                            EnterProxyDropCargoSelect();
                            break;
                        case UnitSelectPurpose.Board: ExecuteBoard(clickedUnit); break;
                    }
                }
                break;

            case State.TileSelect:
                if (tileCandidates.Contains(cell))
                {
                    if (tileSelectPurpose == TileSelectPurpose.Drop) ExecuteDrop(cell);
                    else ExecuteProxyDrop(cell);
                }
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
            case State.UnitTargetSelect:
                // 対象選択 → メニューへ戻る（移動した位置はそのまま保持）
                OpenCommandMenu();
                break;

            case State.TileSelect:
                // マス選択からは1段階戻る。貨物リストを経由していた（貨物が複数だった）なら
                // リストへ、そうでなければさらに前の段階へ戻る
                if (tileSelectPurpose == TileSelectPurpose.ProxyDrop)
                {
                    if (RescueRules.GetProxyDropCargoes(context.unit, proxyCarrier).Count > 1)
                        EnterProxyDropCargoSelect();
                    else
                        EnterProxyCarrierSelect();
                }
                else
                {
                    // 降ろせる貨物が複数ならリストへ戻る（1体なら自動選択なのでメニューへ。Phase 15 から
                    // 「降ろせる貨物」で数える＝地形の都合で降ろせない貨物はリストに出ないため）
                    if (RescueRules.GetDroppableCargoes(context.unit, grid).Count > 1) EnterDropCargoSelect();
                    else OpenCommandMenu();
                }
                break;

            case State.CargoSelect:
                // 貨物リストから1段階戻る
                cargoMenu.Hide();
                switch (cargoSelectPurpose)
                {
                    case CargoSelectPurpose.Drop: OpenCommandMenu(); break;
                    case CargoSelectPurpose.TakeOver: EnterTakeOverCarrierSelect(); break;
                    case CargoSelectPurpose.ProxyDrop: EnterProxyCarrierSelect(); break;
                }
                break;

            case State.CommandMenu:
                // メニュー → 移動を取り消して、移動選択をやり直す
                // （救出・引き受けの後なら「取り消し不能点」の位置までしか戻らない）
                menu.Hide();
                context.RevertMove(grid);
                ShowMoveRange();
                break;

            case State.MoveSelect:
                if (context.usedFlight)
                    CancelFlightAndReturn(); // 飛翔の解除（1段階戻る）
                else if (context.committed)
                    OpenCommandMenu(); // 救出済みなどで選択解除はできない → メニューへ
                else
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
        ShowMoveRange();
        Debug.Log($"選択: {unit.Data.unitName}（移動できるマス {reachableCells.Count} 個）");
    }

    /// <summary>
    /// いまの文脈の残り移動力で移動範囲を計算・表示して MoveSelect に入る。
    /// 通常の選択時は移動力の全量、救出後の再移動では残量が予算になる。
    /// </summary>
    private void ShowMoveRange()
    {
        Unit unit = context.unit;
        reachableCells = MovementCalculator.GetReachableCells(grid, unit, context.RemainingMove, moveCosts);

        grid.ResetAllHighlights();
        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);
        foreach (Vector2Int c in reachableCells)
            grid.AddHighlight(c, HighlightKind.MoveRange);

        state = State.MoveSelect;
    }

    /// <summary>行動メニューを開く（移動直後・静止選択時・対象選択からの戻りで呼ばれる）。</summary>
    private void OpenCommandMenu()
    {
        Unit unit = context.unit;
        forecast.Hide(); // 対象選択から戻ったときは戦闘予測を消す（Phase 15）

        // 移動範囲・対象候補の表示を消し、選択マークだけ現在位置に付け直す
        grid.ResetAllHighlights();
        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);

        menu.Show(grid.CellToWorld(unit.GridPosition), grid.cellSize, BuildCommands());
        state = State.CommandMenu;
    }

    /// <summary>
    /// いまの文脈（context）で実行できるコマンドの一覧を作る。
    /// Phase 14 のコマンド（飛翔）はここに項目を足していく。
    /// </summary>
    private List<ActionMenu.Entry> BuildCommands()
    {
        var commands = new List<ActionMenu.Entry>();
        Unit unit = context.unit;

        // 救出を使った後は「再移動 → 待機」だけ（仕様：使用していない移動力分の再移動のみ）。
        // ただし飛行兵は救出の後にも飛翔できる（作者仕様変更 2026-07-12）
        if (context.usedRescue)
        {
            if (unit.Class == UnitClass.Flier && !unit.IsFlying)
                commands.Add(new ActionMenu.Entry("飛翔", ExecuteFlight));
            commands.Add(new ActionMenu.Entry("待機", FinishAction));
            return commands;
        }

        // 引き受けを使った後は「降ろす / 待機」だけ（合意(f)）。
        // 空中（飛翔中）で引き受けた場合は降ろせないので「待機」のみ（作者仕様 2026-07-12）
        if (context.usedTakeOver)
        {
            if (unit.IsRescuing && !unit.IsFlying && RescueRules.GetDroppableCargoes(unit, grid).Count > 0)
                commands.Add(new ActionMenu.Entry("降ろす", EnterDropCargoSelect));
            commands.Add(new ActionMenu.Entry("待機", FinishAction));
            return commands;
        }

        attackTargets = FindAttackTargets(unit, context.hasMoved);
        if (attackTargets.Count > 0)
            commands.Add(new ActionMenu.Entry("攻撃", EnterTargetSelect));

        // 飛翔：飛行兵が非飛翔中ならいつでも使える（移動前でも移動後でも。作者仕様変更 2026-07-12）。
        // 行動は消費せず、残り移動力ぶんの飛行移動ができる
        //（攻撃→行動終了の流れがあるので、戦闘後の飛翔は起こらない）
        if (unit.Class == UnitClass.Flier && !unit.IsFlying)
            commands.Add(new ActionMenu.Entry("飛翔", ExecuteFlight));

        // 着陸：飛翔中の飛行兵が使える。飛翔を発動したその行動（発動ターン）では使えない。
        // 使うと飛翔が解除され、即行動済みになる（作者仕様 2026-07-12）
        if (unit.IsFlying && !context.usedFlight)
            commands.Add(new ActionMenu.Entry("着陸", ExecuteLanding));

        // 救出：騎乗ユニットが、隣接する同陣営の歩兵を格納する（救出中の攻撃は可＝合意(d)。
        // 輸送隊は騎乗ユニットも救出できる＝Phase 12）
        List<Unit> rescueTargets = RescueRules.FindRescueTargets(unit, grid);
        if (rescueTargets.Count > 0)
            commands.Add(new ActionMenu.Entry("救出", () => EnterUnitTargetSelect(UnitSelectPurpose.Rescue, rescueTargets)));

        // 乗り込む：隣接する輸送隊に自分から格納される（Phase 12。自分は行動終了）
        List<Unit> transporters = RescueRules.FindBoardTransporters(unit, grid);
        if (transporters.Count > 0)
            commands.Add(new ActionMenu.Entry("乗り込む", () => EnterUnitTargetSelect(UnitSelectPurpose.Board, transporters)));

        // 降ろす：救出中で、貨物を降ろせる隣接マスがあるとき（救出と同一行動では不可＝合意(e)。
        // usedRescue のときは上で分岐済みなので、ここに来るのは前のターンに救出した場合）。
        // 飛翔中は降ろせない（空中では乗り降りできない。着地後に降ろせる。Phase 14）。
        // 降ろせるかは貨物の兵種ごとに判定する（Phase 15）
        if (unit.IsRescuing && !unit.IsFlying && RescueRules.GetDroppableCargoes(unit, grid).Count > 0)
            commands.Add(new ActionMenu.Entry("降ろす", EnterDropCargoSelect));

        // 引き受け：隣接する救出中の味方から貨物をもらう
        // （通常の騎乗ユニットは歩兵の貨物のみ。輸送隊は何でも＝Phase 12）
        if (RescueRules.FindTakeOverCarriers(unit, grid).Count > 0)
            commands.Add(new ActionMenu.Entry("引き受け", EnterTakeOverCarrierSelect));

        // 代わりに降ろす：歩兵が、隣接する救出中の味方の貨物を降ろしてあげる
        List<Unit> proxyCarriers = RescueRules.FindProxyDropCarriers(unit, grid);
        if (proxyCarriers.Count > 0)
            commands.Add(new ActionMenu.Entry("代わりに降ろす", EnterProxyCarrierSelect));

        commands.Add(new ActionMenu.Entry("待機", FinishAction));
        return commands;
    }

    /// <summary>「攻撃」が選ばれた：対象の敵を赤表示して、クリックを待つ。</summary>
    private void EnterTargetSelect()
    {
        menu.Hide();

        foreach (Unit enemy in attackTargets)
            grid.AddHighlight(enemy.GridPosition, HighlightKind.TargetChoice);

        // 対象にマウスを重ねると戦闘予測が出る（Phase 15）
        forecast.Show(context.unit, attackTargets, grid);

        state = State.TargetSelect;
        Debug.Log($"攻撃対象を選択（{attackTargets.Count} 体）。右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>救出・引き受け・代わりに降ろすの相手ユニットを赤表示して、クリックを待つ。</summary>
    private void EnterUnitTargetSelect(UnitSelectPurpose purpose, List<Unit> candidates)
    {
        menu.Hide();
        unitSelectPurpose = purpose;
        unitCandidates = candidates;

        foreach (Unit u in candidates)
            grid.AddHighlight(u.GridPosition, HighlightKind.TargetChoice);

        state = State.UnitTargetSelect;
        Debug.Log($"相手を選択（{candidates.Count} 体）。右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>「代わりに降ろす」の最初の段階：どの運び手の貨物を降ろすかを選ぶ。</summary>
    private void EnterProxyCarrierSelect()
    {
        // マス選択・貨物選択から戻ってきた場合もあるので、候補を取り直してハイライトも付け直す
        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);
        EnterUnitTargetSelect(UnitSelectPurpose.ProxyDropCarrier,
                              RescueRules.FindProxyDropCarriers(context.unit, grid));
    }

    /// <summary>「引き受け」の最初の段階：どの運び手からもらうかを選ぶ。</summary>
    private void EnterTakeOverCarrierSelect()
    {
        // 貨物選択から戻ってきた場合もあるので、候補を取り直してハイライトも付け直す
        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);
        EnterUnitTargetSelect(UnitSelectPurpose.TakeOver,
                              RescueRules.FindTakeOverCarriers(context.unit, grid));
    }

    // ===== 貨物選択（Phase 12）=====
    // 貨物が1体だけなら自動選択してリストは出さない。複数のときだけ CargoListMenu を開く。

    /// <summary>
    /// 「降ろす」の貨物選択：自分の貨物からどれを降ろすかを選ぶ。
    /// 降ろせるマスが無い貨物はリストに出さない（騎乗の貨物は山側に降ろせない等。Phase 15）。
    /// </summary>
    private void EnterDropCargoSelect()
    {
        Unit unit = context.unit;
        List<Unit> droppable = RescueRules.GetDroppableCargoes(unit, grid);
        if (droppable.Count == 1)
        {
            selectedCargo = droppable[0];
            EnterDropTileSelect();
            return;
        }
        ShowCargoList(CargoSelectPurpose.Drop, unit, droppable,
                      cargo => { cargoMenu.Hide(); selectedCargo = cargo; EnterDropTileSelect(); });
    }

    /// <summary>「引き受け」の貨物選択：運び手の貨物からどれをもらうかを選ぶ。</summary>
    private void EnterTakeOverCargoSelect()
    {
        List<Unit> cargoes = RescueRules.GetTakeOverCargoes(context.unit, takeOverCarrier);
        if (cargoes.Count == 1)
        {
            ExecuteTakeOver(cargoes[0]);
            return;
        }
        ShowCargoList(CargoSelectPurpose.TakeOver, takeOverCarrier, cargoes,
                      cargo => { cargoMenu.Hide(); ExecuteTakeOver(cargo); });
    }

    /// <summary>「代わりに降ろす」の貨物選択：運び手の貨物からどれを降ろすかを選ぶ。</summary>
    private void EnterProxyDropCargoSelect()
    {
        List<Unit> cargoes = RescueRules.GetProxyDropCargoes(context.unit, proxyCarrier);
        if (cargoes.Count == 1)
        {
            selectedCargo = cargoes[0];
            EnterProxyDropTileSelect();
            return;
        }
        ShowCargoList(CargoSelectPurpose.ProxyDrop, proxyCarrier, cargoes,
                      cargo => { cargoMenu.Hide(); selectedCargo = cargo; EnterProxyDropTileSelect(); });
    }

    /// <summary>貨物リストメニューを開いて CargoSelect 状態に入る（共通処理）。</summary>
    private void ShowCargoList(CargoSelectPurpose purpose, Unit anchor, List<Unit> cargoes,
                               System.Action<Unit> onSelect)
    {
        menu.Hide();
        cargoSelectPurpose = purpose;

        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);
        if (anchor != context.unit)
            grid.AddHighlight(anchor.GridPosition, HighlightKind.TargetChoice);

        cargoMenu.Show(grid.CellToWorld(anchor.GridPosition), grid.cellSize, cargoes, onSelect);
        state = State.CargoSelect;
        Debug.Log($"貨物を選択（{cargoes.Count} 体）。右クリック/ESCで戻る。");
    }

    /// <summary>「降ろす」が選ばれた：選んだ貨物を置ける隣の空きマスを赤表示して、クリックを待つ。</summary>
    private void EnterDropTileSelect()
    {
        menu.Hide();
        tileSelectPurpose = TileSelectPurpose.Drop;
        tileCandidates = RescueRules.GetDropCells(context.unit, selectedCargo, grid);

        foreach (Vector2Int c in tileCandidates)
            grid.AddHighlight(c, HighlightKind.TargetChoice);

        state = State.TileSelect;
        Debug.Log($"降ろすマスを選択（{tileCandidates.Count} マス）。右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>「代わりに降ろす」の2段階目：運び手の隣で、選んだ貨物を置けるマスを選ぶ。</summary>
    private void EnterProxyDropTileSelect()
    {
        tileSelectPurpose = TileSelectPurpose.ProxyDrop;
        tileCandidates = RescueRules.GetDropCellsAround(proxyCarrier.GridPosition, selectedCargo, grid);

        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);
        foreach (Vector2Int c in tileCandidates)
            grid.AddHighlight(c, HighlightKind.TargetChoice);

        state = State.TileSelect;
        Debug.Log($"降ろすマスを選択（{tileCandidates.Count} マス）。右クリック/ESCで運び手の選択へ戻る。");
    }

    // ===== 救出系コマンドの実行 =====

    /// <summary>救出を実行：相手を格納し、残り移動力での再移動へ移る（以後、取り消し不可）。</summary>
    private void ExecuteRescue(Unit target)
    {
        Unit unit = context.unit;
        unit.StoreUnit(target);
        context.usedRescue = true;
        context.Commit(); // ここが新しい「戻れる限界」になる

        Debug.Log($"{unit.Data.unitName} は {target.Data.unitName} を救出した（再移動できる移動力：{context.RemainingMove}）");

        ShowMoveRange();
        if (reachableCells.Count == 0)
            OpenCommandMenu(); // 動けるマスが無ければそのまま待機メニューへ
    }

    /// <summary>引き受けを実行：運び手から選んだ貨物をもらい、メニューへ（降ろす/待機のみ）。</summary>
    private void ExecuteTakeOver(Unit cargo)
    {
        Unit unit = context.unit;
        takeOverCarrier.RemoveCargo(cargo);
        unit.StoreUnit(cargo);
        context.usedTakeOver = true;
        context.Commit();

        Debug.Log($"{unit.Data.unitName} は {takeOverCarrier.Data.unitName} から {cargo.Data.unitName} を引き受けた");
        OpenCommandMenu();
    }

    /// <summary>乗り込むを実行：自分が輸送隊に格納され、行動を終える（輸送隊の行動は消費しない）。</summary>
    private void ExecuteBoard(Unit transporter)
    {
        Unit unit = context.unit;
        transporter.StoreUnit(unit);

        Debug.Log($"{unit.Data.unitName} は {transporter.Data.unitName} に乗り込んだ" +
                  $"（積載 {transporter.Carried.Count}/{transporter.CarryCapacity}）");
        FinishAction(); // 乗り込んだら自分の行動は終了（仕様）
    }

    // ===== 飛翔（Phase 14）=====

    /// <summary>
    /// 飛翔を実行：飛翔状態になり、行動は消費せず「残り移動力」で移動選択へ戻る。
    /// 移動前でも移動後でも使える（移動後なら残りの移動力ぶんだけ飛行移動できる）。
    /// 行動を確定する前なら右クリック/ESC で1段階ずつ取り消せる（OnCancelPressed）。
    /// </summary>
    private void ExecuteFlight()
    {
        Unit unit = context.unit;
        unit.StartFlight();
        context.MarkFlight(); // いまの位置を「取り消しの中間点」として記録

        Debug.Log($"{unit.Data.unitName} は飛翔した（発動ターンを含めて {unit.FlightTurnsLeft} ターン持続。" +
                  $"残り移動力 {context.RemainingMove}。行動確定前なら右クリック/ESCで取り消し可）");

        ShowMoveRange(); // 飛翔状態の移動範囲（コスト1・すり抜け）を残り移動力で表示
        if (reachableCells.Count == 0)
            OpenCommandMenu(); // 動けるマスが無ければそのままメニューへ（攻撃・待機など）
    }

    /// <summary>着陸を実行：飛翔状態を解除し、即行動済みになる（作者仕様 2026-07-12）。</summary>
    private void ExecuteLanding()
    {
        Unit unit = context.unit;
        unit.CancelFlight();
        Debug.Log($"{unit.Data.unitName} は着陸した（行動終了）");
        FinishAction();
    }

    /// <summary>
    /// 確定前の飛翔を取り消して、飛翔する前の状態へ1段階戻る。
    /// 飛翔の前に移動していたならその位置のメニューへ、移動する前に飛翔していたなら
    /// 地上の移動範囲の表示へ戻る。右クリック/ESC と範囲外クリックの両方から呼ばれる
    /// （救出後の取り消し不能状態でも、飛翔だけはこの方法で取り消せる）。
    /// </summary>
    private void CancelFlightAndReturn()
    {
        context.unit.CancelFlight();
        context.UnmarkFlight();
        Debug.Log($"{context.unit.Data.unitName} は飛翔を取りやめた");

        if (context.hasMoved) OpenCommandMenu();
        else ShowMoveRange();
    }

    /// <summary>降ろすを実行：選んだ貨物を指定マスへ再配置し、行動を終える。</summary>
    private void ExecuteDrop(Vector2Int cell)
    {
        Unit unit = context.unit;
        Unit cargo = selectedCargo;
        unit.ReleaseUnitAt(cargo, grid, cell);
        cargo.SetActed(true); // 降ろされたユニットはそのターン行動済み（合意(b)）

        Debug.Log($"{unit.Data.unitName} は {cargo.Data.unitName} を {cell} に降ろした");
        FinishAction(); // 降ろしたら行動終了（作者合意）
    }

    /// <summary>代わりに降ろすを実行：運び手の選んだ貨物を指定マスへ再配置し、自分の行動を終える。</summary>
    private void ExecuteProxyDrop(Vector2Int cell)
    {
        Unit cargo = selectedCargo;
        proxyCarrier.ReleaseUnitAt(cargo, grid, cell);
        cargo.SetActed(true); // 降ろされたユニットはそのターン行動済み（合意(b)）

        Debug.Log($"{context.unit.Data.unitName} は {proxyCarrier.Data.unitName} の {cargo.Data.unitName} を {cell} に降ろした");
        FinishAction(); // 降ろしたら行動終了（作者合意）
    }

    // ===== 行動の終了・取り消し =====

    /// <summary>行動を確定して終える。ここまで来たら移動の取り消しはもうできない。</summary>
    private void FinishAction()
    {
        Unit acted = context != null ? context.unit : null;
        ClearSelection();

        if (acted != null) acted.SetActed(true);
        turnManager.NotifyUnitActed();
    }

    /// <summary>
    /// 行動を中断して選択を解除する（移動していれば元の位置へ戻す）。
    /// 確定前の飛翔もここで取り消される（作者仕様 2026-07-12：クリックでの選択解除＝飛翔もキャンセル）。
    /// </summary>
    private void CancelAll()
    {
        if (context != null)
        {
            if (context.usedFlight)
            {
                context.unit.CancelFlight();
                context.UnmarkFlight(); // 先に中間点を破棄 → RevertMove が行動開始位置まで戻す
                Debug.Log($"{context.unit.Data.unitName} は飛翔を取りやめた");
            }
            context.RevertMove(grid);
        }
        ClearSelection();
    }

    /// <summary>選択に関わる表示と内部状態をすべて片付ける（移動の取り消しはしない）。</summary>
    private void ClearSelection()
    {
        menu.Hide();
        cargoMenu.Hide();
        forecast.Hide();
        grid.ResetAllHighlights();
        context = null;
        reachableCells = null;
        attackTargets = null;
        unitCandidates = null;
        tileCandidates = null;
        proxyCarrier = null;
        takeOverCarrier = null;
        selectedCargo = null;
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
