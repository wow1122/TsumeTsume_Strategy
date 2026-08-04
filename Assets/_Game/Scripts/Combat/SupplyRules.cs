using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 補給圏（陣中／孤立）の共有ルール集（フェーズ32）。
/// CombatRules / RescueRules / StaffRules / ExchangeRules / DurabilityRules と同じ方針で、
/// 判定を1箇所に集めてある。
///
/// 物語上の設定（局地戦なので「兵站」ではなく人の往復で説明する）:
/// 輸送隊は部隊自身の装具一式を積んだ荷駄で、従者が前線と荷駄を往復して傷んだ得物を替えている。
/// だから距離が関係し、敵に道を塞がれると往復できなくなり、山や城壁は越えられない。
/// 輸送隊から従者が通える範囲を「陣中」、通えなくなった状態を「孤立」と呼ぶ。
///
/// ここにあるルール：
///  ・補給元    … 同陣営で生存している輸送隊（いなければ補給の仕組み自体を止める）
///  ・補給圏    … 輸送隊から歩いて SupplyRange 以内のマス ＋ その上下左右の隣マス
///  ・回復      … 自軍フェイズ開始時、陣中のユニットが持つ標準装備の武器の耐久が RepairPerTurn 増える
///
/// 【重要】補給圏は MovementCalculator.GetReachableCells では作れない。あちらは
/// 「止まれるマス」を返す関数で、誰かが立っているマスを除外する（`if (!occupied)`）。
/// 補給圏で最も重要なのは味方が立っているマスなので、それを落とすと足元に穴が空く。
/// GetDistanceMap は CanTraverse しか見ないので占有マスを含み、しかも
///  ・敵のマスは通れない  → 敵1体で補給線が切れる
///  ・味方のマスは通れる  → 味方が列を作っても切れない
///  ・輸送隊は騎乗扱い    → 山は通れず森はコスト3
/// が追加のコードなしでそのまま成立する。
/// </summary>
public static class SupplyRules
{
    // ── 調整用の定数（手触りを変えたいときはここだけ見ればよい）──

    /// <summary>
    /// 補給圏の広さ（輸送隊からの移動コスト）。輸送隊の移動力（Class_Transporter＝5）に
    /// 合わせてある＝「輸送隊が動ける範囲」。実際にはさらに隣1マスぶん外まで届く（下記）。
    /// </summary>
    public const int SupplyRange = 5;

    /// <summary>陣中のユニットが自軍フェイズ開始ごとに回復する耐久。</summary>
    public const int RepairPerTurn = 1;

    // ── 補給元（輸送隊）──

    /// <summary>このユニットが補給元（輸送隊）か。</summary>
    public static bool IsSupplySource(Unit unit)
    {
        return unit != null && unit.IsAlive && unit.Class == UnitClass.Transporter;
    }

    /// <summary>
    /// 指定陣営の補給元（輸送隊）を返す。いなければ null＝そのステージでは補給の仕組みを使わない。
    /// GetUnits は格納中（盤上にいない）ユニットを含まないので、輸送隊自身が誰かに
    /// 運ばれている場合も自動的に対象外になる（盤上の位置が定まらないため）。
    /// 複数いる場合は先頭の1体（現状そういうステージは無い）。
    /// </summary>
    public static Unit FindSupplySource(Faction faction)
    {
        foreach (Unit u in UnitRegistry.GetUnits(faction))
            if (IsSupplySource(u)) return u;
        return null;
    }

    // ── 補給圏 ──

