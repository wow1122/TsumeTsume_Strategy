using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 戦闘予測パネル（Phase 15・IMGUI）。攻撃の対象選択中、対象の敵にマウスを重ねると
/// 「与えるダメージの内訳・挟撃の見込み・相手の残りHP」を画面右下に表示する。
/// 計算は実際の戦闘と同じ共有関数（DamageCalculator / CombatRules）を使うので、
/// 表示された数字と実戦の結果は必ず一致する。
/// BattleController が自動生成し、対象選択の開始・終了で Show / Hide を呼ぶ。
/// </summary>
public class BattleForecast : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("パネルの幅（ピクセル）。ダメージ内訳が1行で収まる幅にしてある")]
    public float panelWidth = 430f;
    [Tooltip("1行の高さ（ピクセル）")]
    public float lineHeight = 24f;
    [Tooltip("パネルの枠と文字の間の余白（ピクセル）")]
    public float padding = 10f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 14;

    private Unit attacker;
    private List<Unit> targets;
    private GridManager grid;

    /// <summary>対象選択の開始時に呼ぶ（attacker はいまの位置から targets を攻撃できる前提）。</summary>
    public void Show(Unit attacker, List<Unit> targets, GridManager grid)
    {
        this.attacker = attacker;
        this.targets = targets;
        this.grid = grid;
    }

    /// <summary>対象選択の終了時（攻撃実行・キャンセル・選択解除）に呼ぶ。</summary>
    public void Hide()
    {
        attacker = null;
        targets = null;
        grid = null;
    }

    void OnGUI()
    {
        if (attacker == null || targets == null || grid == null) return;
        if (Mouse.current == null || Camera.main == null) return;

        // マウスの下のマスに攻撃対象がいるときだけ表示する
        Vector3 world = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        if (!grid.TryWorldToCell(world, out Vector2Int cell)) return;

        TileData tile = grid.GetTile(cell);
        Unit defender = tile != null ? tile.Occupant : null;
        if (defender == null || !targets.Contains(defender)) return;

        DrawForecast(defender);
    }

    /// <summary>defender への攻撃の予測を組み立てて描く。</summary>
    private void DrawForecast(Unit defender)
    {
        // 本体ダメージ（実戦と同じ計算窓口。内訳つき）
        DamageBreakdown main = DamageCalculator.CalculateBreakdown(attacker, defender, grid);
        int total = main.Total;

        // 挟撃の見込み：成立するか・ガードで無効化されるか・追加でいくら入るか
        Unit ally = CombatRules.FindPincerAlly(attacker.GridPosition, attacker, defender, grid);
        Unit guard = (ally != null) ? CombatRules.FindPincerGuard(defender, grid) : null;
        int pincerDamage = 0;
        if (ally != null && guard == null)
        {
            pincerDamage = DamageCalculator.Calculate(ally, defender, grid);
            total += pincerDamage;
        }

        int hpAfter = Mathf.Max(0, defender.CurrentHP - total);

        var lines = new List<string>
        {
            $"{attacker.Data.unitName} → {defender.Data.unitName}",
            $"与ダメージ　{main.ToLogString()}",
        };
        if (ally != null)
        {
            lines.Add(guard == null
                ? $"挟撃 +{pincerDamage}（{ally.Data.unitName}）"
                : $"挟撃はガードで無効（{guard.Data.unitName}）");
        }
        lines.Add(hpAfter == 0
            ? $"相手HP {defender.CurrentHP} → 0　撃破！"
            : $"相手HP {defender.CurrentHP} → {hpAfter}");

        // 画面右下に描く（左下の地形情報表示と重ならない位置）
        float panelHeight = (lines.Count + 1) * lineHeight + padding * 2f; // +1 は見出しの行
        var rect = new Rect(Screen.width - panelWidth - 12f,
                            Screen.height - panelHeight - 12f,
                            panelWidth, panelHeight);

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        GUI.Box(rect, "戦闘予測", boxStyle);

        var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
        for (int i = 0; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + padding,
                               rect.y + padding + (i + 1) * lineHeight,
                               panelWidth - padding * 2f, lineHeight),
                      lines[i], lineStyle);
        }
    }
}
