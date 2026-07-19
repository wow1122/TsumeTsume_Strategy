/// <summary>
/// ユニットが実際に持っている所持品1つ分の「ランタイム状態」（フェーズ22）。
/// アセット（ItemData）は全ユニット共有の定義なので、道具の残り回数のような
/// ユニットごとの消費はこちらで持つ（Unit のランタイム能力値と同じ考え方）。
/// </summary>
public class ItemSlot
{
    /// <summary>所持品の定義（武器または道具のアセット）。</summary>
    public ItemData Item { get; }

    /// <summary>残り使用回数（道具のみ意味を持つ。武器は当面耐久なしのため 0）。</summary>
    public int UsesLeft { get; set; }

    public ItemSlot(ItemData item)
    {
        Item = item;
        UsesLeft = item is ToolData tool ? tool.maxUses : 0;
    }
}
