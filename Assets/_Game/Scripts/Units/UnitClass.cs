using UnityEngine;

/// <summary>
/// ユニットの兵種（クラス）。分類の階層（作者合意 2026-07-11）：
///   歩兵
///   騎乗兵 ─ 騎兵（輸送隊は騎兵の一種）
///          └ 飛行兵
/// 救出（Phase 11〜）や飛翔（Phase 14）は、この兵種を条件に使えるようになる。
/// 「騎兵向け」のルールを書くときは IsCavalry() を使うと輸送隊にも自動で効く。
/// 標準の移動力の目安：歩兵4・騎兵6・飛行兵6・輸送隊5。
/// </summary>
public enum UnitClass
{
    [InspectorName("歩兵")] Infantry,       // 歩兵
    [InspectorName("騎兵")] Cavalry,        // 騎兵（騎乗）
    [InspectorName("飛行兵")] Flier,        // 飛行兵（騎乗）
    [InspectorName("輸送隊")] Transporter,  // 輸送隊（騎乗・騎兵の一種）
}

/// <summary>UnitClass への便利な問い合わせ（拡張メソッド）。</summary>
public static class UnitClassExtensions
{
    /// <summary>騎乗ユニットか（歩兵以外はすべて騎乗扱い。輸送隊も含む）。</summary>
    public static bool IsMounted(this UnitClass unitClass)
    {
        return unitClass != UnitClass.Infantry;
    }

    /// <summary>騎兵系か（騎兵と、その一種である輸送隊。飛行兵は含まない）。</summary>
    public static bool IsCavalry(this UnitClass unitClass)
    {
        return unitClass == UnitClass.Cavalry || unitClass == UnitClass.Transporter;
    }
}
