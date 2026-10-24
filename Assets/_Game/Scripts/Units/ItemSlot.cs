/// <summary>
/// ユニットが実際に持っている所持品1つ分の「ランタイム状態」（フェーズ22）。
/// アセット（ItemData）は全ユニット共有の定義なので、道具の残り回数のような
/// ユニットごとの消費はこちらで持つ（Unit のランタイム能力値と同じ考え方）。
///  ・道具 … 使い切ると所持品から消える（Unit.UseTool）
///  ・武器 … 回数を持たない（何度でも使える）。フェーズ31〜32 で入れた武器の耐久と補給は
///           2026-10-24 の作者仕様変更で撤廃した
/// </summary>
public class ItemSlot
{
    /// <summary>所持品の定義（武器または道具のアセット）。</summary>
    public ItemData Item { get; }

    /// <summary>残り回数（道具の残りの使用回数）。</summary>
    public int UsesLeft { get; set; }

    /// <summary>新品のときの回数（0 ＝ 回数の概念を持たない品。武器は常に0）。</summary>
    public int MaxUses { get; }

    /// <summary>回数（道具の使用回数）を持つ品か。</summary>
    public bool HasUses => MaxUses > 0;

    public ItemSlot(ItemData item)
    {
        Item = item;
        MaxUses = item is ToolData tool ? tool.maxUses : 0;
        UsesLeft = MaxUses;
    }
}
