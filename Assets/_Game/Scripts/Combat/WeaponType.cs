/// <summary>
/// 武器の種類。剣・槍・斧で三すくみ（相性）を作る。
/// 弓・魔法は遠距離用で、三すくみには参加しない（中立）。
/// 三すくみ： 剣 ＞ 斧 ＞ 槍 ＞ 剣
/// </summary>
public enum WeaponType
{
    Sword, // 剣
    Lance, // 槍
    Axe,   // 斧
    Bow,   // 弓（遠距離）
    Magic, // 魔法（遠距離）
}
