using UnityEngine;

/// <summary>
/// 武器1種類分のデータ。Project で右クリック →
/// Create → TsumeTsume → Weapon Data から作れます。
/// 射程は「最小〜最大」で表し、近接武器は 1〜1、弓・魔法は 2〜2 など。
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "TsumeTsume/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("基本情報")]
    public string weaponName = "武器";
    public WeaponType type = WeaponType.Sword;

    [Tooltip("前衛（剣・斧・槍）か後衛（弓・魔法）か。三すくみ・挟撃・射程ルールに使う")]
    public WeaponCategory category = WeaponCategory.Melee;

    [Header("性能")]
    [Tooltip("威力（ダメージに加算される）")]
    public int might = 5;

    [Tooltip("最小射程（隣接攻撃なら1、後衛武器は2）")]
    public int minRange = 1;
    [Tooltip("最大射程（静止時の後衛武器はここまで届く。弓3・魔法2）")]
    public int maxRange = 1;

    /// <summary>武器種から見た「正しい分類」。弓・魔法だけが後衛。</summary>
    public static WeaponCategory DefaultCategoryFor(WeaponType type)
    {
        return (type == WeaponType.Bow || type == WeaponType.Magic)
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
        if (category != DefaultCategoryFor(type))
        {
            Debug.LogWarning(
                $"武器「{weaponName}」({name}): 種類 {type} に対して分類が {category} になっています。" +
                $"通常は 剣・斧・槍=前衛(Melee)、弓・魔法=後衛(Ranged) です。意図的でなければ修正してください。");
        }
    }
}
