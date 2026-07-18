using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 戦闘予測パネル（Phase 15・IMGUI）。攻撃の対象選択に入った瞬間から
/// 「与えるダメージの内訳・三すくみ補正・挟撃の見込み・相手の残りHP」を画面右下に表示する。
/// 対象が複数いるときは最初の対象で表示を始め、別の対象にマウスを重ねるとそちらへ切り替わる
/// （マウスが離れても最後の対象を表示し続ける。2026-07-18 作者要望 — 以前は重ねたときだけ表示）。
/// 計算は実際の戦闘と同じ共有関数（DamageCalculator / CombatRules）を使うので、
/// 表示された数字と実戦の結果は必ず一致する。
/// BattleController が自動生成し、対象選択の開始・終了で Show / Hide を呼ぶ。
///
/// 被ダメモード（2026-07-18 作者要望）: 「被ダメ確認」コマンドでは向きが逆になり、
/// ホバーした敵から「選択中の味方が攻撃されたら」の被ダメージ予測を表示する（ShowIncoming）。
/// 反撃なしの仕様なので、これは攻撃時ではなく立ち位置を決めるための見積り。
/// 敵がどこから攻撃してくるかは未来の話で確定できないため、単発ダメージのみ
/// （ダメージ式は攻撃側の位置に依存しないので単発の数値自体は正確）。挟撃の追加は含まない。
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

    private Unit attacker;         // 与ダメモード: 攻撃する自分側（被ダメモードでは null）
    private Unit incomingDefender; // 被ダメモード: 攻撃を受ける自分側（与ダメモードでは null）
    private List<Unit> targets;    // ホバー候補（与ダメ: 攻撃対象の敵 ／ 被ダメ: 攻撃してくる敵）
    private GridManager grid;
    private Unit current; // いま予測を表示している対象（選択直後は最初の対象）

    /// <summary>対象選択の開始時に呼ぶ（attacker はいまの位置から targets を攻撃できる前提）。</summary>
    public void Show(Unit attacker, List<Unit> targets, GridManager grid)
    {
        this.attacker = attacker;
        incomingDefender = null;
        this.targets = targets;
        this.grid = grid;

        // 対象選択に入った瞬間から最初の対象で表示する（マウスを重ねるのを待たない）
        current = (targets != null && targets.Count > 0) ? targets[0] : null;
    }

    /// <summary>
    /// 被ダメ確認の開始時に呼ぶ（2026-07-18 作者要望）。defender（選択中の味方）が
    /// attackers のそれぞれから攻撃されたときの、被ダメージ予測を表示するモードになる。
    /// </summary>
    public void ShowIncoming(Unit defender, List<Unit> attackers, GridManager grid)
    {
        attacker = null;
        incomingDefender = defender;
        targets = attackers;
        this.grid = grid;
        current = (attackers != null && attackers.Count > 0) ? attackers[0] : null;
    }

    /// <summary>対象選択の終了時（攻撃実行・キャンセル・選択解除）に呼ぶ。</summary>
    public void Hide()
    {
        attacker = null;
        incomingDefender = null;
        targets = null;
        grid = null;
        current = null;
    }

    void OnGUI()
    {
        if ((attacker == null && incomingDefender == null) || targets == null || grid == null) return;

        // マウスの下に別の対象がいればそちらへ切り替える（離れても最後の対象を表示し続ける）
        if (Mouse.current != null && Camera.main != null)
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            if (grid.TryWorldToCell(world, out Vector2Int cell))
            {
                TileData tile = grid.GetTile(cell);
                Unit hovered = tile != null ? tile.Occupant : null;
                if (hovered != null && targets.Contains(hovered)) current = hovered;
            }
        }

        if (current == null) return;
        if (attacker != null) DrawForecast(current);
        else DrawIncoming(current);
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
            BuildTriangleLine(main.triangle, "有利", "不利"),
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

        DrawPanel(lines, "戦闘予測");
    }

    /// <summary>current の敵から「攻撃を受けたら」の予測を組み立てて描く（被ダメモード）。</summary>
    private void DrawIncoming(Unit enemy)
    {
        var lines = new List<string>
        {
            $"{enemy.Data.unitName} → {incomingDefender.Data.unitName}（受ける攻撃）",
        };

        // そもそも攻撃が成立しない相手は、その理由だけを出す
        string blocked = null;
        if (enemy.Weapon == null)
            blocked = "この相手は武装無しのため攻撃してこない";
        else if (!CombatRules.CanEngage(enemy, incomingDefender))
            blocked = "この相手からは攻撃を受けない（飛翔の制限）";

        if (blocked != null)
        {
            lines.Add(blocked);
            DrawPanel(lines, "被ダメージ予測");
            return;
        }

        DamageBreakdown main = DamageCalculator.CalculateBreakdown(enemy, incomingDefender, grid);
        int hpAfter = Mathf.Max(0, incomingDefender.CurrentHP - main.Total);

        lines.Add($"被ダメージ　{main.ToLogString()}");
        lines.Add(BuildTriangleLine(main.triangle, "相手が有利", "相手が不利"));
        lines.Add(hpAfter == 0
            ? $"自分のHP {incomingDefender.CurrentHP} → 0　倒される！"
            : $"自分のHP {incomingDefender.CurrentHP} → {hpAfter}");
        lines.Add("※相手の位置どり・挟撃の追加は含まない");

        DrawPanel(lines, "被ダメージ予測");
    }

    /// <summary>
    /// 三すくみ補正の行（2026-07-18 作者要望: 補正0のときも必ず表示する）。
    /// 式の中の「相性+3」表記とは別に、補正値だけを取り出して分かりやすく1行にする。
    /// 有利・不利の言い回しは与ダメ（自分が有利）と被ダメ（相手が有利）で呼び分ける。
    /// </summary>
    private static string BuildTriangleLine(int triangle, string advantage, string disadvantage)
    {
        if (triangle > 0) return $"三すくみ +{triangle}（{advantage}）";
        if (triangle < 0) return $"三すくみ {triangle}（{disadvantage}）";
        return "三すくみ ±0（補正なし）";
    }

    /// <summary>組み立てた行を画面右下のパネルに描く（title は見出し）。</summary>
    private void DrawPanel(List<string> lines, string title)
    {

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
}
