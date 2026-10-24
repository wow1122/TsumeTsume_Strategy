using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ユニット情報を左右2カラムで描く共通部品（2026-08-16 作者仕様・IMGUI）。
/// UI/GaugeBars・UI/UnitInfoLines と同じく MonoBehaviour ではない「描画の共通処理」で、
/// パネル側（UnitInfoPanel）は矩形を渡すだけ。文字の組み立ては UnitInfoLines に任せる。
///
/// レイアウト（左右のカラムは同じ幅。これがバーの長さを揃える条件になっている）:
///   左カラム … 顔枠（空欄）→ 名前／HP の1行 → クラス名／移動力 の1行 → ステータス枠
///              → 使用可能武器の1行 → 兵種経験値バー
///   右カラム … 状態枠 → スキル枠 → 持ち物枠 → 必殺技の1行 → メモリ・気勢ゲージ
///
/// **パネルの高さは右カラムで決まり**、左カラムの顔枠が残りの高さを全部もらって下端を揃える。
///
/// 2026-08-16 の作者の修正案で、初版から次を変えている:
///  ・レイアウトの切り替え（状態＋スキル／持ち物）を**廃止**し、3つの枠を縦に並べて同時に見せる
///  ・「ステータス」「自軍／敵軍」「状態」の見出しの文言を削除＝その枠は枠線だけになり、1行ぶん上へ詰まる
///  ・名前の行から Lv を削り、行そのものを顔枠の下へ移動
///  ・クラス名と「移動力 : x」を1行にまとめてステータス枠の上へ、使用可能武器を兵種経験値バーの上へ
///
/// 続く修正案2（同日・作者の Play 確認後）で:
///  ・ステータス枠から装備武器を削除し、中を**ぴったり二等分**（左＝力/魔力/技/速さ・右＝守備/魔防）に戻した
///  ・持ち物の行の右端に**耐久値「残り／最大」**を出す（名前が長い行は実際の描画幅で測って丸める）。
///    ※武器の耐久は 2026-10-24 に撤廃。右端の欄はそのまま残し、道具の残り回数などを出す
///  ・名前／HP の行だけ文字を一回り大きくし、下のクラス名の行との隙間を詰めた
///
/// レベル・スキル・クラスごとの必殺技・兵種経験値は**まだ実装が無い**（2026-08-16 時点）。
/// この画面はその置き場所を先に確保するもので、中身は「－」や空欄で描く。
///
/// ホバー詳細: どの項目もマウスを重ねると、その項目の直下に小さな枠が出る。
/// 中身があるのは持ち物の行だけで、それ以外は1行分の空欄の枠になる
/// （作者仕様）。枠は下の項目に**重なる**（項目を押し下げるとマウスを動かすたびに
/// レイアウトが動いてちらつくため、この形で合意）。
/// </summary>
public static class UnitInfoView
{
    // ===== 見た目の定数 =====

    /// <summary>
    /// パネルの既定の幅。1カラムは234px・枠の中は222px＝全角15文字ぶん。
    /// 2026-08-16 に 520→480 へ狭めたあと、持ち物の行に耐久値「残り／最大」を足したことで
    /// 一番長い「・アーマーキラー（装備中）　6／6」が入らなくなり、作者の指示で 500 に戻した。
    /// 狭められる下限は状態枠の「ガード：〇（前衛歩兵と隣接）」（全角14文字＝約196px）。
    /// </summary>
    public const float DefaultPanelWidth = 500f;

    /// <summary>左右のカラムのあいだの隙間。</summary>
    private const float ColumnGap = 12f;

    /// <summary>枠（GUI.Box）の見出しに使う高さ。見出しを持つのはスキル枠と持ち物枠だけ。</summary>
    private const float BoxTitleHeight = 20f;

    /// <summary>枠の内側の余白。</summary>
    private const float BoxPadding = 6f;

    /// <summary>枠と枠のあいだの隙間。</summary>
    private const float SectionGap = 6f;

    /// <summary>状態枠に確保する行数（状態・ガード・救出中）。</summary>
    private const int StateRows = 3;

    /// <summary>スキル枠に確保する行数（作者仕様。中身は後日）。</summary>
    private const int SkillRows = 4;

