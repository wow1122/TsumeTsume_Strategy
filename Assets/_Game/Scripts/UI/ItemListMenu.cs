using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持ち物のメニュー（IMGUI）。2つの使い方がある。
///
/// (1)「持ち物」コマンド: 持ち物リスト（列1）と、その行動サブメニュー（列2：外す/装備/使う＋捨てる）を
///    横に並べて同時に描く（Show → SetActions / ClearActions）。
/// (2)「交換」コマンド（フェーズ27）: 自分の持ち物（列1）と相手の持ち物（列2）を横に並べ、
///    どちらの行もクリックで反対側へ移す（ShowExchange）。列の上に見出し
///    （名前・HP・持ち物の数・装備武器）を描き、受け取れない側の行は灰色の無効行になる。
/// (3) 攻撃時の武器選択（2026-08-09）: 攻撃に使う武器の一覧（列1のみ。行動サブメニューは使わない）。
///    行にマウスを重ねると onHover で行番号が通知され、呼び出し側が盤面に「その武器で狙える敵」を出す。
///    一覧は avoidRects（戦闘予測・ユニット情報のパネルの矩形）と重ならない位置へ逃がすが、
///    **逃がす位置を決めるのは開いた最初の1回だけ**で、開いているあいだは動かさない
///    （毎フレーム決め直すとクリックの最中に一覧が動いて押せなくなる。理由は DrawItemMenu のコメント）。
///
/// どちらの使い方でも、選んだ項目の性能詳細を画面右下の定位置に固定表示する。
/// 項目は Entry（表示名＋押せるか＋処理＋ホバー詳細）で受け取り、押せない一次行動
/// （装備不可の武器の「装備」・満タンの道具の「使う」など）は GUI.enabled=false で灰色の無効行にする。
/// 詳細（右下パネル）は持ち物リストの行にホバーした内容を覚えて出し続ける（列やメニューを離れても維持）。
/// リストの位置は ActionMenu / CargoListMenu と同じ：ユニットの右隣、画面右端では左に回り込む。
/// 行動サブメニューはリストの右隣（右端では左隣）に、クリックした行の高さに合わせて出す。
/// キャンセル（右クリック/ESC）は BattleController 側が状態遷移として処理する。
/// </summary>
public class ItemListMenu : MonoBehaviour
{
    /// <summary>メニューの1項目（表示名・押せるか・押されたときの処理・ホバー詳細）。</summary>
    public struct Entry
    {
        public string label;
        public bool enabled;          // false のときは灰色で押せない（説明用の無効行）
        public System.Action onSelect;
        public List<string> detailLines; // ホバーで出す詳細（null/空＝詳細なし。サブメニューには付けない）

        public Entry(string label, bool enabled, System.Action onSelect)
        {
            this.label = label;
            this.enabled = enabled;
            this.onSelect = onSelect;
            this.detailLines = null;
        }

        public Entry(string label, bool enabled, System.Action onSelect, List<string> detailLines)
        {
            this.label = label;
            this.enabled = enabled;
            this.onSelect = onSelect;
            this.detailLines = detailLines;
        }
    }

    [Header("見た目（持ち物リスト）")]
    // 2026-08-08 に 170 → 210 へ拡げた。特効武器の名前が長く、170px（全角10文字）では
    // 「ホースキラー（装備中）」11文字・「アーマーキラー（装備中）」12文字が右端で見切れたため。
    // 全角1文字あたり約17pxなので、210px で12文字ぶん。交換の2列表示もこの幅を共有する
    [Tooltip("ボタン1個の幅（ピクセル）。「アーマーキラー（装備中）」が収まる幅にしてある")]
    public float buttonWidth = 210f;
    [Tooltip("ボタン1個の高さ（ピクセル）")]
    public float buttonHeight = 34f;
    [Tooltip("メニューの枠とボタンの間の余白（ピクセル）")]
    public float padding = 6f;
    [Tooltip("ボタンの文字の大きさ")]
    public int fontSize = 16;

