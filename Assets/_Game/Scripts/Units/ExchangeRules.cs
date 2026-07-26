using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「交換」（隣接する味方との持ち物の受け渡し）の可否判定を集めた共有ルール集（フェーズ26）。
/// CombatRules / RescueRules / StaffRules と同じ方針で、判定を1箇所にまとめて
/// UI（BattleController）と将来のAIが同じ関数を呼べるようにする。
///
/// 作者と合意済みのルール（2026-07-26）：
///  ・相手は「上下左右に隣接する同陣営の盤上ユニット」のみ（斜めは対象外）
///  ・行動済みの味方も相手にできる（救出の合意(h)と同じ理屈。受け取る側は受け身）
///  ・飛翔状態が同じ相手同士でのみ交換できる（空中↔空中・地上↔地上。空中↔地上は不可）
///    ＝「引き受けは同じ飛翔状態同士のみ」（飛翔の合意仕様）と同じ整理
///  ・格納中の貨物とは交換できない。運び手を経由して貨物の持ち物に触ることもできない
///    （貨物は盤上から外れていて隣接マスの占有者にならないので、自然に除外される）
///  ・所持数の上限は UnitData.InventoryCapacity（7）。いっぱいの相手へは渡せない
///  ・交換は行動を消費しない（合意(a)）。ただし1個でも動かすとその行動は取り消せなくなる
///    （合意(c)。取り消し不能点を打つのは呼び出し側 BattleController の役割）
///
/// 敵AIは当面 交換を使わない（「敵は救出系を使わない」「敵は所持品を使わない」と同じ前例）。
/// 使う日が来ても、ここの関数を呼ぶだけで判断がプレイヤーとズレないようにしてある。
/// </summary>
public static class ExchangeRules
{
    /// <summary>隣接4方向（上下左右）。RescueRules と同じく各ルール集で1本ずつ持つ。</summary>
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    };

    /// <summary>
    /// user がいまの位置から交換できる隣接ユニットの一覧（双方向＝渡す・もらうのどちらかができる相手）。
    /// </summary>
    public static List<Unit> FindExchangePartners(Unit user, GridManager grid)
    {
        var result = new List<Unit>();
        foreach (Unit neighbor in GetAdjacentUnits(user.GridPosition, grid))
        {
            if (CanExchange(user, neighbor)) result.Add(neighbor);
        }
        return result;
    }

    /// <summary>
    /// user と partner の間で、どちらかの向きに1個でも動かせるか（＝「交換」コマンドを出すか）。
    /// </summary>
    public static bool CanExchange(Unit user, Unit partner)
    {
        if (!IsExchangeablePair(user, partner)) return false;
        return CanGive(user, partner) || CanGive(partner, user);
    }

    /// <summary>
    /// giver が receiver へ1個渡せるか（持ち物が1つ以上あり、受け側に空きがある）。
    /// 相手として成立するか（隣接・同陣営・飛翔状態）は IsExchangeablePair が見る。
    /// </summary>
    public static bool CanGive(Unit giver, Unit receiver)
    {
        return giver.Items.Count > 0 && CanReceive(receiver);
    }

    /// <summary>あと1つでも所持品を受け取れるか（上限は UnitData.InventoryCapacity＝7）。</summary>
    public static bool CanReceive(Unit receiver)
    {
        return receiver != null && receiver.Items.Count < UnitData.InventoryCapacity;
    }

    /// <summary>
    /// そもそも交換の相手として成立する組み合わせか（持ち物の中身は見ない）。
    /// 同陣営・生存・自分以外・盤上にいる（格納中でない）・飛翔状態が同じ。
    /// ※隣接しているかは呼び出し側（隣接マスの占有者を列挙する側）が保証する。
    /// </summary>
    public static bool IsExchangeablePair(Unit user, Unit partner)
    {
        if (user == null || partner == null || partner == user) return false;
        if (!partner.IsAlive) return false;
        if (partner.Faction != user.Faction) return false;
        if (partner.IsCarried || user.IsCarried) return false; // 格納中の貨物とはやり取りできない
        if (partner.IsFlying != user.IsFlying) return false;   // 空中と地上の間では手渡しできない
        return true;
    }

    /// <summary>指定マスの上下左右にいる生存ユニットを列挙する（RescueRules と同じ作り）。</summary>
    private static IEnumerable<Unit> GetAdjacentUnits(Vector2Int center, GridManager grid)
    {
        foreach (Vector2Int dir in Directions)
        {
            TileData tile = grid.GetTile(center + dir);
            Unit occupant = tile != null ? tile.Occupant : null;
            if (occupant != null && occupant.IsAlive)
                yield return occupant;
        }
    }
}
