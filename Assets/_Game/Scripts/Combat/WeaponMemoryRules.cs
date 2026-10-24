using UnityEngine;

/// <summary>
/// 武器のメモリ消費の共有ルール集（2026-10-24 作者仕様「メモリゲージ使用による武器仕様改善案」）。
/// CombatRules / RescueRules / StaffRules / ExchangeRules / WeaponChoiceRules と同じ方針で、
/// 「この武器で攻撃できるか・攻撃するといくら払うか・敵は払えなくなったら何に持ち替えるか」を1か所に集める。
/// 武器の耐久（フェーズ31）と輸送隊の補給（フェーズ32）を撤廃し、その代わりに入れた仕組み。
///
/// ここにあるルール：
///  ・払い方 … 武器で1回攻撃するたびに WeaponData.memoryCost を払う。挟撃の支援役も自分の武器のぶんを払う。
///    メモリゲージは本数も崩して払う（1本＋3＝合計33 から5を払うと 0本＋28。作者合意）
///  ・装備 … メモリに関係なく、兵種が扱える武器なら装備できる（CanEquipByClass。武器側の兵種限定も見る）
///  ・攻撃 … いまのメモリで払えない武器では攻撃できない（攻撃の武器一覧で「メモリ不足」の灰色になる）。
///    判定そのものは CombatRules.TryGetAttackRange（射程なし）と CombatRules.FindPincerAlly（支援しない）が
///    CanAfford を呼んで行う。攻撃を受けたときの三すくみ・ガード役は装備どおりに働く（メモリは要らない）
///  ・持ち替え … 味方は自動で持ち替えない（攻撃のときに払える武器を自分で選ぶ）。
///    武器を選ぶ操作を持たない敵だけ、払えなくなったら消費の低い武器（同じなら持ち物の上の方）へ
///    自動で持ち替える（無ければ非武装）
///
/// 【経緯】最初（2026-10-24 段階2）は味方も「払えない武器は装備できない・払えなくなったら自動で持ち替える」だった。
/// 同日、作者が「プレイヤー自身に選ばせて、いま何を装備しているかを自分の操作の結果にした方が
/// ルールを理解しやすい」と見直し、味方の装備の制限と自動の持ち替えをやめた。
///
/// 【警告】ここで払う・持ち替える処理（PayForAttack / AutoSwitchIfUnaffordable）はログを伴う「本番の処理」。
/// 戦闘予測や敵AIの見積りからは呼ばないこと（見積りには副作用の無い CanAfford / CostOf / MemoryOf を使う）。
/// </summary>
public static class WeaponMemoryRules
{
    /// <summary>この武器で1回攻撃するのに払うメモリ（武器が無ければ0）。</summary>
    public static int CostOf(WeaponData weapon)
    {
        return weapon != null ? Mathf.Max(0, weapon.memoryCost) : 0;
    }

    /// <summary>いまのメモリの合計（本数×30＋値）。</summary>
    public static int MemoryOf(Unit unit)
    {
        return unit != null && unit.Memory != null ? unit.Memory.Total : 0;
    }

    /// <summary>
    /// unit がいまのメモリで weapon を使える（攻撃できる）か。extra はほかに一緒に払う量（必殺技の1本＝30 など）。
    /// 消費0の武器（鉄の武器・杖）は extra が無ければいつでも使える。武器が無ければ false。
    /// </summary>
    public static bool CanAfford(Unit unit, WeaponData weapon, int extra = 0)
    {
        if (unit == null || weapon == null) return false;

        int need = CostOf(weapon) + Mathf.Max(0, extra);
        return need <= 0 || MemoryOf(unit) >= need;
    }

    /// <summary>
    /// 兵種としてこの武器を装備できるか（メモリは見ない＝払えない武器も装備できる）。
    /// 兵種の装備可能武器（ClassData.CanUse）と、武器側の兵種限定（WeaponData.exclusiveClasses。
    /// 長弓は弓聖だけ）の両方を見る。兵種データを持たない古いユニットは、限定の無い武器ならこれまでどおり装備できる。
    /// </summary>
    public static bool CanEquipByClass(Unit unit, WeaponData weapon)
    {
        if (unit == null || weapon == null) return false;

        ClassData cls = unit.Data.classData;
        if (cls != null && !cls.CanUse(weapon.type)) return false;
        return weapon.AllowsClass(cls);
    }

