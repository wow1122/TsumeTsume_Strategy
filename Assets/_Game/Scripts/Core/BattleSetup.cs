using UnityEngine;

/// <summary>
/// 戦闘開始時に、StageData（ステージの初期配置アセット）を読んで
/// ユニットを盤上へ配置する。
/// 「どのユニットをどこに置くか」はシーンではなく StageData アセット側で管理する。
/// 配置を変えたいときは Stage_Test.asset を編集すればよい（シーン操作は不要）。
/// </summary>
public class BattleSetup : MonoBehaviour
{
    [Tooltip("グリッド（GridManager）への参照")]
    public GridManager grid;

    [Tooltip("このステージの初期配置データ（StageData アセット）")]
    public StageData stage;

    // グリッドは Awake で作られるので、配置は Start で行えば盤面は完成済み。
    void Start()
    {
        if (grid == null)
        {
            Debug.LogError("BattleSetup: GridManager が設定されていません。Inspector で割り当ててください。");
            return;
        }

        if (stage == null)
        {
            Debug.LogError("BattleSetup: StageData が設定されていません。Inspector で Stage_Test を割り当ててください。");
            return;
        }

        foreach (StageData.Placement entry in stage.placements)
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
