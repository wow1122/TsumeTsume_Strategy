using System;
using UnityEngine;

/// <summary>
/// 兵種の特攻タグ（この兵種が「受ける」特攻の種類。複数指定可）。
/// 例：騎馬特攻の武器は、騎馬タグを持つ兵種に特攻ダメージを与える——
/// という戦闘反映は、特効武器とともに将来実装（現段階ではデータのみ）。
/// </summary>
[Flags]
public enum ClassTag
{
    None = 0,
    [InspectorName("騎馬")] Horse   = 1 << 0, // 騎馬特攻を受ける
    [InspectorName("天馬")] Pegasus = 1 << 1, // 天馬特攻を受ける
    [InspectorName("竜")]   Dragon  = 1 << 2, // 竜特攻を受ける
}
