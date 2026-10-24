using UnityEngine;

/// <summary>
/// 攻撃1回を解決する。
///  ・攻撃側が防御側にダメージを与える（Console に内訳ログを出す）
///  ・反撃は「基本なし」。CanCounter フックが true を返したときだけ反撃する
///    （今は常に false。将来スキル/地形/兵種で条件を足せる）
///  ・挟撃（はさみ撃ち）：成立・無効化の判定は CombatRules に一元化してある
///  ・PredictTotalDamage は「この攻撃で合計何ダメージ入るか」の予測窓口。
///    敵AIの行動評価と、将来の戦闘予測UIが同じ数字を見る。
///  ・気勢ゲージ（フェーズ28）：戦闘1回ごとに「相手を倒したか」を GaugeRules へ知らせる。
///    連続撃破が続くと、倒した側の全ユニットに気勢が配られる。
///  ・必殺技（フェーズ29）：critical = true で呼ぶと、本体の一撃だけダメージに倍率がかかる。
///    挟撃の追撃は等倍のまま（倍率が乗るのはメモリを使った本人の攻撃だけ）。
///    必殺技のメモリの消費は呼び出し側（BattleController）が行う。
///  ・静止撃ち有利（2026-07-30 作者仕様）：後衛武器（弓・魔法）が「マスから動かずに」攻撃すると
///    三すくみ有利が付く。攻撃側が移動したかは呼び出し側しか知らないので attackerHasMoved で受け取る。
///  ・メモリの支払い（2026-10-24 作者仕様）：攻撃した側と挟撃の支援役が、使った武器のメモリ消費を払う
///    （WeaponMemoryRules.PayForAttack。払えなくなっても味方は装備したまま、敵だけ安い武器へ自動で持ち替える）。
///    払うのは「この戦闘が全部終わってから」＝その一撃の中で武器が変わり、挟撃の成立判定や
///    気勢の配分がずれることは無い（フェーズ31の武器の耐久の消耗と同じ位置。耐久は撤廃した）。
/// </summary>
public static class CombatSystem
{
    /// <summary>
    /// 攻撃1回を解決する。attackerHasMoved は攻撃側がこの行動で移動したか
    /// （プレイヤーは ActionContext.hasMoved、敵AIは「移動先 ≠ 元の位置」。既定 true ＝ 補正なし側）。
    /// </summary>
    public static void ResolveAttack(Unit attacker, Unit defender, GridManager grid,
                                    bool critical = false, bool attackerHasMoved = true)
    {
        if (attacker == null || defender == null) return;

        // 0) メモリを払う「実際に使った武器」を、攻撃を解決する「前」に控えておく（2026-10-24）。
        //    最後にメモリを払うと、敵は自動の持ち替えで Unit.Weapon が別の武器（や null）に変わるため
        WeaponData attackerWeapon = attacker.Weapon;

        // 1) 攻撃側 → 防御側（内訳付きログ）
        //    必殺技（フェーズ29）のときは、この本体の一撃にだけ倍率がかかる（挟撃は等倍のまま）
        float multiplier = critical ? GaugeRules.CriticalMultiplier : 1f;
        DamageBreakdown breakdown = DamageCalculator.CalculateBreakdown(attacker, defender, grid,
                                                                       multiplier, attackerHasMoved);
        defender.TakeDamage(breakdown.Total);
        Debug.Log($"{(critical ? "必殺技！" : "攻撃:")} {attacker.Data.unitName} → {defender.Data.unitName} に {breakdown.Total} ダメージ" +
                  $"〔{breakdown.ToLogString()}〕" +
                  (defender.IsAlive ? $"（残りHP {defender.CurrentHP}）" : "（戦闘不能）"));

        // 2) 反撃（基本なし。フックが true のときだけ）
        //    反撃側に静止撃ち有利を乗せるかは、反撃を実装するときに決める（今は補正なしの既定）
        if (defender.IsAlive && CanCounter(attacker, defender, grid))
        {
            int counter = DamageCalculator.Calculate(defender, attacker, grid);
            attacker.TakeDamage(counter);
            Debug.Log($"反撃: {defender.Data.unitName} → {attacker.Data.unitName} に {counter} ダメージ");
        }

        // 3) 挟撃（はさみ撃ち）。支援役も自分の武器のメモリを払うので、誰が・どの武器で撃ったかを受け取る
        Unit pincerAlly = TryPincer(attacker, defender, grid);
        WeaponData pincerWeapon = pincerAlly != null ? pincerAlly.Weapon : null;

        // 4) 気勢ゲージ（フェーズ28）：この戦闘で相手を倒したかを連鎖の判定へ渡す。
        //    挟撃のとどめもこの戦闘の結果に含める（誰がとどめを刺したかは問わない＝作者合意）。
        //    倒れた直後の defender は Destroy 済みで「== null」が true になるので、そこも撃破として扱う
        bool killed = defender == null || !defender.IsAlive;
        GaugeRules.NotifyBattleResult(attacker.Faction, killed);

        // 5) メモリの支払い（2026-10-24 作者仕様）：攻撃側 → 挟撃の支援役の順に、使った武器のぶんを払う。
        //    払えなくなっても味方は装備したまま（次の攻撃で自分で選ぶ）。敵だけ安い武器へ自動で持ち替える。
        //    この戦闘の判定（ダメージ・挟撃・気勢）が全部終わってから払う＝その一撃の中で武器は変わらない
        WeaponMemoryRules.PayForAttack(attacker, attackerWeapon);
        WeaponMemoryRules.PayForAttack(pincerAlly, pincerWeapon);
    }

