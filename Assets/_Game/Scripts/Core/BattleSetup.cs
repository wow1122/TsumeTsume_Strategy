using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 戦闘開始時に、指定したユニットを盤上へ配置する。
/// Inspector の一覧に「どの UnitData を、どのマス(x,y)に置くか」を並べておくと、
/// 再生時に自動で生成する（プレハブ不要・見た目はコードで作る）。
/// </summary>
public class BattleSetup : MonoBehaviour
{
    /// <summary>1体分の配置情報（Inspector で編集できる）。</summary>
    [System.Serializable]
    public class SpawnEntry
    {
        public UnitData unitData;   // 配置するユニットの能力値
        public Vector2Int cell;     // 配置するマス（x, y）
    }

    [Tooltip("グリッド（GridManager）への参照")]
    public GridManager grid;

    [Tooltip("配置するユニットの一覧")]
    public List<SpawnEntry> spawns = new List<SpawnEntry>();

    // グリッドは Awake で作られるので、配置は Start で行えば盤面は完成済み。
    void Start()
    {
        if (grid == null)
        {
            Debug.LogError("BattleSetup: GridManager が設定されていません。Inspector で割り当ててください。");
            return;
        }

        foreach (SpawnEntry entry in spawns)
        {
            if (entry.unitData == null) continue;

            if (!grid.IsInside(entry.cell))
            {
                Debug.LogWarning($"配置先 {entry.cell} が盤面の外です。スキップします。");
                continue;
            }

            TileData tile = grid.GetTile(entry.cell);
            if (tile != null && tile.Occupant != null)
            {
                Debug.LogWarning($"マス {entry.cell} はすでに埋まっています。スキップします。");
                continue;
            }

            var go = new GameObject("Unit");
            var unit = go.AddComponent<Unit>();
            unit.Initialize(entry.unitData, grid, entry.cell);
        }
    }
}
