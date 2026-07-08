using UnityEngine;

/// <summary>ユニットの陣営（どちらの軍か）。</summary>
public enum Faction
{
    [InspectorName("自軍")] Player, // 自軍（プレイヤーが操作する）
    [InspectorName("敵軍")] Enemy,  // 敵軍
}
