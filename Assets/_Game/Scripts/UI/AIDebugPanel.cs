using UnityEngine;

/// <summary>
/// 敵AIの性格をデバッグ用に出す小さなパネル（2026-08-16 作者の修正案・IMGUI）。
///
/// もともとはユニット情報パネルの「状態」枠の1行だったが、遊ぶ人には要らない情報なので
/// 作者の指示で専用のパネルへ切り出した。出す相手はユニット情報パネルが**いま出しているユニット**で
/// （UnitInfoPanel.DisplayedUnit）、それが敵のときだけ描く＝クリックした敵の性格がそのまま見える。
///
/// 置き場所は**ユニット情報パネルのすぐ下・右詰め**（作者指示。当初は画面左下だったが、
/// 誰の情報かが離れていて分かりにくいので情報パネルに寄せた）。
/// 位置は UnitInfoPanel.PanelRect から毎フレーム決めるので、パネルの幅や高さを変えても付いてくる。
/// 枠は中身の文字にぴったり合わせる（短い「AI: 突撃型」のときに大きな空き枠が出ないように）。
///
/// 見るだけのパネルでゲームの状態は一切変えない。BattleController が自動生成する
/// （シーンへの追加作業は不要）。
/// </summary>
public class AIDebugPanel : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("ユニット情報パネルとのあいだに空ける隙間（ピクセル）")]
    public float gap = 6f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 13;

    private UnitInfoPanel unitInfo;   // 誰の情報を出すか・どこに置くかはこのパネルに合わせる

    void Start()
    {
        if (unitInfo == null) unitInfo = GetComponent<UnitInfoPanel>();
    }

    void OnGUI()
    {
        if (unitInfo == null) return;

        Unit unit = unitInfo.DisplayedUnit;
        if (unit == null || unit.Faction != Faction.Enemy) return; // 味方には性格が無い

        // ユニット情報パネルが出ていないと置く場所が決まらない
        // （PanelRect は同じフレームの UnitInfoPanel.OnGUI が入れる。生成順で必ずこちらが後になる）
        Rect panel = unitInfo.PanelRect;
        if (panel.width <= 0f) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = fontSize,
            padding = new RectOffset(8, 8, 4, 4),
        };

        // 情報パネルとは別の枠なので、誰の性格かが分かるように名前を添える
        var content = new GUIContent($"{unit.Data.unitName}　{UnitInfoLines.BuildAIRow(unit)}");
        Vector2 size = style.CalcSize(content);

        GUI.Box(new Rect(panel.xMax - size.x, panel.yMax + gap, size.x, size.y), content, style);
    }
}
