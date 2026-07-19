using UnityEngine;

/// <summary>
/// 所持品（ユニットのインベントリに入る物）の共通の親クラス（フェーズ22）。
/// 種類は仕様書どおり武器（WeaponData）と道具（ToolData）の2つ（「素材」は将来の検討枠）。
/// 親子関係にすることで、UnitData の所持品リストに武器と道具を混ぜて入れられる。
/// 直列化フィールドは持たない（既存の WeaponData アセットの保存内容を一切変えないため）。
/// </summary>
public abstract class ItemData : ScriptableObject
{
    /// <summary>所持品リストなどで表示する名前（武器名・道具名を子クラスが返す）。</summary>
    public abstract string DisplayName { get; }
}
