using System.Collections.Generic;

/// <summary>
/// ユニット1体の情報を「表示用の文字」に組み立てる共通処理（2026-07-26／2026-08-16 に2カラム化で作り直し）。
/// ここは文字を作るだけでゲームの状態には一切触れない。
///
/// 2026-08-16 の刷新前は「行を縦に並べるだけ」のパネルが2枚（右上のデバッグ用・右下の対象選択用）あり、
/// その共通の行を BuildBasic が作っていた。いまはパネルが1枚（UnitInfoPanel）に統合され、
/// 描画は UI/UnitInfoView が2カラムで行うので、ここは**枠ごとの短い文字を作る部品**の置き場になっている。
/// 持ち物のホバー詳細（BuildItemDetail）は持ち物メニュー（BattleController）とも共用する
/// ＝片方だけ直して表示がずれる、ということが起きないようにするのがこのクラスの役目。
/// </summary>
public static class UnitInfoLines
{
    /// <summary>兵種の表示名。兵種データがあればその名前（例：剣闘士）、無ければ移動タイプ名。</summary>
    public static string ClassName(Unit unit)
    {
        if (unit.Data.classData != null) return unit.Data.classData.className;
        return unit.Class.DisplayName();
    }

    /// <summary>
    /// クラス名の行の右に出す移動力（2026-08-10 の地上／飛翔の2本立て）。
    /// 2026-08-16 の修正でステータス枠の外に出て1行を丸ごと使えるようになったので、
    /// 以前の短縮形「4(飛6)」をやめて読みやすい形にした。
    /// 値が同じ兵種（飛行兵以外の全部）は数字1つだけ。
    /// </summary>
    public static string MoveLine(Unit unit)
    {
        if (unit.FlightMove == unit.GroundMove) return $"移動力 : {unit.GroundMove}";
        return $"移動力 : {unit.GroundMove}（飛翔{unit.FlightMove}）";
    }

    /// <summary>
    /// 兵種が装備できる武器種の行（「使用可能武器: 剣・斧」）。兵種経験値バーの真上に出す（2026-08-16 作者仕様）。
    /// 兵種データを持たない古い作りのユニットは種類が決まらないので「－」にする。
    /// </summary>
    public static string UsableWeaponsLine(Unit unit)
    {
        ClassData cls = unit.Data.classData;
        if (cls == null || cls.usableWeapons.Count == 0) return "使用可能武器: －";

        var names = new List<string>();
        foreach (WeaponType type in cls.usableWeapons) names.Add(type.DisplayName());
        return "使用可能武器: " + string.Join("・", names);
    }

    /// <summary>
    /// 持ち物リストの1行の左側（「鉄の剣（装備中）」「傷薬」）。
    /// 「装備中」の目印は EquippedWeapon で判定する（Weapon 窓口は「いま使えない武器」で null を返すため）。
    /// 残り回数は行の右端に別に出すので（ItemUsesText）、ここには付けない（2026-08-16 作者の修正案2）。
    /// 威力・射程・特効などの詳しい数字はホバー詳細（BuildItemDetail）に送る。
    /// </summary>
    public static string ItemLabel(ItemSlot slot, Unit owner)
    {
        string label = slot.Item.DisplayName;

        if (slot.Item is WeaponData && slot.Item == owner.EquippedWeapon) label += "（装備中）";

        return label;
    }

    /// <summary>
    /// 持ち物リストの1行の右端に出す数字（2026-08-16 作者指示で武器の耐久値を出していた場所。耐久は 2026-10-24 に撤廃）。
    ///  ・道具 … 残り回数「残り／最大」
    ///  ・武器 … メモリ消費「消費5」（2026-10-24。消費0の武器＝鉄の武器・杖は何も出さない）
    /// 何も出さない品は空文字。品の名前と並べると行の幅（222px）ぎりぎりになるので、描く側は実際の幅を測って
    /// 名前のほうを丸めること（UnitInfoView.FitText）。
    /// </summary>
    public static string ItemRightText(ItemSlot slot)
    {
        if (slot == null) return "";

        if (slot.Item is WeaponData weapon)
        {
            int cost = WeaponMemoryRules.CostOf(weapon);
            return cost > 0 ? $"消費{cost}" : "";
        }

        if (!slot.HasUses) return "";
        return $"{slot.UsesLeft}／{slot.MaxUses}";
    }

