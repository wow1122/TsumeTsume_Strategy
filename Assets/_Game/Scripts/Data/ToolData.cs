using UnityEngine;

/// <summary>
/// 道具1種類分のデータ（フェーズ22。仕様書の「道具」— 今回は傷薬のみ）。
/// Project で右クリック → Create → TsumeTsume → Tool Data から作れます。
/// 使うと使用者自身に効果があり、使用回数の限度がある（残り回数はユニット側の
/// ランタイム状態 ItemSlot が持つ。ここにあるのは「新品のときの回数」）。
/// 効果は今はHP回復のみ。能力上昇など2種類目の効果が必要になったときに
/// 効果種別の欄を追加する（過剰設計を避ける従来方針）。
/// </summary>
[CreateAssetMenu(fileName = "ToolData", menuName = "TsumeTsume/Tool Data")]
public class ToolData : ItemData
{
    [Header("基本情報")]
    public string toolName = "道具";

    [Header("効果")]
    [Tooltip("使用者のHPを回復する量")]
    public int healAmount = 10;

    [Tooltip("使える回数（使い切ると所持品から消える）")]
    public int maxUses = 3;

    /// <summary>所持品リストでの表示名（道具名。ItemData の窓口）。</summary>
    public override string DisplayName => toolName;
}
