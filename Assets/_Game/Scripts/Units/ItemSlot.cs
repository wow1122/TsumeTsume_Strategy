/// <summary>
/// ユニットが実際に持っている所持品1つ分の「ランタイム状態」（フェーズ22）。
/// アセット（ItemData）は全ユニット共有の定義なので、道具の残り回数や武器の耐久値のような
/// ユニットごとの消費はこちらで持つ（Unit のランタイム能力値と同じ考え方）。
/// フェーズ31から武器も回数（＝耐久値）を持つようになった。道具と武器で意味は違うが
/// 「新品のときの回数」と「残り」という形は同じなので、同じ2つの欄で扱う。
///  ・道具 … 使い切ると所持品から消える（Unit.UseTool）
///  ・武器 … 0 になっても所持品から消えず、装備もできる。攻撃だけができなくなる
///           （Unit.ConsumeWeaponUse。補給で回復する＝フェーズ32）
/// </summary>
public class ItemSlot
{
    /// <summary>所持品の定義（武器または道具のアセット）。</summary>
    public ItemData Item { get; }

    /// <summary>残り回数（道具＝残りの使用回数、武器＝残りの耐久値）。</summary>
    public int UsesLeft { get; set; }

    /// <summary>新品のときの回数（0 ＝ 回数の概念を持たない品）。</summary>
    public int MaxUses { get; }

    /// <summary>回数（道具の使用回数・武器の耐久値）を持つ品か。</summary>
    public bool HasDurability => MaxUses > 0;

    public ItemSlot(ItemData item)
    {
        Item = item;

        if (item is ToolData tool) MaxUses = tool.maxUses;
        else if (item is WeaponData weapon) MaxUses = weapon.maxUses;  // 0 なら耐久なしの武器
        else MaxUses = 0;

        UsesLeft = MaxUses;
    }

    /// <summary>
    /// 回数を回復する（輸送隊からの補給で武器の耐久が戻る。フェーズ32）。上限は超えない。
    /// 戻り値は実際に増えた量（0 なら満タンか、回数の概念を持たない品）。
    /// 誰の何が回復するかの判断は SupplyRules が行う。ここは数を動かすだけ。
    /// </summary>
    public int Refill(int amount)
    {
        if (!HasDurability || amount <= 0) return 0;

        int before = UsesLeft;
        UsesLeft = System.Math.Min(MaxUses, UsesLeft + amount);
        return UsesLeft - before;
    }
}
