using System.Collections.Generic;

/// <summary>
/// 攻撃するときに「どの武器で殴るか」を選ぶための共有ルール集（2026-08-09 作者要望）。
/// CombatRules / RescueRules / StaffRules / ExchangeRules と同じ方針で、
/// 「どの武器が候補になるか・その武器でどの相手を攻撃できるか」の判定をここへ集めている。
///
/// ここにあるルール：
///  ・候補になる武器 … プレイヤーは所持品の中の「杖でない・その兵種で装備できる」武器すべて
///    （メモリが足りない武器も候補に入れる＝一覧で「メモリ不足」と理由を見せるため。2026-10-24）。
///    敵は装備中の1本だけ（既存合意「敵AIは所持品を使わない」に沿う＝敵の行動範囲プレビューも従来どおり）
///  ・その武器で攻撃できるか … いまのメモリで払えるか（WeaponMemoryRules）を見たうえで、
///    一時的にその武器を装備して、既存の CombatRules.CanAttack を呼ぶ
///  ・その武器で狙える敵の一覧 … FindTargets（武器1本ぶん）と FindAllTargets（持っている武器の合算）
///  ・使えない理由 … 一覧の灰色の行に添える短い注記（メモリ不足／空中不可／届く敵なし）
///  ・extraCost … 武器の消費とは別に一緒に払う量（必殺技の1本＝30）。必殺技では
///    「1本＋武器の消費」を両方払える武器だけが選べる（2026-10-24 の既定）
///
/// 【probe（お試し装備）方式について】
/// 射程・三すくみ・特効・対空・挟撃・撃墜は、どれも Unit.Weapon 窓口を見て決まる。
/// 武器ごとの判定のために共有関数すべてに「武器」引数を足すのではなく、
/// 一時的に装備を差し替えて評価し、必ず元へ戻す（WithWeapon）。
/// Phase 20 の敵AIが飛翔の損得を StartFlight/CancelFlight で試すのと同じ手口で、
/// 戦闘予測の数字と実戦の結果がズレないことが構造的に保証される
/// （※そのため Unit.Equip / Unequip には副作用を足さないこと。Unit.cs 側にも警告あり）。
/// </summary>
public static class WeaponChoiceRules
{
    /// <summary>
    /// この陣営は攻撃のたびに武器を選べるか。
    /// プレイヤーのみ＝敵は所持品を使わない（救出系・持ち物・交換・ゲージと同じ既存合意）。
    /// </summary>
    public static bool CanChooseWeapon(Unit unit)
    {
        return unit != null && unit.Faction == Faction.Player;
    }

    /// <summary>
    /// 攻撃に使える候補の武器（並びは所持品の順）。
    ///  プレイヤー … 所持品の武器のうち「杖でない・その兵種で装備できる」もの。
    ///  敵         … 装備中の武器1本だけ
    /// いま装備している武器は、兵種が扱えない例外装備でも必ず候補に入れる
    /// （アセット側は「武器と兵種の不一致は警告のみで動作は続行」＝意図的な例外装備を許す方針。
    ///   ここで弾くと、これまで攻撃できていたユニットが急に攻撃できなくなる）。
    /// </summary>
    public static List<WeaponData> GetAttackWeapons(Unit unit)
    {
        var weapons = new List<WeaponData>();
        if (unit == null) return weapons;

        if (CanChooseWeapon(unit))
        {
            foreach (ItemSlot slot in unit.Items)
            {
                if (!(slot.Item is WeaponData weapon)) continue;
                if (weapon.type == WeaponType.Staff) continue;                    // 杖は攻撃できない武器
                if (!WeaponMemoryRules.CanEquipByClass(unit, weapon) && weapon != unit.EquippedWeapon) continue;
                weapons.Add(weapon);
            }
        }

        // 装備中の武器は必ず候補に入れる（敵はこれだけ。プレイヤーでも所持品に無い武器を
        // 装備しているデータの作り方に備えた保険。※ここは EquippedWeapon を読む
        //   ＝ Weapon 窓口は「この武器では戦えない」条件を足す場所なので〔フェーズ31の耐久はそこで null を返していた〕、
        //     そちらを読むと、条件が付いたときに候補から消えて「なぜ攻撃できないか」が一覧で見えなくなる）
        WeaponData equipped = unit.EquippedWeapon;
        if (equipped != null && equipped.type != WeaponType.Staff && !weapons.Contains(equipped))
            weapons.Insert(0, equipped);

        return weapons;
    }

    /// <summary>
    /// unit が weapon を装備した状態で body を実行し、必ず元の装備へ戻す（probe）。
    /// 【重要】戻すのは finally なので、body が例外を投げても装備は残らない。
    /// 装備が同じときは何もせずそのまま実行する（無駄な差し替えを避ける）。
    /// </summary>
    public static T WithWeapon<T>(Unit unit, WeaponData weapon, System.Func<T> body)
    {
        WeaponData original = unit.EquippedWeapon;
        if (weapon == original) return body();

        unit.Equip(weapon);
        try { return body(); }
        finally { unit.Equip(original); }
    }

