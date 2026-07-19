using UnityEngine;

/// <summary>
/// 兵種の階級。下級職だけが昇格先（上級職）を持てる。
/// クラスチェンジの実行処理はレベル・経験値とともに将来実装（現段階ではデータのみ）。
/// </summary>
public enum ClassTier
{
    [InspectorName("下級")] Base,     // 下級職
    [InspectorName("上級")] Advanced, // 上級職
}
