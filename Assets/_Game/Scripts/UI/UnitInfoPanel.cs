using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ユニット情報パネル（IMGUI・画面右上）。2026-08-16 の刷新で、それまで2枚あったパネル
/// （右上のデバッグ用 DebugUnitPanel と、右下の対象選択用）を**1枚に統合**したもの。
/// 中身の描画は UI/UnitInfoView が左右2カラムで行い、ここは「誰の情報を・どこに出すか」を決める。
///
/// 2つのモードを持つ:
///  ・常設モード … 盤上のユニットをクリックすると、そのユニットを出し続ける。空きマスのクリックで閉じる。
///                 BattleController の操作とは独立に自分でクリックを読むので、敵フェイズ中や
///                 メニュー表示中でも見られる（旧デバッグ用パネルの挙動をそのまま引き継いでいる）。
///  ・文脈モード … 相手ユニットを選ぶ場面（救出・引き受け・乗り込む・代わりに降ろす・杖・交換・攻撃）で
///                 BattleController が Show を呼ぶ。見出しでどのコマンドの相手かが分かり、
///                 盤上の候補にマウスを重ねると表示が切り替わる（戦闘予測と同じ作法）。
///                 貨物リストの行ホバーのように盤上にいない相手を出す「単体モード」もある。
/// 文脈モードが動いているあいだはそちらが優先で、Hide で畳むと常設モードの表示に戻る。
///
/// 見出しの行は**文脈モードのときだけ**出す（2026-08-16 作者の修正案）。
/// 常設モードの「ユニット情報」という文言は情報として無意味なので消し、中身を上へ詰めている。
/// 一方で「攻撃する相手」などの見出しは、どのコマンドの相手を選んでいるかを示すので残す。
///
/// 見るだけのパネルでゲームの状態は一切変えない。素材差し替え時期に uGUI で作り直す前提の暫定UI。
/// BattleController が自動生成する（シーンへの追加作業は不要）。
/// </summary>
public class UnitInfoPanel : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("パネルの幅（ピクセル）。左右2カラムに分けるので、1カラムはこの半分ほどになる")]
    public float panelWidth = UnitInfoView.DefaultPanelWidth;
    [Tooltip("1行の高さ（ピクセル）。パネルの高さはこの値から自動で決まる")]
    public float lineHeight = 24f;
    [Tooltip("パネルの枠と中身の間の余白（ピクセル）")]
    public float padding = 10f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 14;

    /// <summary>
    /// 直近に描いたパネルの矩形（GUI座標。出していなければ Rect.zero）。
    /// 武器の一覧がこのパネルを避けるために公開している（2026-08-09。
    /// BattleForecast.PanelRect と同じ作法）。パネル内クリックの判定にも使う。
    /// </summary>
    public Rect PanelRect { get; private set; }

    private List<Unit> candidates;   // 文脈モード（盤面）: 選択できる相手（null＝盤面モードではない）
    private Unit pinnedUnit;         // 文脈モード（単体）: 呼び出し側が指定した1体
    private string contextTitle;     // 文脈モードの見出し（「救出する相手」など）
    private Unit current;            // 文脈モードでいま出している相手（選択直後は最初の候補）
    private Unit inspected;          // 常設モード: 盤上クリックで見ているユニット
    private GridManager grid;        // マス→ユニットの割り出しと、ガードの判定に使う

    /// <summary>文脈モード（相手選択中）かどうか。</summary>
    private bool HasContext => pinnedUnit != null || candidates != null;

    /// <summary>
    /// いまパネルに出しているユニット（出していなければ null）。文脈モードが優先で、
    /// 無ければ盤上クリックで見ているユニット。画面左下の敵AIパネル（UI/AIDebugPanel）が読む。
    /// </summary>
    public Unit DisplayedUnit
    {
        get
        {
            Unit unit = (HasContext && current != null && current.IsAlive) ? current : inspected;
            return (unit != null && unit.IsAlive) ? unit : null;
        }
    }

    void Start()
    {
        if (grid == null) grid = FindAnyObjectByType<GridManager>();
    }

    /// <summary>
    /// 対象選択の開始時に呼ぶ（文脈モード・盤面）。title は見出し（例：「救出する相手」）。
    /// 盤上の候補にマウスを重ねると表示が切り替わる。
    /// </summary>
    public void Show(List<Unit> candidates, string title, GridManager grid)
    {
        this.candidates = candidates;
        this.contextTitle = title;
        if (grid != null) this.grid = grid;
        pinnedUnit = null;
        current = (candidates != null && candidates.Count > 0) ? candidates[0] : null;
    }

    /// <summary>
    /// 指定した1体の情報を出す（文脈モード・単体。2026-07-26）。貨物リストの行ホバーのように、
    /// 盤上にいないユニット（格納中の貨物）を出したいときに使う。
    /// 切り替えは呼び出し側が都度この関数を呼ぶことで行い、盤面ホバーは見ない。
    /// </summary>
    public void Show(Unit unit, string title)
    {
        candidates = null;
        contextTitle = title;
        pinnedUnit = unit;
        current = unit;
    }

    /// <summary>
    /// 対象選択の終了時（実行・キャンセル・選択解除）に呼ぶ。
    /// 畳むのは**文脈モードだけ**で、盤上クリックで見ているユニット（常設モード）は残す。
    /// </summary>
    public void Hide()
    {
        candidates = null;
        pinnedUnit = null;
        contextTitle = null;
        current = null;
    }

    /// <summary>
    /// スクリーン座標（左下原点＝Mouse.current.position と同じ）がパネルの上かどうか。
    /// BattleController が「パネルへのクリックを盤面に流さない」ために読む。
    /// </summary>
    public bool ContainsScreenPoint(Vector2 screenPos)
    {
        if (PanelRect.width <= 0f) return false;
        return PanelRect.Contains(new Vector2(screenPos.x, Screen.height - screenPos.y));
    }

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 screenPos = Mouse.current.position.ReadValue();

        // パネルの内側のクリックは、盤面の処理にも表示中ユニットの切り替えにも使わない
        // （パネルが盤面に重なったときに、押したつもりのない場所が反応するのを防ぐ）
        if (ContainsScreenPoint(screenPos)) return;

        // クリックしたマスのユニットを常設モードの表示対象にする（BattleController と同じ座標変換の手順）。
        // 盤外クリックは無視して表示を保ち、空きマスのクリックで閉じる
        // ※IMGUIのボタン（行動メニューなど）はクリックを横取りしないため、
        //   ボタンの下のマスにも反応して表示が変わることがある（見るだけのUIなので許容）
        if (grid == null || Camera.main == null) return;

        Vector3 world = Camera.main.ScreenToWorldPoint(screenPos);
        if (!grid.TryWorldToCell(world, out Vector2Int cell)) return;

        TileData tile = grid.GetTile(cell);
        inspected = tile != null ? tile.Occupant : null;
    }

    void OnGUI()
    {
        PanelRect = new Rect(); // 描かなかったときは「避ける相手は無い」扱いに戻す

        // 文脈モードでは、マウスの下に別の候補がいればそちらへ切り替える
        // （離れても最後の候補を表示し続ける）。単体モードは呼び出し側が切り替える
        if (candidates != null && grid != null && Mouse.current != null && Camera.main != null)
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            if (grid.TryWorldToCell(world, out Vector2Int cell))
            {
                TileData tile = grid.GetTile(cell);
                Unit hovered = tile != null ? tile.Occupant : null;
                if (hovered != null && candidates.Contains(hovered)) current = hovered;
            }
        }

        // 文脈モード（相手選択中）が優先。相手がいなければ、盤上クリックで見ているユニットに戻る
        bool showContext = HasContext && current != null && current.IsAlive;
        Unit unit = showContext ? current : inspected;
        if (unit == null || !unit.IsAlive) return; // 倒れて Destroy されたら自動で閉じる

        // 見出しの行は文脈モードのときだけ確保する（常設モードは中身を上へ詰める・2026-08-16）
        float titleHeight = showContext ? lineHeight : 0f;
        float contentHeight = UnitInfoView.ContentHeight(lineHeight);
        float panelHeight = contentHeight + titleHeight + padding * 2f;

        var rect = new Rect(Screen.width - panelWidth - 12f, 10f, panelWidth, panelHeight);
        PanelRect = rect;

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        if (showContext) GUI.Box(rect, contextTitle, boxStyle);
        else GUI.Box(rect, GUIContent.none);

        var content = new Rect(rect.x + padding, rect.y + padding + titleHeight,
                               panelWidth - padding * 2f, contentHeight);
        UnitInfoView.Draw(content, unit, grid, fontSize, lineHeight);
    }
}
