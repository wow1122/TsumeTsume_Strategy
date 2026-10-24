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

        // 先に地形を盤面へ適用してから、ユニットを配置する（Phase 13）
        grid.ApplyTerrain(stage);

        foreach (StageData.Placement entry in stage.placements)
        {
            if (entry.unitData == null) continue;

            if (!grid.IsInside(entry.cell))
            {
                Debug.LogWarning($"配置先 {entry.cell} が盤面の外です。スキップします。");
                continue;
            }

            TileData tile = grid.GetTile(entry.cell);
            // その兵種が立てない地形には置けない（山は徒歩のみ等の兵種制限も見る。Phase 15）。
            // 飛行兵は飛行で入れるマス（城壁・山など）ならどこでも置ける（TileData.IsWalkableFor）
            if (tile != null && !tile.IsWalkableFor(entry.unitData.EffectiveClass))
            {
                Debug.LogWarning($"マス {entry.cell} は {entry.unitData.unitName} が立てない地形です。スキップします。");
                continue;
            }
            if (tile != null && tile.Occupant != null)
            {
                Debug.LogWarning($"マス {entry.cell} はすでに埋まっています。スキップします。");
                continue;
            }

            var go = new GameObject("Unit");
            var unit = go.AddComponent<Unit>();
            unit.Initialize(entry.unitData, grid, entry.cell);

            // 敵AIの性格を書き込む（Phase 17）。プレイヤーのユニットに待ち伏せ型を
            // 指定しても意味が無いので、注意だけ出して既定（突撃型）のままにする
            if (entry.aiProfile != EnemyAIProfile.Assault && entry.unitData.faction != Faction.Enemy)
                Debug.LogWarning($"{entry.unitData.unitName} は敵ではないため、AI性格の指定を無視しました。");
            else
                unit.SetAIProfile(entry.aiProfile);
        }
    }
}
