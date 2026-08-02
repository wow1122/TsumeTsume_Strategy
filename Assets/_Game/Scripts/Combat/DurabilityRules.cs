using UnityEngine;

/// <summary>
/// 武器の耐久（摩耗）の共有ルール集（フェーズ31）。
/// CombatRules / RescueRules / StaffRules / ExchangeRules と同じ方針で、
/// 「誰の武器がどれだけ減るか」の判定を1箇所に集めてある。
/// 耐久値の出し入れそのものは Unit / ItemSlot の担当で、ここはルールとログだけを持つ。
///
/// ここにあるルール：
///  ・摩耗するのは自軍だけ … 敵は補給（フェーズ32）を受けないので、減らすと際限なく弱くなる。
///    「敵は救出系・所持品・交換・ゲージを使わない」と同じ、敵側は簡単なままにしておく方針。
///  ・1回使うと1減る … 通常攻撃・必殺技・挟撃の追撃・杖の回復、どれも同じく1。
///
/// 耐久が尽きた武器の扱いは Unit.Weapon が null を返すことだけで表す（＝装備無し扱い）。
/// 装備したままでも外しても戦闘上の結果は同じなので、外す操作を強いられることはない。
/// 物語上は、輸送隊から従者が替えの刃を前線へ運んでいる設定なので、
/// 耐久が尽きた武器は「壊れて無くなる」のではなく「傷んで使えない」状態として持ち続ける。
/// </summary>
public static class DurabilityRules
{
    /// <summary>この陣営の武器は摩耗するか（作者合意：自軍のみ）。</summary>
    public static bool WearsOut(Unit unit)
    {
        return unit != null && unit.Faction == Faction.Player;
    }

    /// <summary>
    /// 武器を1回ぶん使ったときに呼ぶ（通常攻撃・必殺技・挟撃の追撃・杖の回復）。
    /// 摩耗しない陣営や耐久なしの武器では何も起きない。実際に減ったときだけログを出す。
    /// 【重要】ダメージ計算・挟撃の判定が全て終わってから呼ぶこと。先に呼ぶと、その一撃の
    /// 途中で Weapon が null（装備無し扱い）に変わり、威力や三すくみがずれる。
    /// </summary>
    public static void Consume(Unit unit)
    {
        if (!WearsOut(unit)) return;

        int left = unit.ConsumeWeaponUse();
        if (left < 0) return;   // 耐久なしの武器＝無限に使える

        string weaponName = unit.EquippedWeapon.DisplayName;
        Debug.Log(left > 0
            ? $"{unit.Data.unitName} の {weaponName} の耐久 残り{left}"
            : $"{unit.Data.unitName} の {weaponName} は傷んだ（耐久0：この武器では攻撃できない）");
    }
}
