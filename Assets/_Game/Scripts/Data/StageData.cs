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

        [Tooltip("開始時から飛翔状態にする残りターン数（0=地上から開始。飛行兵のみ有効。Phase 14）")]
        public int initialFlightTurns = 0;

        [Tooltip("敵AIの性格（敵のみ有効。Phase 17）。\n" +
                 "突撃型=従来どおり攻める／待ち伏せ型=挑発（被弾 または 攻撃できる相手の出現）まで動かない")]
        public EnemyAIProfile aiProfile = EnemyAIProfile.Assault;
    }

    [Tooltip("このステージに配置するユニットの一覧")]
    public List<Placement> placements = new List<Placement>();

    [Header("地形（Phase 13）")]
    [Tooltip("地形定義の一覧表（TerrainTable アセット）")]
    public TerrainTable terrainTable;

    [Tooltip("地形の文字マップ。1行が盤面の横1列で、リストの先頭が「盤面の一番上の行」。\n" +
             "記号は TerrainTable で定義（例: .=平地 F=森 M=山 T=砦 #=壁）")]
    public List<string> terrainRows = new List<string>();

    [Header("盤面の大きさ（Phase 15）")]
    [Tooltip("盤面の横のマス数（0以下なら GridManager の Inspector 値をそのまま使う）")]
    public int gridWidth = 10;

    [Tooltip("盤面の縦のマス数（0以下なら GridManager の Inspector 値をそのまま使う）")]
    public int gridHeight = 10;

    [Header("勝敗条件（Phase 15）")]
    [Tooltip("ターン制限。このターン数を使い切っても敵が残っていたら敗北（0なら無制限）")]
    public int turnLimit = 0;
}
