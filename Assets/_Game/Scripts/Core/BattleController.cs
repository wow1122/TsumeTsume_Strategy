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
///  「降ろす」→ 隣の空きマスを選択【TileSelect】→ 再配置 → 残り移動力で再移動 → 待機
///  「引き受け」→ 隣の救出中の味方を選択 → 貨物をもらう → 降ろす/待機のみ
///  （引き受けた行動中に降ろせるのは、引き受けを実行したマスの上にいるときだけ）
///  「代わりに降ろす」→ 救出中の味方を選択 → その隣の空きマスを選択 → 行動終了
///  （徒歩ユニット専用。運び手は輸送隊のみ＝2026-07-26 の仕様変更）
///  救出・引き受けを実行すると「取り消し不能」になり、以後は選択解除できない
///  （選択解除できると移動力を使い直せてしまうため）。
///
/// Phase 12（輸送隊）で追加された流れ：
///  「乗り込む」→ 隣接する輸送隊を選択 → 自分が格納されて行動終了（輸送隊の行動は消費しない）
///  貨物が複数のとき、「降ろす」「引き受け」「代わりに降ろす」は貨物リスト【CargoSelect】から
///  対象を選ぶ（1体だけなら自動選択でリストは出ない）。
///
/// Phase 14 の「飛翔」「着陸」コマンドと、飛翔の段階的な取り消しは 2026-10-24 に撤廃した
/// （飛行兵は常に地形を無視して飛ぶ兵種になり、コマンドは要らなくなった）。
///
/// Phase 15 の追加：
///  攻撃の対象選択中、対象にマウスを重ねると戦闘予測（BattleForecast）が画面右下に出る。
///  「降ろす」系の対象・マスは貨物の兵種で判定する（騎乗の貨物は山に降ろせない等）。
///
/// フェーズ26〜27（交換）で追加された流れ：
///  「交換」→ 隣接する同陣営のユニットを選択【UnitTargetSelect】→ 交換画面【ExchangeSelect】。
///  交換画面は2列（左＝自分の持ち物・右＝相手の持ち物）で、どちらの行もクリックで反対側へ移る
///  （双方向・1回の交換で何個でも）→ 右クリック/ESC で交換を終えて行動メニューへ。
///  行動は消費しない（持ち物の操作と同じ）ので、そのまま攻撃・持ち物・待機を選べる。
///  ただし1個でも動かすと取り消し不能になる（救出・引き受けと同じ。相手の状態を
///  変えてしまうため巻き戻せない）。1個も動かさずに閉じたときは何も起きていない扱い。
///  相手は「隣接・同陣営・盤上（貨物は不可）」（判定は ExchangeRules）。
///
/// 2026-08-09 の追加（攻撃は「武器を選んでから相手を選ぶ」順）：
///  「攻撃」→【武器選択】どの武器で殴るかを選ぶ（武器1本でも必ず通る）→
///  【対象選択】その武器で狙える敵をクリック → その場で攻撃 → 行動終了。
///  「攻撃」コマンドが出る条件は「持っている武器のどれかで届く敵がいるか」（装備中の武器だけではない）。
///  武器の一覧で行にマウスを重ねると、その武器で狙える敵が盤面で赤く光る。
///  行をクリックするとその武器を装備し（＝この時点で持ち替えが確定。行動は消費しない）、対象選択へ進む。
///  敵にマウスを重ねると、画面右上に敵ユニット情報・右下に戦闘予測が出る（2026-08-16）。
///  右クリック/ESC で1段ずつ戻る（対象選択 → 武器選択 → 行動メニュー）。
///  必殺技のメモリが減るのは敵をクリックした瞬間なので、それまでに戻ればメモリは減らない。
///
/// 2026-10-24 の追加（メモリゲージで払う武器）：
///  強い武器は攻撃のたびにメモリゲージを払う（判定は WeaponMemoryRules）。払えない武器も装備はでき、
///  攻撃の武器一覧で「（メモリ不足）」の灰色行になって選べないだけ（持ち物の行にも同じ目印が付く）。
///  払った後にその武器を払えなくなっても自動では持ち替えない＝次の攻撃のときに払える武器を自分で選ぶ
///  （同日の作者仕様の見直し。自動で持ち替えるのは武器を選ぶ操作を持たない敵だけ）。
///  必殺技は「1本＋その武器の消費」を両方払える武器だけ選べる。
///  剣で攻撃した戦闘で相手を倒したら、行動を終える前に上下左右の1マスだけ動ける【CantoSelect】。
///  青いマスをクリックで移動、自分のマス・右クリック・ESC でその場に留まる。どちらでも行動は終わる。
///
/// 2026-08-15 の追加（コマンド一覧）：
///  ユニットがいないマスをクリックすると「状況／設定／終了」のコマンド一覧が出る【SystemMenu】。
///  「状況」はターン数・フェイズ・クリア条件・両軍の数のパネル、「設定」は将来の調整項目の枠
///  （どちらも見るだけ【SystemInfo】）、「終了」は自軍フェイズの終了。
///  画面左上に常駐していたターン数の表示と「ターン終了」ボタンはこれに置き換えて撤去した。
///  右クリック/ESC で1段ずつ戻る（パネル → コマンド一覧 → 閉じる）。
///
/// 2026-07-26 の仕様変更（救出系の2点）：
///  ・引き受けた行動中に「降ろす」を選べるのは、引き受けを実行したマスの上にいるときだけ
///    （引き受け後に移動した先では降ろせない。引き受けたマスへ戻れば また選べる）。
///  ・「降ろす」の後も、使っていない移動力が残っていればその範囲で再移動できる
///    （降ろした時点では行動終了にならず、待機で確定する。「代わりに降ろす」は従来どおり即終了）。
/// </summary>
public class BattleController : MonoBehaviour
{
    [Tooltip("グリッド（GridManager）への参照")]
    public GridManager grid;

    [Tooltip("ターン管理（TurnManager）への参照")]
    public TurnManager turnManager;

    // 操作の状態（ハイライト色の設定は GridManager に集約した）
    private enum State { Idle, MoveSelect, CommandMenu, WeaponSelect, TargetSelect, UnitTargetSelect, TileSelect, CargoSelect, DamageCheck, ItemSelect, ItemActionSelect, ExchangeSelect, RefreshMenu, SystemMenu, SystemInfo, CantoSelect }
    private State state = State.Idle;

    // ユニット選択（UnitTargetSelect）・マス選択（TileSelect）・貨物選択（CargoSelect）が「どのコマンドのためか」
    private enum UnitSelectPurpose { Rescue, TakeOver, ProxyDropCarrier, Board, Heal, Exchange }
    private enum TileSelectPurpose { Drop, ProxyDrop }
    private enum CargoSelectPurpose { Drop, TakeOver, ProxyDrop }
    private UnitSelectPurpose unitSelectPurpose;
    private TileSelectPurpose tileSelectPurpose;
    private CargoSelectPurpose cargoSelectPurpose;

    private ActionContext context;              // いま行動中のユニットの文脈（移動取り消し用）
    private HashSet<Vector2Int> reachableCells; // 移動できるマス
    private Dictionary<Vector2Int, int> moveCosts = new Dictionary<Vector2Int, int>(); // 各マスまでの移動コスト（再移動の予算計算用）
    private List<Unit> attackTargets;           // 攻撃できる敵（メニュー＝全武器の合算／対象選択＝選んだ武器ぶん）
    private bool criticalPending;               // 武器・対象選択中の攻撃が必殺技かどうか（フェーズ29）

    /// <summary>
    /// 武器の消費とは別に一緒に払うメモリ（必殺技なら1本＝30、通常の攻撃なら0）。
    /// 武器選択の「メモリ不足」と、その武器で狙える敵の判定に渡す（2026-10-24）。
    /// </summary>
    private int CriticalExtraCost => criticalPending ? GaugeRules.CriticalMemoryCost : 0;
    private List<WeaponData> weaponChoices;     // 武器選択の候補（並びは所持品の順・2026-08-09）
    private int hoveredWeaponRow = -1;          // 武器一覧でいま赤表示を出している行（-1＝まだ無し）
    private readonly Rect[] avoidPanelBuffer = new Rect[2]; // 武器選択のUIが避けるパネルの矩形（使い回し）
    private List<Unit> unitCandidates;          // 救出・引き受け・代わりに降ろすの相手候補
    private List<Vector2Int> tileCandidates;    // 降ろす先のマス候補
    private Unit proxyCarrier;                  // 「代わりに降ろす」で選んだ運び手
    private Unit takeOverCarrier;               // 「引き受け」で選んだ運び手（貨物選択のあいだ保持）
    private Unit selectedCargo;                 // 貨物リストで選んだ（または自動選択された）貨物
    private Unit exchangePartner;               // 「交換」で選んだ相手（交換画面のあいだ保持・フェーズ26）
    private Unit refreshCandidate;              // 「再行動」のメニューを出している行動済みの味方（フェーズ30）
    private HashSet<Vector2Int> cantoCells;     // 剣で敵を倒した後に1マスだけ動けるマス（2026-10-24）
    private int exchangedCount;                 // この交換で動かした品の数（0なら「無かったこと」にする）
    private ActionMenu menu;                    // 行動メニュー（IMGUI）
    private CargoListMenu cargoMenu;            // 貨物リストメニュー（IMGUI・Phase 12）
    private ItemListMenu itemMenu;              // 所持品リストメニュー（IMGUI・フェーズ23）
    private BattleForecast forecast;            // 戦闘予測パネル（IMGUI・Phase 15）
    private UnitInfoPanel unitInfo;             // ユニット情報パネル（IMGUI・右上・2026-08-16 に統合）
    private SystemInfoPanel systemInfo;         // 「状況」「設定」のパネル（IMGUI・2026-08-15）
    private Vector2Int systemMenuCell;          // コマンド一覧を出している空きマス（パネルから戻る先）
    private Unit previewedEnemy;                // 行動範囲を下見表示中の敵（Idle のときだけ使う）

