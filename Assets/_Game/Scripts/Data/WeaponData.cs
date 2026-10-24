using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器1種類分のデータ。Project で右クリック →
/// Create → TsumeTsume → Weapon Data から作れます。
/// 射程は「最小〜最大」で表し、近接武器は 1〜1、弓・魔法は 2〜2 など。
/// 所持品（ItemData）の一種でもあり、ユニットの所持品リストに入る（フェーズ22）。
/// 2026-10-24 から、強い武器は攻撃のたびにメモリゲージを払う（memoryCost。判定は WeaponMemoryRules）。
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

    // ※フェーズ31〜32 の耐久（maxUses）と標準装備（isStandardIssue）の欄は、
    //   2026-10-24 の作者仕様変更（耐久と補給の撤廃）で取り除いた。

    // 【重要】ここから下の欄はすべて既定値が「何も持たない」（0 / false / None / 空）。
    // この欄を持たない既存アセットを読んでも従来どおりに動く＝既存アセットを壊さない
    // （aiProfile・items と同じ「新旧共存」パターン）。

    [Header("メモリ（2026-10-24）")]
    [Tooltip("この武器で1回攻撃するたびに払うメモリの量。0 なら払わない（鉄の武器・杖など）。\n" +
             "メモリが足りないとこの武器では攻撃できない（装備はできる。敵は払える武器へ自動で持ち替わる）")]
    public int memoryCost = 0;

    [Header("装備付与（2026-10-24）")]
    [Tooltip("魔法武器。力ではなく魔力で攻撃し、相手は守備ではなく魔防で受ける（魔導書と同じ計算）。\n" +
             "三すくみ・有効・特効は武器種のまま（ソニックソードなら剣として扱う）")]
    public bool magicDamage = false;

    [Tooltip("装備しているあいだ、速さがユニットの移動力ぶん下がる（鋼の武器・中級魔導）。0未満にはならない")]
    public bool reducesSpeedByMove = false;

    [Tooltip("三すくみ逆転の相手。このタグを持ち、かつ三すくみでこの武器に勝つ武器（剣なら槍）を装備した相手とは、\n" +
             "有利と不利が入れ替わる（アーマーキラー＝重装・斬馬刀＝騎馬・風薙ぎの剣＝白翼）")]
    public ClassTag triangleReversalTags = ClassTag.None;

    [Tooltip("この武器を装備できる兵種の限定。空なら限定なし（兵種の装備可能武器どおり）。例：長弓は弓聖だけ")]
    public List<ClassData> exclusiveClasses = new List<ClassData>();

    [Header("特効（2026-08-06）")]

    // 武器種そのものが持つ相性（槍→騎馬・白翼など）はアセットではなく EffectiveRules の表が持つので、
    // ここに書くのは「その1本だけが持つ特効」（ドラゴンキラー・斬馬刀など）だけ。

    [Tooltip("この武器だけが持つ特効（武器威力3倍）の相手。\n武器種そのものの相性（槍→騎馬など）はここに書かない＝コード側の表が持つ")]
    public ClassTag effectiveTags = ClassTag.None;

    // ※対空状態（grantsAntiAir。2026-08-06〜）の欄は、2026-10-24 の飛翔と対空の撤廃で取り除いた
    //   （ドラゴンキラー・風薙ぎの剣・ドラゴンアクスは特効だけが残る）。

    /// <summary>所持品リストでの表示名（武器名。ItemData の窓口）。</summary>
    public override string DisplayName => weaponName;

    /// <summary>
    /// 魔法ダメージ（魔力 vs 魔防）で計算する武器か。武器種が魔導書・光魔法のとき、
    /// または魔法武器の印（magicDamage）が付いているとき（2026-10-24。ソニックソードなど）。
    /// </summary>
    public bool DealsMagicDamage => type.IsMagicDamage() || magicDamage;

    /// <summary>
    /// その兵種がこの武器の「兵種の限定」を満たすか（限定が空なら誰でも可）。
    /// 兵種データを持たないユニットは、限定付きの武器を装備できない。
    /// 兵種の装備可能武器（ClassData.CanUse）との組み合わせは WeaponMemoryRules.CanEquipByClass が見る。
    /// </summary>
    public bool AllowsClass(ClassData cls)
    {
        if (exclusiveClasses == null || exclusiveClasses.Count == 0) return true;
        return cls != null && exclusiveClasses.Contains(cls);
    }

    /// <summary>武器種から見た「正しい分類」。弓・魔導書・光魔法・杖が後衛。</summary>
    public static WeaponCategory DefaultCategoryFor(WeaponType type)
    {
        return (type == WeaponType.Bow || type == WeaponType.Magic
             || type == WeaponType.LightMagic || type == WeaponType.Staff)
            ? WeaponCategory.Ranged
            : WeaponCategory.Melee;
    }

    // アセットを新規作成した瞬間に呼ばれる。分類の初期値を武器種から自動設定する。
    private void Reset()
    {
        category = DefaultCategoryFor(type);
    }

    // Inspector で値を変更したときに呼ばれる。種類と分類が食い違っていたら警告を出す
    // （例：剣なのに後衛、弓なのに前衛）。動作は止めない＝意図的な特殊武器も作れる。
    private void OnValidate()
    {
        if (memoryCost < 0) memoryCost = 0;

        // 三すくみ逆転は前衛武器どうしの相性表を入れ替えるルールなので、後衛武器に付けても働かない
        if (triangleReversalTags != ClassTag.None && category != WeaponCategory.Melee)
        {
            Debug.LogWarning(
                $"武器「{weaponName}」({name}): 三すくみ逆転の相手が設定されていますが、後衛武器なので働きません" +
                "（三すくみの相性表を使うのは前衛武器どうしの戦闘だけです）。");
        }

        if (category != DefaultCategoryFor(type))
        {
            Debug.LogWarning(
                $"武器「{weaponName}」({name}): 種類 {type} に対して分類が {category} になっています。" +
                $"通常は 剣・斧・槍=前衛(Melee)、弓・魔導書・光魔法・杖=後衛(Ranged) です。意図的でなければ修正してください。");
        }
    }
}
