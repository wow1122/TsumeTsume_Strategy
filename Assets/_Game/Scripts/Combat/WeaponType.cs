using UnityEngine;

/// <summary>
/// 武器の種類。剣・槍・斧で三すくみ（相性）を作る。
/// 弓・魔法系は遠距離用で、三すくみには参加しない（中立）。
/// 三すくみ： 剣 ＞ 斧 ＞ 槍 ＞ 剣
/// 魔法＝魔導＝魔導書はすべて同じ武器（Magic）。杖は攻撃できない武器（効果は将来実装）。
/// ※ 既存アセットが種類を整数で保存しているため、追加は必ず末尾へ（挿入・並べ替え禁止）。
/// </summary>
public enum WeaponType
{
    [InspectorName("剣")] Sword,          // 剣
    [InspectorName("槍")] Lance,          // 槍
    [InspectorName("斧")] Axe,            // 斧
    [InspectorName("弓")] Bow,            // 弓（遠距離）
    [InspectorName("魔導書")] Magic,      // 魔導書（遠距離・魔法ダメージ）
    [InspectorName("杖")] Staff,          // 杖（攻撃不可。兵種データ導入時に追加）
    [InspectorName("光魔法")] LightMagic, // 光魔法（遠距離・魔法ダメージ。武器アセットは将来）
}

/// <summary>WeaponType への便利な問い合わせ（拡張メソッド）。</summary>
public static class WeaponTypeExtensions
{
    /// <summary>魔法ダメージ（魔力 vs 魔防）で計算する武器種か。</summary>
    public static bool IsMagicDamage(this WeaponType type)
    {
        return type == WeaponType.Magic || type == WeaponType.LightMagic;
    }

    /// <summary>
    /// 持ち物リストなどで見せる日本語の種類名（剣・槍・斧・弓・魔導書・杖・光魔法）。
    /// enum の [InspectorName] は Inspector 専用で実行時には読めないため、ここで用意する。
    /// </summary>
    public static string DisplayName(this WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Sword: return "剣";
            case WeaponType.Lance: return "槍";
            case WeaponType.Axe: return "斧";
            case WeaponType.Bow: return "弓";
            case WeaponType.Magic: return "魔導書";
            case WeaponType.Staff: return "杖";
            case WeaponType.LightMagic: return "光魔法";
            default: return type.ToString();
        }
    }
}