    /// <summary>
    /// 攻撃に使った武器のメモリを払う。CombatSystem.ResolveAttack が戦闘の判定（ダメージ・挟撃・覚醒）を
    /// すべて終えてから、攻撃側 → 挟撃の支援役の順に呼ぶ。weapon は戦闘の前に控えた「実際に使った武器」。
    /// 払った後にその武器を払えなくなったら、敵だけ自動で持ち替える（味方は装備したまま）。
    /// </summary>
    public static void PayForAttack(Unit unit, WeaponData weapon)
    {
        if (unit == null || !unit.IsAlive) return;

        int cost = CostOf(weapon);
        if (cost <= 0) return;

        int before = MemoryOf(unit);
        if (!unit.SpendMemory(cost))
        {
            // 攻撃の前に払えることを確かめている（払えない武器には射程が無い）ので、通常は来ない
            Debug.LogWarning($"{unit.Data.unitName}: {weapon.DisplayName} のメモリ {cost} を払えなかった（残り {before}）");
            return;
        }

        Debug.Log($"{unit.Data.unitName} は {weapon.DisplayName} のメモリ {cost} を払った（{before} → {MemoryOf(unit)}）");
        AutoSwitchIfUnaffordable(unit);
    }

    /// <summary>
    /// 装備中の武器をいまのメモリで払えないなら、払える武器へ自動で持ち替える（無ければ非武装）。
    /// 持ち替えるのは**武器を選ぶ操作を持たない陣営（＝敵。WeaponChoiceRules.CanChooseWeapon が false）だけ**。
    /// 味方は何もしない＝払えない武器も装備したまま、攻撃のときに自分で選ぶ（2026-10-24 作者仕様の見直し）。
    /// 呼ぶのは「メモリを払った直後」（PayForAttack）と「マップ開始時」（Unit.Initialize）だけ。
    /// 持ち替えた（または非武装になった）ら true。
    /// </summary>
    public static bool AutoSwitchIfUnaffordable(Unit unit)
    {
        if (unit == null || WeaponChoiceRules.CanChooseWeapon(unit)) return false;

        WeaponData current = unit.EquippedWeapon;
        if (current == null || CanAfford(unit, current)) return false;

        WeaponData next = FindAutoEquip(unit, MemoryOf(unit));
        if (next != null) unit.Equip(next);
        else unit.Unequip();

        Debug.Log(next != null
            ? $"{unit.Data.unitName} はメモリが足りず {current.DisplayName}（消費{CostOf(current)}）で攻撃できなくなったので、" +
              $"{next.DisplayName} に持ち替えた"
            : $"{unit.Data.unitName} はメモリが足りず {current.DisplayName}（消費{CostOf(current)}）で攻撃できなくなった。" +
              "ほかに払える武器が無いので非武装になった");
        return true;
    }

    /// <summary>
    /// メモリが memory のときに自動で持ち替える先（副作用なし。敵の持ち替えで使う）。
    /// 候補＝持ち物の武器のうち、兵種として装備できて memory で払えるもの（杖も含む）。
    /// メモリ消費の低い順・同じなら持ち物の上の方（作者仕様）。候補が無ければ null（＝非武装）。
    /// </summary>
    public static WeaponData FindAutoEquip(Unit unit, int memory)
    {
        if (unit == null) return null;

        WeaponData best = null;
        foreach (ItemSlot slot in unit.Items)
        {
            if (!(slot.Item is WeaponData weapon)) continue;
            if (!CanEquipByClass(unit, weapon)) continue;
            if (CostOf(weapon) > memory) continue;

            // 「より安い」ときだけ入れ替える＝同じ消費なら先に見つけた（持ち物の上の）方が残る
            if (best == null || CostOf(weapon) < CostOf(best)) best = weapon;
        }
        return best;
    }
}
