using UnityEngine;

/// <summary>
/// 武器のメモリ消費の共有ルール集（2026-10-24 作者仕様「メモリゲージ使用による武器仕様改善案」）。
/// CombatRules / RescueRules / StaffRules / ExchangeRules / WeaponChoiceRules と同じ方針で、
/// 「この武器を装備できるか・攻撃するといくら払うか・払えなくなったら何に持ち替えるか」を1か所に集める。
/// 武器の耐久（フェーズ31）と輸送隊の補給（フェーズ32）を撤廃し、その代わりに入れた仕組み。
///
/// ここにあるルール：
///  ・払い方 … 武器で1回攻撃するたびに WeaponData.memoryCost を払う。挟撃の支援役も自分の武器のぶんを払う。
///    メモリゲージは本数も崩して払う（1本＋3＝合計33 から5を払うと 0本＋28。作者合意）
///  ・装備できる条件 … 兵種として装備できる（武器側の兵種限定も含む）＋ いまのメモリで払える
///  ・自動の持ち替え … 払ったせいで装備中の武器を払えなくなったら、持ち物の中で装備できる武器のうち
///    メモリ消費の低いもの（同じなら持ち物の上の方）へ持ち替える。1本も無ければ非武装（作者仕様）。
///    持ち替えが起きるのは「払った直後」と「マップ開始時」だけ。外す・捨てる・渡すでは起きない
///  ・敵味方対称（作者合意）。メモリは敵にも貯まるので同じルールにそろえる
///
/// 「払えない武器では戦えない」ことは、Unit.Weapon 窓口が null を返すことで表す（フェーズ31の耐久と同じ手口）。
/// これ1か所で、攻撃の可否・挟撃とガードの資格（支援役が払えなければ挟撃は起きない）・対空・敵AI・
/// 武器選択の probe（お試し装備）がすべて自動で同じ判定になる。
///
/// 【警告】ここで払う・持ち替える処理（PayForAttack / EnsureEquippable）はログを伴う「本番の処理」。
/// 戦闘予測や敵AIの見積りからは呼ばないこと（見積りには副作用の無い CanAfford / FindAutoEquip /
/// PredictAfterAttack を使う）。
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
    /// unit がいまのメモリで weapon を使えるか。extra はほかに一緒に払う量（必殺技の1本＝30 など）。
    /// 消費0の武器（鉄の武器・杖）は extra が無ければいつでも使える。武器が無ければ false。
    /// </summary>
    public static bool CanAfford(Unit unit, WeaponData weapon, int extra = 0)
    {
        if (unit == null || weapon == null) return false;

        int need = CostOf(weapon) + Mathf.Max(0, extra);
        return need <= 0 || MemoryOf(unit) >= need;
    }

    /// <summary>
    /// 兵種としてこの武器を装備できるか（メモリは見ない）。兵種の装備可能武器（ClassData.CanUse）と、
    /// 武器側の兵種限定（WeaponData.exclusiveClasses。長弓は弓聖だけ）の両方を見る。
    /// 兵種データを持たない古いユニットは、限定の無い武器ならこれまでどおり装備できる。
    /// </summary>
    public static bool CanEquipByClass(Unit unit, WeaponData weapon)
    {
        if (unit == null || weapon == null) return false;

        ClassData cls = unit.Data.classData;
        if (cls != null && !cls.CanUse(weapon.type)) return false;
        return weapon.AllowsClass(cls);
    }

    /// <summary>いま装備できるか＝兵種として装備できて、メモリも払える（持ち物の「装備」が押せる条件）。</summary>
    public static bool CanEquipNow(Unit unit, WeaponData weapon)
    {
        return CanEquipByClass(unit, weapon) && CanAfford(unit, weapon);
    }

    /// <summary>
    /// 攻撃に使った武器のメモリを払い、払えなくなったらその場で自動で持ち替える。
    /// CombatSystem.ResolveAttack が戦闘の判定（ダメージ・挟撃・気勢）をすべて終えてから、
    /// 攻撃側 → 挟撃の支援役の順に呼ぶ。weapon は戦闘の前に控えた「実際に使った武器」。
    /// </summary>
    public static void PayForAttack(Unit unit, WeaponData weapon)
    {
        if (unit == null || !unit.IsAlive) return;

        int cost = CostOf(weapon);
        if (cost <= 0) return;

        int before = MemoryOf(unit);
        if (!unit.SpendMemory(cost))
        {
            // 攻撃の前に払えることを確かめている（払えない武器は Unit.Weapon が null）ので、通常は来ない
            Debug.LogWarning($"{unit.Data.unitName}: {weapon.DisplayName} のメモリ {cost} を払えなかった（残り {before}）");
            return;
        }

        Debug.Log($"{unit.Data.unitName} は {weapon.DisplayName} のメモリ {cost} を払った（{before} → {MemoryOf(unit)}）");
        EnsureEquippable(unit);
    }

    /// <summary>
    /// 装備中の武器をいまのメモリで払えないなら、払える武器へ自動で持ち替える（無ければ非武装）。
    /// 呼ぶのは「メモリを払った直後」（PayForAttack）と「マップ開始時」（Unit.Initialize）だけ。
    /// 外す・捨てる・渡すで非武装になったときは呼ばない（作者合意＝従来どおり）。
    /// 持ち替えた（または非武装になった）ら true。
    /// </summary>
    public static bool EnsureEquippable(Unit unit)
    {
        if (unit == null) return false;

        WeaponData current = unit.EquippedWeapon;
        if (current == null || CanAfford(unit, current)) return false;

        WeaponData next = FindAutoEquip(unit, MemoryOf(unit));
        if (next != null) unit.Equip(next);
        else unit.Unequip();

        Debug.Log(next != null
            ? $"{unit.Data.unitName} はメモリが足りず {current.DisplayName}（消費{CostOf(current)}）を装備できなくなったので、" +
              $"{next.DisplayName} に持ち替えた"
            : $"{unit.Data.unitName} はメモリが足りず {current.DisplayName}（消費{CostOf(current)}）を装備できなくなった。" +
              "ほかに装備できる武器が無いので非武装になった");
        return true;
    }

    /// <summary>
    /// メモリが memory のときに自動で持ち替える先（副作用なし）。
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

    /// <summary>
    /// weapon で攻撃した後のメモリの見込み（戦闘予測用・副作用なし）。extra は必殺技の1本など、一緒に払う量。
    /// switches が true なら攻撃の後に自動の持ち替えが起き、そのときの持ち替え先が next（null なら非武装）。
    /// 消費0の武器では持ち替えは起きない（PayForAttack が持ち替えを判定するのは払ったときだけ）。
    /// </summary>
    public static int PredictAfterAttack(Unit unit, WeaponData weapon, int extra,
                                         out bool switches, out WeaponData next)
    {
        int cost = CostOf(weapon);
        int after = MemoryOf(unit) - Mathf.Max(0, extra) - cost;

        switches = cost > 0 && cost > after;
        next = switches ? FindAutoEquip(unit, after) : weapon;
        return after;
    }
}
