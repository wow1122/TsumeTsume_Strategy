using UnityEngine;

/// <summary>
/// ダメージ計算（決定論的・命中判定なし）。
///   ダメージ = max(0, 攻撃 + 武器威力 + 三すくみ補正 − (防御 + 地形防御))
/// 三すくみ： 剣 ＞ 斧 ＞ 槍 ＞ 剣。弓・魔法は中立（補正なし）。
/// </summary>
public static class DamageCalculator
{
    /// <summary>三すくみで有利なときに加算／不利なときに減算する量（バランス調整用）。</summary>
    public const int WeaponTriangleBonus = 1;

    public static int Calculate(Unit attacker, Unit defender, GridManager grid)
    {
        WeaponData aWeapon = attacker.Data.weapon;
        int might = aWeapon != null ? aWeapon.might : 0;

        int triangle = GetTriangleBonus(aWeapon, defender.Data.weapon);

        // 防御側がいるマスの地形防御ボーナス
        TileData defTile = grid.GetTile(defender.GridPosition);
        int terrainDef = defTile != null ? defTile.DefenseBonus : 0;

        int damage = attacker.Data.attack + might + triangle
                     - (defender.Data.defense + terrainDef);

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