    void Awake()
    {
        // メニューは自分と同じ GameObject に自動で用意する（シーンへの追加作業を不要にするため）
        menu = GetComponent<ActionMenu>();
        if (menu == null) menu = gameObject.AddComponent<ActionMenu>();
        cargoMenu = GetComponent<CargoListMenu>();
        if (cargoMenu == null) cargoMenu = gameObject.AddComponent<CargoListMenu>();
        itemMenu = GetComponent<ItemListMenu>();
        if (itemMenu == null) itemMenu = gameObject.AddComponent<ItemListMenu>();
        forecast = GetComponent<BattleForecast>();
        if (forecast == null) forecast = gameObject.AddComponent<BattleForecast>();
        // ユニット情報パネル（画面右上・2026-08-16 にデバッグ用パネルと統合）。
        // 盤上クリックでの表示は自分でクリックを読むので呼び出し不要。ここから呼ぶのは
        // 相手選択中の Show / Hide と、パネル内クリックを盤面に流さないための判定だけ
        unitInfo = GetComponent<UnitInfoPanel>();
        if (unitInfo == null) unitInfo = gameObject.AddComponent<UnitInfoPanel>();

        // 敵AIの性格をデバッグ用に画面左下へ出すパネル（2026-08-16）。
        // 出す相手はユニット情報パネルから自分で読むので、ここからの呼び出しは無い
        if (GetComponent<AIDebugPanel>() == null) gameObject.AddComponent<AIDebugPanel>();

        // 敵全員の攻撃範囲を薄い赤紫で重ねる表示（Rキーで切り替え・2026-10-25）。
        // キーの読み取りも範囲の計算し直しも自分で行うので、ここからの呼び出しは無い
        if (GetComponent<DangerZoneView>() == null) gameObject.AddComponent<DangerZoneView>();

        // コマンド一覧の「状況」「設定」で出るパネル（2026-08-15）
        systemInfo = GetComponent<SystemInfoPanel>();
        if (systemInfo == null) systemInfo = gameObject.AddComponent<SystemInfoPanel>();
    }

