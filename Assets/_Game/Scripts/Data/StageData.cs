using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ステージ1面分の「初期配置データ」。
/// どの UnitData を、どのマス (x, y) に置くかの一覧を持つ。
/// Project ウィンドウで右クリック → Create → TsumeTsume → Stage Data から作れます。
/// シーンに直接書かずアセットにしておくことで、配置の追加・変更が
/// このアセットの編集だけで完結する（シーンを触らなくてよい）。
/// </summary>
[CreateAssetMenu(fileName = "StageData", menuName = "TsumeTsume/Stage Data")]
public class StageData : ScriptableObject
{
    /// <summary>1体分の配置情報。</summary>
    [System.Serializable]
    public class Placement
    {
        public UnitData unitData;   // 配置するユニットの能力値
        public Vector2Int cell;     // 配置するマス（x, y）
    }

    [Tooltip("このステージに配置するユニットの一覧")]
    public List<Placement> placements = new List<Placement>();
}
