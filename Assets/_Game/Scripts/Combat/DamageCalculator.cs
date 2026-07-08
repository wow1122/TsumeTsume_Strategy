using UnityEngine;

/// <summary>
/// ダメージ計算（決定論的・命中判定なし）。
///   物理武器（剣・斧・槍・弓）： ダメージ = max(0, 力   + 武器威力 + 三すくみ補正 − (守備 + 地形防御))
///   魔法武器　　　　　　　　　： ダメージ = max(0, 魔力 + 武器威力 + 三すくみ補正 − (魔防 + 地形防御))
/// 地形防御は物理・魔法の両方に効く（作者合意）。
/// 三すくみ： 剣 ＞ 斧 ＞ 槍 ＞ 剣。弓・魔法は中立（補正なし）。
/// ※三すくみを技・速さベースの新補正式 max(2, 差) に変えるのは Phase 9 で行う。
/// </summary>
public static class DamageCalculator
{
    /// <summary>三すくみで有利なときに加算／不利なときに減算する量（バランス調整用）。</summary>
    public const int WeaponTriangleBonus = 1;

    public static int Calculate(Unit attacker, Unit defender, GridManager grid)
    {
        WeaponData aWeapon = attacker.Weapon;
        int might = aWeapon != null ? aWeapon.might : 0;

        int triangle = GetTriangleBonus(aWeapon, defender.Weapon);

        // 防御側がいるマスの地形防御ボーナス
        TileData defTile = grid.GetTile(defender.GridPosition);
        int terrainDef = defTile != null ? defTile.DefenseBonus : 0;

        // 魔法武器なら 魔力 vs 魔防、それ以外は 力 vs 守備
        bool isMagic = aWeapon != null && aWeapon.type == WeaponType.Magic;
        int attackPower = isMagic ? attacker.Magic : attacker.Strength;
        int guardPower = isMagic ? defender.Resistance : defender.Defense;

        int damage = attackPower + might + triangle - (guardPower + terrainDef);

        return Mathf.Max(0, damage);
    }

    /// <summary>
    /// 攻撃側の武器が防御側の武器に対し有利なら +、不利なら −、それ以外 0。
    /// </summary>
    private static int GetTriangleBonus(WeaponData attacker, WeaponData defender)
    {
        if (attacker == null || defender == null) return 0;

        if (Beats(attacker.type, defender.type)) return WeaponTriangleBonus;
        if (Beats(defender.type, attacker.type)) return -WeaponTriangleBonus;
        return 0;
    }

    /// <summary>x が y に三すくみで勝つか（剣＞斧＞槍＞剣）。</summary>
    private static bool Beats(WeaponType x, WeaponType y)
    {
        return (x == WeaponType.Sword && y == WeaponType.Axe)
            || (x == WeaponType.Axe && y == WeaponType.Lance)
            || (x == WeaponType.Lance && y == WeaponType.Sword);
    }
}