    /// <summary>
    /// 補給圏のマスの集合を作る。輸送隊がいなければ null を返す
    /// （＝補給の仕組みを使わないステージ。全員を陣中扱いにして従来どおり動かす）。
    ///
    /// 作り方:
    ///  1. 輸送隊の現在地を目標に GetDistanceMap を引く（敵の通せんぼと地形を見る）
    ///  2. 距離が SupplyRange 以下のマスを集める（輸送隊自身のマスは距離0なので必ず入る）
    ///  3. さらにその上下左右の隣マスを足す。従者は手渡しできるので、輸送隊が入れない
    ///     地形（山など）に立っている味方にも届く。足すのは1周ぶんだけなので、
    ///     壁の向こう側や敵の後ろまで漏れ出すことはない
    ///     （足すのは誰かが立てるマスだけ。壁・城壁は誰も立てないので足さない＝紫に塗らない）
    /// </summary>
    public static HashSet<Vector2Int> GetSupplyCells(GridManager grid, Faction faction)
    {
        if (grid == null) return null;

        Unit source = FindSupplySource(faction);
        if (source == null) return null;

        var goals = new List<Vector2Int> { source.GridPosition };
        Dictionary<Vector2Int, int> dist =
            MovementCalculator.GetDistanceMap(grid, source, goals, ignoreEnemyUnits: false);

        var cells = new HashSet<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, int> pair in dist)
            if (pair.Value <= SupplyRange) cells.Add(pair.Key);

