using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持ち物のメニュー（IMGUI）。持ち物リスト（列1）と、その行動サブメニュー（列2：外す/装備/使う＋捨てる）を
/// 横に並べて同時に描き、選んだ項目の性能詳細を画面右下の定位置に固定表示する。
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
    [Tooltip("ボタン1個の幅（ピクセル）。「傷薬（残り3）」「剣（装備中）」が収まる幅にしてある")]
    public float buttonWidth = 170f;
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

    private List<Entry> entries;         // 列1：持ち物リスト
    private List<Entry> actionEntries;   // 列2：行動サブメニュー（null＝出さない）
    private int actionRowIndex;          // 行動サブメニューを合わせる列1の行
    private List<string> currentDetail;  // 右下に出す詳細（最後にホバーした行の内容を保持）
    private Vector3 anchorWorld;   // 基準となるユニットのワールド座標
    private float cellWorldSize;   // 1マスのワールド上の大きさ（右隣へのずらし量に使う）

    /// <summary>持ち物リストを表示する（行動サブメニューは無しの状態に戻す）。anchorWorld は基準ユニットの位置。</summary>
    public void Show(Vector3 anchorWorld, float cellWorldSize, List<Entry> entries)
    {
        this.anchorWorld = anchorWorld;
        this.cellWorldSize = cellWorldSize;
        this.entries = entries;
        actionEntries = null;   // 新しいリスト＝サブメニューはいったん消す
        IsVisible = true;
    }

    /// <summary>行動サブメニュー（列2）を出す。rowIndex は合わせる列1の行。リストはそのまま残る。</summary>
    public void SetActions(List<Entry> actions, int rowIndex)
    {
        actionEntries = actions;
        actionRowIndex = rowIndex;
    }

    /// <summary>行動サブメニュー（列2）だけ消す（持ち物リストと詳細は残す）。</summary>
    public void ClearActions()
    {
        actionEntries = null;
    }

    /// <summary>メニューを全部隠す（リスト・サブメニュー・詳細）。</summary>
    public void Hide()
    {
        IsVisible = false;
        entries = null;
        actionEntries = null;
        currentDetail = null;
    }

    void OnGUI()
    {
        if (!IsVisible || entries == null || entries.Count == 0 || Camera.main == null) return;

        // 列1（持ち物リスト）の位置：ユニットの右隣。画面右端にはみ出すなら左隣へ回り込む
        Vector3 center = Camera.main.WorldToScreenPoint(anchorWorld);
        Vector3 right = Camera.main.WorldToScreenPoint(anchorWorld + new Vector3(cellWorldSize, 0f, 0f));
        if (center.z <= 0f) return; // カメラの後ろなら描かない

        float listWidth = buttonWidth + padding * 2f;
        float listHeight = entries.Count * buttonHeight + padding * 2f;
        float listX = right.x;
        if (listX + listWidth > Screen.width)
            listX = center.x - (right.x - center.x) - listWidth;
        // GUI座標は左上原点・Y下向きなので、スクリーンYを反転。縦はユニットの中心に合わせる
        float listY = Screen.height - center.y - listHeight * 0.5f;
        listY = Mathf.Clamp(listY, 0f, Mathf.Max(0f, Screen.height - listHeight));
        var listRect = new Rect(listX, listY, listWidth, listHeight);

        Vector2 mouse = Event.current.mousePosition;

        // ホバー中の行の詳細を覚える（右下に固定表示。列やメニューを離れても最後の内容を保つ＝維持）
        for (int i = 0; i < entries.Count; i++)
        {
            var r = new Rect(listX + padding, listY + padding + i * buttonHeight, buttonWidth, buttonHeight);
            if (r.Contains(mouse) && entries[i].detailLines != null && entries[i].detailLines.Count > 0)
                currentDetail = entries[i].detailLines;
        }

        // 詳細パネルはメニューより先に（＝後ろに）描く：重なってもメニューのボタンが隠れない
        if (currentDetail != null && currentDetail.Count > 0)
            DrawDetailPanel(currentDetail);

        var style = new GUIStyle(GUI.skin.button) { fontSize = fontSize };
        System.Action selected = null; // 押された処理はループの外で呼ぶ（処理中に Hide されても安全）

        // 列1：持ち物リスト
        GUI.Box(listRect, GUIContent.none);
        for (int i = 0; i < entries.Count; i++)
        {
            var r = new Rect(listX + padding, listY + padding + i * buttonHeight, buttonWidth, buttonHeight);
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
            float aX = listRect.xMax + columnGap;
            if (aX + aWidth > Screen.width) aX = listRect.x - columnGap - aWidth; // 右がはみ出すなら左へ
            float aY = Mathf.Clamp(listY + padding + actionRowIndex * buttonHeight,
                                   0f, Mathf.Max(0f, Screen.height - aHeight));
            var actionRect = new Rect(aX, aY, aWidth, aHeight);

            GUI.Box(actionRect, GUIContent.none);
            for (int i = 0; i < actionEntries.Count; i++)
            {
                var r = new Rect(aX + padding, aY + padding + i * buttonHeight, actionButtonWidth, buttonHeight);
                GUI.enabled = actionEntries[i].enabled;
                if (GUI.Button(r, actionEntries[i].label, style))
                    selected = actionEntries[i].onSelect;
            }
            GUI.enabled = true;
        }

        selected?.Invoke();
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
