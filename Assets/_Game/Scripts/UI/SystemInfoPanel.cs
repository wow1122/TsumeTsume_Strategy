using System.Collections.Generic;
using UnityEngine;

/// <summary>パネルの用途（コマンド一覧のどの項目から開かれたか）。</summary>
public enum SystemInfoKind
{
    Status,   // 「状況」：ターン数・フェイズ・クリア条件・両軍の数
    Settings, // 「設定」：将来の調整項目（いまは枠だけ）
}

/// <summary>
/// コマンド一覧の「状況」「設定」で出る情報パネル（2026-08-15 の UI 刷新・IMGUI）。
/// 常時表示していた画面左上のターン数・フェイズはここへ移した。
///
/// 表示場所は画面右上（盤面を左へ寄せて空けた領域）。デバッグ用ユニット情報パネルと
/// 同じ位置だが、コマンド一覧は「空きマスのクリック」で開き、そのクリックで
/// デバッグ用パネルは閉じる作りなので重ならない。
///
/// 見るだけのパネルでゲームの状態は一切変えない（BattleController の State.SystemInfo と対）。
/// BattleController が自動生成し、コマンドの選択・キャンセルで Show / Hide を呼ぶ。
/// 用途が増えても SystemInfoKind と BuildLines に足すだけで済むようにしてある。
/// </summary>
public class SystemInfoPanel : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("パネルの幅（ピクセル）。「20 ターン以内に勝利できない」が収まる幅にしてある")]
    public float panelWidth = 320f;
    [Tooltip("1行の高さ（ピクセル）")]
    public float lineHeight = 24f;
    [Tooltip("パネルの枠と文字の間の余白（ピクセル）")]
    public float padding = 10f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 14;

    /// <summary>パネルが表示中か。</summary>
    public bool IsVisible { get; private set; }

    /// <summary>
    /// 直近に描いたパネルの矩形（GUI座標。出していなければ Rect.zero）。
    /// 他のUIが避ける／積むために公開している（BattleForecast.PanelRect と同じ作法）。
    /// </summary>
    public Rect PanelRect { get; private set; }

    private SystemInfoKind kind;
    private TurnManager turnManager;

    /// <summary>パネルを表示する（BattleController から呼ぶ）。</summary>
    public void Show(SystemInfoKind kind, TurnManager turnManager)
    {
        this.kind = kind;
        this.turnManager = turnManager;
        IsVisible = true;
    }

    /// <summary>パネルを隠す。</summary>
    public void Hide()
    {
        IsVisible = false;
        turnManager = null;
        PanelRect = new Rect();
    }

    void OnGUI()
    {
        PanelRect = new Rect(); // 描かなかったときは「避ける相手は無い」扱いに戻す
        if (!IsVisible) return;

        List<string> lines = BuildLines();
        string title = (kind == SystemInfoKind.Status) ? "状況" : "設定";

        // 画面右上に描く（盤面を左へ寄せて空けた領域）。行数は条件の数で変わるので上端を固定して下へ伸ばす
        float panelHeight = (lines.Count + 1) * lineHeight + padding * 2f; // +1 は見出しの行
        var rect = new Rect(Screen.width - panelWidth - 12f, 10f, panelWidth, panelHeight);
        PanelRect = rect;

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
    }

    /// <summary>用途に応じた表示行を作る。</summary>
    private List<string> BuildLines()
    {
        List<string> lines = (kind == SystemInfoKind.Status) ? BuildStatusLines() : BuildSettingsLines();
        lines.Add("");
        lines.Add("右クリック / ESC で戻る");
        return lines;
    }

    /// <summary>「状況」の中身：ターン数・フェイズ・クリア条件・両軍の数。</summary>
    private List<string> BuildStatusLines()
    {
        var lines = new List<string>();

        if (turnManager == null)
        {
            lines.Add("ターン情報を取得できません");
            return lines;
        }

        // ターン制限があるステージでは「ターン 3／20」のように残りが分かる表示にする（Phase 15 から）
        string turnLabel = (turnManager.TurnLimit > 0)
            ? $"ターン {turnManager.TurnNumber}／{turnManager.TurnLimit}"
            : $"ターン {turnManager.TurnNumber}";
        string phaseLabel = (turnManager.CurrentPhase == TurnPhase.Player) ? "自軍フェイズ" : "敵フェイズ";
        lines.Add($"{turnLabel} ／ {phaseLabel}");
        lines.Add("");

        // クリア条件は TurnManager の勝敗判定から組み立てたものをそのまま出す
        lines.Add("【クリア条件】");
        lines.AddRange(turnManager.BuildClearConditionLines());
        lines.Add("");

        // 数え方は勝敗判定と同じ（UnitRegistry＝格納中の貨物も生きていれば数に入る）
        int players = UnitRegistry.CountAlive(Faction.Player);
        int enemies = UnitRegistry.CountAlive(Faction.Enemy);
        lines.Add($"味方 {players} 体（未行動 {CountUnacted(Faction.Player)}）");
        lines.Add($"敵　 {enemies} 体");

        return lines;
    }

    /// <summary>「設定」の中身：項目が決まるまでは案内だけ（作者合意 2026-08-15）。</summary>
    private List<string> BuildSettingsLines()
    {
        return new List<string>
        {
            "設定項目は準備中です",
            "",
            "（音量・メッセージ速度などを",
            "　ここに追加していく予定）",
        };
    }

    /// <summary>まだ行動していないユニットの数（格納中の貨物は盤上にいないので数えない）。</summary>
    private static int CountUnacted(Faction faction)
    {
        int count = 0;
        foreach (Unit u in UnitRegistry.GetUnits(faction))
            if (u.IsAlive && !u.HasActed) count++;
        return count;
    }
}
