using UnityEngine;

/// <summary>
/// ユニット1種類分の「能力値データ」。ScriptableObject なので、
/// Project ウィンドウで右クリック → Create → TsumiTsumi → Unit Data から
/// アセットとして何個でも作れます（味方剣士・敵戦士…など）。
/// 数値はここを編集するだけで調整できます。
/// </summary>
[CreateAssetMenu(fileName = "UnitData", menuName = "TsumiTsumi/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("基本情報")]
    public string unitName = "ユニット";
    public Faction faction = Faction.Player;

    [Header("能力値")]
    public int maxHP = 20;
    public int attack = 5;
    public int defense = 3;

    [Tooltip("1ターンに移動できるマス数")]
    public int move = 4;

    // 武器は戦闘フェーズ(Phase 5)で追加します:
    //   public WeaponData weapon;
}
