using UnityEngine;

/// <summary>
/// 攻撃1回を解決する。
///  ・攻撃側が防御側にダメージを与える（Console に内訳ログを出す）
///  ・反撃は「基本なし」。CanCounter フックが true を返したときだけ反撃する
///    （今は常に false。将来スキル/地形/兵種で条件を足せる）
///  ・挟撃（はさみ撃ち）：成立・無効化の判定は CombatRules に一元化してある
///  ・PredictTotalDamage は「この攻撃で合計何ダメージ入るか」の予測窓口。
///    敵AIの行動評価と、将来の戦闘予測UIが同じ数字を見る。
/// </summary>
public static class CombatSystem
{
    public static void ResolveAttack(Unit attacker, Unit defender, GridManager grid)
    {
        if (attacker == null || defender == null) return;

        // 1) 攻撃側 → 防御側（内訳付きログ）
        DamageBreakdown breakdown = DamageCalculator.CalculateBreakdown(attacker, defender, grid);
        defender.TakeDamage(breakdown.Total);
        Debug.Log($"攻撃: {attacker.Data.unitName} → {defender.Data.unitName} に {breakdown.Total} ダメージ" +
                  $"〔{breakdown.ToLogString()}〕" +
                  (defender.IsAlive ? $"（残りHP {defender.CurrentHP}）" : "（戦闘不能）"));

        // 2) 反撃（基本なし。フックが true のときだけ）
        if (defender.IsAlive && CanCounter(attacker, defender, grid))
        {
            int counter = DamageCalculator.Calculate(defender, attacker, grid);
            attacker.TakeDamage(counter);
            Debug.Log($"反撃: {defender.Data.unitName} → {attacker.Data.unitName} に {counter} ダメージ");
        }

        // 3) 挟撃（はさみ撃ち）
        TryPincer(attacker, defender, grid);
    }

    /// <summary>
    /// 挟撃：近接攻撃のとき、防御側を挟んだ反対側の隣マスに前衛武器の味方がいれば、
    /// その味方も追加で攻撃する（上下左右の一直線のみ・斜めは対象外）。
    /// 参加した味方は行動を消費しない（おまけの追撃）。
    /// ただし防御側の隣に「前衛武器の歩兵」の味方（ガード役）がいると無効化される。
    /// 成立条件・無効化条件の本体は CombatRules にある（AIの予測と共通）。
    /// </summary>
    private static void TryPincer(Unit attacker, Unit defender, GridManager grid)
    {
        if (defender == null || !defender.IsAlive) return;

        Unit ally = CombatRules.FindPincerAlly(attacker.GridPosition, attacker, defender, grid);
        if (ally == null) return;

        // 無効化（ガード）判定：防御側の隣に前衛武器の歩兵がいれば挟撃は防がれる
        Unit guard = CombatRules.FindPincerGuard(defender, grid);
        if (guard != null)
        {
            Debug.Log($"ガード！ {guard.Data.unitName} が隣にいるため、{defender.Data.unitName} への挟撃は無効化された");
            return;
        }

        DamageBreakdown breakdown = DamageCalculator.CalculateBreakdown(ally, defender, grid);
        defender.TakeDamage(breakdown.Total);
        Debug.Log($"挟撃！ {ally.Data.unitName} も {defender.Data.unitName} に {breakdown.Total} ダメージ" +
                  $"〔{breakdown.ToLogString()}〕" +
                  (defender.IsAlive ? $"（残りHP {defender.CurrentHP}）" : "（戦闘不能）"));
    }

    /// <summary>
    /// 「attacker が fromCell に立って defender を攻撃したら、合計何ダメージ入るか」の予測。
    /// 本体ダメージ＋（挟撃が成立して無効化されないなら）挟撃ダメージ。
    /// 敵AIの行動評価と、将来のプレイヤー向け戦闘予測UIの共有窓口。
    /// ※fromCell を引数にしているのは「移動した場合」を仮定して評価できるようにするため。
    /// </summary>
    public static int PredictTotalDamage(Unit attacker, Vector2Int fromCell, Unit defender, GridManager grid)
    {
        int total = DamageCalculator.Calculate(attacker, defender, grid);

        Unit ally = CombatRules.FindPincerAlly(fromCell, attacker, defender, grid);
        if (ally != null && !CombatRules.IsPincerNegated(defender, grid))
            total += DamageCalculator.Calculate(ally, defender, grid);

        return total;
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
