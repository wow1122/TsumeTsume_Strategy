using System.Collections.Generic;

/// <summary>
/// ユニット1体の情報を「表示用の行」に組み立てる共通処理（2026-07-26）。
/// デバッグ用のユニット情報パネル（DebugUnitPanel）と、対象選択中の情報パネル
/// （UnitInfoPanel）が同じ内容を出すための置き場。どちらか片方だけ直して
/// 表示がずれる、ということが起きないようにここへ集約している。
/// 文字を作るだけでゲームの状態には一切触れない。
/// </summary>
public static class UnitInfoLines
{
    /// <summary>
    /// 基本の行（名前・兵種・座標／HP／能力値2行／装備武器／持ち物／状態）を組み立てる。
    /// 敵AIや挟撃まわりの行はデバッグ専用なので、必要な側（DebugUnitPanel）で足す。
    /// </summary>
    public static List<string> BuildBasic(Unit unit)
    {
        var lines = new List<string>();

        // 見出し行。格納中の貨物は盤上にいない＝座標が意味を持たないので出さない（2026-07-26）
        string side = unit.Faction == Faction.Player ? "自軍" : "敵軍";
        string place = unit.IsCarried ? "" : $"({unit.GridPosition.x},{unit.GridPosition.y})";
        lines.Add($"{unit.Data.unitName}（{side}・{ClassName(unit)}）{place}");
        lines.Add($"HP {unit.CurrentHP}／{unit.MaxHP}");
        lines.Add($"力{unit.Strength}　魔力{unit.Magic}　技{unit.Skill}　速さ{unit.Speed}");
        lines.Add($"守備{unit.Defense}　魔防{unit.Resistance}　移動{MoveText(unit)}");
        lines.Add(WeaponLine(unit));
        AddWeaponEffectLines(unit, lines);
        AddDurabilityLines(unit, lines);
        AddItemLines(unit, lines);
        AddStateLines(unit, lines);

        return lines;
    }

    /// <summary>
    /// 移動力の表示（2026-08-10）。飛行兵は地上と飛翔で値が違うので「4（飛翔6）」と両方出す。
    /// 値が同じ兵種（飛行兵以外の全部）は今までどおり数字1つだけ＝行が伸びない。
    /// </summary>
    private static string MoveText(Unit unit)
    {
        if (unit.FlightMove == unit.GroundMove) return unit.GroundMove.ToString();
        return $"{unit.GroundMove}（飛翔{unit.FlightMove}）";
    }

    /// <summary>
    /// 装備中の武器の行。「武器: 槍（前衛・威力5・射程1）」のような形式。
    /// 【重要】ここは Weapon 窓口ではなく EquippedWeapon を読む。傷んだ武器も「装備している」
    /// ことに変わりはなく、何を持っているかが消えてしまうと状況が読めなくなるため。
    /// 耐久と「傷んでいる」の注記はこの行には足さない（下の AddDurabilityLines が別の行にする）。
    /// </summary>
    public static string WeaponLine(Unit unit)
    {
        WeaponData w = unit.EquippedWeapon;
        if (w == null) return "武器: なし（攻撃不可）";

        string cat = w.category == WeaponCategory.Melee ? "前衛" : "後衛";
        string range = w.minRange == w.maxRange ? $"{w.maxRange}" : $"{w.minRange}〜{w.maxRange}";
        return $"武器: {w.weaponName}（{cat}・威力{w.might}・射程{range}）";
    }

    /// <summary>
    /// 装備中の武器の相性の行（2026-08-06・有効と特効）。
    /// 「特効: 竜」「有効: 騎馬・天馬」のように、この武器が何に強いかを出す。
    /// 武器の行に足すと幅（全角22文字）を超えるので別の行にする（耐久の行と同じ理由）。
    /// 相性を持たない武器（剣・杖など）では1行も足さない。
    /// 耐久が尽きていても「何を装備しているか」は変わらないので、ここも EquippedWeapon を読む。
    /// </summary>
    public static void AddWeaponEffectLines(Unit unit, List<string> lines)
    {
        foreach (string line in EffectiveRules.BuildWeaponEffectLines(unit.EquippedWeapon))
            lines.Add(line);
    }

    /// <summary>
    /// 装備中の武器の耐久の行（耐久ありの武器のときだけ足す。フェーズ31）。
    /// 【重要】1行に詰め込まないこと。このパネルは幅340px・余白10pxで文字に使えるのは320pxしかなく、
    /// 「武器: 魔道書（後衛・威力5・射程2）」だけでもう250px近くを使う。耐久と注記を同じ行に
    /// 続けると右端で見切れる（2026-08-03 に実際に見切れた）。だから行を分け、
    /// 傷んでいるときの説明はさらに次の行へ送る。
    /// </summary>
    public static void AddDurabilityLines(Unit unit, List<string> lines)
    {
        ItemSlot slot = unit.EquippedSlot;
        if (slot == null || !slot.HasDurability) return;

        bool worn = unit.IsEquippedWeaponWorn;
        lines.Add($"耐久: {slot.UsesLeft}／{slot.MaxUses}" + (worn ? "　※傷んでいる" : ""));
        if (worn) lines.Add("　攻撃できない（装備無し扱い）");
    }

