using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地形定義（TerrainDef）の一覧表。ScriptableObject アセットとして1つ作り、
/// StageData から参照する。地形の数値・色の調整はこのアセットの編集だけで完結する。
/// ステージの文字マップ（terrainRows）の「記号 → 地形定義」の変換もここが担当。
/// </summary>
[CreateAssetMenu(fileName = "TerrainTable", menuName = "TsumeTsume/Terrain Table")]
public class TerrainTable : ScriptableObject
{
    [Tooltip("地形定義の一覧。先頭が「既定の地形（平地）」として使われる")]
    public List<TerrainDef> terrains = new List<TerrainDef>();

    /// <summary>既定の地形（リスト先頭＝平地）。リストが空なら null。</summary>
    public TerrainDef DefaultTerrain =>
        (terrains != null && terrains.Count > 0) ? terrains[0] : null;

    /// <summary>記号1文字から地形定義を探す。見つからなければ null。</summary>
    public TerrainDef FindBySymbol(char symbol)
    {
        if (terrains == null) return null;

        foreach (TerrainDef def in terrains)
        {
            if (!string.IsNullOrEmpty(def.symbol) && def.symbol[0] == symbol)
                return def;
        }
        return null;
    }
}
