using UnityEngine;

/// <summary>
/// 攻撃1回を解決する。
///  ・攻撃側が防御側にダメージを与える
///  ・反撃は「基本なし」。CanCounter フックが true を返したときだけ反撃する
///    （今は常に false。将来スキル/地形/兵種で条件を足せる）
///  ・挟撃（はさみ撃ち）は Phase 5b でここに追加予定
/// </summary>
public static class CombatSystem
{
    public static void ResolveAttack(Unit attacker, Unit defender, GridManager grid)
    {
        if (attacker == null || defender == null) return;

        // 1) 攻撃側 → 防御側
        int damage = DamageCalculator.Calculate(attacker, defender, grid);
        defender.TakeDamage(damage);
        Debug.Log($"攻撃: {attacker.Data.unitName} → {defender.Data.unitName} に {damage} ダメージ" +
                  (defender.IsAlive ? $"（残りHP {defender.CurrentHP}）" : "（戦闘不能）"));

        // 2) 反撃（基本なし。フックが true のときだけ）
        if (defender.IsAlive && CanCounter(attacker, defender, grid))
        {
            int counter = DamageCalculator.Calculate(defender, attacker, grid);
            attacker.TakeDamage(counter);
            Debug.Log($"反撃: {defender.Data.unitName} → {attacker.Data.unitName} に {counter} ダメージ");
        }

        // 3) 挟撃（はさみ撃ち）は Phase 5b でここに追加します。
    }

    /// <summary>
    /// 反撃できるかどうかの判定（拡張ポイント）。
    /// 今は常に false（反撃なし）。将来ここに条件を書くと反撃が有効化される。
    /// 例：防御側が反撃スキルを持つ／特定地形にいる／攻撃を射程内で受けた等。
    /// </summary>
    private static bool CanCounter(Unit attacker, Unit defender, GridManager grid)
    {
        return false;
    }
}
