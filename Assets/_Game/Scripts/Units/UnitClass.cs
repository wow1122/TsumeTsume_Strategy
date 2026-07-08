using UnityEngine;

/// <summary>
/// ユニットの兵種（クラス）。
/// 大分類は「歩兵」と「騎乗兵」の2つで、騎乗兵はさらに騎兵・飛行兵に分かれる。
/// 仕様書では輸送隊も騎乗兵の分類に含まれる。
/// 救出（Phase 11〜）や飛翔（Phase 14）は、この兵種を条件に使えるようになる。
/// 標準の移動力の目安：歩兵4・騎兵6・飛行兵6・輸送隊5。
/// </summary>
public enum UnitClass
{
    [InspectorName("歩兵")] Infantry,       // 歩兵
    [InspectorName("騎兵")] Cavalry,        // 騎兵（騎乗）
    [InspectorName("飛行兵")] Flier,        // 飛行兵（騎乗）
    [InspectorName("輸送隊")] Transporter,  // 輸送隊（仕様上、騎乗の分類に含まれる）
}

/// <summary>UnitClass への便利な問い合わせ（拡張メソッド）。</summary>
public static class UnitClassExtensions
{
    /// <summary>騎乗ユニットか（歩兵以外はすべて騎乗扱い。輸送隊も含む）。</summary>
    public static bool IsMounted(this UnitClass unitClass)
    {
        return unitClass != UnitClass.Infantry;
    }
}