    [Header("行動サブメニュー（リストの右隣に並べる）")]
    [Tooltip("行動ボタン（外す/装備/使う/捨てる）1個の幅（ピクセル）")]
    public float actionButtonWidth = 100f;
    [Tooltip("持ち物リストと行動サブメニューの間の隙間（ピクセル）")]
    public float columnGap = 8f;

    [Header("交換の2列表示（フェーズ27）")]
    [Tooltip("列の見出し（名前・HP・持ち物の数・装備武器）1行の高さ（ピクセル）")]
    public float headerLineHeight = 20f;
    [Tooltip("列の見出しの文字の大きさ")]
    public int headerFontSize = 13;

    [Header("詳細パネル（画面右下に固定）")]
    [Tooltip("詳細パネルの幅（ピクセル）")]
    public float detailWidth = 220f;
    [Tooltip("詳細パネルの1行の高さ（ピクセル）")]
    public float detailLineHeight = 22f;
    [Tooltip("詳細パネルの文字の大きさ")]
    public int detailFontSize = 14;
    [Tooltip("画面の端からの余白（ピクセル）")]
    public float screenMargin = 12f;

    /// <summary>メニューが表示中か（持ち物リストが出ているか）。</summary>
    public bool IsVisible { get; private set; }

    private List<Entry> entries;         // 列1：持ち物リスト（交換モードでは自分の持ち物）
    private List<Entry> actionEntries;   // 列2：行動サブメニュー（null＝出さない）
    private System.Action<int> onHover;  // 列1の行にマウスが乗ったときの通知（省略可・行番号を渡す）
    private System.Func<Rect[]> avoidRects; // 避ける矩形（他のパネル）を返す窓口（省略可）
    private bool placementResolved;      // 避ける位置をもう決めたか（開いているあいだは決め直さない）
    private Vector2 placementOffset;     // 決めたずらし量（基準位置からの差）
    private int actionRowIndex;          // 行動サブメニューを合わせる列1の行
    private List<string> currentDetail;  // 右下に出す詳細（最後にホバーした行の内容を保持）
    private Vector3 anchorWorld;   // 基準となるユニットのワールド座標
    private float cellWorldSize;   // 1マスのワールド上の大きさ（右隣へのずらし量に使う）

    // ── 交換モード（フェーズ27）──
    private bool exchangeMode;             // true＝2列とも持ち物リスト（列2は相手の持ち物）
    private List<Entry> partnerEntries;    // 交換モードの列2：相手の持ち物
    private List<string> ownHeader;        // 列1の見出し（複数行）
    private List<string> partnerHeader;    // 列2の見出し（複数行）

    /// <summary>
    /// 持ち物リストを表示する（行動サブメニューは無しの状態に戻す）。anchorWorld は基準ユニットの位置。
    /// onHover を渡すと、行にマウスを重ねたときにその行番号を通知する（2026-08-09。
    /// 攻撃時の武器選択で、その武器で狙える敵を盤面に赤く出すのに使う。
    /// CargoListMenu.Show の onHover と同じ作法）。
    /// avoidRects を渡すと、他のパネル（戦闘予測・ユニット情報）と重ならない位置へ逃がす。
    /// </summary>
    public void Show(Vector3 anchorWorld, float cellWorldSize, List<Entry> entries,
                     System.Action<int> onHover = null, System.Func<Rect[]> avoidRects = null)
    {
        this.anchorWorld = anchorWorld;
        this.cellWorldSize = cellWorldSize;
        this.entries = entries;
        this.onHover = onHover;
        this.avoidRects = avoidRects;
        placementResolved = false; // 開くたびに位置を決め直す（開いているあいだは動かさない）
        actionEntries = null;   // 新しいリスト＝サブメニューはいったん消す
        exchangeMode = false;   // 「持ち物」コマンドの表示（交換モードではない）
        IsVisible = true;
    }

