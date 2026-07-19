using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 盤面から「次の相手フェイズにどんな危険があるか」を読み取る道具集（Phase 19〜）。
/// 判定の部品は CombatRules / MovementCalculator / DamageCalculator にあるものを使い、
/// ここではそれを組み合わせるだけ（一元化の方針）。
/// 扱う脅威は「挟撃の脅威」（Phase 19）と「対空射程マス」（Phase 20）。
/// </summary>
public static class ThreatMap
{
    /// <summary>隣接4方向（上下左右）。</summary>
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    };

    /// <summary>挟撃の「挟む軸」は 上下 と 左右 の2通り。</summary>
    private static readonly Vector2Int[] PairAxes = { Vector2Int.up, Vector2Int.right };

    /// <summary>
    /// self（これから動く敵）から見て「次のプレイヤーフェイズに挟撃されそうな味方」→
    /// 「ガードで防げる最大ダメージ」の一覧（Phase 19・守り配置AI用）。
    ///
    /// 味方1体ごとの判定（すべて満たすと「脅威あり」）：
    ///   ・飛翔中でない（飛翔中は挟撃されないし、地上のガードでは守れない。Phase 14）
    ///   ・自分以外のガード役がまだ付いていない
    ///     （先に動いた敵が守った味方は自動でここから外れる＝重複ガードの防止）
    ///   ・上下 または 左右 の対マスの両側それぞれに「前衛のプレイヤーが既に立っている か、
    ///     空きマスで前衛のプレイヤーが移動で立てる」があり、両側が別々のプレイヤーである
    ///
    /// 防げるダメージ ＝ 挟撃に参加できるプレイヤーの攻撃ダメージの最大値。
    /// 実際の挟撃の追加打は「反対側に立った1人」の分だが、誰がどちらに立つかまでは
    /// 読まないので、参加できる中の最大で見積もる（やや過大見積り。守り優先の安全側）。
    ///
    /// self を渡す理由：自分は今から動くので「盤面に居ない」ものとして判定する。
    ///   (1) 自分がガード役でも「ガード済み」に数えない —— これが無いと、守り続けている敵が
    ///       翌ターン「この味方はもう守られている」と勘違いして持ち場を離れてしまう
    ///   (2) 自分が立って塞いでいる対マスは「空き」とみなす（CanReachIfSelfLeaves も参照）
    /// </summary>
    public static Dictionary<Unit, int> FindPincerThreatenedAllies(GridManager grid, Unit self)
    {
        var result = new Dictionary<Unit, int>();
        if (self == null) return result;

        // 挟撃してくる側（相手陣営）の前衛ユニットと、その到達マス集合を先に集めておく
        Faction opponentFaction = self.Faction == Faction.Player ? Faction.Enemy : Faction.Player;
        var attackers = new List<Unit>();
        var reaches = new List<HashSet<Vector2Int>>();
        foreach (Unit p in UnitRegistry.GetUnits(opponentFaction))
        {
            if (!CombatRules.IsPincerCapable(p)) continue; // 後衛・武装無しは挟撃に参加できない
            attackers.Add(p);
            reaches.Add(MovementCalculator.GetReachableCells(grid, p));
        }
        if (attackers.Count < 2) return result; // 挟撃は2人がかり。前衛が1人以下なら脅威なし

        foreach (Unit ally in UnitRegistry.GetUnits(self.Faction))
        {
            if (ally == self || ally.IsFlying) continue;
            if (HasGuardOtherThan(ally, self, grid)) continue;

            int prevented = 0;
            foreach (Vector2Int axis in PairAxes)
            {
                List<Unit> sideA = FindSideAttackers(grid, ally.GridPosition + axis, ally, self, attackers, reaches);
                if (sideA.Count == 0) continue;
                List<Unit> sideB = FindSideAttackers(grid, ally.GridPosition - axis, ally, self, attackers, reaches);
                if (sideB.Count == 0) continue;

                // 両側に来られるのが同じ1人だけなら、体は1つなので挟めない
                bool distinctPair = sideA.Count > 1 || sideB.Count > 1 || sideA[0] != sideB[0];
                if (!distinctPair) continue;

                foreach (Unit u in sideA)
                    prevented = Mathf.Max(prevented, DamageCalculator.Calculate(u, ally, grid));
                foreach (Unit u in sideB)
                    prevented = Mathf.Max(prevented, DamageCalculator.Calculate(u, ally, grid));
            }

            if (prevented > 0) result[ally] = prevented;
        }
        return result;
    }

    /// <summary>
    /// 対マスの片側 cell から ally を挟撃してきうるプレイヤーの一覧。
    ///   ・盤外 → 誰も挟めない
    ///   ・誰かが立っている → 前衛のプレイヤー本人ならその1人が脅威。それ以外のユニットは
    ///     「栓」になっていて挟めない（自分 self のマスだけは、今から動くので空き扱い）
    ///   ・空きマス → 移動でそこへ立てる前衛プレイヤー全員（飛翔の制限 CanEngage も確認）
    /// </summary>
    private static List<Unit> FindSideAttackers(
        GridManager grid, Vector2Int cell, Unit ally, Unit self,
        List<Unit> attackers, List<HashSet<Vector2Int>> reaches)
    {
        var list = new List<Unit>();
        TileData tile = grid.GetTile(cell);
        if (tile == null) return list; // 盤外

        Unit occupant = tile.Occupant;
        bool selfHere = occupant == self; // 自分のマスは空き扱い（今から動くため）
        if (occupant != null && !selfHere)
        {
            if (occupant.Faction != ally.Faction
                && CombatRules.IsPincerCapable(occupant)
                && CombatRules.CanEngage(occupant, ally))
                list.Add(occupant); // 前衛のプレイヤーが既に片側に立っている
            return list;
        }

        for (int i = 0; i < attackers.Count; i++)
        {
            if (!CombatRules.CanEngage(attackers[i], ally)) continue; // 飛翔の制限（Phase 14）

            bool reachable = selfHere
                ? CanReachIfSelfLeaves(reaches[i], cell)
                : reaches[i].Contains(cell);
            if (reachable) list.Add(attackers[i]);
        }
        return list;
    }

    /// <summary>
    /// 自分（self）が立って塞いでいるマスに、相手が「自分が退いたあと」で立てそうか。
    /// 相手の到達範囲は自分が居る盤面で計算済みなので、そのマス自体は範囲に入っていない。
    /// 代わりに「隣のマスまで来られるなら、空けば入れる」とみなす
    /// （移動力1マスぶんの過大見積り。守りを離れて穴を空けるより安全側に倒す）。
    /// </summary>
    private static bool CanReachIfSelfLeaves(HashSet<Vector2Int> reach, Vector2Int cell)
    {
        if (reach.Contains(cell)) return true;
        foreach (Vector2Int dir in Directions)
            if (reach.Contains(cell + dir)) return true;
        return false;
    }

    // ===== 対空射程マス（Phase 20・飛翔AI用）=====

    /// <summary>
    /// self（飛翔中／これから飛翔する敵）から見て「次のプレイヤーフェイズに、空中の自分を
    /// 攻撃されうるマス」の集合（Phase 20・空中巡航の停止マス選び用）。
    ///
    /// 空中のユニットを攻撃できるのは「後衛武器（弓・魔法の対空）」か「飛翔中のユニット」だけ
    /// （CombatRules.CanEngage）。そこで相手陣営のその2種について、
    ///   ・静止して撃つ … 今いるマスから武器の上限射程まで
    ///   ・移動して撃つ … 移動で立てる各マスから（後衛武器は最小射程ちょうどに縮む）
    /// の届くマスを全部集める。移動後の射程短縮は TryGetAttackRange がそのまま反映する。
    ///
    /// ※相手の飛行兵が「次のターンに飛翔コマンドで飛び上がる」可能性までは読まない
    ///   （今飛翔中の相手だけを数える。v1 の割り切り）。
    /// </summary>
    public static HashSet<Vector2Int> GetAirThreatCells(GridManager grid, Unit self)
    {
        var result = new HashSet<Vector2Int>();
        if (self == null) return result;

        Faction opponentFaction = self.Faction == Faction.Player ? Faction.Enemy : Faction.Player;
        foreach (Unit p in UnitRegistry.GetUnits(opponentFaction))
        {
            // 空中の相手を攻撃できるユニットか（対空できる後衛の攻撃武器 or 飛翔中。
            // 杖・武装無しは対空できない。判定は CombatRules.IsAntiAirCapable に一元化。Phase 25）
            if (!CombatRules.IsAntiAirCapable(p) && !p.IsFlying) continue;

            // 静止して撃つ（武器の上限射程まで届く）
            AddAttackRing(result, grid, p, p.GridPosition, hasMoved: false);

            // 移動して撃つ（後衛武器は最小射程ちょうどに縮む）
            foreach (Vector2Int cell in MovementCalculator.GetReachableCells(grid, p))
                AddAttackRing(result, grid, p, cell, hasMoved: true);
        }
        return result;
    }

    /// <summary>unit が center に立って（移動有無 hasMoved で）撃てるマスを result に足す。</summary>
    private static void AddAttackRing(
        HashSet<Vector2Int> result, GridManager grid, Unit unit, Vector2Int center, bool hasMoved)
    {
        if (!CombatRules.TryGetAttackRange(unit, hasMoved, out int min, out int max)) return;

        for (int dx = -max; dx <= max; dx++)
        {
            for (int dy = -max; dy <= max; dy++)
            {
                int d = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (d < min || d > max) continue;

                var cell = new Vector2Int(center.x + dx, center.y + dy);
                if (grid.GetTile(cell) == null) continue; // 盤外
                result.Add(cell);
            }
        }
    }

    /// <summary>ally に self 以外のガード役が既に付いているか（FindPincerGuard の self 除外版）。</summary>
    private static bool HasGuardOtherThan(Unit ally, Unit self, GridManager grid)
    {
        foreach (Vector2Int dir in Directions)
        {
            Vector2Int cell = ally.GridPosition + dir;
            TileData tile = grid.GetTile(cell);
            Unit guard = tile != null ? tile.Occupant : null;

            if (guard != null && guard != self && CombatRules.WouldGuardAt(guard, cell, ally))
                return true;
        }
        return false;
    }
}
