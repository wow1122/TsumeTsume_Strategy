using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 所持品リストのメニュー（フェーズ23〜24・IMGUI）。武器の持ち替え・武装解除・道具の使用を選ぶ。
/// 項目は Entry（表示名＋押せるか＋押されたときの処理）の
/// リストで受け取り、押せない行（装備中・装備不可・満タンの道具など）は
/// GUI.enabled=false で灰色の無効行として見せる（何があるかは分かるが押せない）。
/// 表示のしかたは ActionMenu / CargoListMenu と同じ：ユニットの右隣、画面右端では左に回り込む。
/// キャンセル（右クリック/ESC）は BattleController 側が状態遷移として処理する。
/// </summary>
public class ItemListMenu : MonoBehaviour
{
    /// <summary>メニューの1項目（表示名・押せるか・押されたときの処理）。</summary>
    public struct Entry
    {
        public string label;
        public bool enabled;          // false のときは灰色で押せない（説明用の無効行）
        public System.Action onSelect;

        public Entry(string label, bool enabled, System.Action onSelect)
        {
            this.label = label;
            this.enabled = enabled;
            this.onSelect = onSelect;
        }
    }

    [Header("見た目")]
    [Tooltip("ボタン1個の幅（ピクセル）。「傷薬（残り3）」「剣（装備中）」が収まる幅にしてある")]
    public float buttonWidth = 170f;
    [Tooltip("ボタン1個の高さ（ピクセル）")]
    public float buttonHeight = 34f;
    [Tooltip("メニューの枠とボタンの間の余白（ピクセル）")]
    public float padding = 6f;
    [Tooltip("ボタンの文字の大きさ")]
    public int fontSize = 16;

    /// <summary>メニューが表示中か。</summary>
    public bool IsVisible { get; private set; }

    private List<Entry> entries;
    private Vector3 anchorWorld;   // 基準となるユニットのワールド座標
    private float cellWorldSize;   // 1マスのワールド上の大きさ（右隣へのずらし量に使う）

    /// <summary>所持品リストを表示する。anchorWorld は基準ユニットの位置（ワールド座標）。</summary>
    public void Show(Vector3 anchorWorld, float cellWorldSize, List<Entry> entries)
    {
        this.anchorWorld = anchorWorld;
        this.cellWorldSize = cellWorldSize;
        this.entries = entries;
        IsVisible = true;
    }

    /// <summary>メニューを隠す。</summary>
    public void Hide()
    {
        IsVisible = false;
        entries = null;
    }

    void OnGUI()
    {
        if (!IsVisible || entries == null || entries.Count == 0 || Camera.main == null) return;

        // ユニットの中心と「1マス右」の点をスクリーン座標へ（カメラが動いても追従する）
        Vector3 center = Camera.main.WorldToScreenPoint(anchorWorld);
        Vector3 right = Camera.main.WorldToScreenPoint(anchorWorld + new Vector3(cellWorldSize, 0f, 0f));
        if (center.z <= 0f) return; // カメラの後ろなら描かない

        float menuWidth = buttonWidth + padding * 2f;
        float menuHeight = entries.Count * buttonHeight + padding * 2f;

        // 右隣に置く。画面右端にはみ出すなら左隣へ回り込む
        float menuX = right.x;
        if (menuX + menuWidth > Screen.width)
            menuX = center.x - (right.x - center.x) - menuWidth;

        // GUI座標は左上原点・Y下向きなので、スクリーンYを反転。縦はユニットの中心に合わせる
        float menuY = Screen.height - center.y - menuHeight * 0.5f;
        menuY = Mathf.Clamp(menuY, 0f, Mathf.Max(0f, Screen.height - menuHeight));

        var menuRect = new Rect(menuX, menuY, menuWidth, menuHeight);
        GUI.Box(menuRect, GUIContent.none);

        var style = new GUIStyle(GUI.skin.button) { fontSize = fontSize };

        // 押された処理はループの外で呼ぶ（処理の中で Hide されても安全なように）
        System.Action selected = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var rect = new Rect(menuX + padding, menuY + padding + i * buttonHeight,
                                buttonWidth, buttonHeight);
            GUI.enabled = entries[i].enabled;        // 無効行は灰色・押せない
            if (GUI.Button(rect, entries[i].label, style))
                selected = entries[i].onSelect;
        }
        GUI.enabled = true;                          // 後続のGUIのために必ず戻す
        selected?.Invoke();
    }
}
