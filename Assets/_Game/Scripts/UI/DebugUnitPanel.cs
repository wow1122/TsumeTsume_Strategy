using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// デバッグ・テスト用のユニット情報パネル（IMGUI・画面右上）。
/// 盤上のユニット（敵味方どちらでも）をクリックすると、能力値・武器・状態を表示する。
/// 空きマスをクリックすると閉じる。盤外クリックでは表示を保つ。
/// BattleController の操作（選択・メニュー）とは独立に自分でクリックを読むだけなので、
/// 敵フェイズ中やメニュー表示中でも確認できる。ゲームの状態は一切変えない。
/// あくまで開発中の確認用 — 本番のユニットUIは素材差し替え時期に uGUI でまとめて作る方針。
/// BattleController が自動生成する（シーンへの追加作業は不要）。
/// </summary>
public class DebugUnitPanel : MonoBehaviour
{
    [Tooltip("パネルを表示するか（デバッグ用UIなので、邪魔なときはここでOFF）")]
    public bool panelEnabled = true;

    [Header("見た目")]
    [Tooltip("パネルの幅（ピクセル）")]
    public float panelWidth = 340f;
    [Tooltip("1行の高さ（ピクセル）")]
    public float lineHeight = 24f;
    [Tooltip("パネルの枠と文字の間の余白（ピクセル）")]
    public float padding = 10f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 14;

    /// <summary>
    /// 直近に描いたパネルの矩形（GUI座標。出していなければ Rect.zero）。
    /// 攻撃時の武器選択のUI（一覧・「実行」）がこのパネルに被らないよう避けるために公開している
    /// （2026-08-09。BattleForecast.PanelRect と同じ作法）。
    /// </summary>
    public Rect PanelRect { get; private set; }

    private GridManager grid;
    private Unit unit; // いま表示しているユニット（null = 非表示）

    void Start()
    {
        grid = FindAnyObjectByType<GridManager>();
    }

    void Update()
    {
        if (grid == null || Mouse.current == null || Camera.main == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // クリックしたマスのユニットを表示対象にする（BattleController と同じ座標変換の手順）。
        // 盤外クリックは無視して表示を保ち、空きマスのクリックで閉じる。
        // ※IMGUIのボタン（ターン終了・行動メニュー）はクリックを横取りしないため、
        //   ボタンの下のマスにも反応して表示が変わることがある（デバッグ用UIなので許容）
        Vector3 world = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        if (!grid.TryWorldToCell(world, out Vector2Int cell)) return;

        TileData tile = grid.GetTile(cell);
        unit = tile != null ? tile.Occupant : null;
    }

    void OnGUI()
    {
        PanelRect = new Rect(); // 出していないときは「避ける矩形は無い」扱いに戻す

        if (!panelEnabled) return;
        if (unit == null || !unit.IsAlive) return; // 倒れて Destroy されたら自動で閉じる

        List<string> lines = BuildLines();

        // 画面右上に描く（左上のターン表示・左下の地形情報・右下の戦闘予測と重ならない位置）
        // 文字行の下にマナ・気勢のゲージバー2本ぶんの高さを足す（フェーズ28）
        float panelHeight = (lines.Count + 1) * lineHeight + padding * 2f + GaugeBars.TotalHeight; // +1 は見出しの行
        var rect = new Rect(Screen.width - panelWidth - 12f, 10f, panelWidth, panelHeight);
        PanelRect = rect; // 武器選択のUIの重なり回避に使う（2026-08-09）

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        GUI.Box(rect, "ユニット情報（デバッグ用）", boxStyle);

        var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
        for (int i = 0; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + padding,
                               rect.y + padding + (i + 1) * lineHeight,
                               panelWidth - padding * 2f, lineHeight),
                      lines[i], lineStyle);
        }

        // 文字行の下にマナ・気勢のゲージバーを描く（フェーズ28）
        GaugeBars.Draw(new Rect(rect.x + padding,
                                rect.y + padding + (lines.Count + 1) * lineHeight,
                                panelWidth - padding * 2f, GaugeBars.TotalHeight),
                       unit, fontSize);
    }

    /// <summary>
    /// 表示する行のリストを組み立てる（行数はユニットの状態で変わる）。
    /// 基本の行（能力値・武器・持ち物・状態）は対象選択中のパネルと共通の UnitInfoLines を使い、
    /// ここではデバッグ専用の行（敵AI・挟撃まわり）だけを足す。
    /// </summary>
    private List<string> BuildLines()
    {
        List<string> lines = UnitInfoLines.BuildBasic(unit);

        if (unit.Faction == Faction.Enemy)
            lines.Add(BuildAILine());

        // 挟撃まわり（該当するときだけ）— 敵AIの守り配置・ガード役潰しを観察するときに便利
        if (CombatRules.IsGuardingSomeone(unit, grid))
            lines.Add("誰かの挟撃ガード役になっている");
        if (CombatRules.IsPincerNegated(unit, grid))
            lines.Add("ガードで挟撃を無効化されている");

        return lines;
    }

    /// <summary>敵AIの行。性格と、待ち伏せ型なら挑発されたかどうかも出す。</summary>
    private string BuildAILine()
    {
        if (unit.AIProfile == EnemyAIProfile.Ambush)
            return unit.IsProvoked ? "AI: 待ち伏せ型（挑発済み＝突撃化）" : "AI: 待ち伏せ型（未挑発）";
        return "AI: 突撃型";
    }
}
