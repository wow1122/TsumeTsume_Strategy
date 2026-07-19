using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 兵種（クラス）1種類分のデータ。Project ウィンドウで右クリック →
/// Create → TsumeTsume → Class Data から作れます。
/// 移動タイプ・移動力・装備できる武器種は兵種で決まる（FE系の職業システム）。
/// 昇格先（上級職）と特攻タグは現段階ではデータのみで、
/// クラスチェンジの実行や特攻ダメージの戦闘反映は将来実装。
/// Inspector の日本語表示は Editor/ClassDataEditor.cs が担当（表示だけ。動作に影響なし）。
/// </summary>
[CreateAssetMenu(fileName = "ClassData", menuName = "TsumeTsume/Class Data")]
public class ClassData : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("兵種の名前（例：剣闘士）")]
    public string className = "兵種";

    [Tooltip("階級。下級職だけが昇格先を持てる")]
    public ClassTier tier = ClassTier.Base;

    [Header("移動")]
    [Tooltip("移動タイプ。地形・救出・飛翔・積載など既存ルールはすべてこの値で判定する")]
    public UnitClass moveType = UnitClass.Infantry;

    [Tooltip("1ターンに移動できるマス数")]
    public int move = 3;

    [Header("武器")]
    [Tooltip("この兵種が装備できる武器種の一覧")]
    public List<WeaponType> usableWeapons = new List<WeaponType>();

    [Header("特攻・昇格（現段階ではデータのみ・戦闘効果なし）")]
    [Tooltip("この兵種が受ける特攻の種類")]
    public ClassTag tags = ClassTag.None;

    [Tooltip("昇格先の兵種（下級職のみ・最大2つ）")]
    public List<ClassData> promotions = new List<ClassData>();

    /// <summary>この兵種で武器種 type を装備できるか。</summary>
    public bool CanUse(WeaponType type)
    {
        return usableWeapons.Contains(type);
    }

    // Inspector で値を変更したときに呼ばれる。警告のみで動作は止めない（WeaponData と同じ方針）。
    private void OnValidate()
    {
        if (promotions.Count > 2)
        {
            Debug.LogWarning(
                $"兵種「{className}」({name}): 昇格先は最大2つです（現在 {promotions.Count} 件）。");
        }
        if (tier == ClassTier.Advanced && promotions.Count > 0)
        {
            Debug.LogWarning(
                $"兵種「{className}」({name}): 上級職なのに昇格先が設定されています。");
        }
    }
}
