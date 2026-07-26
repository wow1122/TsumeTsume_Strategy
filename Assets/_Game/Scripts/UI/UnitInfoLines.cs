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
        lines.Add($"守備{unit.Defense}　魔防{unit.Resistance}　移動{unit.Move}");
        lines.Add(WeaponLine(unit));
        AddItemLines(unit, lines);
        lines.Add(StateLine(unit));

        return lines;
    }

    /// <summary>装備中の武器の行。「武器: 槍（前衛・威力5・射程1）」のような形式。</summary>
    public static string WeaponLine(Unit unit)
    {
        WeaponData w = unit.Weapon;
        if (w == null) return "武器: なし（攻撃不可）";

        string cat = w.category == WeaponCategory.Melee ? "前衛" : "後衛";
        string range = w.minRange == w.maxRange ? $"{w.maxRange}" : $"{w.minRange}〜{w.maxRange}";
        return $"武器: {w.weaponName}（{cat}・威力{w.might}・射程{range}）";
    }

    /// <summary>持ち物の行を追加する。装備中の武器は「装備中」、道具は残り回数を添える。</summary>
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
            else if (slot.Item == unit.Weapon) note = "（装備中）";
            lines.Add($"・{slot.Item.DisplayName}{note}");
        }
    }

    /// <summary>
    /// 状態の行。行動済みかどうかに、飛翔・救出・格納の状態を「・」でつなげる。
    /// 飛翔中か地上かは全ユニットで必ず出す（2026-07-26 作者要望。飛べない兵種は常に「地上」）。
    /// </summary>
    public static string StateLine(Unit unit)
    {
        var parts = new List<string> { unit.HasActed ? "行動済み" : "未行動" };

        if (unit.IsFlying) parts.Add($"飛翔中（残り{unit.FlightTurnsLeft}ターン）");
        else parts.Add("地上");
        if (unit.IsRescuing)
        {
            var names = new List<string>();
            foreach (Unit cargo in unit.Carried) names.Add(cargo.Data.unitName);
            parts.Add($"救出中: {string.Join("、", names)}");
        }
        if (unit.IsCarried) parts.Add("格納中（盤上にいない）");

        return "状態: " + string.Join("・", parts);
    }

    /// <summary>兵種の表示名。兵種データがあればその名前（例：剣闘士）、無ければ移動タイプ名。</summary>
    public static string ClassName(Unit unit)
    {
        if (unit.Data.classData != null) return unit.Data.classData.className;
        return unit.Class.DisplayName();
    }
}
