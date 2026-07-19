using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 杖（回復）の「可否・対象・回復量」の判定を集めた共有ルール集（Phase 25）。
/// CombatRules / RescueRules と同じ方針で、判定を1箇所にまとめて重複やズレを防ぐ。
/// 敵AIは当面 杖を使わない（初期装備のまま）ので、いまの利用者は BattleController だけ。
///
/// ここにあるルール：
///  ・杖の射程   … 装備している杖の minRange〜maxRange（攻撃の後衛武器と違い、移動しても縮まない）
///  ・回復の対象 … 同陣営・生存・HPが満タン未満・射程内・高さ（飛翔）の関係が合う相手
///  ・回復量     … 使用者の魔力 ＋ 杖の威力（キックオフ合意(c)）
/// </summary>
public static class StaffRules
{
    /// <summary>杖を装備しているか（攻撃コマンドの代わりに「杖」コマンドを出すかの判定）。</summary>
    public static bool HasStaffEquipped(Unit unit)
    {
        WeaponData w = unit.EquippedWeapon;
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

        WeaponData staff = unit.EquippedWeapon;
        minRange = staff.minRange;
        maxRange = staff.maxRange;
        return true;
    }

    /// <summary>
    /// user が杖で回復できる味方の一覧を返す（BattleController の「杖」コマンド用）。
    /// 対象＝同陣営・生存・HPが満タン未満（合意(b)：満タンには使えない）・射程内・
    /// 高さ（飛翔）の関係が合う相手。自分自身は対象外（自分の回復は傷薬で行う）。
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
            if (!CanReach(user, ally)) continue;         // 飛翔の高さの関係（下記）

            int d = CombatRules.Manhattan(user.GridPosition, ally.GridPosition);
            if (d < min || d > max) continue;
            targets.Add(ally);
        }
        return targets;
    }

    /// <summary>杖の回復量＝使用者の魔力＋杖の威力（合意(c)）。杖を装備していなければ0。</summary>
    public static int HealAmount(Unit user)
    {
        WeaponData staff = user.EquippedWeapon;
        if (staff == null || staff.type != WeaponType.Staff) return 0;
        return user.Magic + staff.might;
    }

    /// <summary>
    /// 杖が対象に「届く」高さの関係か（飛翔の扱い。CombatRules.CanEngage のミラー）。
    /// 杖は後衛武器なので、地上からでも空中の味方に届く（対空と同じ「空に届く」考え方）。
    /// 逆に使用者が飛翔中なら、地上の味方には届かない（空から地上へは届かない）。
    /// ※現状の杖の担い手は歩兵の修道士だけなので user.IsFlying は常に false だが、
    ///   将来 飛行できる杖ユニットが出ても正しく働くようにミラーで書いておく。
    /// </summary>
    private static bool CanReach(Unit user, Unit ally)
    {
        if (ally.IsFlying) return true;   // 空中の味方には地上からでも届く（後衛武器）
        return !user.IsFlying;            // 使用者が飛翔中なら地上の味方には届かない
    }
}
