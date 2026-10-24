using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 戦闘予測パネル（Phase 15・IMGUI）。攻撃の対象選択に入った瞬間から
/// 「与えるダメージの内訳・三すくみ補正・挟撃の見込み・相手の残りHP」を盤面の下に表示する。
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
///
/// 武器の行（2026-08-09 作者仕様）: 攻撃は「武器を選んでから相手を選ぶ」順になり、
/// 対象選択に入る時点で武器はもう装備されている。どの武器で殴るのかを
/// 1行目に添える（BuildWeaponLine）。予測はいつも装備中の武器で組み立てるので、
/// 内訳・三すくみ・静止撃ち・特効・挟撃・撃墜は何もしなくてもその武器の数字になる。
///
/// 置き場所（2026-08-17 作者指示）: **盤上の4隅のどれか**。
/// 「攻撃するユニット自身と、選べる相手が乗っているマス」を囲む範囲（と地形情報の枠）に
/// 一番重ならない隅を選ぶ＝狙う相手がパネルで隠れない。
/// パネルは**縦長**（幅は固定・高さは中身しだい）で、長い注記の行は折り返す。
/// ユニット情報は画面右上なので場所を取り合わない
/// （置き場所の変遷: 2026-08-09 右下に縦積み → 08-16 ユニット情報が右上へ移り縦積み撤去 →
/// 同日 盤面の下 → 08-17 盤上の4隅）。
///
/// ダメージの内訳（2026-08-17 作者指示）: **計算式は出さない**。代わりに
/// **「攻撃力」（力/魔力＋武器威力。三すくみを含まない）**を与ダメージの下、
/// **「防御力」（守備/魔防＋地形防御。同じく三すくみを含まない）**を三すくみの下に出す。
/// 読み手は「攻撃力 ＋ 三すくみ − 防御力 ＝ ダメージ」と足し引きできる。
/// </summary>
public class BattleForecast : MonoBehaviour
{
    [Header("見た目")]
    [Tooltip("パネルの幅（ピクセル）。縦長にするため狭くしてあり、長い注記の行は折り返す")]
    public float panelWidth = 220f;
    [Tooltip("1行の高さ（ピクセル）。折り返した行はこれより高くなる")]
    public float lineHeight = 20f;
    [Tooltip("パネルの枠と文字の間の余白（ピクセル）")]
    public float padding = 8f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 14;

    /// <summary>パネルと盤面の端のあいだに空ける距離（隅に置くときの寄せ具合）。</summary>
    private const float PanelInset = 8f;

    /// <summary>隅の候補（左下・右下・左上・右上）。使い回して余計なゴミを作らない。</summary>
    private readonly Rect[] cornerBuffer = new Rect[4];

    /// <summary>行ごとの高さ（折り返した行があるので毎回測る）。使い回す。</summary>
    private readonly List<float> rowHeights = new List<float>();

    private Unit attacker;         // 与ダメモード: 攻撃する自分側（被ダメモードでは null）
    private Unit incomingDefender; // 被ダメモード: 攻撃を受ける自分側（与ダメモードでは null）
    private List<Unit> targets;    // ホバー候補（与ダメ: 攻撃対象の敵 ／ 被ダメ: 攻撃してくる敵）
    private GridManager grid;
    private Unit current;  // いま予測を表示している対象（選択直後は最初の対象）
    private bool critical; // 必殺技モードか（フェーズ29。倍率込みの数字を出す）
    private bool attackerHasMoved; // 攻撃側がこの行動で移動したか（後衛武器の静止撃ち有利の判定）

