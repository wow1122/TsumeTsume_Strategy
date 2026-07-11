using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 格納中の貨物リストから1体を選ぶメニュー（Phase 12・IMGUI）。
/// 輸送隊のように複数の貨物を持つユニットで「降ろす」「引き受け」「代わりに降ろす」を
/// 使うとき、どの貨物が対象かをここで選ぶ（名前＋HP表示。右クリック/ESCのキャンセルは
/// BattleController 側が状態遷移として処理する）。
/// 表示のしかたは ActionMenu と同じ：ユニットの右隣、画面右端では左に回り込む。
/// </summary>
public class CargoListMenu : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("ボタン1個の幅（ピクセル）。「名前（HP 99）」が収まる幅にしてある")]
    public float buttonWidth = 190f;
    [Tooltip("ボタン1個の高さ（ピクセル）")]
    public float buttonHeight = 34f;
    [Tooltip("メニューの枠とボタンの間の余白（ピクセル）")]
    public float padding = 6f;
    [Tooltip("ボタンの文字の大きさ")]
    public int fontSize = 16;

    /// <summary>メニューが表示中か。</summary>
    public bool IsVisible { get; private set; }

    private List<Unit> cargoes;
    private System.Action<Unit> onSelect;
    private Vector3 anchorWorld;   // 基準となるユニットのワールド座標
    private float cellWorldSize;   // 1マスのワールド上の大きさ（右隣へのずらし量に使う）

    /// <summary>貨物リストを表示する。選ばれたら onSelect(貨物) が呼ばれる。</summary>
    public void Show(Vector3 anchorWorld, float cellWorldSize, List<Unit> cargoes, System.Action<Unit> onSelect)
    {
        this.anchorWorld = anchorWorld;
        this.cellWorldSize = cellWorldSize;
        this.cargoes = cargoes;
        this.onSelect = onSelect;
        IsVisible = true;
    }

    /// <summary>メニューを隠す。</summary>
    public void Hide()
    {
        IsVisible = false;
        cargoes = null;
        onSelect = null;
    }

    void OnGUI()
    {
        if (!IsVisible || cargoes == null || cargoes.Count == 0 || Camera.main == null) return;

        // ユニットの中心と「1マス右」の点をスクリーン座標へ（カメラが動いても追従する）
        Vector3 center = Camera.main.WorldToScreenPoint(anchorWorld);
        Vector3 right = Camera.main.WorldToScreenPoint(anchorWorld + new Vector3(cellWorldSize, 0f, 0f));
        if (center.z <= 0f) return; // カメラの後ろなら描かない

        float menuWidth = buttonWidth + padding * 2f;
        float menuHeight = cargoes.Count * buttonHeight + padding * 2f;

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

        // ボタンの処理はループの外で呼ぶ（処理の中で Hide されても安全なように）
        Unit selected = null;
        for (int i = 0; i < cargoes.Count; i++)
        {
            var rect = new Rect(menuX + padding, menuY + padding + i * buttonHeight,
                                buttonWidth, buttonHeight);
            string label = $"{cargoes[i].Data.unitName}（HP {cargoes[i].CurrentHP}）";
            if (GUI.Button(rect, label, style))
                selected = cargoes[i];
        }
        if (selected != null) onSelect?.Invoke(selected);
    }
}
