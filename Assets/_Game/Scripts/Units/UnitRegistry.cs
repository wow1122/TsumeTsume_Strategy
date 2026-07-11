using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 盤上に存在する全ユニットの名簿（レジストリ）。
/// Unit が生成時に登録・戦闘不能時に削除するので、
/// 「今どんなユニットがいるか」はここに聞けば分かる。
///
/// FindObjectsByType での全検索をやめた理由：
///  非アクティブなユニット（将来の「救出で格納中」など）が検索から消えてしまい、
///  勝敗判定が誤動作するため。名簿方式なら「盤上には見えないが生きている」を区別できる。
/// </summary>
public static class UnitRegistry
{
    private static readonly List<Unit> units = new List<Unit>();

    /// <summary>
    /// 味方の輸送隊が倒されたか（Phase 12・敗北条件）。
    /// 死亡と同時に名簿から消えるため、後から数えても分からない。
    /// そこで Unit.Die() が死亡の瞬間にここへ記録し、勝敗判定（TurnManager）が読む。
    /// </summary>
    public static bool PlayerTransporterLost { get; private set; }

    // Enter Play Mode Options（ドメインリロード無効）でも、static な名簿が
    // 前回プレイの内容を引きずらないよう、プレイ開始のたびに空にする。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        units.Clear();
        PlayerTransporterLost = false;
    }

    /// <summary>戦闘不能の発生を記録する（Unit.Die から呼ばれる）。</summary>
    public static void NotifyDeath(Unit unit)
    {
        if (unit.Faction == Faction.Player && unit.Class == UnitClass.Transporter)
            PlayerTransporterLost = true;
    }

    /// <summary>名簿に登録する（Unit.Initialize から呼ばれる）。</summary>
    public static void Register(Unit unit)
    {
        if (unit != null && !units.Contains(unit)) units.Add(unit);
    }

    /// <summary>名簿から外す（戦闘不能・破棄時に呼ばれる）。</summary>
    public static void Unregister(Unit unit)
    {
        units.Remove(unit);
    }

    /// <summary>登録されている全ユニット（読み取り専用）。</summary>
    public static IReadOnlyList<Unit> All => units;

    /// <summary>
    /// 指定陣営の「盤上で動けるユニット」の一覧を返す。
    /// 非アクティブのユニット（将来：救出で格納中）は含めない。
    /// 行動・攻撃対象探し・全員行動済み判定はこちらを使う。
    /// </summary>
    public static List<Unit> GetUnits(Faction faction)
    {
        var result = new List<Unit>();
        foreach (Unit u in units)
        {
            if (u == null || !u.IsAlive) continue;
            if (!u.gameObject.activeInHierarchy) continue; // 盤上にいない（格納中など）
            if (u.Faction == faction) result.Add(u);
        }
        return result;
    }

    /// <summary>
    /// 指定陣営の生存数を数える。勝敗判定はこちらを使う。
    /// 非アクティブでも生きていれば数える（格納中のユニットがいるだけで
    /// 全滅扱いにならないように、GetUnits とは条件をあえて変えている）。
    /// </summary>
    public static int CountAlive(Faction faction)
    {
        int count = 0;
        foreach (Unit u in units)
        {
            if (u == null) continue;
            if (u.Faction == faction && u.IsAlive) count++;
        }
        return count;
    }
}
