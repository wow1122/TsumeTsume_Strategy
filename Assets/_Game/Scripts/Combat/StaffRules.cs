using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 杖（回復）の「可否・対象・回復量」の判定を集めた共有ルール集（Phase 25）。
/// CombatRules / RescueRules と同じ方針で、判定を1箇所にまとめて重複やズレを防ぐ。
/// 敵AIは当面 杖を使わない（初期装備のまま）ので、いまの利用者は BattleController だけ。
///
/// ここにあるルール：
///  ・杖の射程   … 装備している杖の minRange〜maxRange（攻撃の後衛武器と違い、移動しても縮まない）
///  ・回復の対象 … 同陣営・生存・HPが満タン未満・射程内の相手（飛翔の高さの制限は 2026-10-24 に撤廃）
///  ・回復量     … 使用者の魔力 ＋ 杖の威力（キックオフ合意(c)）
///
/// 【重要】武器は EquippedWeapon ではなく Weapon 窓口から読む（フェーズ31から）。
/// Weapon は「いま使えない武器」で null を返す窓口なので、使えない条件が足されたときに
/// 杖も攻撃の武器と同じ扱いに自動でそろう（フェーズ31では傷んだ杖をこれで止めていた）。
/// </summary>
public static class StaffRules
{
    /// <summary>杖を装備しているか（攻撃コマンドの代わりに「杖」コマンドを出すかの判定）。</summary>
    public static bool HasStaffEquipped(Unit unit)
    {
        WeaponData w = unit.Weapon;   // 使えない武器なら null＝杖コマンドは出ない
        return w != null && w.type == WeaponType.Staff;
    }

    /// <summary>
    /// unit が装備している杖の射程（最小〜最大）を返す。杖を装備していなければ false。
    /// 攻撃の後衛武器（弓・魔法）と違い、移動の有無で射程は縮まない（杖は攻撃ではないため）。
    /// </summary>
    public static bool TryGetStaffRange(Unit unit, out int minRange, out int maxRange)
    {
        if (!HasStaffEquipped(unit))
        {
            minRange = 0;
            maxRange = -1;
            return false;
        }

        WeaponData staff = unit.Weapon;
        minRange = staff.minRange;
        maxRange = staff.maxRange;
        return true;
    }

    /// <summary>
    /// user が杖で回復できる味方の一覧を返す（BattleController の「杖」コマンド用）。
    /// 対象＝同陣営・生存・HPが満タン未満（合意(b)：満タンには使えない）・射程内の相手。
    /// 自分自身は対象外（自分の回復は傷薬で行う）。
    /// </summary>
    public static List<Unit> FindHealTargets(Unit user)
    {
        var targets = new List<Unit>();
        if (!TryGetStaffRange(user, out int min, out int max)) return targets;

        foreach (Unit ally in UnitRegistry.GetUnits(user.Faction))
        {
            if (ally == user) continue;                  // 自分は対象外
            if (!ally.IsAlive) continue;
            if (ally.CurrentHP >= ally.MaxHP) continue;  // 満タンは対象外（合意(b)）

            int d = CombatRules.Manhattan(user.GridPosition, ally.GridPosition);
            if (d < min || d > max) continue;
            targets.Add(ally);
        }
        return targets;
    }

    /// <summary>杖の回復量＝使用者の魔力＋杖の威力（合意(c)）。杖を装備していなければ0
    /// （使えない杖も装備していない扱いになる）。</summary>
    public static int HealAmount(Unit user)
    {
        WeaponData staff = user.Weapon;
        if (staff == null || staff.type != WeaponType.Staff) return 0;
        return user.Magic + staff.might;
    }
}