    /// <summary>
    /// 交換の2列を表示する（フェーズ27）。列1＝自分の持ち物、列2＝相手の持ち物で、
    /// どちらの行もクリックで反対側へ移す（処理は呼び出し側の Entry が持つ）。
    /// 見出しは複数行で受け取り、行の高さを揃えるため両列で行数の多い側に合わせて描く。
    /// 品を1つ動かすたびに呼び直して、数・装備・押せるかどうかを作り直す想定。
    /// </summary>
    public void ShowExchange(Vector3 anchorWorld, float cellWorldSize,
                             List<Entry> ownEntries, List<string> ownHeader,
                             List<Entry> partnerEntries, List<string> partnerHeader)
    {
        this.anchorWorld = anchorWorld;
        this.cellWorldSize = cellWorldSize;
        this.entries = ownEntries;
        this.ownHeader = ownHeader;
        this.partnerEntries = partnerEntries;
        this.partnerHeader = partnerHeader;
        onHover = null;         // 交換ではホバーの通知を使わない（詳細は右下パネルが担当）
        avoidRects = null;
        placementResolved = false;
        actionEntries = null;
        exchangeMode = true;
        IsVisible = true;
    }

    /// <summary>
    /// 行動サブメニュー（列2）を出す。rowIndex は合わせる列1の行。リストはそのまま残る。
    /// avoidRects を渡すと、その矩形（戦闘予測・ユニット情報のパネル）と重ならない位置へ逃がす
    /// （2026-08-09。描画のたびに評価するので、パネルの大きさが変わっても追随する）。
    /// 省略したときは、リストを出したとき（Show）に渡された指定をそのまま使う。
    /// </summary>
    public void SetActions(List<Entry> actions, int rowIndex, System.Func<Rect[]> avoidRects = null)
    {
        actionEntries = actions;
        actionRowIndex = rowIndex;
        if (avoidRects != null) this.avoidRects = avoidRects;
    }

    /// <summary>行動サブメニュー（列2）だけ消す（持ち物リストと詳細は残す）。</summary>
    public void ClearActions()
    {
        actionEntries = null;
    }

    /// <summary>メニューを全部隠す（リスト・サブメニュー・交換の2列目・詳細）。</summary>
    public void Hide()
    {
        IsVisible = false;
        entries = null;
        actionEntries = null;
        onHover = null;
        avoidRects = null;
        placementResolved = false;
        currentDetail = null;
        exchangeMode = false;
        partnerEntries = null;
        ownHeader = null;
        partnerHeader = null;
    }

    void OnGUI()
    {
        if (!IsVisible || entries == null || entries.Count == 0 || Camera.main == null) return;

        // 位置の基準：ユニットの中心と「1マス右」の点（カメラが動いても追従する）
        Vector3 center = Camera.main.WorldToScreenPoint(anchorWorld);
        Vector3 right = Camera.main.WorldToScreenPoint(anchorWorld + new Vector3(cellWorldSize, 0f, 0f));
        if (center.z <= 0f) return; // カメラの後ろなら描かない

        if (exchangeMode) DrawExchange(center, right);
        else DrawItemMenu(center, right);
    }

