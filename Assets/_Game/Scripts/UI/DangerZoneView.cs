using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

/// <summary>
/// 敵の攻撃範囲を盤面に重ねて見せる（2026-10-25 作者要望）。見るだけの機能で、ゲームの状態は一切変えない。
/// 2つの表示を受け持つ：
///
/// 【敵全員の攻撃範囲（薄い赤紫の塗り）】Rキーで表示／非表示を切り替える（戦闘開始時は非表示）。
/// ・赤紫になるのは「次の敵フェイズに、どれかの敵の攻撃が届くマス」（ThreatMap.GetThreatCells。
///   敵AIが攻撃先を探すときと同じ決まり＝待ち伏せ型の敵も含む）
/// ・Rキーはいつでも効く（自軍フェイズのどの操作の途中でも・敵フェイズ中でも・決着後でも）
/// ・塗るのは GridManager（SetDangerZone）。移動範囲などのハイライトとは別の層なので、選択や解除で消えない。
///   青い移動範囲にも重なって青紫になる＝移動先の安全・危険が選ぶ前に分かる（作者合意。混ぜ方は GridManager.ResolveColor）
/// ・いまの状態（表示中か）は、コマンド一覧の「状況」パネルが1行で案内する（SystemInfoPanel が IsVisible を読む）
///
/// 【印を付けた敵の範囲（濃い赤の外枠）】何も選んでいないときに敵をクリックすると印が付き、
/// その敵の範囲（移動＋攻撃。ThreatMap.GetUnitRangeCells）の外枠が残る。何体でも付けられ、
/// 印の付いた敵をもう一度クリックすると外れる（付け外しの入口は BattleController の ToggleMark 呼び出し）。
/// ・印を付けた敵が複数いるときは、範囲を合わせた外周を1本の枠で囲む（作者合意）。描くのは GridManager（SetMarkedOutline）
/// ・印の付いた敵の絵は赤く染まる＝範囲を調べている途中の敵だと分かる（作者要望。色は GridManager.markedUnitTint、
///   染めるのは Unit.SetMarkTint。印を外すと元の色に戻る）
/// ・ターンをまたいでも残り、敵が動けば付いていく。倒れた敵の印は自動で外れる。ESC や右クリックでは外れない
///
/// 【描き直すタイミング】ユニットが動く・倒れる・救出で盤上から消える・敵の装備やメモリが変わると、範囲も変わる。
/// それを起きる場所ごとに知らせてもらう代わりに、何か表示しているあいだは毎フレーム「盤面の様子」
/// （全ユニットの位置・生死・盤上にいるか、敵の装備とメモリ）を数字の並びにして、最後に描いたときと比べ、
/// 違うときだけ描き直す。これで行動メニューを出している間の仮の移動・その取り消し・敵フェイズ中の敵の移動にも
/// 自動で付いていく（味方は敵の通り道を塞ぐので、味方が動くだけで範囲が変わることがある）。
///
/// BattleController が自動生成する（シーンへの追加作業は不要）。
/// </summary>
public class DangerZoneView : MonoBehaviour
{
    /// <summary>敵全員の攻撃範囲（赤紫）を表示中か（Rキーで切り替える。戦闘開始時は非表示）。</summary>
    public bool IsVisible { get; private set; }

    private GridManager grid;

    // 印の付いた敵（並びは付けた順。倒れたら Update で外す）
    private readonly List<Unit> markedEnemies = new List<Unit>();

    // 盤面の様子を数字の並びにしたもの（いまのフレームのぶんと、最後に描いたときのぶん）
    private readonly List<int> boardState = new List<int>();
    private readonly List<int> paintedBoardState = new List<int>();

    private bool zoneDirty;  // 赤紫の塗りを描き直す必要がある（Rキー・盤面の変化）
    private bool marksDirty; // 赤い外枠を描き直す必要がある（印の付け外し・盤面の変化）
    private bool announce;   // 赤紫を表示に切り替えた直後の計算で、何マスになったかをログに出す

    void Start()
    {
        // 盤面は BattleController が持っている参照を使う（無ければシーンから探す保険）
        BattleController controller = GetComponent<BattleController>();
        grid = (controller != null && controller.grid != null) ? controller.grid : FindAnyObjectByType<GridManager>();
    }