    /// <summary>
    /// 挟撃：近接攻撃のとき、防御側を挟んだ反対側の隣マスに前衛武器の味方がいれば、
    /// その味方も追加で攻撃する（上下左右の一直線のみ・斜めは対象外）。
    /// 参加した味方は行動を消費しない（おまけの追撃）。
    /// ただし防御側の隣に「前衛武器の徒歩ユニット」の味方（ガード役）がいると無効化される。
    /// 成立条件・無効化条件の本体は CombatRules にある（AIの予測と共通）。
    /// 戻り値は実際に追撃した味方（挟撃が起きなかった・ガードで無効化されたときは null）。
    /// 呼び出し側はこれを使って支援役にもメモリを払わせる（2026-10-24 作者仕様）。
    /// </summary>
    private static Unit TryPincer(Unit attacker, Unit defender, GridManager grid)
    {
        if (defender == null || !defender.IsAlive) return null;

        Unit ally = CombatRules.FindPincerAlly(attacker.GridPosition, attacker, defender, grid);
        if (ally == null) return null;

        // 無効化（ガード）判定：防御側の隣に前衛武器の徒歩ユニットがいれば挟撃は防がれる
        Unit guard = CombatRules.FindPincerGuard(defender, grid);
        if (guard != null)
        {
            Debug.Log($"ガード！ {guard.Data.unitName} が隣にいるため、{defender.Data.unitName} への挟撃は無効化された");
            return null;
        }

        // 挟み役は必ず前衛武器（IsPincerCapable）なので、静止撃ち有利は関係しない＝既定のまま呼ぶ
        DamageBreakdown breakdown = DamageCalculator.CalculateBreakdown(ally, defender, grid);
        defender.TakeDamage(breakdown.Total);
        Debug.Log($"挟撃！ {ally.Data.unitName} も {defender.Data.unitName} に {breakdown.Total} ダメージ" +
                  $"〔{breakdown.ToLogString()}〕" +
                  (defender.IsAlive ? $"（残りHP {defender.CurrentHP}）" : "（戦闘不能）"));
        return ally;
    }

    /// <summary>
    /// 「attacker が fromCell に立って defender を攻撃したら、合計何ダメージ入るか」の予測。
    /// 本体ダメージ＋（挟撃が成立して無効化されないなら）挟撃ダメージ。
    /// 敵AIの行動評価と、将来のプレイヤー向け戦闘予測UIの共有窓口。
    /// ※fromCell を引数にしているのは「移動した場合」を仮定して評価できるようにするため。
    /// ※attackerHasMoved は fromCell から自動で決めない（既定値も置かない）。実行時は移動を
    ///   済ませてから呼ぶこともあり「fromCell ＝ 現在地」が静止を意味しなくなるため、
    ///   静止かどうかは必ず呼び出し側が明示する（後衛武器の静止撃ち有利がここで効く）。
    /// </summary>
    public static int PredictTotalDamage(Unit attacker, Vector2Int fromCell, Unit defender, GridManager grid,
                                        bool attackerHasMoved)
    {
        int total = DamageCalculator.Calculate(attacker, defender, grid, 1f, attackerHasMoved);

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
