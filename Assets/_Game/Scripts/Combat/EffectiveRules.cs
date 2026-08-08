using UnityEngine;

/// <summary>
/// 有効・特効の判定を集めた共有ルール（2026-08-06 作者仕様「有効と特効の仕様書」）。
///
///   有効 … 武器威力が2倍　　特効 … 武器威力が3倍
///
/// 倍になるのは「武器威力」だけで、ダメージ全体ではない
/// （現行の武器は威力5なので、有効で+5点・特効で+10点）。
///
/// 判定の相手は移動タイプ（UnitClass）ではなく、兵種データの特攻タグ（ClassTag）。
/// 飛行兵の中に天馬と亜竜の両方がいるように、移動タイプでは書き分けられないため。
///
/// ここにあるルール：
///  ・武器種そのものが持つ相性 … 前衛（槍・斧）は「有効」、後衛（弓・魔導書）は「特効」。
///    「全ての槍は」という書き方のルールなので、武器アセットではなくこのコードの表で持つ
///  ・個別の特効武器（竜殺しの剣など）… WeaponData.effectiveTags で武器ごとに持つ
///  ・重なったときは一番大きい倍率1つだけ（掛け合わせない。作者合意）
///
/// 窓口は GetMultiplier ひとつ。DamageCalculator だけがこれを呼ぶので、
/// 戦闘本体・挟撃・敵AIの見積り・戦闘予測UIは自動で同じ倍率を見る
/// （杖の回復は DamageCalculator を通らないので、特効の影響を受けない）。
/// </summary>
public static class EffectiveRules
{
    /// <summary>有効の倍率（武器威力2倍）。</summary>
    public const int EffectiveMultiplier = 2;

    /// <summary>特効の倍率（武器威力3倍）。</summary>
    public const int SuperEffectiveMultiplier = 3;

    /// <summary>
    /// weapon で defender を攻撃したときの武器威力の倍率（1＝等倍・2＝有効・3＝特効）。
    /// 武器なし・タグなしの相手は必ず1を返す。
    /// </summary>
    public static int GetMultiplier(WeaponData weapon, Unit defender)
    {
        if (weapon == null || defender == null) return 1;

        ClassTag tags = defender.Tags;
        if (tags == ClassTag.None) return 1;

        int multiplier = 1;

        // 武器種そのものが持つ相性
        if (ClassTagExtensions.Matches(tags, GetTypeEffectiveTargets(weapon.type)))
            multiplier = Mathf.Max(multiplier, EffectiveMultiplier);

        if (ClassTagExtensions.Matches(tags, GetTypeSuperEffectiveTargets(weapon.type)))
            multiplier = Mathf.Max(multiplier, SuperEffectiveMultiplier);

        // 個別の特効武器（竜殺しの剣・斬馬刀など）。武器種の相性と重なっても、大きい方だけを採る
        if (ClassTagExtensions.Matches(tags, weapon.effectiveTags))
            multiplier = Mathf.Max(multiplier, SuperEffectiveMultiplier);

        return multiplier;
    }

    /// <summary>weapon が defender に対して有効・特効を持つか（表示の分岐用）。</summary>
    public static bool IsEffective(WeaponData weapon, Unit defender)
    {
        return GetMultiplier(weapon, defender) > 1;
    }

    /// <summary>倍率の日本語名（1＝なし・2＝有効・3＝特効）。</summary>
    public static string GetLabel(int multiplier)
    {
        if (multiplier >= SuperEffectiveMultiplier) return "特効";
        if (multiplier >= EffectiveMultiplier) return "有効";
        return "なし";
    }

    /// <summary>
    /// 武器種そのものが持つ「有効」（武器威力2倍）の相手。
    ///   槍 … 騎馬・天馬（馬と天馬を止める）
    ///   斧 … 重装・亜竜（硬い相手と飛竜を叩き割る。竜そのものには効かない）
    /// </summary>
    private static ClassTag GetTypeEffectiveTargets(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Lance: return ClassTag.Horse | ClassTag.Pegasus;
            case WeaponType.Axe:   return ClassTag.Armor | ClassTag.SubDragon;
            default:               return ClassTag.None;
        }
    }

    /// <summary>
    /// 武器種そのものが持つ「特効」（武器威力3倍）の相手。
    ///   弓     … 天馬（空の的を落とす）
    ///   魔導書 … 竜（亜竜も含む。竜の鱗を貫く）
    /// 光魔法は当面なし（武器アセットを作るときに一緒に決める）。
    /// </summary>
    private static ClassTag GetTypeSuperEffectiveTargets(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Bow:   return ClassTag.Pegasus;
            case WeaponType.Magic: return ClassTag.Dragon;
            default:               return ClassTag.None;
        }
    }
}
