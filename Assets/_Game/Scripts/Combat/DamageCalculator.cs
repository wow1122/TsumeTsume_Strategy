using UnityEngine;

/// <summary>
/// ダメージ1回分の内訳。合計だけでなく「何がいくつ効いたか」を持ち、
/// Console のログ表示と、将来の戦闘予測UI（ダメージ見込み表示）の共有基盤になる。
/// </summary>
public struct DamageBreakdown
{
    public bool isMagic;       // 魔法攻撃か（魔力 vs 魔防で計算したか）
    public int attackPower;    // 力 または 魔力
    public int might;          // 武器威力
    public int triangle;       // 三すくみ補正（有利で＋、不利で−、なしは0）
    public int guardPower;     // 守備 または 魔防
    public int terrainDefense; // 地形防御（防御側のマス）

    /// <summary>最終ダメージ（0未満にはならない）。</summary>
    public int Total => Mathf.Max(0, attackPower + might + triangle - (guardPower + terrainDefense));

    /// <summary>ログ用の内訳文字列。例：「力6＋威力5＋相性+2 − (守備2＋地形0) = 11」</summary>
    public string ToLogString()
    {
        string atkName = isMagic ? "魔力" : "力";
        string grdName = isMagic ? "魔防" : "守備";
        string triText = triangle == 0 ? "" : $"＋相性{(triangle > 0 ? "+" : "")}{triangle}";
        return $"{atkName}{attackPower}＋威力{might}{triText} − ({grdName}{guardPower}＋地形{terrainDefense}) = {Total}";
    }
}

/// <summary>
/// ダメージ計算（決定論的・命中判定なし）。
///   物理武器（剣・斧・槍・弓）： ダメージ = max(0, 力   + 武器威力 + 三すくみ補正 − (守備 + 地形防御))
///   魔法武器　　　　　　　　　： ダメージ = max(0, 魔力 + 武器威力 + 三すくみ補正 − (魔防 + 地形防御))
/// 地形防御は物理・魔法の両方に効く（作者合意）。
/// 三すくみ補正の中身（技・速さの差、最低2）は CombatRules.GetTriangleModifier が担当。
/// </summary>
public static class DamageCalculator
{
    /// <summary>ダメージの内訳付き計算。ログや予測表示に使う。</summary>
    public static DamageBreakdown CalculateBreakdown(Unit attacker, Unit defender, GridManager grid)
    {
        WeaponData weapon = attacker.Weapon;

        // 防御側がいるマスの地形防御ボーナス
        TileData defTile = grid.GetTile(defender.GridPosition);

        // 魔法系武器（魔導書・光魔法）なら 魔力 vs 魔防、それ以外は 力 vs 守備
        bool isMagic = weapon != null && weapon.type.IsMagicDamage();

        return new DamageBreakdown
        {
            isMagic = isMagic,
            attackPower = isMagic ? attacker.Magic : attacker.Strength,
            might = weapon != null ? weapon.might : 0,
            triangle = CombatRules.GetTriangleModifier(attacker, defender),
            guardPower = isMagic ? defender.Resistance : defender.Defense,
            // 飛翔中はタイルの効果を受けない（地形防御なし。Phase 14）
            terrainDefense = (defTile != null && !defender.IsFlying) ? defTile.DefenseBonus : 0,
        };
    }

    /// <summary>最終ダメージだけが欲しいときの窓口。</summary>
    public static int Calculate(Unit attacker, Unit defender, GridManager grid)
    {
        return CalculateBreakdown(attacker, defender, grid).Total;
    }
}