    /// <summary>ステータス枠の行数（左＝力・魔力・技・速さ／右＝守備・魔防）。</summary>
    private const int StatsRows = 4;

    /// <summary>能力値の名前に使う幅（一番長い「速さ」「魔力」「守備」「魔防」の2文字＝約28px＋隙間）。</summary>
    private const float StatNameWidth = 52f;

    /// <summary>持ち物の行で、品の名前と右端の数字（道具の残り回数など）のあいだに空ける最小の隙間。</summary>
    private const float ItemUsesGap = 6f;

    /// <summary>名前とHPの行だけ大きくする文字の差（2026-08-16 作者の修正案2「一回り大きく」）。</summary>
    private const int NameFontBoost = 3;

    /// <summary>
    /// 名前／HP の行と、その下のクラス名／移動力の行のあいだの隙間。
    /// この2行はひとまとまりに見せたいので枠と枠の隙間（SectionGap）より詰める（作者の修正案2）。
    /// </summary>
    private const float TextRowGap = 0f;

    /// <summary>
    /// 顔枠に最低限あける高さ。ふつうは右カラムのほうが高いので使われないが、
    /// 1行の高さを大きくしたときに顔枠が潰れないようにする保険。
    /// </summary>
    private const float MinFaceHeight = 100f;

    /// <summary>ホバー詳細の枠と、その元になった項目のあいだの隙間。</summary>
    private const float DetailGap = 2f;

    /// <summary>持ち物の行の右端「消費N」を、いまのメモリで払えない武器のときに塗る色（暗い枠の上で読める明るい赤）。</summary>
    private static readonly Color MemoryShortageColor = new Color(1f, 0.45f, 0.45f);

    /// <summary>兵種経験値バーの色（仕様未定なので見た目だけ。メモリ＝緑・気勢＝青と分ける明るい黄）。</summary>
    private static readonly Color ExpColor = new Color(0.95f, 0.80f, 0.35f);

    // ===== 高さの計算（数値を変えても崩れないよう、ハードコードせず式で出す）=====

    /// <summary>枠1つぶんの高さ。見出しを持たない枠は、その分を上の余白に置き換える。</summary>
    private static float BoxHeight(int rows, float lineHeight, bool hasTitle) =>
        (hasTitle ? BoxTitleHeight : BoxPadding) + rows * lineHeight + BoxPadding;

    /// <summary>右カラムの高さ。3つの枠を同時に出すので、ここがパネルの高さを決める。</summary>
    private static float RightHeight(float lineHeight) =>
        BoxHeight(StateRows, lineHeight, false) + SectionGap
        + BoxHeight(SkillRows, lineHeight, true) + SectionGap
        + BoxHeight(UnitData.InventoryCapacity, lineHeight, true) + SectionGap
        + lineHeight + SectionGap + GaugeBars.TotalHeight;

    /// <summary>左カラムの、顔枠より下にある部分の高さ（顔枠はここから残りをもらう）。</summary>
    private static float LeftLowerHeight(float lineHeight) =>
        lineHeight + TextRowGap                                  // 名前／HP（次の行とは詰めて置く）
        + lineHeight + SectionGap                                // クラス名／移動力
        + BoxHeight(StatsRows, lineHeight, false) + SectionGap   // ステータス枠
        + lineHeight + SectionGap                                // 使用武器
        + GaugeBars.SlimRowHeight;                               // 兵種経験値バー

    /// <summary>中身の高さ。ユニットによらず一定なのでパネルの位置・大きさは動かない。</summary>
    public static float ContentHeight(float lineHeight) =>
        Mathf.Max(RightHeight(lineHeight),
                  MinFaceHeight + SectionGap + LeftLowerHeight(lineHeight));

    // ===== 描画 =====

    /// <summary>
    /// 指定した領域いっぱいに2カラムを描く。grid はガードの判定に使う（無ければその行は空欄）。
    /// ホバー詳細の枠は、すべて描き終えたあとに重ねて描く（IMGUI は後から描いたものが上に来る）。
    /// </summary>
    public static void Draw(Rect content, Unit unit, GridManager grid, int fontSize, float lineHeight)
    {
        if (unit == null) return;

        var label = new GUIStyle(GUI.skin.label) { fontSize = fontSize, alignment = TextAnchor.MiddleLeft };
        var labelRight = new GUIStyle(GUI.skin.label) { fontSize = fontSize, alignment = TextAnchor.MiddleRight };
        var boxTitle = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.Max(10, fontSize - 1),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };

