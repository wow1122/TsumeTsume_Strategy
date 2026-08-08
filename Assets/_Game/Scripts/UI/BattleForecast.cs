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
///
/// 静止撃ち有利（2026-07-30 作者仕様）: 後衛武器（弓・魔法）は「マスから動かずに撃つ」と
/// 三すくみ有利が付く。与ダメモードでは選択中の味方が実際に移動したか（attackerHasMoved）で
/// 確定した数字を出す。被ダメモードでは敵が動くかどうかを確定できないので、
/// 相手が後衛武器のときだけ「移動して撃つ場合／静止して撃つ場合」の両方を並べて見せる。
///
/// 撃墜（2026-08-05 作者仕様）: 対空武器で飛翔中の相手を撃つと相手は着地する。
/// 攻撃する前に分かるように、その組み合わせのときだけ注記を1行足す（BuildShootDownNote）。
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
    private Unit current;  // いま予測を表示している対象（選択直後は最初の対象）
    private bool critical; // 必殺技モードか（フェーズ29。倍率込みの数字を出す）
    private bool attackerHasMoved; // 攻撃側がこの行動で移動したか（後衛武器の静止撃ち有利の判定）

    /// <summary>
    /// 対象選択の開始時に呼ぶ（attacker はいまの位置から targets を攻撃できる前提）。
    /// critical = true なら必殺技の予測（ダメージに倍率がかかった数字）を表示する。
    /// attackerHasMoved は攻撃側がこの行動で移動したか（ActionContext.hasMoved をそのまま渡す）。
    /// </summary>
    public void Show(Unit attacker, List<Unit> targets, GridManager grid, bool critical = false,
                    bool attackerHasMoved = true)
    {
        this.attacker = attacker;
        incomingDefender = null;
        this.targets = targets;
        this.grid = grid;
        this.critical = critical;
        this.attackerHasMoved = attackerHasMoved;

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
        critical = false;
        attackerHasMoved = true; // 被ダメモードでは相手の移動を仮定せず、両方の数字を並べて出す
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
        critical = false;
        attackerHasMoved = true;
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
        // 本体ダメージ（実戦と同じ計算窓口。内訳つき）。
        // 必殺技モードでは実戦と同じ倍率を渡すので、表示された数字と実際の結果は必ず一致する
        float multiplier = critical ? GaugeRules.CriticalMultiplier : 1f;
        DamageBreakdown main = DamageCalculator.CalculateBreakdown(attacker, defender, grid,
                                                                  multiplier, attackerHasMoved);
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

        // 後衛武器なら「静止撃ちの有利が付いたか」を1行で添える（数字の理由が分かるように）
        string standNote = BuildStandingShotNote(attacker, attackerHasMoved);
        if (standNote != null) lines.Add(standNote);

        // 有効・特効が乗っているなら、その理由も1行で添える（2026-08-06）
        string effectiveNote = BuildEffectiveNote(attacker, defender, incoming: false);
        if (effectiveNote != null) lines.Add(effectiveNote);

        if (critical)
            lines.Add($"必殺技 ×{GaugeRules.CriticalMultiplier:0.#}倍（マナを1本使う。挟撃は等倍）");
        if (ally != null)
        {
            lines.Add(guard == null
                ? $"挟撃 +{pincerDamage}（{ally.Data.unitName}）"
                : $"挟撃はガードで無効（{guard.Data.unitName}）");
        }
        lines.Add(hpAfter == 0
            ? $"相手HP {defender.CurrentHP} → 0　撃破！"
            : $"相手HP {defender.CurrentHP} → {hpAfter}");

        // 撃墜（2026-08-05 作者仕様）。倒し切るなら着地は起きないので、生き残るときだけ出す
        if (hpAfter > 0)
        {
            string shootDownNote = BuildShootDownNote(attacker, defender, incoming: false);
            if (shootDownNote != null) lines.Add(shootDownNote);
        }

        DrawPanel(lines, critical ? "戦闘予測（必殺技）" : "戦闘予測");
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
            blocked = enemy.IsEquippedWeaponWorn
                ? "この相手は武器が傷んでいるため攻撃してこない（装備無し扱い）"
                : "この相手は武装無しのため攻撃してこない";
        else if (!CombatRules.CanEngage(enemy, incomingDefender))
            blocked = "この相手からは攻撃を受けない（飛翔の制限）";

        if (blocked != null)
        {
            lines.Add(blocked);
            DrawPanel(lines, "被ダメージ予測");
            return;
        }

        // 相手が後衛武器（弓・魔法）なら、静止撃ち有利が付くかは相手が動くかどうかで決まる。
        // どちらになるかは事前に確定できないので、両方の数字を並べて見せる（2026-07-30 作者仕様）
        if (CombatRules.HasStandingShotAdvantage(enemy))
        {
            DrawIncomingBothCases(enemy, lines);
            return;
        }

        DamageBreakdown main = DamageCalculator.CalculateBreakdown(enemy, incomingDefender, grid);
        int hpAfter = Mathf.Max(0, incomingDefender.CurrentHP - main.Total);

        lines.Add($"被ダメージ　{main.ToLogString()}");
        lines.Add(BuildTriangleLine(main.triangle, "相手が有利", "相手が不利"));

        string incomingEffectiveNote = BuildEffectiveNote(enemy, incomingDefender, incoming: true);
        if (incomingEffectiveNote != null) lines.Add(incomingEffectiveNote);

        lines.Add(hpAfter == 0
            ? $"自分のHP {incomingDefender.CurrentHP} → 0　倒される！"
            : $"自分のHP {incomingDefender.CurrentHP} → {hpAfter}");

        string shootDownNote = BuildShootDownNote(enemy, incomingDefender, incoming: true);
        if (shootDownNote != null) lines.Add(shootDownNote);

        lines.Add("※相手の位置どり・挟撃の追加は含まない");

        DrawPanel(lines, "被ダメージ予測");
    }

    /// <summary>
    /// 相手が後衛武器（弓・魔法）のときの被ダメージ予測（2026-07-30 作者仕様）。
    /// 「移動して撃つ場合」と「マスから動かずに撃つ場合（三すくみ有利つき）」を並べて描く。
    /// 静止して撃たれる方が必ず痛いので、立ち位置を決めるときは下の数字を見ればよい。
    /// </summary>
    private void DrawIncomingBothCases(Unit enemy, List<string> lines)
    {
        DamageBreakdown moved = DamageCalculator.CalculateBreakdown(
            enemy, incomingDefender, grid, 1f, attackerHasMoved: true);
        DamageBreakdown stand = DamageCalculator.CalculateBreakdown(
            enemy, incomingDefender, grid, 1f, attackerHasMoved: false);

        int hp = incomingDefender.CurrentHP;
        int hpMoved = Mathf.Max(0, hp - moved.Total);
        int hpStand = Mathf.Max(0, hp - stand.Total);

        lines.Add($"移動時　{moved.ToLogString()}");
        lines.Add($"静止時　{stand.ToLogString()}");
        lines.Add($"三すくみ　移動時 {Signed(moved.triangle)} ／ 静止時 {Signed(stand.triangle)}（相手が有利）");

        // 有効・特効は移動したかに関係なく同じだけ乗るので、両方の場合に共通の1行として出す
        string effectiveNote = BuildEffectiveNote(enemy, incomingDefender, incoming: true);
        if (effectiveNote != null) lines.Add(effectiveNote);

        string hpLine = $"自分のHP {hp} → {hpMoved}（移動時）／ {hpStand}（静止時）";
        if (hpStand == 0) hpLine += hpMoved == 0 ? "　どちらでも倒される！" : "　静止して撃たれると倒される！";
        lines.Add(hpLine);

        string shootDownNote = BuildShootDownNote(enemy, incomingDefender, incoming: true);
        if (shootDownNote != null) lines.Add(shootDownNote);

        lines.Add("※弓・魔法はマスから動かずに撃つと三すくみ有利（静止時）");
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

    /// <summary>補正値の符号つき表記（0 は「±0」）。両方の数字を1行に並べるときに使う。</summary>
    private static string Signed(int value)
    {
        if (value > 0) return $"+{value}";
        if (value < 0) return value.ToString();
        return "±0";
    }

    /// <summary>
    /// 後衛武器（弓・魔法）の攻撃にだけ添える注記（2026-07-30 作者仕様・静止撃ち有利）。
    /// 三すくみの行に出た数字が「なぜ付いた／付かなかった」のかを1行で説明する。
    /// 前衛武器・杖・武装無しでは静止撃ち有利が関係しないので null（行を足さない）。
    /// 対象かどうかの判定は戦闘本体と同じ CombatRules.HasStandingShotAdvantage を使う。
    /// </summary>
    private static string BuildStandingShotNote(Unit attacker, bool hasMoved)
    {
        if (!CombatRules.HasStandingShotAdvantage(attacker)) return null;

        return hasMoved
            ? "※移動して撃つため、静止撃ちの三すくみ有利なし"
            : "※マスから動かずに撃つため、三すくみ有利がつく";
    }

    /// <summary>
    /// 有効・特効の注記（2026-08-06 作者仕様）。相手の兵種の特攻タグに武器が刺さっているとき、
    /// 「なぜ威力が増えているのか」を1行で説明する（式の中には「威力5×3」としか出ないため）。
    /// 相性が無ければ null（行を足さない）。武器が傷んでいるときは Weapon が null になるので
    /// 自動的に注記も出ない。判定は戦闘本体と同じ EffectiveRules を使うので、
    /// 表示とダメージが食い違うことはない。
    /// </summary>
    private static string BuildEffectiveNote(Unit attacker, Unit defender, bool incoming)
    {
        WeaponData weapon = attacker.Weapon;
        int multiplier = EffectiveRules.GetMultiplier(weapon, defender);
        if (multiplier <= 1) return null;

        string label = EffectiveRules.GetLabel(multiplier);
        string tags = EffectiveRules.GetMatchedTags(weapon, defender).DisplayName();
        string who = incoming ? "相手の" : "";

        return $"※{who}{label}: {tags}（武器威力{multiplier}倍）";
    }

    /// <summary>
    /// 撃墜の注記（2026-08-05 作者仕様）。対空武器（弓・魔導書・光魔法）の攻撃が
    /// 飛翔中の相手に当たると、相手はその場に着地する（落下ダメージなし）。
    /// 与ダメ・被ダメどちらのモードでも、その組み合わせのときだけ1行足す。
    /// 判定は戦闘本体と同じ CombatRules.ShootsDown を使うので、表示と実際の結果は必ず一致する。
    /// </summary>
    private static string BuildShootDownNote(Unit attacker, Unit defender, bool incoming)
    {
        if (!CombatRules.ShootsDown(attacker, defender)) return null;

        return incoming
            ? "※当たると着地させられる（この後は近接にも狙われる）"
            : "※当てると着地させる（飛翔が解除される）";
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