        // 隣マスを1周ぶん足す（地形は見ない＝従者の手渡し）。
        // 元の集合を回しながら足すと広がり続けるので、いったん別の集合に集めてから合流する
        var fringe = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in cells)
        {
            AddIfInside(grid, fringe, cell + Vector2Int.up);
            AddIfInside(grid, fringe, cell + Vector2Int.down);
            AddIfInside(grid, fringe, cell + Vector2Int.left);
            AddIfInside(grid, fringe, cell + Vector2Int.right);
        }
        cells.UnionWith(fringe);

        return cells;
    }

    /// <summary>
    /// 隣マスとして補給圏に足す（盤内で、地上ユニットが立てるマスだけ）。
    /// 地形の移動コストは見ない（従者は手渡しするだけなので、山でも森でも届く）。
    /// 壁・城壁は誰も立てないので足さない＝ハイライトで紫に塗られることもない。
    /// </summary>
    private static void AddIfInside(GridManager grid, HashSet<Vector2Int> set, Vector2Int cell)
    {
        TileData tile = grid.GetTile(cell);
        if (tile != null && tile.IsWalkable) set.Add(cell);
    }

    /// <summary>
    /// このユニットが陣中か（補給圏の集合を持っている側から呼ぶ）。
    /// cells が null＝輸送隊のいないステージでは、全員を陣中扱いにする
    /// （Stage_AITest_* のように輸送隊を置いていない検証用ステージを従来どおり動かすため）。
    /// </summary>
    public static bool IsSupplied(Unit unit, HashSet<Vector2Int> cells)
    {
        if (unit == null) return false;
        if (cells == null) return true;
        return cells.Contains(unit.GridPosition);
    }

    // ── 表示用（1フレームに1回だけ計算する）──
    // 情報パネルは OnGUI から毎フレーム（しかも Layout と Repaint の2回）呼ばれるので、
    // そのたびに塗りつぶしを走らせない。フレームが変わったら必ず計算し直すので、
    // 輸送隊が動いた直後でも表示は最新になる。
    // 回復の本体（TickRepair）はこのキャッシュを使わず、その場で計算する
    // （フェイズ開始の直前に敵が動いているので、同じフレームの古い結果を使ってはいけない）。

    private static GridManager cachedGrid;
    private static int cachedFrame = -1;
    private static Faction cachedFaction;
    private static HashSet<Vector2Int> cachedCells;

    /// <summary>
    /// 表示用に補給圏を返す（同じフレーム内なら計算済みのものを使い回す）。
    /// 盤面の参照は自分で探す（情報パネルは GridManager を持っていないため）。
    /// </summary>
    public static HashSet<Vector2Int> GetSupplyCellsCached(Faction faction)
    {
        if (cachedGrid == null) cachedGrid = Object.FindAnyObjectByType<GridManager>();
        if (cachedGrid == null) return null;

        if (cachedFrame == Time.frameCount && cachedFaction == faction) return cachedCells;

        cachedCells = GetSupplyCells(cachedGrid, faction);
        cachedFrame = Time.frameCount;
        cachedFaction = faction;
        return cachedCells;
    }

    /// <summary>
    /// 表示用の陣中判定。輸送隊のいないステージ・格納中のユニットでは false を返す
    /// （どちらも「陣中／孤立」を出す意味が無い。格納中は盤上の位置が定まらないため）。
    /// 補給圏があるかどうかは HasSupplyLine で先に確かめる。
    /// </summary>
    public static bool IsSuppliedForDisplay(Unit unit)
    {
        if (unit == null || unit.IsCarried) return false;
        HashSet<Vector2Int> cells = GetSupplyCellsCached(unit.Faction);
        return cells != null && cells.Contains(unit.GridPosition);
    }

    /// <summary>そのユニットに「陣中／孤立」を出す意味があるか（輸送隊がいて、盤上にいる）。</summary>
    public static bool HasSupplyLine(Unit unit)
    {
        if (unit == null || unit.IsCarried) return false;
        return GetSupplyCellsCached(unit.Faction) != null;
    }

    // ── 毎ターンの回復 ──

    /// <summary>
    /// 自軍フェイズ開始時に呼ぶ。陣中のユニットが持つ標準装備の武器の耐久を RepairPerTurn 増やす。
    ///
    /// 回復するのは装備中の1本だけではなく所持品の中の標準装備の武器すべて
    /// （「傷んだ方を装備し直して待つ」という作業を発生させないため＝作者合意）。
    /// 輸送隊に格納されている味方も、運んでいる側が陣中なら一緒に回復する
    /// （荷駄の中にいるのだから当然届く）。
    ///
    /// 標準装備でない武器（isStandardIssue = false）は資源が要る想定なので、ここでは回復しない。
    /// 資源システムはまだ無いため、現状そういう武器は存在しない。
    /// </summary>
    public static void TickRepair(Faction faction, GridManager grid)
    {
        HashSet<Vector2Int> cells = GetSupplyCells(grid, faction);
        if (cells == null) return; // 輸送隊のいないステージでは補給そのものを行わない

        var isolated = new List<string>();

        foreach (Unit unit in UnitRegistry.GetUnits(faction))
        {
            if (!IsSupplied(unit, cells))
            {
                if (HasWornOrDamagedWeapon(unit)) isolated.Add(unit.Data.unitName);
                continue;
            }

            RepairUnit(unit);
            foreach (Unit cargo in unit.Carried) RepairUnit(cargo); // 荷駄・騎乗に運ばれている味方
        }

        if (isolated.Count > 0)
            Debug.Log($"孤立: {string.Join("、", isolated)}（輸送隊から従者が通えないので武器は回復しない）");
    }

    /// <summary>ユニット1体ぶんの回復。標準装備で耐久のある武器だけが対象。</summary>
    private static void RepairUnit(Unit unit)
    {
        if (unit == null) return;

        foreach (ItemSlot slot in unit.Items)
        {
            if (!(slot.Item is WeaponData weapon)) continue;
            if (!weapon.isStandardIssue) continue;

            int before = slot.UsesLeft;
            if (slot.Refill(RepairPerTurn) <= 0) continue;

            Debug.Log($"補給: {unit.Data.unitName} の {weapon.DisplayName} の耐久 {before} → {slot.UsesLeft}");
        }
    }

    /// <summary>孤立のログに出す価値があるか（傷んだ、または減っている武器を持っているか）。</summary>
    private static bool HasWornOrDamagedWeapon(Unit unit)
    {
        foreach (ItemSlot slot in unit.Items)
            if (slot.Item is WeaponData && slot.HasDurability && slot.UsesLeft < slot.MaxUses)
                return true;
        return false;
    }
}
