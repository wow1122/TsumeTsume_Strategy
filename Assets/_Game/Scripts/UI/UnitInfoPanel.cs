using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 対象ユニット選択中の情報パネル（2026-07-26 作者要望・IMGUI・画面右下）。
/// 「救出」で貨物にする相手を選ぶときのように、相手ユニットを選ぶ場面で
/// その相手の能力値・装備武器・持ち物・状態を見えるようにする。
/// 対象は救出だけでなく、引き受け・乗り込む・代わりに降ろす・杖（回復）も同じ扱い。
///
/// 表示の作法は戦闘予測（BattleForecast）と同じ：選択に入った瞬間から最初の候補を表示し、
/// 別の候補にマウスを重ねるとそちらへ切り替わる（離れても最後の候補を表示し続ける）。
/// 貨物リスト（CargoListMenu）の行ホバーからも使うため、盤上のユニットを追う「盤面モード」と、
/// 指定された1体だけを出す「単体モード」の2通りがある（格納中の貨物は盤上にいないため）。
/// 場所は戦闘予測と同じ画面右下（救出・杖などの相手選択中は戦闘予測が出ないので重ならない）。
/// 表示内容は UnitInfoLines に集約してあり、デバッグ用パネルと同じ内容になる。
/// 見るだけのパネルでゲームの状態は一切変えない。
/// BattleController が自動生成し、対象選択の開始・終了で Show / Hide を呼ぶ。
///
/// 縦積み（2026-08-09 作者合意）: 攻撃の対象選択だけは戦闘予測と同時に出る。
/// このときは stackAbove で戦闘予測パネルの矩形を受け取り、その**上に**積む
/// （下＝戦闘予測が従来の定位置のまま／上＝敵ユニット情報）。渡されなければ従来どおり右下の定位置。
/// </summary>
public class UnitInfoPanel : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("パネルの幅（ピクセル）")]
    public float panelWidth = 340f;
    [Tooltip("1行の高さ（ピクセル）")]
    public float lineHeight = 24f;
    [Tooltip("パネルの枠と文字の間の余白（ピクセル）")]
    public float padding = 10f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 14;
    [Tooltip("下に積むパネル（戦闘予測）との隙間（ピクセル）")]
    public float stackGap = 6f;

    /// <summary>
    /// 直近に描いたパネルの矩形（GUI座標。出していなければ Rect.zero）。
    /// 武器の一覧がこのパネルを避けるために公開している（2026-08-09。
    /// BattleForecast.PanelRect / DebugUnitPanel.PanelRect と同じ作法）。
    /// </summary>
    public Rect PanelRect { get; private set; }

    private List<Unit> candidates; // 盤面モード: 選択できる相手（null＝盤面モードではない）
    private GridManager grid;      // 盤面モードでのみ使う（マス→ユニットの割り出し）
    private bool pinned;           // 単体モード: 呼び出し側が指定した1体だけを出す
    private string title;
    private Unit current;          // いま情報を表示している相手（選択直後は最初の候補）
    private System.Func<Rect> stackAbove; // このパネルを上に積む相手の矩形（null＝右下の定位置）

    /// <summary>
    /// 対象選択の開始時に呼ぶ（盤面モード）。title は見出し（例：「救出する相手」）。
    /// 盤上の候補にマウスを重ねると表示が切り替わる。
    /// stackAbove を渡すと、その矩形（戦闘予測パネル）の上に積む（2026-08-09。
    /// 描画のたびに評価するので、予測パネルの行数が相手ごとに変わっても追随する）。
    /// </summary>
    public void Show(List<Unit> candidates, string title, GridManager grid,
                     System.Func<Rect> stackAbove = null)
    {
        this.candidates = candidates;
        this.title = title;
        this.grid = grid;
        this.stackAbove = stackAbove;
        pinned = false;
        current = (candidates != null && candidates.Count > 0) ? candidates[0] : null;
    }

    /// <summary>
    /// 指定した1体の情報を出す（単体モード。2026-07-26）。貨物リストの行ホバーのように、
    /// 盤上にいないユニット（格納中の貨物）を出したいときに使う。
    /// 切り替えは呼び出し側が都度この関数を呼ぶことで行い、盤面ホバーは見ない。
    /// </summary>
    public void Show(Unit unit, string title)
    {
        candidates = null;
        grid = null;
        stackAbove = null;
        this.title = title;
        pinned = true;
        current = unit;
    }

    /// <summary>対象選択の終了時（実行・キャンセル・選択解除）に呼ぶ。</summary>
    public void Hide()
    {
        candidates = null;
        grid = null;
        stackAbove = null;
        pinned = false;
        current = null;
        PanelRect = new Rect(); // 消えたパネルの位置に武器の一覧が引きずられないように戻す
    }

    void OnGUI()
    {
        PanelRect = new Rect(); // 描かなかったときは「避ける相手は無い」扱いに戻す

        if (!pinned && (candidates == null || grid == null)) return;

        // 盤面モードでは、マウスの下に別の候補がいればそちらへ切り替える
        // （離れても最後の候補を表示し続ける）。単体モードは呼び出し側が切り替える
        if (!pinned && Mouse.current != null && Camera.main != null)
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            if (grid.TryWorldToCell(world, out Vector2Int cell))
            {
                TileData tile = grid.GetTile(cell);
                Unit hovered = tile != null ? tile.Occupant : null;
                if (hovered != null && candidates.Contains(hovered)) current = hovered;
            }
        }

        if (current == null || !current.IsAlive) return;

        List<string> lines = UnitInfoLines.BuildBasic(current);

        // 画面右下に描く（盤面の下の地形情報と重ならない位置。
        // 行数は持ち物の数で変わるので、下端を固定して上へ伸ばす）
        // 文字行の下にマナ・気勢のゲージバー2本ぶんの高さを足す（フェーズ28）
        float panelHeight = (lines.Count + 1) * lineHeight + padding * 2f + GaugeBars.TotalHeight; // +1 は見出しの行
        float bottom = Screen.height - 12f;

        // 攻撃の対象選択では戦闘予測と同時に出るので、その上に積む（2026-08-09 作者合意）。
        // 戦闘予測が出ていなければ（Rect.zero）従来どおり右下の定位置
        Rect below = stackAbove != null ? stackAbove() : new Rect();
        if (below.height > 0f) bottom = below.yMin - stackGap;

        var rect = new Rect(Screen.width - panelWidth - 12f, bottom - panelHeight,
                            panelWidth, panelHeight);
        PanelRect = rect; // 武器の一覧の重なり回避に使う（2026-08-09）

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        GUI.Box(rect, title, boxStyle);

        var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
        for (int i = 0; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + padding,
                               rect.y + padding + (i + 1) * lineHeight,
                               panelWidth - padding * 2f, lineHeight),
                      lines[i], lineStyle);
        }

        // 文字行の下にマナ・気勢のゲージバーを描く（フェーズ28。デバッグ用パネルと同じ部品）
        GaugeBars.Draw(new Rect(rect.x + padding,
                                rect.y + padding + (lines.Count + 1) * lineHeight,
                                panelWidth - padding * 2f, GaugeBars.TotalHeight),
                       current, fontSize);
    }
}
