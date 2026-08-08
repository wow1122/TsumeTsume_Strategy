using UnityEngine;

/// <summary>
/// 武器1種類分のデータ。Project で右クリック →
/// Create → TsumeTsume → Weapon Data から作れます。
/// 射程は「最小〜最大」で表し、近接武器は 1〜1、弓・魔法は 2〜2 など。
/// 所持品（ItemData）の一種でもあり、ユニットの所持品リストに入る（フェーズ22）。
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "TsumeTsume/Weapon Data")]
public class WeaponData : ItemData
{
    [Header("基本情報")]
    public string weaponName = "武器";
    public WeaponType type = WeaponType.Sword;

    [Tooltip("前衛（剣・斧・槍）か後衛（弓・魔導書・光魔法・杖）か。三すくみ・挟撃・射程ルールに使う")]
    public WeaponCategory category = WeaponCategory.Melee;

    [Header("性能")]
    [Tooltip("威力（ダメージに加算される）")]
    public int might = 5;

    [Tooltip("最小射程（隣接攻撃なら1、後衛武器は2）")]
    public int minRange = 1;
    [Tooltip("最大射程（静止時の後衛武器はここまで届く。弓3・魔法2）")]
    public int maxRange = 1;

    [Header("耐久（フェーズ31）")]

    // 【重要】既定値は 0（耐久なし）。この欄を持たない古いアセットを読んでも
    // 従来どおり無限に使える＝既存アセットを壊さない（aiProfile・items と同じ「新旧共存」パターン）。
    // 新しく作る武器は Reset で DefaultMaxUses が入るので、既定で耐久ありになる。
    [Tooltip("耐久値の上限＝この武器で攻撃（杖は回復）できる回数。0 なら耐久なし＝無限に使える")]
    public int maxUses = 0;

    [Tooltip("標準装備。輸送隊からの補給を資源なしで受けられる武器（フェーズ32）。\n将来、資源が要る特別な武器を作るときだけ外す")]
    public bool isStandardIssue = true;

    [Header("特効（2026-08-06）")]

    // 【重要】既定値は None / false（＝何も持たない）。この欄を持たない既存アセットを読んでも
    // 従来どおりに動く＝既存アセットを壊さない（maxUses・items と同じ「新旧共存」パターン）。
    // 武器種そのものが持つ相性（槍→騎馬・天馬など）はアセットではなく EffectiveRules の表が持つので、
    // ここに書くのは「その1本だけが持つ特効」（竜殺しの剣・斬馬刀など）だけ。

    [Tooltip("この武器だけが持つ特効（武器威力3倍）の相手。\n武器種そのものの相性（槍→騎馬など）はここに書かない＝コード側の表が持つ")]
    public ClassTag effectiveTags = ClassTag.None;

    [Tooltip("対空状態を付与する武器か（飛翔中の相手に届き、当てると撃墜する）。\n前衛武器では「特効の相手」にだけ対空として働くので、上の特効の相手も必ず指定すること")]
    public bool grantsAntiAir = false;

    /// <summary>新しく作る武器の耐久値の既定（作者合意：上限6）。既存アセットには影響しない。</summary>
    public const int DefaultMaxUses = 6;

    /// <summary>所持品リストでの表示名（武器名。ItemData の窓口）。</summary>
    public override string DisplayName => weaponName;

    /// <summary>武器種から見た「正しい分類」。弓・魔導書・光魔法・杖が後衛。</summary>
    public static WeaponCategory DefaultCategoryFor(WeaponType type)
    {
        return (type == WeaponType.Bow || type == WeaponType.Magic
             || type == WeaponType.LightMagic || type == WeaponType.Staff)
            ? WeaponCategory.Ranged
            : WeaponCategory.Melee;
    }

    // アセットを新規作成した瞬間に呼ばれる。分類の初期値を武器種から自動設定する。
    // 耐久値もここで既定（6）を入れる。フィールドの初期値（0）ではなく Reset で入れるのは、
    // 初期値を 6 にすると耐久欄を持たない既存アセットまで耐久ありに変わってしまうため。
    private void Reset()
    {
        category = DefaultCategoryFor(type);
        maxUses = DefaultMaxUses;
    }

    // Inspector で値を変更したときに呼ばれる。種類と分類が食い違っていたら警告を出す
    // （例：剣なのに後衛、弓なのに前衛）。動作は止めない＝意図的な特殊武器も作れる。
    private void OnValidate()
    {
        if (maxUses < 0) maxUses = 0;

        // 前衛の対空武器は「特効の相手」にだけ対空として働く（2026-08-08 作者仕様）。
        // 特効の相手が空のままだと、対空にチェックを入れても誰にも効かない設定になってしまう
        if (grantsAntiAir && effectiveTags == ClassTag.None && category == WeaponCategory.Melee)
        {
            Debug.LogWarning(
                $"武器「{weaponName}」({name}): 対空にチェックが入っていますが、特効の相手が空です。" +
                "前衛武器の対空は特効が効く相手にだけ働くので、このままでは誰にも対空になりません。");
        }

        if (category != DefaultCategoryFor(type))
        {
            Debug.LogWarning(
                $"武器「{weaponName}」({name}): 種類 {type} に対して分類が {category} になっています。" +
                $"通常は 剣・斧・槍=前衛(Melee)、弓・魔導書・光魔法・杖=後衛(Ranged) です。意図的でなければ修正してください。");
        }
    }
}
