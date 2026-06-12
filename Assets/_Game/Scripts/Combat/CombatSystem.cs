using UnityEngine;

/// <summary>
/// 攻撃1回を解決する。
///  ・攻撃側が防御側にダメージを与える
///  ・反撃は「基本なし」。CanCounter フックが true を返したときだけ反撃する
///    （今は常に false。将来スキル/地形/兵種で条件を足せる）
///  ・挟撃（はさみ撃ち）は Phase 5b でここに追加予定
/// </summary>
public static class CombatSystem
{
    public static void ResolveAttack(Unit attacker, Unit defender, GridManager grid)
    {
        if (attacker == null || defender == null) return;

        // 1) 攻撃側 → 防御側
        int damage = DamageCalculator.Calculate(attacker, defender, grid);
        defender.TakeDamage(damage);
        Debug.Log($"攻撃: {attacker.Data.unitName} → {defender.Data.unitName} に {damage} ダメージ" +
                  (defender.IsAlive ? $"（残りHP {defender.CurrentHP}）" : "（戦闘不能）"));

        // 2) 反撃（基本なし。フックが true のときだけ）
        if (defender.IsAlive && CanCounter(attacker, defender, grid))
        {
            int counter = DamageCalculator.Calculate(defender, attacker, grid);
            attacker.TakeDamage(counter);
            Debug.Log($"反撃: {defender.Data.unitName} → {attacker.Data.unitName} に {counter} ダメージ");
        }

        // 3) 挟撃（はさみ撃ち）
        TryPincer(attacker, defender, grid);
    }

    /// <summary>
    /// 挟撃：近接攻撃のとき、防御側を挟んだ反対側の隣マスに攻撃側の味方がいれば、
    /// その味方も追加で攻撃する（上下左右の一直線のみ・斜めは対象外）。
    /// 参加した味方は行動を消費しない（おまけの追撃）。
    /// </summary>
    private static void TryPincer(Unit attacker, Unit defender, GridManager grid)
    {
        if (defender == null || !defender.IsAlive) return;

        // 近接攻撃のみ：攻撃側が防御側に隣接（マンハッタン距離1）しているか
        Vector2Int diff = defender.GridPosition - attacker.GridPosition;
        if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) != 1) return; // 隣接でない＝遠距離攻撃

        // 防御側を挟んだ反対側のマス
        Vector2Int oppositeCell = defender.GridPosition + diff;
        TileData tile = grid.GetTile(oppositeCell);
        Unit ally = tile != null ? tile.Occupant : null;

        // 反対側に、攻撃側と同じ陣営の別ユニットがいれば挟撃成立
        if (ally == null || ally == attacker || ally.Faction != attacker.Faction) return;

        int damage = DamageCalculator.Calculate(ally, defender, grid);
        defender.TakeDamage(damage);
        Debug.Log($"挟撃！ {ally.Data.unitName} も {defender.Data.unitName} に {damage} ダメージ" +
                  (defender.IsAlive ? $"（残りHP {defender.CurrentHP}）" : "（戦闘不能）"));
    }

    /// <summary>
    /// 反撃できるかどうかの判定（拡張ポイント）。
    /// 今は常に false（反撃なし）。将来ここに条件を書くと反撃が有効化される。
    /// 例：防御側が反撃スキルを持つ／特定地形にいる／攻撃を射程内で受けた等。
    /// </summary>
    private static bool CanCounter(Unit attacker, Unit defender, GridManager grid)
    {
        return false;
    }
}