    /// <summary>戻り値の無い版の probe（表示の組み立てなど）。</summary>
    public static void WithWeapon(Unit unit, WeaponData weapon, System.Action body)
    {
        WithWeapon(unit, weapon, () => { body(); return true; });
    }

    /// <summary>
    /// unit がいまの位置から weapon で target を攻撃できるか。
    /// まず「いまのメモリで払えるか」（extraCost は必殺技の1本など、一緒に払う量）を見て、
    /// 判定の残りは既存の CombatRules.CanAttack（射程＋飛翔の相性）に任せる。
    /// </summary>
    public static bool CanAttackWith(Unit unit, WeaponData weapon, Unit target, bool hasMoved, int extraCost = 0)
    {
        if (unit == null || weapon == null || target == null) return false;
        if (!WeaponMemoryRules.CanAfford(unit, weapon, extraCost)) return false;

        return WithWeapon(unit, weapon, () => CombatRules.CanAttack(unit, unit.GridPosition, target, hasMoved));
    }

    /// <summary>
    /// unit がいまの位置から weapon で攻撃できる敵の一覧（並びは名簿順）。
    /// 武器を1本だけ装備した状態で名簿を1周する（probe は一覧ぶんで1回だけ）。
    /// 武器選択で行にホバーしたときの赤ハイライトと、武器を決めた後の対象選択が これを使う。
    /// メモリで払えない武器（extraCost 込み）なら空の一覧になる。
    /// </summary>
    public static List<Unit> FindTargets(Unit unit, WeaponData weapon, bool hasMoved, int extraCost = 0)
    {
        var targets = new List<Unit>();
        if (unit == null || weapon == null) return targets;
        if (!WeaponMemoryRules.CanAfford(unit, weapon, extraCost)) return targets;

        WithWeapon(unit, weapon, () =>
        {
            foreach (Unit other in UnitRegistry.GetUnits(OpponentOf(unit)))
                if (CombatRules.CanAttack(unit, unit.GridPosition, other, hasMoved))
                    targets.Add(other);
        });
        return targets;
    }

    /// <summary>
    /// 持っている武器のどれかで攻撃できる敵の一覧（並びは名簿順・重複なし）。
    /// 「攻撃」コマンドを出すかどうか・被ダメ確認の相手・移動選択中の赤い攻撃範囲がこれを使う。
    /// 敵は候補が装備中の1本だけなので、従来（装備中の武器で判定）と同じ結果になる。
    /// extraCost を渡すと「その量も一緒に払える武器」だけで数える（「必殺技」コマンドを出すかの判定）。
    /// </summary>
    public static List<Unit> FindAllTargets(Unit unit, bool hasMoved, int extraCost = 0)
    {
        var targets = new List<Unit>();
        if (unit == null) return targets;

        List<WeaponData> weapons = GetAttackWeapons(unit);
        foreach (Unit other in UnitRegistry.GetUnits(OpponentOf(unit)))
        {
            foreach (WeaponData weapon in weapons)
            {
                if (!CanAttackWith(unit, weapon, other, hasMoved, extraCost)) continue;
                targets.Add(other);
                break; // 1本でも届けば対象（同じ相手を二重に入れない）
            }
        }
        return targets;
    }

    /// <summary>
    /// その武器がいま使えない理由（使えるなら null）。相手より先に武器を選ぶので、相手は引数に取らない
    /// ＝「その武器で狙える敵が1体でもいるか」で決める（2026-08-09 の作者仕様変更）。
    /// 武器一覧の灰色の行に「（届く敵なし）」のように添える注記なので、短い言葉にしてある
    /// （一番長い「シャイニングボウ（メモリ不足）」が収まるよう、ボタン幅は ItemListMenu 側で決めている）。
    /// extraCost は必殺技の1本など、武器の消費と一緒に払う量。
    /// </summary>
    public static string GetUnusableReason(Unit unit, WeaponData weapon, bool hasMoved, int extraCost = 0)
    {
        if (unit == null || weapon == null) return "使えない";
        if (!WeaponMemoryRules.CanAfford(unit, weapon, extraCost)) return "メモリ不足";   // 2026-10-24
        if (FindTargets(unit, weapon, hasMoved, extraCost).Count > 0) return null;      // 狙える敵がいる＝押せる行

        // 射程には敵がいるのに戦えないなら、原因は飛翔の相性（対空でない等）。
        // それ以外は単に届く敵がいない（射程外・敵が遠い）
        return WithWeapon(unit, weapon, () =>
        {
            if (!CombatRules.TryGetAttackRange(unit, hasMoved, out int min, out int max)) return "届く敵なし";

            foreach (Unit other in UnitRegistry.GetUnits(OpponentOf(unit)))
            {
                int distance = CombatRules.Manhattan(unit.GridPosition, other.GridPosition);
                if (distance >= min && distance <= max) return "空中不可";
            }
            return "届く敵なし";
        });
    }

    /// <summary>その陣営の相手側（プレイヤー↔敵）。対象さがしで名簿を引くのに使う。</summary>
    private static Faction OpponentOf(Unit unit)
    {
        return (unit.Faction == Faction.Player) ? Faction.Enemy : Faction.Player;
    }
}