        float columnWidth = (content.width - ColumnGap) * 0.5f;
        var left = new Rect(content.x, content.y, columnWidth, content.height);
        var right = new Rect(content.x + columnWidth + ColumnGap, content.y, columnWidth, content.height);

        Vector2 mouse = Event.current.mousePosition;
        var hover = new HoverState();

        DrawLeftColumn(left, unit, hover, mouse, label, labelRight, fontSize, lineHeight);
        DrawRightColumn(right, unit, grid, hover, mouse, label, labelRight, boxTitle, fontSize, lineHeight);

        if (hover.Found) DrawDetail(hover, content, fontSize, lineHeight);
    }

    /// <summary>
    /// 左カラム：顔枠 → 名前／HP → クラス名／移動力 → ステータス枠 → 使用武器 → 兵種経験値バー。
    /// 顔枠が「右カラムの高さから残りをもらう」ので、左右の下端が揃う。
    /// </summary>
    private static void DrawLeftColumn(Rect column, Unit unit, HoverState hover, Vector2 mouse,
                                       GUIStyle label, GUIStyle labelRight, int fontSize, float lineHeight)
    {
        // 顔枠（空欄。顔の絵は後日）
        float faceHeight = Mathf.Max(MinFaceHeight,
                                     column.height - SectionGap - LeftLowerHeight(lineHeight));
        var face = new Rect(column.x, column.y, column.width, faceHeight);
        GUI.Box(face, GUIContent.none);
        hover.Track(face, column, mouse);

        // 名前（左寄せ）と HP（右寄せ）を同じ1行に。Lv は 2026-08-16 の修正案で削除。
        // この行だけ文字を一回り大きくする（作者の修正案2）
        var nameStyle = new GUIStyle(label) { fontSize = fontSize + NameFontBoost };
        var nameStyleRight = new GUIStyle(labelRight) { fontSize = fontSize + NameFontBoost };
        var nameRow = new Rect(column.x, face.yMax + SectionGap, column.width, lineHeight);
        GUI.Label(nameRow, unit.Data.unitName, nameStyle);
        GUI.Label(nameRow, $"HP {unit.CurrentHP}／{unit.MaxHP}", nameStyleRight);
        hover.Track(nameRow, column, mouse);

        // クラス名（左寄せ）と移動力（右寄せ）。移動力はステータス枠から出してここへ移した。
        // 名前の行とはひとまとまりに見せたいので隙間を詰める（作者の修正案2）
        var classRow = new Rect(column.x, nameRow.yMax + TextRowGap, column.width, lineHeight);
        GUI.Label(classRow, UnitInfoLines.ClassName(unit), label);
        GUI.Label(classRow, UnitInfoLines.MoveLine(unit), labelRight);
        hover.Track(classRow, column, mouse);

        // ステータス枠：見出しの文言は削除したので枠線だけ。中をぴったり左右に二等分し（区切り線は描かない）、
        // 左に 力・魔力・技・速さ、同じ行から右に 守備・魔防。
        // 装備武器はここには出さない（2026-08-16 の修正案2で削除＝持ち物枠の「（装備中）」で分かる）
        var stats = new Rect(column.x, classRow.yMax + SectionGap, column.width,
                             BoxHeight(StatsRows, lineHeight, false));
        GUI.Box(stats, GUIContent.none);

        float innerX = stats.x + BoxPadding;
        float innerY = stats.y + BoxPadding;
        float half = (stats.width - BoxPadding * 2f) * 0.5f;

        DrawStat(new Rect(innerX, innerY, half, lineHeight), "力", unit.Strength, hover, column, mouse, label);
        DrawStat(new Rect(innerX, innerY + lineHeight, half, lineHeight), "魔力", unit.Magic, hover, column, mouse, label);
        DrawStat(new Rect(innerX, innerY + lineHeight * 2f, half, lineHeight), "技", unit.Skill, hover, column, mouse, label);
        DrawStat(new Rect(innerX, innerY + lineHeight * 3f, half, lineHeight), "速さ", SpeedText(unit), hover, column, mouse, label);

        float rightX = innerX + half;
        DrawStat(new Rect(rightX, innerY, half, lineHeight), "守備", unit.Defense, hover, column, mouse, label);
        DrawStat(new Rect(rightX, innerY + lineHeight, half, lineHeight), "魔防", unit.Resistance, hover, column, mouse, label);

        // 兵種が装備できる武器種（兵種経験値バーの真上・作者仕様 2026-08-16）
        var usableRow = new Rect(column.x, stats.yMax + SectionGap, column.width, lineHeight);
        GUI.Label(usableRow, UnitInfoLines.UsableWeaponsLine(unit), label);
        hover.Track(usableRow, column, mouse);

        // 兵種経験値バー（仕様未定＝レイアウトだけ。メモリ・気勢と同じ長さ・半分の太さ）
        var expRow = new Rect(column.x, usableRow.yMax + SectionGap, column.width, GaugeBars.SlimRowHeight);
        GaugeBars.DrawSlim(expRow, "兵種", 0f, ExpColor, "－", fontSize);
        hover.Track(expRow, column, mouse);
    }

    /// <summary>右カラム：状態枠 → スキル枠 → 持ち物枠 → 必殺技 → メモリ・気勢ゲージ。</summary>
    private static void DrawRightColumn(Rect column, Unit unit, GridManager grid, HoverState hover, Vector2 mouse,
                                        GUIStyle label, GUIStyle labelRight, GUIStyle boxTitle,
                                        int fontSize, float lineHeight)
    {
        // 状態枠。見出しの文言（「状態」と座標）は 2026-08-16 の修正案で廃止し、
        // 代わりに1行目が「状態 : …」で始まる。座標は盤面の下の地形情報の帯へ移した
        var state = new Rect(column.x, column.y, column.width, BoxHeight(StateRows, lineHeight, false));
        GUI.Box(state, GUIContent.none);

        List<string> stateRows = UnitInfoLines.BuildStateRows(unit, grid);
        for (int i = 0; i < StateRows; i++)
        {
            var row = new Rect(state.x + BoxPadding, state.y + BoxPadding + i * lineHeight,
                               state.width - BoxPadding * 2f, lineHeight);
            string text = i < stateRows.Count ? stateRows[i] : "";
            if (string.IsNullOrEmpty(text)) continue; // 中身の無い行はホバーの対象にもしない

            GUI.Label(row, text, label);
            hover.Track(row, column, mouse);
        }

        // スキル枠 → 持ち物枠 → 必殺技 → ゲージ の順に上から積む。
        // 高さの式（RightHeight）と同じ並びなので、最後がちょうど下端に来る
        var skills = new Rect(column.x, state.yMax + SectionGap, column.width,
                              BoxHeight(SkillRows, lineHeight, true));
        DrawSkillBox(skills, column, hover, mouse, label, boxTitle, lineHeight);

        var items = new Rect(column.x, skills.yMax + SectionGap, column.width,
                             BoxHeight(UnitData.InventoryCapacity, lineHeight, true));
        DrawItemBox(items, column, unit, hover, mouse, label, labelRight, boxTitle, lineHeight);

        // 必殺技（1行・左寄せ）。クラスごとの必殺技は後日実装
        var critical = new Rect(column.x, items.yMax + SectionGap, column.width, lineHeight);
        GUI.Label(critical, "必殺技: －", label);
        hover.Track(critical, column, mouse);

        // メモリ（緑）→ 気勢（黄）の2本。1本ぶんずつホバーできるように矩形を分けて登録する
        var gauges = new Rect(column.x, critical.yMax + SectionGap, column.width, GaugeBars.TotalHeight);
        GaugeBars.Draw(gauges, unit, fontSize);
        float gaugeRow = GaugeBars.TotalHeight * 0.5f;
        hover.Track(new Rect(gauges.x, gauges.y, gauges.width, gaugeRow), column, mouse);
        hover.Track(new Rect(gauges.x, gauges.y + gaugeRow, gauges.width, gaugeRow), column, mouse);
    }

    /// <summary>スキル枠（4行・中身は後日。いまは空欄の行だけ）。</summary>
    private static void DrawSkillBox(Rect box, Rect column, HoverState hover, Vector2 mouse,
                                     GUIStyle label, GUIStyle boxTitle, float lineHeight)
    {
        GUI.Box(box, "スキル", boxTitle);

        for (int i = 0; i < SkillRows; i++)
        {
            var row = new Rect(box.x + BoxPadding, box.y + BoxTitleHeight + i * lineHeight,
                               box.width - BoxPadding * 2f, lineHeight);
            GUI.Label(row, "・－", label);
            hover.Track(row, column, mouse);
        }
    }

    /// <summary>
    /// 持ち物枠（最大7行）。左に品の名前、**右端に数字**（2026-08-16 作者の修正案2。当時は武器の耐久値。
    /// 耐久を撤廃した 2026-10-24 からは道具の残り回数「残り／最大」）。
    /// 威力・射程などの詳しい数字は行のホバー詳細で見せる。
    /// </summary>
    private static void DrawItemBox(Rect box, Rect column, Unit unit, HoverState hover, Vector2 mouse,
                                    GUIStyle label, GUIStyle labelRight, GUIStyle boxTitle, float lineHeight)
    {
        GUI.Box(box, $"持ち物　{unit.Items.Count}／{UnitData.InventoryCapacity}", boxTitle);

        // いまのメモリで払えない武器の「消費N」は赤字にする（2026-10-24。払えない武器も装備はできるので、
        // 攻撃に使えるかどうかをここで一目で分かるようにする）
        var shortageRight = new GUIStyle(labelRight);
        shortageRight.normal.textColor = MemoryShortageColor;

        // 空いているスロットの行はホバーの対象にしない（品が無いところで空欄の枠が出ないように）
        for (int i = 0; i < unit.Items.Count && i < UnitData.InventoryCapacity; i++)
        {
            var row = new Rect(box.x + BoxPadding, box.y + BoxTitleHeight + i * lineHeight,
                               box.width - BoxPadding * 2f, lineHeight);
            if (row.yMax > box.yMax - BoxPadding) break; // 枠からはみ出す行は描かない

            ItemSlot slot = unit.Items[i];

            // 右端の数字は、その実際の幅ぶんを空けて名前を描くので、
            // 長い名前（「・アーマーキラー（装備中）」など）でも数字と重ならない
            string uses = UnitInfoLines.ItemRightText(slot);
            float usesWidth = string.IsNullOrEmpty(uses)
                ? 0f
                : labelRight.CalcSize(new GUIContent(uses)).x + ItemUsesGap;

            var nameRect = new Rect(row.x, row.y, row.width - usesWidth, row.height);
            GUI.Label(nameRect, FitText("・" + UnitInfoLines.ItemLabel(slot, unit), label, nameRect.width), label);

            if (!string.IsNullOrEmpty(uses))
            {
                bool shortage = slot.Item is WeaponData weapon && !WeaponMemoryRules.CanAfford(unit, weapon);
                GUI.Label(row, uses, shortage ? shortageRight : labelRight);
            }

            hover.TrackItem(row, column, slot, mouse);
        }
    }

    /// <summary>能力値1つ（名前＋数字）。</summary>
    private static void DrawStat(Rect row, string name, int value, HoverState hover, Rect column,
                                 Vector2 mouse, GUIStyle label)
    {
        DrawStat(row, name, value.ToString(), hover, column, mouse, label);
    }

    /// <summary>能力値1つ（名前＋数字の文字）。数字に注記を添えたいとき（速さの「2(-3)」）はこちら。</summary>
    private static void DrawStat(Rect row, string name, string valueText, HoverState hover, Rect column,
                                 Vector2 mouse, GUIStyle label)
    {
        GUI.Label(new Rect(row.x, row.y, StatNameWidth, row.height), name, label);
        GUI.Label(new Rect(row.x + StatNameWidth, row.y, row.width - StatNameWidth, row.height),
                  valueText, label);
        hover.Track(row, column, mouse);
    }

    /// <summary>
    /// 速さの表示。鋼の武器などで下がっているときは「2(-3)」のように下がった量を添える（2026-10-24）。
    /// 数字の欄は約59px（全角4文字ぶん）あり、半角5文字の「2(-3)」は余裕で入る。
    /// </summary>
    private static string SpeedText(Unit unit)
    {
        int drop = unit.BaseSpeed - unit.Speed;
        return drop > 0 ? $"{unit.Speed}(-{drop})" : unit.Speed.ToString();
    }

    /// <summary>
    /// 幅に入りきらない文字列を末尾から削って「…」で丸める。
    /// 文字数から幅を見積もる（全角＝文字の大きさ・半角＝その半分）やり方と違い、
    /// **実際に描かれる幅**（GUIStyle.CalcSize）で測るので、フォントが違っても見切れない。
    /// ほとんどの品は丸めずに収まるので、ここを通っても実質1回の計測で終わる。
    /// </summary>
    private static string FitText(string text, GUIStyle style, float width)
    {
        var content = new GUIContent(text);
        if (style.CalcSize(content).x <= width) return text;

        for (int length = text.Length - 1; length > 0; length--)
        {
            content.text = text.Substring(0, length) + "…";
            if (style.CalcSize(content).x <= width) return content.text;
        }
        return "…";
    }

    /// <summary>
    /// ホバー詳細の枠を、元になった項目の直下に重ねて描く。
    /// 下に入りきらないときは上側へ開く（持ち物の最下行やゲージの行で効く）。
    /// 詳細が用意されていない項目は、1行分の空欄の枠になる（作者仕様）。
    /// </summary>
    private static void DrawDetail(HoverState hover, Rect content, int fontSize, float lineHeight)
    {
        List<string> lines = hover.Lines;
        bool hasLines = lines != null && lines.Count > 0;
        float rowHeight = Mathf.Max(16f, lineHeight - 2f);
        int rows = hasLines ? lines.Count : 1;
        float height = rows * rowHeight + BoxPadding * 2f;

        float y = hover.Row.yMax + DetailGap;
        if (y + height > content.yMax) y = hover.Row.y - DetailGap - height;
        y = Mathf.Clamp(y, content.y, Mathf.Max(content.y, content.yMax - height));

        var rect = new Rect(hover.Column.x, y, hover.Column.width, height);

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        GUI.Box(rect, hasLines ? lines[0] : "", boxStyle);

        if (!hasLines) return;

        var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, fontSize - 1) };
        for (int i = 1; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + BoxPadding, rect.y + BoxPadding + i * rowHeight,
                               rect.width - BoxPadding * 2f, rowHeight),
                      lines[i], lineStyle);
        }
    }

    /// <summary>
    /// いまマウスが乗っている項目を覚えておくための入れ物。
    /// 描いている途中で見つけ、すべて描き終えてから詳細の枠を重ねる（重なりの順番のため）。
    /// </summary>
    private class HoverState
    {
        public bool Found;
        public Rect Row;      // 乗っている項目の矩形（枠はこの真下に出す）
        public Rect Column;   // その項目があるカラム（枠の幅と左端に使う）
        public List<string> Lines; // 詳細の中身（null＝用意されていない＝空欄の枠）

        /// <summary>詳細が用意されていない項目（＝ホバーすると空欄の枠が出る項目）を登録する。</summary>
        public void Track(Rect row, Rect column, Vector2 mouse)
        {
            if (!row.Contains(mouse)) return;
            Found = true;
            Row = row;
            Column = column;
            Lines = null;
        }

        /// <summary>
        /// 持ち物・装備武器の行を登録する。詳細の文字は**乗っているときだけ**組み立てる
        /// （OnGUI は1フレームに何度も走るので、乗っていない行のぶんまで作るとゴミが増える）。
        /// </summary>
        public void TrackItem(Rect row, Rect column, ItemSlot slot, Vector2 mouse)
        {
            if (!row.Contains(mouse)) return;
            Found = true;
            Row = row;
            Column = column;
            Lines = UnitInfoLines.BuildItemDetail(slot);
        }
    }
}