    /// <summary>
    /// 持ち物の行を追加する。装備中の武器は「装備中」、道具は残り回数、
    /// 耐久のある武器は残りの耐久を添える。
    /// 「装備中」の目印は EquippedWeapon で判定する（傷んでいても装備は外れていないため）。
    /// </summary>
    public static void AddItemLines(Unit unit, List<string> lines)
    {
        if (unit.Items.Count == 0)
        {
            lines.Add("持ち物: なし");
            return;
        }

        lines.Add($"持ち物（{unit.Items.Count}／{UnitData.InventoryCapacity}）:");
        foreach (ItemSlot slot in unit.Items)
        {
            string note = "";
            if (slot.Item is ToolData) note = $"（残り{slot.UsesLeft}回）";
            else if (slot.Item is WeaponData)
            {
                if (slot.Item == unit.EquippedWeapon) note = "（装備中）";
                if (slot.HasDurability) note += $"（耐久{slot.UsesLeft}／{slot.MaxUses}）";
            }
            lines.Add($"・{slot.Item.DisplayName}{note}");
        }
    }

    /// <summary>
    /// 状態の行を組み立てて追加する。中身は「行動済みかどうか＋飛翔・補給・救出・格納」で、
    /// 「・」でつないでいく。
    /// 【重要】1行に詰め込まないこと。飛翔中の飛行兵が誰かを救出していると
    /// 「状態: 未行動・飛翔中（残り3ターン）・陣中・救出中: 味方兵」で約400px になり、
    /// パネルの320px を大きく超えて右端が見切れる（フェーズ32 で気づいた。陣中を足す前から
    /// 超えていた）。状態は今後も増える可能性があるので、幅に収まらなくなったら
    /// 自動で次の行へ送る形にしてある。
    /// </summary>
    public static void AddStateLines(Unit unit, List<string> lines)
    {
        List<string> parts = BuildStateParts(unit);

        string line = "状態: ";
        bool hasPart = false;

        foreach (string part in parts)
        {
            string addition = hasPart ? "・" + part : part;

            if (hasPart && HalfWidthLength(line + addition) > StateLineWidthLimit)
            {
                lines.Add(line);
                line = "　" + part;   // 続きの行は字下げして、状態の続きだと分かるようにする
            }
            else
            {
                line += addition;
            }
            hasPart = true;
        }

        lines.Add(line);
    }

    /// <summary>
    /// 状態の部品を並べる。飛翔中か地上かは全ユニットで必ず出す
    /// （2026-07-26 作者要望。飛べない兵種は常に「地上」）。
    /// 撃墜された飛行兵は、飛翔できない1ターンのあいだ「地上」の代わりにその旨を出す（2026-08-05）。
    /// 陣中／孤立は輸送隊のいるステージでだけ出す（フェーズ32）。
    /// </summary>
    private static List<string> BuildStateParts(Unit unit)
    {
        var parts = new List<string> { unit.HasActed ? "行動済み" : "未行動" };

        if (unit.IsFlying) parts.Add($"飛翔中（残り{unit.FlightTurnsLeft}ターン）");
        else if (unit.IsFlightBlocked) parts.Add("地上（撃墜され飛翔できない）"); // 2026-08-05
        else parts.Add("地上");

        // 輸送隊のいるステージでだけ出す。格納中のユニットは盤上の位置が定まらないので出さない
        // （運んでいる側が陣中なら一緒に回復する＝下の「格納中」の表示で足りる）
        if (SupplyRules.HasSupplyLine(unit))
            parts.Add(SupplyRules.IsSuppliedForDisplay(unit) ? "陣中" : "孤立");

        if (unit.IsRescuing)
        {
            var names = new List<string>();
            foreach (Unit cargo in unit.Carried) names.Add(cargo.Data.unitName);
            parts.Add($"救出中: {string.Join("、", names)}");
        }
        if (unit.IsCarried) parts.Add("格納中（盤上にいない）");

        return parts;
    }

    /// <summary>
    /// 状態の行に入れてよい長さ（半角の個数。全角は2つぶんと数える）。
    /// 情報パネルは幅340px・余白10pxで文字に使えるのが320px、文字の大きさが14px。
    /// 全角は約14px・半角は約7pxなので、半角44個ぶんが上限。少し余裕を見て42にしてある。
    /// </summary>
    private const int StateLineWidthLimit = 42;

    /// <summary>文字列の長さを半角の個数で測る（全角＝2、半角＝1のざっくり計算）。</summary>
    private static int HalfWidthLength(string text)
    {
        int length = 0;
        foreach (char c in text) length += (c < 0x80) ? 1 : 2;
        return length;
    }

    /// <summary>兵種の表示名。兵種データがあればその名前（例：剣闘士）、無ければ移動タイプ名。</summary>
    public static string ClassName(Unit unit)
    {
        if (unit.Data.classData != null) return unit.Data.classData.className;
        return unit.Class.DisplayName();
    }
}
