using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

/// <summary>
/// 敵全員の攻撃範囲を、盤面に薄い赤紫で重ねて見せる（2026-10-25 作者要望）。
/// **Rキーで表示／非表示を切り替える**（戦闘開始時は非表示）。見るだけの機能で、ゲームの状態は一切変えない。
///
/// ・赤紫になるのは「次の敵フェイズに、どれかの敵の攻撃が届くマス」（ThreatMap.GetThreatCells。
///   敵AIが攻撃先を探すときと同じ決まり＝待ち伏せ型の敵も含む）
/// ・Rキーはいつでも効く（自軍フェイズのどの操作の途中でも・敵フェイズ中でも・決着後でも）
/// ・塗るのは GridManager（SetDangerZone）。移動範囲などのハイライトとは別の層なので、選択や解除で消えない。
///   青い移動範囲にも重なって青紫になる＝移動先の安全・危険が選ぶ前に分かる（作者合意。混ぜ方は GridManager.ResolveColor）
/// ・ユニットが動く・倒れる・救出で盤上から消える・敵の装備やメモリが変わると、範囲も変わる。
///   それを起きる場所ごとに知らせてもらう代わりに、毎フレーム「盤面の様子」（全ユニットの位置・生死・
///   盤上にいるか、敵の装備とメモリ）を数字の並びにして、最後に計算したときと比べ、違うときだけ計算し直す。
///   これで行動メニューを出している間の仮の移動・その取り消し・敵フェイズ中の敵の移動にも自動で付いていく
///   （味方は敵の通り道を塞ぐので、味方が動くだけで範囲が変わることがある）
/// ・いまの状態（表示中か）は、コマンド一覧の「状況」パネルが1行で案内する（SystemInfoPanel が IsVisible を読む）
///
/// BattleController が自動生成する（シーンへの追加作業は不要）。
/// </summary>
public class DangerZoneView : MonoBehaviour
{
    /// <summary>敵の攻撃範囲を表示中か（Rキーで切り替える。戦闘開始時は非表示）。</summary>
    public bool IsVisible { get; private set; }

    private GridManager grid;

    // 盤面の様子を数字の並びにしたもの（いまのフレームのぶんと、最後に範囲を計算したときのぶん）
    private readonly List<int> boardState = new List<int>();
    private readonly List<int> paintedBoardState = new List<int>();

    private bool painted;  // 盤面に赤紫を塗ってあるか
    private bool announce; // 表示に切り替えた直後の計算で、何マスになったかをログに出す

    void Start()
    {
        // 盤面は BattleController が持っている参照を使う（無ければシーンから探す保険）
        BattleController controller = GetComponent<BattleController>();
        grid = (controller != null && controller.grid != null) ? controller.grid : FindAnyObjectByType<GridManager>();
    }

    void Update()
    {
        if (grid == null) return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            IsVisible = !IsVisible;
            if (IsVisible) announce = true;
            else Debug.Log("敵の攻撃範囲: 非表示");
        }

        if (!IsVisible)
        {
            if (painted)
            {
                grid.SetDangerZone(null);
                painted = false;
            }
            return;
        }

        // 盤面が最後に計算したときのままなら、塗り直す必要は無い
        ReadBoardState(boardState);
        if (painted && SameState(boardState, paintedBoardState)) return;

        HashSet<Vector2Int> cells = ThreatMap.GetThreatCells(grid, Faction.Enemy);
        grid.SetDangerZone(cells);
        paintedBoardState.Clear();
        paintedBoardState.AddRange(boardState);
        painted = true;

        if (announce)
        {
            Debug.Log($"敵の攻撃範囲: 表示（{cells.Count} マス。もう一度 R キーで消える）");
            announce = false;
        }
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