    void Update()
    {
        if (grid == null || turnManager == null) return;

        // メニュー（行動メニュー・持ち物・貨物・コマンド一覧）を出しているか。
        // 下のセルクリック無効化に使う（IMGUIのボタンとクリックを取り合わないため）
        bool menuOpen = state == State.CommandMenu || state == State.CargoSelect
            || state == State.ItemSelect || state == State.ItemActionSelect
            || state == State.ExchangeSelect || state == State.RefreshMenu
            || state == State.WeaponSelect
            || state == State.SystemMenu || state == State.SystemInfo;

        // 決着後・敵フェイズ中は操作不可。行動の途中なら移動も取り消して選択を解除する
        if (turnManager.IsGameOver || turnManager.CurrentPhase != TurnPhase.Player)
        {
            if (state != State.Idle) CancelAll();
            else ClearEnemyPreview(); // 敵フェイズに入ったら敵の範囲プレビューも片付ける
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

        // メニュー表示中のセルクリックは無効。ボタンの処理は ActionMenu / CargoListMenu /
        // ItemListMenu（IMGUI）側が行うので、ここで盤面まで反応すると二重処理になるのを防ぐ
        if (menuOpen) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();

            // ユニット情報パネルの上のクリックは盤面に流さない（パネル側でレイアウト切替に使う。2026-08-16）。
            // パネルは盤面の右の余白に出るので普段は当たらないが、ウィンドウを小さくすると盤面に重なる
            if (unitInfo.ContainsScreenPoint(screenPos)) return;

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
                {
                    ClearEnemyPreview();
                    EnterMoveSelect(clickedUnit);
                }
                // 行動済みの味方でも、覚醒が1本貯まっていれば「再行動」で動き直せる（フェーズ30）。
                // 攻撃の結果を見てから使うかどうか決められるように、行動を終えた後の入口にしてある
                else if (clickedUnit != null && clickedUnit.Faction == Faction.Player
                         && GaugeRules.CanRefresh(clickedUnit))
                {
                    ClearEnemyPreview();
                    EnterRefreshMenu(clickedUnit);
                }
                // 敵をクリック → その敵の移動できるマス（青）と攻撃可能マス（赤）を下見表示（2026-07-18 作者要望）
                else if (clickedUnit != null && clickedUnit.Faction == Faction.Enemy)
                {
                    ShowEnemyPreview(clickedUnit);
                }
                // ユニットがいないマス → コマンド一覧（状況・設定・終了）を出す（2026-08-15）
                else if (clickedUnit == null)
                {
                    ClearEnemyPreview();
                    OpenSystemMenu(cell);
                }
                // それ以外（行動済みで再行動もできない味方）→ プレビューを消す
                else
                {
                    ClearEnemyPreview();
                }
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
                    CancelAll(); // 未確定の移動を巻き戻してから
                    EnterMoveSelect(clickedUnit); // 別の味方へ選択切替
                }
                else if (!context.committed)
                {
                    CancelAll(); // 範囲外クリック＝選択解除
                }
                // 取り消し不能（救出済みなど）のときは範囲外クリックを無視
                break;

            case State.TargetSelect:
                // 武器はもう選んである（＝装備済み）ので、敵をクリックした時点で攻撃が実行される
                // （2026-08-09 の作者仕様。「実行」の確認は無い＝FEの古典的な流れ）
                if (clickedUnit != null && attackTargets.Contains(clickedUnit))
                    ExecuteAttack(clickedUnit);
                // 対象以外のクリックは無視（待機はメニューの「待機」から。誤クリックでの行動消費を防ぐ）
                break;

            case State.DamageCheck:
                // 被ダメ確認は見るだけのモード。クリックでは何もしない（戻るのは右クリック/ESC）
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
                        case UnitSelectPurpose.Heal: ExecuteHeal(clickedUnit); break;
                        case UnitSelectPurpose.Exchange: EnterExchange(clickedUnit); break;
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

            case State.CantoSelect:
                // 剣で敵を倒した後の1マス再移動（2026-10-24）。青いマスなら動いて終わり、
                // 自分のマスならその場に留まって終わり。それ以外のクリックは無視（誤クリックで留まらないように）
                if (cantoCells.Contains(cell)) FinishCanto(cell);
                else if (cell == context.unit.GridPosition) FinishCanto(null);
                break;
        }
    }

    // ===== キャンセル系統 =====

    /// <summary>右クリック / ESC が押されたとき、いまの状態から1段階だけ戻る。</summary>
    private void OnCancelPressed()
    {
        switch (state)
        {
            case State.Idle:
                ClearEnemyPreview(); // 敵の範囲プレビューを消すだけ（選択は無いので他に戻るものが無い）
                break;

            case State.RefreshMenu:
                // 再行動のメニュー → 何もせず Idle へ（覚醒はまだ使っていない。フェーズ30）
                menu.Hide();
                refreshCandidate = null;
                grid.ResetAllHighlights();
                state = State.Idle;
                break;

            case State.SystemMenu:
                // コマンド一覧 → 閉じるだけ（何も選ばなかった。2026-08-15）
                menu.Hide();
                state = State.Idle;
                break;

            case State.SystemInfo:
                // 状況・設定のパネル → コマンド一覧へ1段戻る（見ていただけなので何も変わらない）
                systemInfo.Hide();
                OpenSystemMenu(systemMenuCell);
                break;

            case State.CantoSelect:
                // 剣の撃破後の1マス再移動 → 動かずにその場で行動を終える（攻撃は済んでいるので戻る先は無い）
                FinishCanto(null);
                break;

            case State.UnitTargetSelect:
            case State.DamageCheck:
                // 相手選択・被ダメ確認 → メニューへ戻る（移動した位置はそのまま保持）
                OpenCommandMenu();
                break;

            case State.TargetSelect:
                // 攻撃の対象選択 → 武器選択へ1段戻る（2026-08-09）。
                // 選んだ武器は装備したまま（装備変更に取り消しは無い＝既存合意）だが、
                // 一覧から別の武器を選び直せる。必殺技のメモリはまだ使っていない
                EnterWeaponSelect(criticalPending);
                break;

            case State.WeaponSelect:
                // 武器選択 → 行動メニューへ1段戻る
                itemMenu.Hide();
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

            case State.ExchangeSelect:
                // 交換を終えて行動メニューへ戻る（1段）。品を1個でも動かしていれば
                // すでに取り消し不能点を打ってあるので、以後は選択解除できない
                EndExchange();
                break;

            case State.ItemActionSelect:
                // 行動サブメニュー（列2）だけ閉じて持ち物リストへ戻る（リスト・詳細は残す・1段）
                itemMenu.ClearActions();
                state = State.ItemSelect;
                break;

            case State.ItemSelect:
                // 持ち物 → メニューへ戻る（すでに装備を変えていても、それは元に戻さない＝
                // 装備変更に取り消しは無い。作者合意(a)）
                itemMenu.Hide();
                OpenCommandMenu();
                break;

            case State.CommandMenu:
                // メニュー → 移動を取り消して、移動選択をやり直す
                // （救出・引き受けの後なら「取り消し不能点」の位置までしか戻らない）
                menu.Hide();
                context.RevertMove(grid);
                ShowMoveRange();
                break;

            case State.MoveSelect:
                if (context.committed)
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
    /// 行動済みの味方をクリックしたときに「再行動」だけのメニューを出す（フェーズ30）。
    /// 覚醒を1本使うと未行動に戻り、いつもどおり移動から選び直せる。
    /// 攻撃の結果を見てから使うか決められるように、行動を終えた後の入口にしてある。
    /// この時点ではまだ何も消費していないので、右クリック/ESC でそのままやめられる。
    /// </summary>
    private void EnterRefreshMenu(Unit unit)
    {
        refreshCandidate = unit;

        grid.ResetAllHighlights();
        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);

        var commands = new List<ActionMenu.Entry>
        {
            new ActionMenu.Entry("再行動", ExecuteRefresh),
        };
        menu.Show(grid.CellToWorld(unit.GridPosition), grid.cellSize, commands);

        state = State.RefreshMenu;
        Debug.Log($"{unit.Data.unitName}: 再行動できる（覚醒ゲージ {unit.Momentum.Stock} 本）。右クリック/ESCでやめる。");
    }

    /// <summary>
    /// 「再行動」が選ばれた（フェーズ30）：覚醒を1本使って行動済みを解除し、移動選択へ戻す。
    /// 覚醒が残っていれば、同じユニットが同じターンに何回でも使える（2026-10-25 作者指示で回数の制限を外した）。
    /// 移動力は新しい ActionContext ができることで全回復する（＝まっさらな1行動として扱う。
    /// 救出済みなどの制限もすべて解けるので、前の行動を引きずらない）。
    /// </summary>
    private void ExecuteRefresh()
    {
        Unit unit = refreshCandidate;
        refreshCandidate = null;

        // メニューを出してから状況が変わった場合の保険（覚醒を使い切った・倒された等）
        if (unit == null || !GaugeRules.CanRefresh(unit))
        {
            menu.Hide();
            grid.ResetAllHighlights();
            state = State.Idle;
            return;
        }

        unit.SpendMomentum();
        unit.SetActed(false); // 行動済みの暗い表示も元に戻る
        Debug.Log($"再行動！ {unit.Data.unitName} がもう一度動けるようになった" +
                  $"（覚醒ゲージの残り {unit.Momentum.Stock} 本）");

        EnterMoveSelect(unit);
    }

    // ===== コマンド一覧（状況・設定・終了）＝2026-08-15 の UI 刷新 =====
    // ユニットがいないマスをクリックすると出る「盤面のメニュー」。
    // 画面に常駐させていた「ターン終了」ボタン・ターン数の表示はここへ集約した。
    // 表示位置・見た目はユニットの行動メニューと同じ部品（ActionMenu）を使い回している。

    /// <summary>空きマスのクリックでコマンド一覧を開く（状況／設定／終了）。</summary>
    private void OpenSystemMenu(Vector2Int cell)
    {
        systemMenuCell = cell; // 「状況」「設定」から戻ってきたとき同じ位置に出し直す

        var commands = new List<ActionMenu.Entry>
        {
            new ActionMenu.Entry("状況", () => OpenSystemInfo(SystemInfoKind.Status)),
            new ActionMenu.Entry("設定", () => OpenSystemInfo(SystemInfoKind.Settings)),
            // 「終了」は一番下に置く（誤クリックでターンが終わる事故を防ぐ＝作者合意）
            new ActionMenu.Entry("終了", ExecuteEndTurn),
        };
        menu.Show(grid.CellToWorld(cell), grid.cellSize, commands);
        state = State.SystemMenu;
    }

    /// <summary>「状況」「設定」のパネルを開く（見るだけ。右クリック/ESC で一覧へ戻る）。</summary>
    private void OpenSystemInfo(SystemInfoKind kind)
    {
        menu.Hide();
        systemInfo.Show(kind, turnManager);
        state = State.SystemInfo;
    }

    /// <summary>「終了」：自軍フェイズを終えて敵フェイズへ（旧「ターン終了」ボタン）。</summary>
    private void ExecuteEndTurn()
    {
        menu.Hide();
        state = State.Idle;
        turnManager.EndPlayerPhase();
    }

    /// <summary>
    /// いまの文脈の残り移動力で移動範囲を計算・表示して MoveSelect に入る。
    /// 通常の選択時は移動力の全量、救出後の再移動では残量が予算になる。
    /// </summary>
    private void ShowMoveRange()
    {
        Unit unit = context.unit;

        // 行動メニューは必ず閉じる（2026-07-30 修正）。「メニューの項目からそのまま移動選択へ戻る」
        // コマンドで閉じ忘れると、前に作ったメニューが残って押せてしまう（当時は飛翔で起きた。
        // 飛翔は 2026-10-24 に撤廃）。移動選択の最中にメニューを出す場面は無いので、ここで一括して閉じる。
        menu.Hide();

        unitInfo.Hide(); // 相手選択から移動選択へ移る（救出の直後など）ときは相手情報を消す
        reachableCells = MovementCalculator.GetReachableCells(grid, unit, context.RemainingMove, moveCosts);

        grid.ResetAllHighlights();

        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);
        foreach (Vector2Int c in reachableCells)
            grid.AddHighlight(c, HighlightKind.MoveRange);

        // 攻撃可能マス（2026-07-18 作者要望）: 立てるマスから武器で届くマスを赤で示す。
        // 救出・引き受け・降ろすの後は攻撃・杖コマンド自体が出ない（BuildCommands と同じ条件）ので表示しない。
        // 杖ユニットは攻撃できないので、攻撃フリンジの代わりに「杖が届くマス」を緑で示す（Phase 25・排他）
        if (!context.usedRescue && !context.usedTakeOver && !context.usedDrop)
        {
            if (StaffRules.HasStaffEquipped(unit))
            {
                foreach (Vector2Int c in ComputeHealCells(unit, reachableCells))
                    grid.AddHighlight(c, HighlightKind.HealRange);
            }
            else
            {
                foreach (Vector2Int c in ComputeAttackCells(unit, reachableCells))
                    grid.AddHighlight(c, HighlightKind.AttackRange);
            }
        }

        state = State.MoveSelect;
    }

    // ===== 敵の行動範囲プレビュー（2026-07-18 作者要望）=====

    /// <summary>
    /// クリックした敵の移動できるマス（青）と攻撃可能マス（赤）を下見表示する。
    /// 見るだけの機能で、選択状態（Idle のまま）も敵の状態も一切変えない。
    /// 別のマスのクリック・右クリック/ESC・味方の選択・敵フェイズ開始で消える。
    /// </summary>
    private void ShowEnemyPreview(Unit enemy)
    {
        previewedEnemy = enemy;

        HashSet<Vector2Int> reachable = MovementCalculator.GetReachableCells(grid, enemy);

        grid.ResetAllHighlights();
        grid.AddHighlight(enemy.GridPosition, HighlightKind.Selection);
        foreach (Vector2Int c in reachable)
            grid.AddHighlight(c, HighlightKind.MoveRange);
        foreach (Vector2Int c in ComputeAttackCells(enemy, reachable))
            grid.AddHighlight(c, HighlightKind.AttackRange);
    }

    /// <summary>敵の範囲プレビューを消す（表示していなければ何もしない）。</summary>
    private void ClearEnemyPreview()
    {
        if (previewedEnemy == null) return;
        previewedEnemy = null;
        grid.ResetAllHighlights();
    }

    /// <summary>行動メニューを開く（移動直後・静止選択時・対象選択からの戻りで呼ばれる）。</summary>
    private void OpenCommandMenu()
    {
        Unit unit = context.unit;
        forecast.Hide();        // 対象選択から戻ったときは戦闘予測を消す（Phase 15）
        unitInfo.Hide();        // 相手選択から戻ったときは相手情報も消す（2026-07-26）
        criticalPending = false; // 必殺技の対象選択から戻ったら通常の攻撃に戻す（フェーズ29）

        // 移動範囲・対象候補の表示を消し、選択マークだけ現在位置に付け直す
        grid.ResetAllHighlights();
        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);

        menu.Show(grid.CellToWorld(unit.GridPosition), grid.cellSize, BuildCommands());
        state = State.CommandMenu;
    }

    /// <summary>
    /// いまの文脈（context）で実行できるコマンドの一覧を作る。
    /// </summary>
    private List<ActionMenu.Entry> BuildCommands()
    {
        var commands = new List<ActionMenu.Entry>();
        Unit unit = context.unit;

        // 救出を使った後は「再移動 → 待機」だけ（仕様：使用していない移動力分の再移動のみ）
        if (context.usedRescue)
        {
            commands.Add(new ActionMenu.Entry("待機", FinishAction));
            return commands;
        }

        // 降ろすを使った後は「再移動 → 待機」だけ（2026-07-26 作者仕様：降ろしても残りの移動力は使える）。
        // 引き受けの後に降ろした場合もここに来るので、usedTakeOver より先に判定する
        if (context.usedDrop)
        {
            commands.Add(new ActionMenu.Entry("待機", FinishAction));
            return commands;
        }

        // 引き受けを使った後は「降ろす / 待機」だけ。
        // 降ろせるのは引き受けを実行したマスの上にいるときだけ（2026-07-26 作者仕様：
        // 引き受けた後に移動した先では降ろせない。引き受けたマスへ戻れば また降ろせる）
        if (context.usedTakeOver)
        {
            if (context.IsAtTakeOverCell && unit.IsRescuing
                && RescueRules.GetDroppableCargoes(unit, grid).Count > 0)
                commands.Add(new ActionMenu.Entry("降ろす", EnterDropCargoSelect));

            commands.Add(new ActionMenu.Entry("待機", FinishAction));
            return commands;
        }

        // 攻撃コマンドを出す条件は「持っている武器のどれかで届く敵がいるか」（2026-08-09 の合意。
        // 装備中の武器基準ではない＝別の武器でしか届かない敵も対象になる）
        attackTargets = FindAttackTargets(unit, context.hasMoved);
        if (attackTargets.Count > 0)
        {
            commands.Add(new ActionMenu.Entry("攻撃", () => EnterWeaponSelect(false)));

            // 必殺技（フェーズ29）: メモリが1本以上貯まっているときだけ「攻撃」の直後に出る。
            // 武器・対象の選び方は攻撃とまったく同じで、ダメージだけ倍率ぶん増える。
            // メモリは敵をクリックした瞬間に1本減る（それまでに戻れば減らない）。
            // 2026-10-24 から武器のメモリ消費も一緒に払うので、「1本＋その武器の消費」を
            // 払える武器で届く敵がいるときだけ出す（鉄の武器なら1本ちょうどで撃てる）
            if (GaugeRules.CanUseCritical(unit)
                && FindAttackTargets(unit, context.hasMoved, GaugeRules.CriticalMemoryCost).Count > 0)
                commands.Add(new ActionMenu.Entry("必殺技", () => EnterWeaponSelect(true)));

            // 被ダメ確認（2026-07-18 作者要望）: 攻撃できる相手から「逆に攻撃されたら」の
            // 予測を見る。見るだけで行動は消費しない
            commands.Add(new ActionMenu.Entry("被ダメ確認", EnterDamageCheck));
        }

        // 杖（Phase 25）：杖を装備していて、回復できる相手が射程内にいるとき。攻撃と同じ位置に出す
        //（杖ユニットは攻撃できない＝上の攻撃ブロックは出ないので、実質「攻撃」の代わりになる）。
        // 回復対象は緑ハイライトで選び、クリックで（魔力＋威力）回復して行動終了（合意(b)）
        if (StaffRules.HasStaffEquipped(unit))
        {
            List<Unit> healTargets = StaffRules.FindHealTargets(unit);
            if (healTargets.Count > 0)
                commands.Add(new ActionMenu.Entry("杖",
                    () => EnterUnitTargetSelect(UnitSelectPurpose.Heal, healTargets, HighlightKind.HealRange)));
        }

        // 救出：騎乗ユニットが、隣接する同陣営の徒歩ユニットを格納する（救出中の攻撃は可＝合意(d)。
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
        // 降ろせるかは貨物の兵種ごとに判定する（Phase 15）
        if (unit.IsRescuing && RescueRules.GetDroppableCargoes(unit, grid).Count > 0)
            commands.Add(new ActionMenu.Entry("降ろす", EnterDropCargoSelect));

        // 引き受け：隣接する救出中の味方から貨物をもらう
        // （通常の騎乗ユニットは徒歩の貨物のみ。輸送隊は何でも＝Phase 12）
        if (RescueRules.FindTakeOverCarriers(unit, grid).Count > 0)
            commands.Add(new ActionMenu.Entry("引き受け", EnterTakeOverCarrierSelect));

        // 代わりに降ろす：徒歩ユニットが、隣接する輸送隊の貨物を降ろしてあげる
        // （2026-07-26 の仕様変更で運び手は輸送隊のみ。通常の騎乗ユニットの貨物は騎乗ユニットの
        //   「引き受け」でしか動かせない）
        List<Unit> proxyCarriers = RescueRules.FindProxyDropCarriers(unit, grid);
        if (proxyCarriers.Count > 0)
            commands.Add(new ActionMenu.Entry("代わりに降ろす", EnterProxyCarrierSelect));

        // 持ち物：武器の持ち替え・外す（非武装になる）・道具の使用・破棄（捨てる）の入口。
        // 持ち物を1つでも持っているときに出す。装備変更・破棄は行動を消費しない。救出・引き受けの
        // 後は上で別メニューに分岐済みなのでここには来ない＝それらの制限（合意）を保つ
        if (unit.Items.Count > 0)
            commands.Add(new ActionMenu.Entry("持ち物", EnterItemSelect));

        // 交換：隣接する同陣営のユニットと持ち物をやり取りする（フェーズ26〜27）。
        // 行動は消費しない（合意(a)＝持ち物の操作と同じ）が、1個でも動かすと
        // その行動は取り消せなくなる（合意(c)＝救出・引き受けと同じ理屈）。
        // 救出・引き受け・降ろすの後は上で別メニューに分岐済みなのでここには来ない＝既存の制限を保つ。
        // 候補は双方向で判定する（フェーズ27）＝「渡せる」か「もらえる」かのどちらかができれば出す。
        // 持ち物が7個でいっぱいの相手や、持ち物0個の自分（輸送隊など）でも交換を開ける
        List<Unit> exchangeTargets = ExchangeRules.FindExchangePartners(unit, grid);
        if (exchangeTargets.Count > 0)
            commands.Add(new ActionMenu.Entry("交換",
                () => EnterUnitTargetSelect(UnitSelectPurpose.Exchange, exchangeTargets)));

        commands.Add(new ActionMenu.Entry("待機", FinishAction));
        return commands;
    }

    // ===== 武器選択 → 対象選択（2026-08-09 作者仕様）=====
    // 攻撃は「武器を選んでから相手を選ぶ」順（FEの古典的な流れ）。武器を1本しか持たない
    // ユニットでも一覧は必ず通る。持ち替えは行動を消費しないので「持ち物」で持ち替えてから
    // 攻撃するのと結果は同じだが、相手に合わせて特効武器へ持ち替える手間（メニューを2往復）が無くなる。

    /// <summary>
    /// 「攻撃」「必殺技」が選ばれた：どの武器で殴るかを選ぶ段階に入る（相手はまだ決めない）。
    /// 対象選択から1段戻ってきたときもここへ来る＝武器を選び直せる。
    /// 入った瞬間から「最初の押せる武器で狙える敵」を赤く出しておく
    /// （戦闘予測・情報パネルと同じ「入った瞬間から最初の候補を見せる」作法）。
    /// </summary>
    private void EnterWeaponSelect(bool critical)
    {
        Unit unit = context.unit;
        menu.Hide();
        forecast.Hide(); // 対象選択から戻ったときは戦闘予測と相手情報を消す
        unitInfo.Hide();
        criticalPending = critical;
        weaponChoices = WeaponChoiceRules.GetAttackWeapons(unit);

        // 候補が0本になることは無い（「攻撃」は武器で届く敵がいるときだけ出る）が、
        // 万一そうなったらメニューへ戻して知らせる
        if (weaponChoices.Count == 0)
        {
            Debug.LogWarning($"{unit.Data.unitName}: 攻撃に使える武器が見つからない（メニューへ戻る）");
            OpenCommandMenu();
            return;
        }

        // 「持ち物」で見た性能詳細が右下に残っていると邪魔なので、いったん片付ける（交換と同じ作法）
        itemMenu.Hide();

        List<ItemListMenu.Entry> rows = BuildWeaponEntries();
        itemMenu.Show(grid.CellToWorld(unit.GridPosition), grid.cellSize,
                      rows, OnWeaponHovered, AvoidPanelRects);

        // 押せる行が1つも無ければ選択マークだけ（武器 null＝狙える敵ゼロとして塗る）
        hoveredWeaponRow = rows.FindIndex(row => row.enabled);
        ShowWeaponTargets(hoveredWeaponRow >= 0 ? weaponChoices[hoveredWeaponRow] : null);

        state = State.WeaponSelect;
        Debug.Log($"{(critical ? "必殺技" : "攻撃")}の武器を選択（{weaponChoices.Count} 本）。" +
                  "右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>
    /// 武器一覧の行を作る。ラベルは武器名＋状態で、使えない武器は理由を添えた灰色の無効行にする
    /// （なぜ選べないかがその場で分かるように。持ち物リストの「装備不可」と同じ考え方）。
    /// 押せるかどうかは相手ではなく武器ごとに決まる＝その武器で狙える敵が1体でもいるか
    /// （相手より先に武器を選ぶため。判定は WeaponChoiceRules.GetUnusableReason）。
    /// メモリで払えない武器は「（メモリ不足）」の灰色行（必殺技なら「1本＋武器の消費」で判定。2026-10-24）。
    /// ホバー詳細（detailLines）は付けない＝行にマウスを重ねたときは盤面の赤表示で見せるため。
    /// </summary>
    private List<ItemListMenu.Entry> BuildWeaponEntries()
    {
        var entries = new List<ItemListMenu.Entry>();
        Unit unit = context.unit;

        foreach (WeaponData weapon in weaponChoices)
        {
            string reason = WeaponChoiceRules.GetUnusableReason(unit, weapon, context.hasMoved, CriticalExtraCost);

            string label = weapon.DisplayName;
            if (reason != null) label += $"（{reason}）";
            else if (weapon == unit.EquippedWeapon) label += "（装備中）";

            entries.Add(new ItemListMenu.Entry(label, reason == null, () => EnterTargetSelect(weapon)));
        }

        return entries;
    }

    /// <summary>
    /// 武器一覧の行にマウスが乗ったとき：その武器で狙える敵を盤面に赤く出す
    /// （相手を先に選べなくなったぶん、どの武器で誰に届くのかをここで見せる。作者合意 2026-08-09）。
    /// 塗り直すのは行が変わったときだけ＝IMGUI は毎フレーム呼ばれるので、
    /// そのたびに対象さがしとハイライトの張り直しをすると無駄が大きい。
    /// </summary>
    private void OnWeaponHovered(int index)
    {
        if (state != State.WeaponSelect) return;
        if (weaponChoices == null || index < 0 || index >= weaponChoices.Count) return;
        if (index == hoveredWeaponRow) return;

        hoveredWeaponRow = index;
        ShowWeaponTargets(weaponChoices[index]);
    }

    /// <summary>
    /// 武器が決まった：その武器を装備し、狙える敵を赤表示してクリックを待つ。
    /// **装備はここで切り替わる**（作者合意・FE準拠）＝この後キャンセルしてもその武器のまま。
    /// 装備変更は行動を消費しないので、そのまま別のコマンドへ進んでも構わない
    /// （既存の「装備変更に取り消しは無い」合意どおり）。
    /// 威力・三すくみ・特効・挟撃の資格は、装備を変えた時点で
    /// 既存の Unit.Weapon 窓口を通じて全部この武器のものになる。
    /// </summary>
    private void EnterTargetSelect(WeaponData weapon)
    {
        Unit unit = context.unit;
        itemMenu.Hide();

        if (weapon != unit.EquippedWeapon)
        {
            unit.Equip(weapon);
            Debug.Log($"{unit.Data.unitName} は {weapon.DisplayName} に持ち替えた（行動は消費しない）");
        }

        attackTargets = WeaponChoiceRules.FindTargets(unit, weapon, context.hasMoved, CriticalExtraCost);
        ShowAttackTargets(attackTargets);

        // 敵にマウスを重ねると、画面右上に敵ユニット情報・右下に戦闘予測が出る
        // （2026-08-16 にユニット情報が右上へ移ったので、右下での縦積みは不要になった）。
        // 移動したかも渡して、後衛武器の静止撃ち有利を実戦と同じ数字で見せる（2026-07-30 作者仕様）
        forecast.Show(unit, attackTargets, grid, criticalPending, context.hasMoved);
        unitInfo.Show(attackTargets, "攻撃する相手", grid);

        state = State.TargetSelect;
        Debug.Log($"{(criticalPending ? "必殺技" : "攻撃")}の対象を選択（{attackTargets.Count} 体）。" +
                  "右クリック/ESCで武器選択へ戻る。");
    }

    /// <summary>その武器で狙える敵を赤く塗る（武器が null なら選択マークだけ＝狙える敵ゼロ）。</summary>
    private void ShowWeaponTargets(WeaponData weapon)
    {
        ShowAttackTargets(WeaponChoiceRules.FindTargets(context.unit, weapon, context.hasMoved, CriticalExtraCost));
    }

    /// <summary>選択マークを付け直し、渡した敵を赤（攻撃対象）で塗る。武器選択・対象選択で共用。</summary>
    private void ShowAttackTargets(List<Unit> targets)
    {
        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);

        foreach (Unit enemy in targets)
            grid.AddHighlight(enemy.GridPosition, HighlightKind.TargetChoice);
    }

    /// <summary>
    /// 武器の一覧が避ける矩形＝戦闘予測（右下）とユニット情報（右上）。
    /// 出ていないパネルは Rect.zero が返り、避ける対象から自然に外れる
    /// （武器選択中に戦闘予測が出ることは無いが、窓口を1つにしておく）。
    /// 呼ばれるのは一覧を開いた最初の描画の1回だけ＝以後この結果でパネルが動いても一覧は動かない
    /// （ItemListMenu が位置を覚える。動かすとクリックの最中に行が逃げて押せなくなる）。
    /// ※配列は使い回して余計なゴミを作らない
    /// </summary>
    private Rect[] AvoidPanelRects()
    {
        avoidPanelBuffer[0] = forecast.PanelRect;
        avoidPanelBuffer[1] = unitInfo.PanelRect;
        return avoidPanelBuffer;
    }

    /// <summary>
    /// 攻撃を実行する（敵をクリックした瞬間。2026-08-09 から「実行」の確認は無い）。
    /// 武器はもう装備してあるので、威力・三すくみ・特効・挟撃の資格は
    /// すべて選んだ武器で決まる。装備は攻撃の後もそのまま＝敵フェイズもこの武器で受ける（FE準拠）。
    /// 武器のメモリ消費を払ってその武器を払えなくなっても持ち替えない（2026-10-24 作者仕様の見直し。
    /// 次に攻撃するときに、武器の一覧で払える武器を選ぶ）。
    /// 必殺技のメモリはここで1本使う（敵をクリックする前に戻ればメモリは減らない）。
    /// 武器の消費は攻撃の後に CombatSystem が払う（武器を選んだ時点で「1本＋消費」を払えることは確認済み）。
    /// </summary>
    private void ExecuteAttack(Unit target)
    {
        Unit unit = context.unit;

        if (criticalPending)
        {
            int before = WeaponMemoryRules.MemoryOf(unit);
            unit.SpendMemory(GaugeRules.CriticalMemoryCost);
            Debug.Log($"{unit.Data.unitName} がメモリを1本使った（{before} → {WeaponMemoryRules.MemoryOf(unit)}）");
        }

        // 攻撃に使う武器を控えておく。剣の「撃破後1マス再移動」は実際に攻撃した武器で決める（2026-10-24）。
        // 味方は攻撃の後も装備が変わらないが、戦闘の後で装備が変わる仕組みが入っても判定がずれないように先に取る
        WeaponData usedWeapon = unit.Weapon;

        // 移動したかも渡す（後衛武器はマスから動かずに撃つと三すくみ有利。2026-07-30 作者仕様）
        CombatSystem.ResolveAttack(unit, target, grid, criticalPending, context.hasMoved);

        // 倒れた相手は Destroy 済みで「== null」が true になるので、そこも撃破として扱う（挟撃のとどめも含む）
        bool killed = target == null || !target.IsAlive;
        if (killed && CombatRules.GrantsStepAfterKill(usedWeapon) && TryEnterCanto()) return;

        FinishAction();
    }

    // ===== 剣の撃破後1マス再移動（2026-10-24 作者仕様）=====
    // 剣で攻撃した戦闘で相手を倒したら、上下左右の1マスだけ動ける（地形のコストは問わない）。
    // 動かずに終えてもよい（自分のマスをクリック・右クリック・ESC）。攻撃はもう済んでいるので取り消しは無い。
    // 敵AIは使わない（救出系などと同じく、当面プレイヤーだけ）。

    /// <summary>
    /// 剣で倒した後の1マス再移動に入る。動けるマスを青で出してクリックを待つ。
    /// 勝利が決まった（敵が残っていない）とき・動けるマスが無いときは入らずに false を返す
    /// （呼び出し側がそのまま行動を終える）。
    /// </summary>
    private bool TryEnterCanto()
    {
        Unit unit = context.unit;
        if (UnitRegistry.CountAlive(Faction.Enemy) == 0) return false; // 勝利が決まっている＝動く意味が無い

        List<Vector2Int> cells = MovementCalculator.GetStepCells(grid, unit);
        if (cells.Count == 0) return false;

        // 攻撃した位置を取り消し不能点にしておく（万一 CancelAll が走っても、攻撃前の位置へ巻き戻らないように）
        context.Commit(keepHasMoved: true);

        cantoCells = new HashSet<Vector2Int>(cells);
        forecast.Hide();
        unitInfo.Hide();
        grid.ResetAllHighlights();
        grid.AddHighlight(unit.GridPosition, HighlightKind.Selection);
        foreach (Vector2Int c in cells)
            grid.AddHighlight(c, HighlightKind.MoveRange);

        state = State.CantoSelect;
        Debug.Log($"{unit.Data.unitName} は剣で敵を倒したので、1マスだけ再移動できる" +
                  "（青いマスをクリック。自分のマス・右クリック・ESC でその場に留まる）");
        return true;
    }

    /// <summary>1マス再移動を終える。cell が null ならその場に留まる。どちらでも行動はここで終わる。</summary>
    private void FinishCanto(Vector2Int? cell)
    {
        Unit unit = context.unit;
        if (cell != null)
        {
            unit.MoveTo(grid, cell.Value);
            Debug.Log($"{unit.Data.unitName} は {cell.Value} へ1マス再移動した");
        }
        else
        {
            Debug.Log($"{unit.Data.unitName} はその場に留まった");
        }
        FinishAction();
    }

    /// <summary>
    /// 被ダメ確認（2026-07-18 作者要望）: 攻撃できる相手を赤表示し、ホバーした敵から
    /// 「逆に攻撃されたら何ダメージ受けるか」の予測パネルを出す。
    /// 見るだけのコマンドで行動は消費しない。右クリック/ESC でメニューへ戻る。
    /// </summary>
    private void EnterDamageCheck()
    {
        menu.Hide();

        foreach (Unit enemy in attackTargets)
            grid.AddHighlight(enemy.GridPosition, HighlightKind.TargetChoice);

        forecast.ShowIncoming(context.unit, attackTargets, grid);

        state = State.DamageCheck;
        Debug.Log($"被ダメ確認（{attackTargets.Count} 体）。右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>
    /// 救出・引き受け・代わりに降ろす・杖の相手ユニットを選ぶ。候補をハイライトしてクリックを待つ。
    /// ハイライト色は用途で変える（救出系＝赤の TargetChoice、杖＝緑の HealRange。既定は赤）。
    /// </summary>
    private void EnterUnitTargetSelect(UnitSelectPurpose purpose, List<Unit> candidates,
                                       HighlightKind highlight = HighlightKind.TargetChoice)
    {
        menu.Hide();
        unitSelectPurpose = purpose;
        unitCandidates = candidates;

        foreach (Unit u in candidates)
            grid.AddHighlight(u.GridPosition, highlight);

        // 相手の能力値・装備武器・持ち物・状態を画面右上に出す（2026-07-26 作者要望）。
        // 最初の候補から表示し、他の候補にマウスを重ねると切り替わる
        unitInfo.Show(candidates, UnitInfoTitle(purpose), grid);

        state = State.UnitTargetSelect;
        Debug.Log($"相手を選択（{candidates.Count} 体）。右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>相手情報パネルの見出し（どのコマンドの相手を選んでいるかを出す）。</summary>
    private static string UnitInfoTitle(UnitSelectPurpose purpose)
    {
        switch (purpose)
        {
            case UnitSelectPurpose.Rescue: return "救出する相手";
            case UnitSelectPurpose.TakeOver: return "引き受ける相手（運び手）";
            case UnitSelectPurpose.ProxyDropCarrier: return "代わりに降ろす相手（運び手）";
            case UnitSelectPurpose.Board: return "乗り込む先";
            case UnitSelectPurpose.Heal: return "杖の対象";
            case UnitSelectPurpose.Exchange: return "交換する相手";
            default: return "相手の情報";
        }
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

    /// <summary>貨物リストの情報パネルの見出し（降ろす・引き受け・代わりに降ろすで共通）。</summary>
    private const string CargoInfoTitle = "貨物の情報";

    /// <summary>
    /// 貨物リストメニューを開いて CargoSelect 状態に入る（共通処理）。
    /// cargoes は1体以上（呼び出し側が「2体以上のときだけ」リストを開く）。
    /// </summary>
    private void ShowCargoList(CargoSelectPurpose purpose, Unit anchor, List<Unit> cargoes,
                               System.Action<Unit> onSelect)
    {
        menu.Hide();
        cargoSelectPurpose = purpose;

        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);
        if (anchor != context.unit)
            grid.AddHighlight(anchor.GridPosition, HighlightKind.TargetChoice);

        // 貨物の情報を右上に出す（2026-07-26 作者要望）。リストが出るのは貨物が2体以上のときだけで、
        // 先頭の貨物から表示し、行にマウスを重ねるとその貨物へ切り替わる
        unitInfo.Show(cargoes[0], CargoInfoTitle);

        cargoMenu.Show(grid.CellToWorld(anchor.GridPosition), grid.cellSize, cargoes, onSelect,
                       cargo => unitInfo.Show(cargo, CargoInfoTitle));
        state = State.CargoSelect;
        Debug.Log($"貨物を選択（{cargoes.Count} 体）。右クリック/ESCで戻る。");
    }

    /// <summary>「降ろす」が選ばれた：選んだ貨物を置ける隣の空きマスを赤表示して、クリックを待つ。</summary>
    private void EnterDropTileSelect()
    {
        menu.Hide();
        unitInfo.Hide(); // マス選択へ移るので相手情報は消す（貨物1体で自動選択のときもここを通る）
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
        unitInfo.Hide(); // マス選択へ移るので相手情報は消す（貨物1体で自動選択のときもここを通る）
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
        context.MarkTakeOver(); // 引き受けたマスも記録（この行動で降ろせるのはこのマスの上だけ）
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

    // ===== 杖（回復。Phase 25）=====

    /// <summary>
    /// 杖で回復を実行：対象のHPを（使用者の魔力＋杖の威力）回復し、行動を終える。
    /// 道具（傷薬）と同じく「使うと行動終了」（合意(b)）。対象は FindHealTargets で
    /// 満タン未満に絞ってあるので、ここでは実際の回復量を確かめてログに出すだけでよい。
    /// </summary>
    private void ExecuteHeal(Unit target)
    {
        Unit unit = context.unit;
        int amount = StaffRules.HealAmount(unit);
        int before = target.CurrentHP;
        target.Heal(amount);
        int healed = target.CurrentHP - before;

        Debug.Log($"{unit.Data.unitName} は {target.Data.unitName} を杖で回復した" +
                  $"（HP{healed}回復 → {target.CurrentHP}／{target.MaxHP}）");
        FinishAction();
    }

    /// <summary>
    /// 降ろすを実行：選んだ貨物を指定マスへ再配置し、残り移動力での再移動へ移る
    /// （2026-07-26 作者仕様：降ろした後も、使っていない移動力があればその範囲で動ける）。
    /// 救出のときと同じく、ここから先は取り消し不能（移動力の使い直しを防ぐため）。
    /// </summary>
    private void ExecuteDrop(Vector2Int cell)
    {
        Unit unit = context.unit;
        Unit cargo = selectedCargo;
        unit.ReleaseUnitAt(cargo, grid, cell);
        cargo.SetActed(true); // 降ろされたユニットはそのターン行動済み（合意(b)）
        context.MarkDrop();
        context.Commit(); // ここが新しい「戻れる限界」になる

        Debug.Log($"{unit.Data.unitName} は {cargo.Data.unitName} を {cell} に降ろした" +
                  $"（再移動できる移動力：{context.RemainingMove}）");

        ShowMoveRange();
        if (reachableCells.Count == 0)
            OpenCommandMenu(); // 動けるマスが無ければそのまま待機メニューへ
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

    // ===== 持ち物（武器の持ち替え・外す〔非武装になる〕・道具の使用・破棄〔捨てる〕）=====
    // 操作は2段階：持ち物リスト（ItemSelect）で1つ選ぶ → 行動サブメニュー（ItemActionSelect）で
    // 「外す/装備/使う」＋「捨てる」を選ぶ。装備変更・外す・破棄は行動を消費しない
    // （FE準拠：同一行動で持ち替え→攻撃ができる。実行後はメニューを開き直して再計算）。
    // 道具（傷薬）だけは「使うと行動終了」（合意(b)）＝使用後に FinishAction する。

    /// <summary>
    /// 「持ち物」が選ばれた：持ち物リストを開く。各行は押せて、クリックするとリストを残したまま
    /// 右隣に行動サブメニュー（外す/装備/使う＋捨てる）が出る。行にホバーすると武器・道具の
    /// 性能の詳細が画面右下の定位置に出る（列やメニューを離れても最後の内容を保つ）。
    /// </summary>
    private void EnterItemSelect()
    {
        menu.Hide();

        // 選択マークだけ現在位置に付け直す（コマンドメニューと同じ見せ方）
        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);

        itemMenu.Show(grid.CellToWorld(context.unit.GridPosition), grid.cellSize, BuildItemEntries());
        state = State.ItemSelect;
        Debug.Log($"持ち物を開いた（{context.unit.Items.Count} 個）。右クリック/ESCでメニューへ戻る。");
    }

    /// <summary>
    /// 持ち物リストの行を作る。各行は押せて、クリックすると行動サブメニュー（外す/装備/使う＋捨てる）
    /// へ進む。ラベルの補足（装備中／装備不可／残りN）は状態の目印として残し、ホバー用の
    /// 性能詳細（BuildItemDetail）も添える。従来の最下行（装備を外すコマンド）は撤廃し、装備中の武器の
    /// サブメニューの「外す」へ移した。
    /// </summary>
    private List<ItemListMenu.Entry> BuildItemEntries()
    {
        var entries = new List<ItemListMenu.Entry>();
        Unit unit = context.unit;

        foreach (ItemSlot slot in unit.Items)
        {
            ItemSlot s = slot; // ラムダが最後の値を掴まないようローカルに退避
            entries.Add(new ItemListMenu.Entry(BuildItemLabel(slot, unit), true,
                                               () => EnterItemActionSelect(s),
                                               UnitInfoLines.BuildItemDetail(s)));
        }

        return entries;
    }

    /// <summary>
    /// 持ち物リストと交換の列で共通の行ラベル（名前＋装備中／装備不可／残り回数）。
    /// 2箇所で同じ書き方に揃えるためにここへ集約している（片方だけ直して表示がずれるのを防ぐ）。
    /// 装備できるかは、その持ち物の持ち主 owner の兵種で判定する。
    /// 「装備中」は EquippedWeapon で見る（Weapon 窓口は「この武器では戦えない」条件を足す場所なので、
    ///  装備の事実を見せたいだけの表示はこちらを読む約束。Unit.Weapon の注記）。
    /// ※武器の性能（威力・射程など）はここには出さない。ボタン幅に限りがあり長い名前で右端が見切れるため、
    ///   行にマウスを重ねたときの詳細（UnitInfoLines.BuildItemDetail）で見る
    ///   ＝ユニット情報パネルの持ち物の行にも同じ詳細が出る。
    /// </summary>
    private string BuildItemLabel(ItemSlot slot, Unit owner)
    {
        string label = slot.Item.DisplayName;

        if (slot.Item is WeaponData weapon)
        {
            if (weapon == owner.EquippedWeapon) label += "（装備中）";
            else if (!WeaponMemoryRules.CanEquipByClass(owner, weapon)) label += "（装備不可）";
            // 装備はできるが、いまのメモリでは攻撃に使えない目印（2026-10-24）
            else if (!WeaponMemoryRules.CanAfford(owner, weapon)) label += "（メモリ不足）";
        }
        else if (slot.Item is ToolData)
        {
            label += $"（残り{slot.UsesLeft}）";
        }

        return label;
    }

    /// <summary>
    /// 持ち物リストで1つ選んだときの行動サブメニュー（10状態目 ItemActionSelect）。持ち物リストは
    /// 残したまま、その右隣に「外す/装備/使う」＋「捨てる」の2ボタンを出す。押せない一次行動
    /// （装備不可の武器の「装備」・満タンの道具の「使う」）は灰色の無効ボタンにし、「捨てる」は
    /// 常に押せる（行動を消費しない）。詳細（右下パネル）は持ち物リスト側の担当なので、ここの Entry には付けない。
    /// </summary>
    private void EnterItemActionSelect(ItemSlot slot)
    {
        Unit unit = context.unit;
        var entries = new List<ItemListMenu.Entry>();

        if (slot.Item is WeaponData weapon)
        {
            if (weapon == unit.EquippedWeapon)
            {
                // 装備中 → 外す（＝非武装になる。従来の最下行のコマンドと同じ機能）
                entries.Add(new ItemListMenu.Entry("外す", true, UnequipWeapon));
            }
            else if (WeaponMemoryRules.CanEquipByClass(unit, weapon))
            {
                // メモリが足りない武器も装備できる（攻撃には使えないだけ。2026-10-24 作者仕様の見直し）
                WeaponData w = weapon;
                entries.Add(new ItemListMenu.Entry("装備", true, () => EquipWeapon(w)));
            }
            else
            {
                // 兵種が装備できない → 「装備」は灰色（押せない）。捨てるはできる（作者合意）
                entries.Add(new ItemListMenu.Entry("装備", false, null));
            }
        }
        else if (slot.Item is ToolData)
        {
            if (unit.CurrentHP < unit.MaxHP)
                entries.Add(new ItemListMenu.Entry("使う", true, () => UseToolItem(slot)));
            else
                entries.Add(new ItemListMenu.Entry("使う", false, null)); // 満タンには使えない（合意(b)）
        }

        // 捨てるは常に押せる（種別を問わず・行動を消費しない）
        entries.Add(new ItemListMenu.Entry("捨てる", true, () => DiscardSelectedItem(slot)));

        // 持ち物リスト（列1）は残したまま、クリックした行に合わせて行動サブメニュー（列2）を出す
        itemMenu.SetActions(entries, unit.Items.IndexOf(slot));
        state = State.ItemActionSelect;
        Debug.Log($"「{slot.Item.DisplayName}」の行動を選択。右クリック/ESCで持ち物へ戻る。");
    }

    /// <summary>武器を装備して行動メニューへ戻る（行動は消費しない）。攻撃対象が新装備で再計算される。</summary>
    private void EquipWeapon(WeaponData weapon)
    {
        itemMenu.Hide();
        context.unit.Equip(weapon);
        Debug.Log($"{context.unit.Data.unitName} は {weapon.DisplayName} を装備した");
        OpenCommandMenu();
    }

    /// <summary>装備を外して（非武装になって）行動メニューへ戻る（行動は消費しない）。攻撃・被ダメ確認が消える。</summary>
    private void UnequipWeapon()
    {
        itemMenu.Hide();
        context.unit.Unequip();
        Debug.Log($"{context.unit.Data.unitName} は装備を外した（非武装）");
        OpenCommandMenu();
    }

    /// <summary>
    /// 道具を使う（フェーズ24）。効果を適用し、残り回数を減らして（0で所持品から消える）、
    /// 「使うと行動終了」（合意(b)）＝装備変更と違って行動を消費する。
    /// 呼ばれるのはHPが満タンでない＝回復できるとき（満タンは無効行なので押せない）。
    /// </summary>
    private void UseToolItem(ItemSlot slot)
    {
        itemMenu.Hide();
        Unit unit = context.unit;
        string name = slot.Item.DisplayName;

        int healed = unit.UseTool(slot);          // 効果適用＋残り回数-1（0で所持品から消える）
        string tail = slot.UsesLeft > 0 ? $"、残り{slot.UsesLeft}回" : "・使い切った";

        Debug.Log($"{unit.Data.unitName} は {name} を使った（HP{healed}回復 → {unit.CurrentHP}／{unit.MaxHP}{tail}）");
        FinishAction();
    }

    /// <summary>
    /// 「捨てる」：持ち物から1つ取り除く。装備中の武器なら Unit 側で自動的に非武装になる。
    /// 行動は消費しない（作者合意）。捨てた後は更新した持ち物リストへ戻り、空になったら行動メニューへ。
    /// </summary>
    private void DiscardSelectedItem(ItemSlot slot)
    {
        itemMenu.Hide();
        Unit unit = context.unit;
        string name = slot.Item.DisplayName;
        bool wasEquipped = slot.Item == unit.EquippedWeapon;

        unit.DiscardItem(slot);
        Debug.Log($"{unit.Data.unitName} は {name} を捨てた" + (wasEquipped ? "（装備していたので非武装になった）" : ""));

        // 持ち物が残っていれば更新したリストへ、無くなったら行動メニューへ戻る
        if (unit.Items.Count > 0) EnterItemSelect();
        else OpenCommandMenu();
    }

    // ===== 交換（隣接する味方との持ち物の受け渡し・フェーズ26〜27）=====
    // 「交換」→ 相手を選ぶ（UnitTargetSelect。右上に相手の情報が出る）→ 交換画面（ExchangeSelect）。
    // 交換画面は2列：左＝自分の持ち物、右＝相手の持ち物。どちらの行もクリックで反対側へ移る
    // （双方向・1回の交換で何個でも。合意(b)）。受け側がいっぱい（7個）の行は灰色で押せない。
    // 行動は消費しないので、閉じたあとも同じ行動で攻撃・持ち物・待機ができる（合意(a)）。
    // ただし1個でも動かしたら取り消し不能点を打つ（合意(c)）＝以後、移動の取り消しは
    // 交換したマスまでしか戻らず、選択解除もできない（相手の状態を変えたので巻き戻せない）。

    /// <summary>
    /// 交換の相手が決まった：2列の交換画面を開く。
    /// 相手の能力値は列の見出し（名前・HP・持ち物の数・装備武器）で見せるので、
    /// 相手を追う表示（文脈モード）は畳む＝ユニット情報パネルは Hide する
    /// （降ろすマスの選択に移るときと同じ作法。盤上クリックで見ていたユニットの表示は残る）。
    /// </summary>
    private void EnterExchange(Unit partner)
    {
        exchangePartner = partner;
        exchangedCount = 0;

        grid.ResetAllHighlights();
        grid.AddHighlight(context.unit.GridPosition, HighlightKind.Selection);
        grid.AddHighlight(partner.GridPosition, HighlightKind.TargetChoice);

        unitInfo.Hide();
        itemMenu.Hide(); // 直前のホバー詳細が右下に残らないように、いったん片付けてから開く
        ShowExchangeColumns();

        Debug.Log($"{context.unit.Data.unitName} は {partner.Data.unitName} と持ち物を交換する" +
                  $"（自分 {context.unit.Items.Count}／{UnitData.InventoryCapacity}・" +
                  $"相手 {partner.Items.Count}／{UnitData.InventoryCapacity}。右クリック/ESCで終了）");
    }

    /// <summary>
    /// 交換の2列を（作り）直して表示する。品を1つ動かすたびに呼ぶので、
    /// 持ち物の数・装備の表示・押せるかどうか（受け側の空き）がその場で更新される。
    /// </summary>
    private void ShowExchangeColumns()
    {
        Unit unit = context.unit;

        itemMenu.ShowExchange(
            grid.CellToWorld(unit.GridPosition), grid.cellSize,
            BuildExchangeColumn(unit, exchangePartner), BuildExchangeHeader(unit),
            BuildExchangeColumn(exchangePartner, unit), BuildExchangeHeader(exchangePartner));

        state = State.ExchangeSelect;
    }

    /// <summary>
    /// 交換の1列を作る：from の持ち物を並べ、クリックすると to へ移る。
    /// ラベルの補足（装備中／装備不可／残りN）は持ち物リストと同じ書き方に揃える
    /// （装備できるかは、その列の持ち主 from の兵種で判定する）。
    /// to がいっぱい（7個）なら全部の行を灰色の無効行にする。
    /// 持ち物が無い列は「（持ち物なし）」の無効行1つにする（列が空だと何も描かれず分からないため）。
    /// </summary>
    private List<ItemListMenu.Entry> BuildExchangeColumn(Unit from, Unit to)
    {
        var entries = new List<ItemListMenu.Entry>();

        if (from.Items.Count == 0)
        {
            entries.Add(new ItemListMenu.Entry("（持ち物なし）", false, null));
            return entries;
        }

        bool canMove = ExchangeRules.CanReceive(to);
        foreach (ItemSlot slot in from.Items)
        {
            ItemSlot s = slot; // ラムダが最後の値を掴まないようローカルに退避
            Unit f = from, t = to;
            entries.Add(new ItemListMenu.Entry(BuildItemLabel(slot, from), canMove,
                                               () => TransferItem(s, f, t),
                                               UnitInfoLines.BuildItemDetail(slot)));
        }

        return entries;
    }

    /// <summary>
    /// 交換の列の見出し（名前／HPと持ち物の数／装備武器／いっぱいの注記）。
    /// 装備の行があるので「武器を全部渡して非武装になった」ことがその場で分かる。
    /// </summary>
    private List<string> BuildExchangeHeader(Unit unit)
    {
        var lines = new List<string>
        {
            unit.Data.unitName,
            $"HP {unit.CurrentHP}／{unit.MaxHP}　持ち物 {unit.Items.Count}／{UnitData.InventoryCapacity}",
            $"装備: {(unit.EquippedWeapon != null ? unit.EquippedWeapon.DisplayName : "なし")}",
        };

        // いっぱいのときだけ注記を足す（灰色で押せない理由がその場で分かるように）
        if (!ExchangeRules.CanReceive(unit)) lines.Add("※いっぱい（受け取れない）");

        return lines;
    }

    /// <summary>
    /// 品を1つ、from から to へ移す（渡す・もらうの両方向で共通）。
    /// 行動は消費しないが、最初の1個で取り消し不能点を打つ。
    /// 装備中の武器なら Unit 側で自動的に非武装になる。
    /// 道具の残り使用回数は ItemSlot ごと移るので引き継がれる（満タンに戻らない）。
    /// </summary>
    private void TransferItem(ItemSlot slot, Unit from, Unit to)
    {
        Unit self = context.unit;
        string name = slot.Item.DisplayName;
        string uses = slot.HasUses ? $"（残り{slot.UsesLeft}）" : "";
        bool wasEquipped = slot.Item == from.EquippedWeapon;

        from.TransferItemTo(slot, to);

        // 最初の1個を動かした時点で「戻れる限界」を作る（救出・引き受けと同じ）。
        // 交換は行動を消費せず、この後そのまま攻撃できるので、移動済みは引き継ぐ
        // （引き継がないと「移動して交換すると後衛武器の射程が伸びる」抜け穴になる）
        if (exchangedCount == 0) context.Commit(keepHasMoved: true);
        exchangedCount++;

        int cap = UnitData.InventoryCapacity;
        string action = (from == self)
            ? $"{self.Data.unitName} は {exchangePartner.Data.unitName} に {name}{uses} を渡した"
            : $"{self.Data.unitName} は {exchangePartner.Data.unitName} から {name}{uses} を受け取った";

        Debug.Log(action +
                  $"（自分 {self.Items.Count}／{cap}・相手 {exchangePartner.Items.Count}／{cap}）" +
                  (wasEquipped ? $"（{from.Data.unitName} の装備だったので非武装になった）" : ""));

        ShowExchangeColumns(); // 両列を作り直す＝数・装備・押せるかどうかが即座に変わる
    }

    /// <summary>
    /// 交換を終えて行動メニューへ戻る（行動は消費しない）。1個も動かしていなければ
    /// 取り消し不能点も打っていないので、開く前と完全に同じ状態に戻る（作者合意(d)）。
    /// </summary>
    private void EndExchange()
    {
        Unit unit = context.unit;

        if (exchangedCount > 0)
            Debug.Log($"{unit.Data.unitName} は {exchangePartner.Data.unitName} との交換を終えた" +
                      $"（{exchangedCount}個やり取り。以後この行動は取り消せない）");
        else
            Debug.Log($"{unit.Data.unitName} は交換をやめた（やり取りなし・行動は消費しない）");

        itemMenu.Hide();
        exchangePartner = null;
        exchangedCount = 0;
        OpenCommandMenu(); // 相手情報パネルの片付けとハイライトの張り直しもここが行う
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
    /// </summary>
    private void CancelAll()
    {
        if (context != null) context.RevertMove(grid);
        ClearSelection();
    }

    /// <summary>選択に関わる表示と内部状態をすべて片付ける（移動の取り消しはしない）。</summary>
    private void ClearSelection()
    {
        menu.Hide();
        cargoMenu.Hide();
        itemMenu.Hide();
        forecast.Hide();
        unitInfo.Hide();
        systemInfo.Hide(); // コマンド一覧の「状況」「設定」も一緒に閉じる（2026-08-15）
        grid.ResetAllHighlights();
        previewedEnemy = null;
        context = null;
        reachableCells = null;
        attackTargets = null;
        criticalPending = false;
        weaponChoices = null;
        hoveredWeaponRow = -1;
        unitCandidates = null;
        tileCandidates = null;
        proxyCarrier = null;
        takeOverCarrier = null;
        selectedCargo = null;
        exchangePartner = null;
        exchangedCount = 0;
        refreshCandidate = null;
        cantoCells = null;
        state = State.Idle;
    }

    // ===== 補助 =====

    /// <summary>
    /// 攻撃可能マス（赤表示）の集合を作る。「立てるマス（現在地＋移動できるマス）」の
    /// それぞれから武器の射程で届くマスを集め、立てるマス自体は除く（そこは青で見せる）。
    /// 射程は実戦と同じ CombatRules.TryGetAttackRange を使うので、
    /// 「後衛武器は移動すると最小射程ちょうどに縮む」ルールも正しく反映される。
    /// 表示用の下見なので、そこに敵がいるかまでは見ない。
    /// 誰も立ち入れない壁マス（通行不可かつ飛行も不可）は塗らない。非武装なら空を返す。
    /// 2026-08-09 から、持っている武器ぶんを合算する（攻撃の対象を「全武器で届く敵」に
    /// した合意に表示を合わせるため）。敵は装備中の1本だけが候補なので、
    /// 敵の行動範囲プレビューの見た目は従来と変わらない。
    /// 2026-10-25 に、届くマスを集める本体を WeaponChoiceRules.GetAttackCellsFrom へ切り出した
    /// （Rキーの「敵全員の攻撃範囲」が同じ計算を使うため。ここに残したのは移動範囲を除く部分だけ）。
    /// </summary>
    private HashSet<Vector2Int> ComputeAttackCells(Unit unit, HashSet<Vector2Int> reachable)
    {
        var stands = new List<Vector2Int>(reachable) { unit.GridPosition };
        HashSet<Vector2Int> result = WeaponChoiceRules.GetAttackCellsFrom(grid, unit, stands);

        // 立てるマスは移動範囲（青）として見せたいので、赤にするのは「移動の外側」だけ
        result.ExceptWith(reachable);
        result.Remove(unit.GridPosition);
        return result;
    }

    /// <summary>
    /// 杖が届くマス（緑表示）の集合を作る（Phase 25・ComputeAttackCells の杖版）。
    /// 「立てるマス（現在地＋移動できるマス）」それぞれから杖の射程で届くマスを集め、
    /// 立てるマス自体は除く（そこは青で見せる）。杖は攻撃と違い、移動しても射程は縮まない。
    /// 表示用の下見なので、そこに回復対象がいるかまでは見ない。
    /// 誰も立ち入れない壁マスは塗らない。杖を装備していなければ空を返す。
    /// </summary>
    private HashSet<Vector2Int> ComputeHealCells(Unit unit, HashSet<Vector2Int> reachable)
    {
        var result = new HashSet<Vector2Int>();
        if (!StaffRules.TryGetStaffRange(unit, out int min, out int max)) return result;

        var stands = new List<Vector2Int>(reachable) { unit.GridPosition };
        foreach (Vector2Int stand in stands)
        {
            // stand を中心に、マンハッタン距離 min〜max のひし形の輪を集める
            for (int dx = -max; dx <= max; dx++)
            {
                int rest = max - Mathf.Abs(dx);
                for (int dy = -rest; dy <= rest; dy++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) < min) continue; // 内側（射程未満）は届かない

                    var cell = new Vector2Int(stand.x + dx, stand.y + dy);
                    TileData tile = grid.GetTile(cell);
                    if (tile == null) continue;                         // 盤外
                    if (!tile.IsWalkable && !tile.CanFlyOver) continue; // 壁（誰も立てない）
                    result.Add(cell);
                }
            }
        }

        // 立てるマスは移動範囲（青）として見せたいので、緑にするのは「移動の外側」だけ
        result.ExceptWith(reachable);
        result.Remove(unit.GridPosition);
        return result;
    }

    private bool IsSelectablePlayer(Unit u)
    {
        return u != null && u.Faction == Faction.Player && !u.HasActed;
    }

    /// <summary>
    /// unit がいまの位置から「持っている武器のどれかで」攻撃できる敵ユニットの一覧を返す。
    /// 「攻撃」「必殺技」「被ダメ確認」を出すかどうかの判定と、被ダメ確認の相手がこれを使う
    /// （どの武器で殴るかは、この後の武器選択で決める）。
    /// 中身は WeaponChoiceRules に集約してある（射程の判定は CombatRules 共通＝敵AIと同じルール）。
    /// 敵は候補が装備中の1本だけなので、敵の行動範囲プレビューの見た目は従来と変わらない。
    /// メモリで払えない武器は数えない。extraCost は必殺技の1本など、一緒に払う量（2026-10-24）。
    /// </summary>
    private List<Unit> FindAttackTargets(Unit unit, bool hasMoved, int extraCost = 0)
    {
        return WeaponChoiceRules.FindAllTargets(unit, hasMoved, extraCost);
    }
}