    /// <summary>「持ち物」コマンド・武器選択の表示（リスト＋行動サブメニュー）。</summary>
    private void DrawItemMenu(Vector3 center, Vector3 right)
    {
        float listWidth = buttonWidth + padding * 2f;
        float listHeight = entries.Count * buttonHeight + padding * 2f;

        // 右隣に置く。画面右端にはみ出すなら左隣へ回り込む
        float rightListX = right.x;
        float leftListX = center.x - (right.x - center.x) - listWidth;
        float listX = (rightListX + listWidth > Screen.width) ? leftListX : rightListX;
        // GUI座標は左上原点・Y下向きなので、スクリーンYを反転。縦はユニットの中心に合わせる
        float listY = Screen.height - center.y - listHeight * 0.5f;
        listY = Mathf.Clamp(listY, 0f, Mathf.Max(0f, Screen.height - listHeight));

        // 他のパネルと重なるなら逃がす（2026-08-09・武器選択のときだけ）。
        // 【重要】逃がす位置は「開いた最初の1回」だけ決めて、以後は動かさない（2026-08-09 バグ修正）。
        // 毎フレーム決め直すと、クリックの最中に一覧が動いてボタンを押せなくなる：
        // デバッグ用パネル（右上）は盤面のクリックを独自に拾うので、武器の行を押した瞬間に
        // 開閉して大きさが変わる → 避ける先が変わって一覧が左右に飛ぶ →
        // マウスを離したときには行が別の場所にあり、クリックが成立しない、という繰り返しになる。
        // ずらし量だけを覚えて基準位置には毎フレーム足すので、カメラが動いても追従する。
        var baseRect = new Rect(listX, listY, listWidth, listHeight);
        if (!placementResolved)
        {
            Rect placed = AvoidPanels(baseRect, listX == rightListX ? leftListX : rightListX);
            placementOffset = new Vector2(placed.x - baseRect.x, placed.y - baseRect.y);

            // 位置を確定するのは最初の Repaint まで待つ。各パネルの矩形は「描いたときに控えた値」なので、
            // 開いた直後の Layout の時点ではまだ今フレームぶんが入っていない
            // （メニューを開いたクリックでデバッグ用パネルが開いた場合、それを見落とすため）
            if (Event.current.type == EventType.Repaint) placementResolved = true;
        }
        var listRect = new Rect(baseRect.x + placementOffset.x, baseRect.y + placementOffset.y,
                                listWidth, listHeight);

        Vector2 mouse = Event.current.mousePosition;

        // ホバー中の行の詳細を覚える（右下に固定表示。列やメニューを離れても最後の内容を保つ＝維持）。
        // onHover が渡されていれば行番号も通知する（武器選択の盤面の赤表示の切り替え用）
        for (int i = 0; i < entries.Count; i++)
        {
            var r = new Rect(listRect.x + padding, listRect.y + padding + i * buttonHeight,
                             buttonWidth, buttonHeight);
            if (!r.Contains(mouse)) continue;

            if (entries[i].detailLines != null && entries[i].detailLines.Count > 0)
                currentDetail = entries[i].detailLines;
            onHover?.Invoke(i);
        }

        // 詳細パネルはメニューより先に（＝後ろに）描く：重なってもメニューのボタンが隠れない
        if (currentDetail != null && currentDetail.Count > 0)
            DrawDetailPanel(currentDetail);

        var style = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
        System.Action selected = null; // 押された処理はループの外で呼ぶ（処理中に Hide されても安全）

        // 列1：持ち物リスト（武器選択では武器の一覧）
        GUI.Box(listRect, GUIContent.none);
        for (int i = 0; i < entries.Count; i++)
        {
            var r = new Rect(listRect.x + padding, listRect.y + padding + i * buttonHeight,
                             buttonWidth, buttonHeight);
            GUI.enabled = entries[i].enabled;
            if (GUI.Button(r, entries[i].label, style))
                selected = entries[i].onSelect;
        }
        GUI.enabled = true;

        // 列2：行動サブメニュー（あれば。列1の右隣・右端では左隣・クリックした行の高さに合わせる）
        if (actionEntries != null && actionEntries.Count > 0)
        {
            float aWidth = actionButtonWidth + padding * 2f;
            float aHeight = actionEntries.Count * buttonHeight + padding * 2f;

            float rightX = listRect.xMax + columnGap;
            float leftX = listRect.x - columnGap - aWidth;

            float aX = rightX;
            if (aX + aWidth > Screen.width) aX = leftX; // 右がはみ出すなら左へ

            float aY = Mathf.Clamp(listRect.y + padding + actionRowIndex * buttonHeight,
                                   0f, Mathf.Max(0f, Screen.height - aHeight));
            var actionRect = AvoidPanels(new Rect(aX, aY, aWidth, aHeight), aX == rightX ? leftX : rightX);

            GUI.Box(actionRect, GUIContent.none);
            for (int i = 0; i < actionEntries.Count; i++)
            {
                var r = new Rect(actionRect.x + padding, actionRect.y + padding + i * buttonHeight,
                                 actionButtonWidth, buttonHeight);
                GUI.enabled = actionEntries[i].enabled;
                if (GUI.Button(r, actionEntries[i].label, style))
                    selected = actionEntries[i].onSelect;
            }
            GUI.enabled = true;
        }

        selected?.Invoke();
    }

