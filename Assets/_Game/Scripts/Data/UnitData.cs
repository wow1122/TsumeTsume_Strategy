using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ユニット1種類分の「能力値データ」。ScriptableObject なので、
/// Project ウィンドウで右クリック → Create → TsumeTsume → Unit Data から
/// アセットとして何個でも作れます（味方剣士・敵戦士…など）。
/// 数値はここを編集するだけで調整できます。
/// 能力値は仕様書の7種：HP・力・魔力・技・速さ・守備・魔防。
/// Inspector の日本語表示は Editor/UnitDataEditor.cs が担当（表示だけ。動作に影響なし）。
/// </summary>
[CreateAssetMenu(fileName = "UnitData", menuName = "TsumeTsume/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("基本情報")]
    public string unitName = "ユニット";
    public Faction faction = Faction.Player;

    [Tooltip("兵種。歩兵以外（騎兵・飛行兵・輸送隊）は騎乗ユニット扱い")]
    public UnitClass unitClass = UnitClass.Infantry;

    [Header("能力値")]
    [Tooltip("最大HP")]
    public int maxHP = 20;

    // 旧フィールド名 attack のデータを引き継ぐため FormerlySerializedAs を付けている。
    // （古い書式のアセットを読んでも値が消えない保険。アセット側も strength へ書き換え済み）
    [FormerlySerializedAs("attack")]
    [Tooltip("力：物理攻撃のダメージに使う")]
    public int strength = 5;

    [Tooltip("魔力：魔法攻撃のダメージに使う")]
    public int magic = 0;

    [Tooltip("技：三すくみ有利時の補正に使う（Phase 9 から）")]
    public int skill = 5;

    [Tooltip("速さ：三すくみ不利時の軽減に使う（Phase 9 から）")]
    public int speed = 5;

    [Tooltip("守備：物理攻撃への防御")]
    public int defense = 3;

    [Tooltip("魔防：魔法攻撃への防御")]
    public int resistance = 0;

    [Tooltip("1ターンに移動できるマス数（標準：歩兵4・騎兵6・飛行兵6・輸送隊5）")]
    public int move = 4;

    [Header("装備")]
    [Tooltip("装備する武器。攻撃の威力・射程・相性に使われます")]
    public WeaponData weapon;
}
