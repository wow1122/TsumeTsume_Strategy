using UnityEngine;

/// <summary>
/// 武器1種類分のデータ。Project で右クリック →
/// Create → TsumiTsumi → Weapon Data から作れます。
/// 射程は「最小〜最大」で表し、近接武器は 1〜1、弓・魔法は 2〜2 など。
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "TsumiTsumi/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("基本情報")]
    public string weaponName = "武器";
    public WeaponType type = WeaponType.Sword;

    [Header("性能")]
    [Tooltip("威力（ダメージに加算される）")]
    public int might = 5;

    [Tooltip("最小射程（隣接攻撃なら1）")]
    public int minRange = 1;
    [Tooltip("最大射程（弓・魔法なら2など）")]
    public int maxRange = 1;
}
