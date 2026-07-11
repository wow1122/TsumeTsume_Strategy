using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 移動後に出る行動メニュー（攻撃/待機など）。IMGUIで描く簡易版（uGUI化は素材差し替え時期に）。
/// 項目は Entry（表示名＋押されたときの処理）のリストで受け取るので、
/// 後のフェーズ（救出・飛翔など）はメニューを呼ぶ側が項目を1つ足すだけで済む。
/// 表示位置はユニットの右隣。画面右端にはみ出すときは左隣に回り込む。
/// </summary>
public class ActionMenu : MonoBehaviour
{
    /// <summary>メニューの1項目（表示名と、押されたときの処理）。</summary>
    public struct Entry
    {
        public string label;
        public System.Action onSelect;

        public Entry(string label, System.Action onSelect)
        {
            this.label = label;
            this.onSelect = onSelect;
        }
    }

    [Header("見た目")]
    [Tooltip("ボタン1個の幅（ピクセル）。「代わりに降ろす」(7文字)が収まる幅にしてある")]
    public float buttonWidth = 150f;
    [Tooltip("ボタン1個の高さ（ピクセル）")]
    public float buttonHeight = 34f;
    [Tooltip("メニューの枠とボタンの間の余白（ピクセル）")]
    public float padding = 6f;
    [Tooltip("ボタンの文字の大きさ")]
    public int fontSize = 16;

    /// <summary>メニューが表示中か。</summary>
    public bool IsVisible { get; private set; }

    /// <summary>直近に描いたメニューの矩形（GUI座標）。クリックの重なり判定用に公開。</summary>
    public Rect MenuRect { get; private set; }

    private List<Entry> entries;
    private Vector3 anchorWorld;   // 基準となるユニットのワールド座標
    private float cellWorldSize;   // 1マスのワールド上の大きさ（右隣へのずらし量に使う）

    /// <summary>メニューを表示する。anchorWorld は基準ユニットの位置（ワールド座標）。</summary>
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

        MenuRect = new Rect(menuX, menuY, menuWidth, menuHeight);
        GUI.Box(MenuRect, GUIContent.none);

        var style = new GUIStyle(GUI.skin.button) { fontSize = fontSize };

        // ボタンの処理はループの外で呼ぶ（処理の中で Hide されても安全なように）
        System.Action selected = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var rect = new Rect(menuX + padding, menuY + padding + i * buttonHeight,
                                buttonWidth, buttonHeight);
            if (GUI.Button(rect, entries[i].label, style))
                selected = entries[i].onSelect;
        }
        selected?.Invoke();
    }
}