    /// <summary>
    /// 直近に描いたパネルの矩形（GUI座標。表示していなければ Rect.zero）。
    /// 武器の一覧がこのパネルを避けるために公開している
    /// （2026-08-09。ActionMenu.MenuRect と同じ「描いた矩形を公開する」作法）。
    /// </summary>
    public Rect PanelRect { get; private set; }

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
        PanelRect = new Rect(); // 消えたパネルの位置に情報パネル・武器の一覧が引きずられないように戻す
    }

    void OnGUI()
    {
        PanelRect = new Rect(); // 描かなかったときは「積む相手・避ける相手は無い」扱いに戻す

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

    /// <summary>defender への攻撃の予測を組み立てて描く（装備中の武器で計算する）。</summary>
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
        };

        // 「いまどの武器で殴ろうとしているか」（2026-08-09。武器を選んでから
        // ここへ来るので、選んだ武器がそのまま出る。武器の一覧の行は武器名だけ＝作者合意）
        string weaponLine = BuildWeaponLine(attacker.EquippedWeapon);
        if (weaponLine != null) lines.Add(weaponLine);

        // 計算式は出さず、攻撃力を与ダメージの下・防御力を三すくみの下に出す（2026-08-17 作者指示）
        lines.Add($"与ダメージ　{main.Total}");
        lines.Add($"攻撃力　{main.AttackTotal}");
        lines.Add(BuildTriangleLine(main.triangle, "有利", "不利"));
        lines.Add($"防御力　{main.DefenseTotal}");

        // 後衛武器なら「静止撃ちの有利が付いたか」を1行で添える（数字の理由が分かるように）
        string standNote = BuildStandingShotNote(attacker, attackerHasMoved);
        if (standNote != null) lines.Add(standNote);

        // 有効・特効が乗っているなら、その理由も1行で添える（2026-08-06）
        string effectiveNote = BuildEffectiveNote(attacker, defender, incoming: false);
        if (effectiveNote != null) lines.Add(effectiveNote);

        if (critical)
            lines.Add($"必殺技 ×{GaugeRules.CriticalMultiplier:0.#}倍（メモリを1本使う。挟撃は等倍）");
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

    /// <summary>
    /// 「武器: 斬馬刀」の行（2026-08-09。当時は残り耐久も添えていたが、耐久は 2026-10-24 に撤廃）。
    /// 非武装（＝武器を持たないユニット）のときは null を返して行を足さない。
    /// </summary>
    private string BuildWeaponLine(WeaponData weapon)
    {
        if (weapon == null) return null;

        return $"武器: {weapon.DisplayName}";
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
            blocked = "この相手は非武装のため攻撃してこない";
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

        lines.Add($"被ダメージ　{main.Total}");
        lines.Add($"攻撃力　{main.AttackTotal}");
        lines.Add(BuildTriangleLine(main.triangle, "相手が有利", "相手が不利"));
        lines.Add($"防御力　{main.DefenseTotal}");

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

        lines.Add($"移動時　{moved.Total}");
        lines.Add($"静止時　{stand.Total}");
        // 攻撃力・防御力は移動したかで変わらない（変わるのは三すくみだけ）ので1行ずつ
        lines.Add($"攻撃力　{moved.AttackTotal}");
        lines.Add($"三すくみ　移動時 {Signed(moved.triangle)} ／ 静止時 {Signed(stand.triangle)}（相手が有利）");
        lines.Add($"防御力　{moved.DefenseTotal}");

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
    /// 前衛武器・杖・非武装では静止撃ち有利が関係しないので null（行を足さない）。
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
    /// 相性が無ければ null（行を足さない）。武器が使えないときは Weapon が null になるので
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

    /// <summary>
    /// 組み立てた行を盤上の隅のパネルに描く（title は見出し）。
    /// パネルは幅が固定の縦長で、長い注記の行は折り返す＝高さは中身しだいに決まる。
    /// </summary>
    private void DrawPanel(List<string> lines, string title)
    {
        var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = true };
        float textWidth = panelWidth - padding * 2f;

        // 行ごとの高さを先に測る（折り返した行は2行分以上になる）
        rowHeights.Clear();
        float contentHeight = 0f;
        foreach (string line in lines)
        {
            float height = Mathf.Max(lineHeight, lineStyle.CalcHeight(new GUIContent(line), textWidth));
            rowHeights.Add(height);
            contentHeight += height;
        }

        float panelHeight = contentHeight + lineHeight + padding * 2f; // +1行ぶんは見出しの行
        Rect rect = ChoosePanelRect(panelHeight);
        PanelRect = rect; // 武器の一覧が避けるのに使う（2026-08-09）

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        GUI.Box(rect, title, boxStyle);

        float y = rect.y + padding + lineHeight;
        for (int i = 0; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + padding, y, textWidth, rowHeights[i]), lines[i], lineStyle);
            y += rowHeights[i];
        }
    }

    /// <summary>
    /// パネルを置く場所を決める（2026-08-17 作者指示）。盤上の4隅のうち、
    /// **攻撃するユニット自身と選べる相手が乗っている範囲**（と地形情報の枠）に
    /// 一番重ならない隅を選ぶ＝狙う相手がパネルで隠れない。
    /// 隅を見る順番は 左下・右下・左上・右上 で固定してあるので、
    /// 同じ重なり具合なら常に同じ隅に出る（見る場所が毎回変わらない）。
    /// </summary>
    private Rect ChoosePanelRect(float panelHeight)
    {
        Rect board = grid != null ? grid.GetBoardScreenRect() : new Rect();

        // 盤面の場所が分からないときは画面の左下（従来の位置）
        if (board.width <= 0f)
        {
            return new Rect(12f, Mathf.Max(12f, Screen.height - panelHeight - 12f),
                            panelWidth, panelHeight);
        }

        float left = board.x + PanelInset;
        float right = board.xMax - panelWidth - PanelInset;
        float top = board.y + PanelInset;
        float bottom = board.yMax - panelHeight - PanelInset;

        cornerBuffer[0] = new Rect(left, bottom, panelWidth, panelHeight);   // 左下
        cornerBuffer[1] = new Rect(right, bottom, panelWidth, panelHeight);  // 右下
        cornerBuffer[2] = new Rect(left, top, panelWidth, panelHeight);      // 左上
        cornerBuffer[3] = new Rect(right, top, panelWidth, panelHeight);     // 右上

        Rect units = BuildUnitsRect();
        Rect terrain = grid != null ? grid.TerrainInfoRect : new Rect();

        Rect best = cornerBuffer[0];
        float bestOverlap = float.MaxValue;
        foreach (Rect candidate in cornerBuffer)
        {
            float overlap = OverlapArea(candidate, units) + OverlapArea(candidate, terrain);
            if (overlap >= bestOverlap) continue;

            best = candidate;
            bestOverlap = overlap;
            if (overlap <= 0f) break; // 何とも重ならない隅が見つかればそれで決まり
        }

        // 盤面が画面より大きいときでもパネルが画面から出ないように押し込む
        best.x = Mathf.Clamp(best.x, 0f, Mathf.Max(0f, Screen.width - panelWidth));
        best.y = Mathf.Clamp(best.y, 0f, Mathf.Max(0f, Screen.height - panelHeight));
        return best;
    }

    /// <summary>
    /// パネルが避ける範囲＝自分（攻撃する側／被ダメを見ている側）と、選べる相手が乗っている
    /// マスをまとめて囲んだ矩形（GUI座標）。誰もいなければ Rect.zero。
    /// ホバーで表示が切り替わっても範囲は変わらないので、選択中にパネルが動き回ることはない。
    /// </summary>
    private Rect BuildUnitsRect()
    {
        bool any = false;
        Vector2Int min = Vector2Int.zero;
        Vector2Int max = Vector2Int.zero;

        Unit self = attacker != null ? attacker : incomingDefender;
        if (IsOnBoard(self))
        {
            min = max = self.GridPosition;
            any = true;
        }

        if (targets != null)
        {
            foreach (Unit unit in targets)
            {
                if (!IsOnBoard(unit)) continue;

                if (!any)
                {
                    min = max = unit.GridPosition;
                    any = true;
                    continue;
                }
                min = Vector2Int.Min(min, unit.GridPosition);
                max = Vector2Int.Max(max, unit.GridPosition);
            }
        }

        return any ? grid.GetCellsScreenRect(min, max) : new Rect();
    }

    /// <summary>盤上にいて座標が意味を持つユニットか（格納中の貨物は盤上にいない）。</summary>
    private static bool IsOnBoard(Unit unit) => unit != null && unit.IsAlive && !unit.IsCarried;

    /// <summary>2つの矩形が重なっている面積（重なっていなければ0）。どちらかが空でも0。</summary>
    private static float OverlapArea(Rect a, Rect b)
    {
        if (a.width <= 0f || a.height <= 0f || b.width <= 0f || b.height <= 0f) return 0f;

        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.x, b.x);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.y, b.y);
        return (w <= 0f || h <= 0f) ? 0f : w * h;
    }
}