    /// <summary>
    /// 敵に印を付ける／外す（BattleController が、何も選んでいないときの敵のクリックで呼ぶ）。
    /// 付けたら true、外したら false を返す（呼び出し側は、付けたときだけ青・橙の下見表示も出す）。
    /// 外枠と赤く染めるのは次の Update で行う（RefreshMarks）。外したときの色戻しはここで行う。
    /// </summary>
    public bool ToggleMark(Unit enemy)
    {
        if (enemy == null || enemy.Faction != Faction.Enemy) return false;

        marksDirty = true;
        if (markedEnemies.Remove(enemy))
        {
            enemy.SetMarkTint(Color.white); // 赤く染めていたのを元の色に戻す
            Debug.Log($"印を外した: {enemy.Data.unitName}（印の付いた敵 {markedEnemies.Count} 体）");
            return false;
        }

        markedEnemies.Add(enemy);
        Debug.Log($"印を付けた: {enemy.Data.unitName}（印の付いた敵 {markedEnemies.Count} 体。もう一度クリックで外す）");
        return true;
    }

    void Update()
    {
        if (grid == null) return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            IsVisible = !IsVisible;
            zoneDirty = true;
            if (IsVisible) announce = true;
            else Debug.Log("敵の攻撃範囲: 非表示");
        }

        // 倒れた敵の印は自動で外す（倒れた敵は Destroy されて == null になる。HP 0 の時点でも外す）
        if (markedEnemies.RemoveAll(u => u == null || !u.IsAlive) > 0) marksDirty = true;

        // 何か表示しているあいだだけ盤面を見張り、最後に描いたときと違えば両方とも描き直す
        if (IsVisible || markedEnemies.Count > 0)
        {
            ReadBoardState(boardState);
            if (!SameState(boardState, paintedBoardState))
            {
                paintedBoardState.Clear();
                paintedBoardState.AddRange(boardState);
                zoneDirty = true;
                marksDirty = true;
            }
        }
        else
        {
            paintedBoardState.Clear(); // 何も表示していない間は覚えない（次に表示するときは必ず計算し直す）
        }

        if (zoneDirty)
        {
            RefreshZone();
            zoneDirty = false;
        }
        if (marksDirty)
        {
            RefreshMarks();
            marksDirty = false;
        }
    }

    /// <summary>敵全員の攻撃範囲の赤紫を描き直す（非表示なら消す）。</summary>
    private void RefreshZone()
    {
        if (!IsVisible)
        {
            grid.SetDangerZone(null);
            return;
        }

        HashSet<Vector2Int> cells = ThreatMap.GetThreatCells(grid, Faction.Enemy);
        grid.SetDangerZone(cells);

        if (announce)
        {
            Debug.Log($"敵の攻撃範囲: 表示（{cells.Count} マス。もう一度 R キーで消える）");
            announce = false;
        }
    }

    /// <summary>印の付いた敵を赤く染め、範囲を合わせた外枠を描き直す（印が無ければ枠を消す）。</summary>
    private void RefreshMarks()
    {
        // 範囲を調べている途中の敵だと分かるように赤く染める（作者要望）。
        // Inspector で色を変えたときにも次の描き直しで追随するよう、毎回塗り直す
        foreach (Unit enemy in markedEnemies)
            enemy.SetMarkTint(grid.markedUnitTint);

        if (markedEnemies.Count == 0)
        {
            grid.SetMarkedOutline(null);
            return;
        }

        var area = new HashSet<Vector2Int>();
        foreach (Unit enemy in markedEnemies)
            area.UnionWith(ThreatMap.GetUnitRangeCells(grid, enemy));
        grid.SetMarkedOutline(area);
    }

    /// <summary>
    /// 盤面の様子を数字の並びにする（範囲が変わりうるものだけを並べる）。
    /// 全ユニット … 識別番号・位置・生きているか・盤上にいるか（救出で格納中の貨物は盤上にいない）。
    ///   味方も並べるのは、味方のマスは敵が通れない＝味方の位置で敵の範囲が変わるため
    /// 敵だけ   … 装備中の武器・メモリ（払えない武器では攻撃が届かない扱いになるため）
    /// 名簿（UnitRegistry.All）は格納中の貨物も含むので、救出・降ろすでも並びが変わる。
    /// </summary>
    private static void ReadBoardState(List<int> state)
    {
        state.Clear();
        foreach (Unit u in UnitRegistry.All)
        {
            if (u == null) continue;

            state.Add(u.GetInstanceID());
            state.Add(u.GridPosition.x);
            state.Add(u.GridPosition.y);
            state.Add(u.IsAlive ? 1 : 0);
            state.Add(u.gameObject.activeInHierarchy ? 1 : 0);

            if (u.Faction != Faction.Enemy) continue; // 武器とメモリが効くのは範囲を出す側（敵）だけ
            state.Add(u.EquippedWeapon != null ? u.EquippedWeapon.GetInstanceID() : 0);
            state.Add(WeaponMemoryRules.MemoryOf(u));
        }
    }

    /// <summary>2つの並びがまったく同じか。</summary>
    private static bool SameState(List<int> a, List<int> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }
        return true;
    }
}