    /// <summary>
    /// 「状態」枠の行を組み立てる（2026-08-16 作者仕様・同日の修正案で3行に絞った）。並びは
    ///  1行目 … 「状態 : 」＋ 前衛/後衛・地上/飛翔（陣中/孤立は 2026-10-24 に補給ごと撤廃）
    ///  2行目 … ガードを受けているか（〇×と、その条件を括弧書き）
    ///  3行目 … 救出中の相手（運んでいるときだけ）
    /// 刷新の初版にあった「隣接した味方をガード中」「未行動／行動済み」の行は作者の指示で削除
    /// （行動済みは盤上でユニットが暗くなる既存表現で足りる）。
    /// 敵AIの性格は画面左下の専用パネル（UI/AIDebugPanel）へ移した。
    /// grid が無いとガードの判定ができないので、その行は空欄にする。
    /// </summary>
    public static List<string> BuildStateRows(Unit unit, GridManager grid)
    {
        var rows = new List<string> { "状態 : " + BuildConditionRow(unit) };

        if (unit.IsCarried)
        {
            // 格納中の貨物は盤上にいない＝ガードの判定に意味が無いので、その行の代わりに出す
            rows.Add("格納中（盤上にいない）");
        }
        else if (grid != null)
        {
            // 〇×だけだと何のガードか分からないので、条件を括弧で添える（2026-08-16 作者仕様）。
            // ×側の文言は列の幅に収めるため作者合意のうえ「隣接した」を省いた短い形にしてある
            rows.Add(CombatRules.IsPincerNegated(unit, grid)
                ? "ガード：〇（前衛歩兵と隣接）"
                : "ガード：×（前衛歩兵なし）");
        }
        else
        {
            rows.Add("");
        }

        if (unit.IsRescuing)
        {
            var names = new List<string>();
            foreach (Unit cargo in unit.Carried) names.Add(cargo.Data.unitName);
            rows.Add(Truncate($"救出中: {string.Join("、", names)}", StateRowWidthLimit));
        }

        return rows;
    }

    /// <summary>状態の1行目の中身。前衛/後衛・地上/飛翔を「・」でつなぐ。</summary>
    private static string BuildConditionRow(Unit unit)
    {
        return RangeText(unit) + "・" + FlightText(unit);
    }

    /// <summary>前衛／後衛。武器を装備していなければ（輸送隊・外した後など）「非武装」と出す。</summary>
    private static string RangeText(Unit unit)
    {
        WeaponData w = unit.EquippedWeapon;
        if (w == null) return "非武装";
        return w.category == WeaponCategory.Melee ? "前衛" : "後衛";
    }

    /// <summary>地上／飛翔。撃墜されて飛べない1ターンは区別して出す（2026-08-05）。</summary>
    private static string FlightText(Unit unit)
    {
        if (unit.IsFlying) return $"飛翔{unit.FlightTurnsLeft}";
        if (unit.IsFlightBlocked) return "地上×";   // 撃墜され飛翔できない
        return "地上";
    }

    /// <summary>
    /// 敵AIの性格の行。待ち伏せ型は挑発されたかどうかも出す（旧デバッグ用パネルから移設）。
    /// 2026-08-16 の修正案で状態枠から外れ、画面左下の UI/AIDebugPanel が呼ぶ窓口になった。
    /// </summary>
    public static string BuildAIRow(Unit unit)
    {
        if (unit.AIProfile == EnemyAIProfile.Ambush)
            return unit.IsProvoked ? "AI: 待ち伏せ型(挑発済)" : "AI: 待ち伏せ型(未挑発)";
        return "AI: 突撃型";
    }