    /// <summary>
    /// avoidRects（戦闘予測パネル・ユニット情報パネル）と重なるとき、
    /// 逃がした矩形を返す（2026-08-09）。
    /// 逃がし方は「反対の隣（altX）へ回り込む」→ それでも重なるなら「縦にずらす」の順。
    /// avoidRects が渡されていない（持ち物・交換）ときは、そのままの位置を返す＝従来どおり。
    /// 出ていないパネルは Rect.zero（幅0）で返るので、Overlaps が常に false になり自然に無視される。
    /// 【重要】呼ぶのはメニューを開いた最初の描画の1回だけ（DrawItemMenu が結果を覚える）。
    /// </summary>
    private Rect AvoidPanels(Rect rect, float altX)
    {
        if (avoidRects == null) return rect;

        Rect[] panels = avoidRects();
        if (panels == null || !OverlapsAny(rect, panels)) return rect;

        // 反対側の隣へ（画面内に収まる場合だけ試す）
        var moved = new Rect(altX, rect.y, rect.width, rect.height);
        if (moved.x >= 0f && moved.xMax <= Screen.width && !OverlapsAny(moved, panels)) return moved;

        return ShiftVertically(rect, panels);
    }

    /// <summary>いずれかのパネルと重なるか。</summary>
    private static bool OverlapsAny(Rect rect, Rect[] panels)
    {
        foreach (Rect p in panels)
            if (p.width > 0f && p.height > 0f && rect.Overlaps(p)) return true;
        return false;
    }

    /// <summary>
    /// 左右で避けきれないとき、パネルの上か下（余裕のある方・近い方）へ縦にずらす。
    /// パネルが2枚あると1回では解けないことがあるので、動かなくなるまで数回だけ繰り返す。
    /// どちらへも逃げられない（画面が狭い）ときは、それ以上動かさずに返す。
    /// </summary>
    private Rect ShiftVertically(Rect rect, Rect[] panels)
    {
        for (int pass = 0; pass <= panels.Length; pass++)
        {
            bool moved = false;
            foreach (Rect p in panels)
            {
                if (p.width <= 0f || p.height <= 0f || !rect.Overlaps(p)) continue;

                float above = p.yMin - rect.height - columnGap;
                float below = p.yMax + columnGap;
                bool canGoAbove = above >= 0f;
                bool canGoBelow = below + rect.height <= Screen.height;

                if (canGoAbove && (!canGoBelow || rect.center.y <= p.center.y)) rect.y = above;
                else if (canGoBelow) rect.y = below;
                else return rect; // 上にも下にも置けない

                moved = true;
            }
            if (!moved) break;
        }
        return rect;
    }

