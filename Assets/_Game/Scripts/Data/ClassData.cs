using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 兵種（クラス）1種類分のデータ。Project ウィンドウで右クリック →
/// Create → TsumeTsume → Class Data から作れます。
/// 移動タイプ・移動力・装備できる武器種は兵種で決まる（FE系の職業システム）。
/// 特攻タグは有効・特効のダメージに使われる（2026-08-06。判定は EffectiveRules）。
/// 昇格先（上級職）は現段階ではデータのみで、クラスチェンジの実行は将来実装。
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

    [Tooltip("1ターンに移動できるマス数（地上を歩くときの値）")]
    public int move = 3;

    // 飛行兵は「地上を歩くとき」と「飛翔中」で移動力が違う（2026-08-10 作者仕様）。
    // 天馬兵系は地上4/飛翔6、竜騎兵系は地上3/飛翔7。飛ばないと本来の脚が出ない。
    // 0 のときは上の移動力と同じ＝飛行兵以外の32兵種はここを触らなくてよい（既存アセットは全て0）。
    [Tooltip("飛翔中の移動力（飛行兵のみ。0＝上の移動力と同じ）")]
    public int flightMove = 0;

    /// <summary>飛翔中の移動力。0（未設定）なら地上の移動力と同じ。</summary>
    public int EffectiveFlightMove => flightMove > 0 ? flightMove : move;

    [Header("武器")]
    [Tooltip("この兵種が装備できる武器種の一覧")]
    public List<WeaponType> usableWeapons = new List<WeaponType>();

    [Header("特攻・昇格（現段階ではデータのみ・戦闘効果なし）")]
    [Tooltip("この兵種が受ける特攻の種類")]
    public ClassTag tags = ClassTag.None;

    [Tooltip("昇格先の兵種（下級職のみ・最大2つ）")]
    public List<ClassData> promotions = new List<ClassData>();

    // マップ上のユニットの絵（2026-10-10）。陣営で色が違うので味方用・敵用の2枠。
    // 未設定なら従来の色付き四角（味方=青／敵=赤）で表示される。
    [Header("見た目")]
    [Tooltip("味方ユニットのマップ上の絵（未設定＝青い四角）")]
    public Sprite playerMapSprite;

    [Tooltip("敵ユニットのマップ上の絵（未設定＝赤い四角）")]
    public Sprite enemyMapSprite;

    /// <summary>陣営に応じたマップ上の絵。未設定なら null。</summary>
    public Sprite GetMapSprite(Faction faction)
    {
        return faction == Faction.Player ? playerMapSprite : enemyMapSprite;
    }

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