    /// <summary>
    /// 持ち物・装備武器にマウスを重ねたときの詳細（1行目＝名前を見出しに、以降＝性能）。
    /// 武器は種類・前衛/後衛・威力・射程・メモリ消費・有効特効・装備付与（魔法・速さ−移動力・三すくみ逆転・
    /// 兵種限定。2026-10-24）、道具は効果・使用回数。
    /// 持ち物メニュー（右下220px）とユニット情報パネル（列の幅・約全角15文字）の**両方**が使うので、
    /// 1行は短くする。長い説明を足したくなったら行を分けること（2026-08-03 に実際に見切れた）。
    /// </summary>
    public static List<string> BuildItemDetail(ItemSlot slot)
    {
        var lines = new List<string>();
        if (slot == null || slot.Item == null) return lines;

        if (slot.Item is WeaponData w)
        {
            string cat = w.category == WeaponCategory.Melee ? "前衛" : "後衛";
            string range = w.minRange == w.maxRange ? $"{w.maxRange}" : $"{w.minRange}〜{w.maxRange}";
            lines.Add(w.weaponName);
            lines.Add($"種類: {w.type.DisplayName()}　{cat}");
            lines.Add($"威力 {w.might}　射程 {range}");

            // 1回攻撃するたびに払うメモリ（2026-10-24）。消費0の武器も「0」と出す＝いつでも使えることが分かる
            lines.Add($"メモリ消費: {WeaponMemoryRules.CostOf(w)}");

            // 有効・特効の行（2026-08-06）。「特効: 竜」「有効: 重装」のように短い行で足す
            // ＝どの武器に持ち替えるかを、この詳細だけで判断できるようにする
            lines.AddRange(EffectiveRules.BuildWeaponEffectLines(w));

            // 装備付与（2026-10-24）。持ち物メニューの詳細（全角14文字）に入る短い行にしてある
            if (w.magicDamage) lines.Add("魔法（魔力−魔防）");
            if (w.reducesSpeedByMove) lines.Add("装備中: 速さ−移動力");

            string reversal = BuildReversalLine(w);
            if (reversal != null) lines.Add(reversal);

            string exclusive = BuildExclusiveLine(w);
            if (exclusive != null) lines.Add(exclusive);
        }
        else if (slot.Item is ToolData t)
        {
            lines.Add(t.toolName);
            lines.Add($"効果: HP{t.healAmount}回復");
            lines.Add($"使用回数: 残り{slot.UsesLeft}／{t.maxUses}回");
        }
        return lines;
    }

    /// <summary>
    /// 三すくみ逆転の行（「三すくみ逆転: 槍の騎馬」。逆転を持たない武器は null）。
    /// 「槍の」は、三すくみでこの武器に勝つ武器種（剣なら槍）＝逆転が起きる相手の武器。
    /// </summary>
    private static string BuildReversalLine(WeaponData weapon)
    {
        if (weapon.triangleReversalTags == ClassTag.None) return null;

        WeaponType? counter = CombatRules.TriangleCounterOf(weapon.type);
        string holder = counter != null ? $"{counter.Value.DisplayName()}の" : "";
        return $"三すくみ逆転: {holder}{weapon.triangleReversalTags.DisplayName()}";
    }

    /// <summary>兵種限定の行（「弓聖専用」。限定の無い武器は null）。長くなりすぎたら「…」で切る。</summary>
    private static string BuildExclusiveLine(WeaponData weapon)
    {
        if (weapon.exclusiveClasses == null || weapon.exclusiveClasses.Count == 0) return null;

        var names = new List<string>();
        foreach (ClassData cls in weapon.exclusiveClasses)
            if (cls != null) names.Add(cls.className);
        if (names.Count == 0) return null;

        return Truncate(string.Join("・", names) + "専用", DetailWidthLimit);
    }

    /// <summary>ホバー詳細の1行に入れてよい長さ（半角の個数。持ち物メニューの詳細＝全角14文字に合わせる）。</summary>
    private const int DetailWidthLimit = 28;

    /// <summary>
    /// 状態の行に入れてよい長さ（半角の個数。全角は2つぶんと数える）。
    /// パネル幅500pxのとき枠の中は222px・文字の大きさ14pxで、全角は約14px・半角は約7pxなので
    /// 半角31個ぶん。少し余裕を見て30にしてある。
    /// </summary>
    private const int StateRowWidthLimit = 30;

    /// <summary>収まらない文字列を「…」で打ち切る（半角の個数で測る）。</summary>
    public static string Truncate(string text, int halfWidthLimit)
    {
        if (string.IsNullOrEmpty(text) || HalfWidthLength(text) <= halfWidthLimit) return text;

        int length = 0;
        for (int i = 0; i < text.Length; i++)
        {
            length += (text[i] < 0x80) ? 1 : 2;
            if (length > halfWidthLimit - 1) return text.Substring(0, i) + "…";
        }
        return text;
    }

    /// <summary>文字列の長さを半角の個数で測る（全角＝2、半角＝1のざっくり計算）。</summary>
    private static int HalfWidthLength(string text)
    {
        int length = 0;
        foreach (char c in text) length += (c < 0x80) ? 1 : 2;
        return length;
    }
}