    /// <summary>
    /// 交換の2列表示（フェーズ27）。列1＝自分の持ち物、列2＝相手の持ち物を同じ高さで並べ、
    /// それぞれの上に見出し（名前・HP・持ち物の数・装備武器）を描く。
    /// 左右の回り込みは2列ぶんの幅で判定する（片方だけが画面外に出ないようにするため）。
    /// </summary>
    private void DrawExchange(Vector3 center, Vector3 right)
    {
        float colWidth = buttonWidth + padding * 2f;

        // 行の高さを両列で揃える：見出しの行数も、品の行数も多い側に合わせる
        int headerLines = Mathf.Max(Count(ownHeader), Count(partnerHeader));
        int rows = Mathf.Max(entries.Count, Count(partnerEntries));
        float colHeight = headerLines * headerLineHeight + rows * buttonHeight + padding * 2f;

        float totalWidth = colWidth * 2f + columnGap;
        float x = right.x;
        if (x + totalWidth > Screen.width)
            x = center.x - (right.x - center.x) - totalWidth; // 右にはみ出すなら2列まとめて左隣へ
        x = Mathf.Max(0f, x);

        // GUI座標は左上原点・Y下向きなので、スクリーンYを反転。縦はユニットの中心に合わせる
        float y = Screen.height - center.y - colHeight * 0.5f;
        y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Screen.height - colHeight));

        float partnerX = x + colWidth + columnGap;
        Vector2 mouse = Event.current.mousePosition;

        // ホバー中の行の詳細を覚える（両列で有効。離れても最後の内容を右下に保つ）
        ScanHoverDetail(entries, x, y, headerLines, mouse);
        ScanHoverDetail(partnerEntries, partnerX, y, headerLines, mouse);

        // 詳細パネルは列より先に（＝後ろに）描く：重なってもボタンが隠れない
        if (currentDetail != null && currentDetail.Count > 0)
            DrawDetailPanel(currentDetail);

        // 押された処理はループの外で呼ぶ（処理の中でリストが作り直されても安全）
        System.Action selected = null;
        DrawExchangeColumn(entries, ownHeader, x, y, colWidth, colHeight, headerLines, ref selected);
        DrawExchangeColumn(partnerEntries, partnerHeader, partnerX, y, colWidth, colHeight, headerLines, ref selected);
        selected?.Invoke();
    }

    /// <summary>交換の1列（見出し＋品の行）を描く。押されたら selected に処理を入れる。</summary>
    private void DrawExchangeColumn(List<Entry> list, List<string> header, float x, float y,
                                    float colWidth, float colHeight, int headerLines,
                                    ref System.Action selected)
    {
        GUI.Box(new Rect(x, y, colWidth, colHeight), GUIContent.none);

        if (header != null)
        {
            var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = headerFontSize, fontStyle = FontStyle.Bold };
            var infoStyle = new GUIStyle(GUI.skin.label) { fontSize = headerFontSize };
            for (int i = 0; i < header.Count; i++)
            {
                GUI.Label(new Rect(x + padding, y + padding + i * headerLineHeight,
                                   colWidth - padding * 2f, headerLineHeight),
                          header[i], i == 0 ? nameStyle : infoStyle);
            }
        }

        if (list == null) return;

        var style = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
        float rowTop = y + padding + headerLines * headerLineHeight;
        for (int i = 0; i < list.Count; i++)
        {
            var r = new Rect(x + padding, rowTop + i * buttonHeight, buttonWidth, buttonHeight);
            GUI.enabled = list[i].enabled;
            if (GUI.Button(r, list[i].label, style))
                selected = list[i].onSelect;
        }
        GUI.enabled = true;
    }

    /// <summary>その列の行にマウスが乗っていれば、右下に出す詳細として覚える。</summary>
    private void ScanHoverDetail(List<Entry> list, float x, float y, int headerLines, Vector2 mouse)
    {
        if (list == null) return;

        float rowTop = y + padding + headerLines * headerLineHeight;
        for (int i = 0; i < list.Count; i++)
        {
            var r = new Rect(x + padding, rowTop + i * buttonHeight, buttonWidth, buttonHeight);
            if (r.Contains(mouse) && list[i].detailLines != null && list[i].detailLines.Count > 0)
                currentDetail = list[i].detailLines;
        }
    }

    /// <summary>null でも 0 を返す件数（見出し・列の行数を揃える計算用）。</summary>
    private static int Count<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }

    /// <summary>
    /// 選んだ持ち物の詳細（1行目＝名前を見出しに、以降＝性能）を画面右下の定位置に描く。
    /// 戦闘予測パネルと同じ隅だが、持ち物の操作中は戦闘予測は出ないので競合しない。
    /// </summary>
    private void DrawDetailPanel(List<string> lines)
    {
        float w = detailWidth;
        float h = lines.Count * detailLineHeight + padding * 2f;
        var rect = new Rect(Screen.width - w - screenMargin, Screen.height - h - screenMargin, w, h);

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = detailFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        GUI.Box(rect, lines[0], boxStyle); // 見出し＝アイテム名

        var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = detailFontSize };
        for (int i = 1; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + padding, rect.y + padding + i * detailLineHeight,
                               w - padding * 2f, detailLineHeight),
                      lines[i], lineStyle);
        }
    }
}
