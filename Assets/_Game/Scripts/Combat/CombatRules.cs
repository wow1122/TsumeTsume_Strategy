using UnityEngine;

/// <summary>
/// 戦闘の「可否・補正」の判定をすべて集めた共有ルール集（Phase 9〜）。
/// プレイヤー操作（BattleController）と敵AI（EnemyAI）が同じ関数を呼ぶことで、
/// ルールを追加・変更したときに両者の判断がズレない構造にしている。
///
/// ここにあるルール：
///  ・攻撃射程   … 前衛=武器の射程どおり／後衛=移動後は最小射程ちょうど、静止時は武器上限まで
///  ・三すくみ   … 前衛武器の攻撃のみ。補正量は 技・速さ の差から計算（最低2）
///  ・挟撃の資格 … 前衛武器の装備者だけが挟撃に参加できる
///  ・挟撃の無効化（ガード）… 防御側の隣に「前衛武器の歩兵」の味方がいれば挟撃されない
///  ・飛翔（Phase 14）… 飛翔中の相手と戦えるのは「飛翔中のユニット」か「後衛武器」だけ。
///    飛翔中のユニットは地上の相手を攻撃できない（CanEngage）。挟撃・ガードも同じ制限に従う
/// </summary>
public static class CombatRules
{
    /// <summary>隣接4方向（上下左右）。挟撃・ガードの判定で使う。</summary>
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
    };

    // ===== 攻撃射程 =====

    /// <summary>
    /// unit の攻撃射程（最小〜最大マス数）を求める。武器が無ければ false（攻撃不可）。
    /// 後衛武器（弓・魔法）の特別ルール：
    ///   移動した後 … 最小射程ちょうど（基本の2マス）しか撃てない
    ///   静止したまま… 最小射程〜武器の最大射程（弓3・魔法2）まで届く
    /// </summary>
    public static bool TryGetAttackRange(Unit unit, bool hasMoved, out int minRange, out int maxRange)
    {
        WeaponData weapon = unit.Weapon;
        if (weapon == null)
        {
            minRange = 0;
            maxRange = -1;
            return false; // 武装無しは攻撃できない
        }

        if (weapon.category == WeaponCategory.Ranged)
        {
            minRange = weapon.minRange;
            maxRange = hasMoved ? weapon.minRange : Mathf.Max(weapon.minRange, weapon.maxRange);
        }
        else
        {
            minRange = weapon.minRange;
            maxRange = weapon.maxRange;
        }
        return true;
    }

    /// <summary>
    /// attacker が fromCell に立って（移動有無 hasMoved で）target を攻撃できるか。
    /// 射程に加えて、飛翔の戦闘制限（CanEngage）も見る。
    /// </summary>
    public static bool CanAttack(Unit attacker, Vector2Int fromCell, Unit target, bool hasMoved)
    {
        if (target == null || !target.IsAlive) return false;
        if (!CanEngage(attacker, target)) return false; // 飛翔の制限（Phase 14）
        if (!TryGetAttackRange(attacker, hasMoved, out int min, out int max)) return false;

        int distance = Manhattan(fromCell, target.GridPosition);
        return distance >= min && distance <= max;
    }

    /// <summary>
    /// 飛翔を考慮して「そもそも戦闘が成立する組み合わせか」（Phase 14）。
    ///   防御側が飛翔中 → 攻撃側も飛翔中か、後衛武器（弓・魔法の対空）のみ可
    ///   攻撃側だけ飛翔中 → 地上の相手とは戦闘できない
    ///   どちらも地上（または着地後） → 制限なし
    /// </summary>
    public static bool CanEngage(Unit attacker, Unit defender)
    {
        if (defender.IsFlying)
            return attacker.IsFlying
                || (attacker.Weapon != null && attacker.Weapon.category == WeaponCategory.Ranged);

        return !attacker.IsFlying; // 攻撃側だけ飛翔中なら不成立
    }

    // ===== 三すくみ =====

    /// <summary>
    /// 攻撃側から見た三すくみ補正（ダメージ式の攻撃項に加算する値）。
    ///   攻撃側が後衛武器・武装無し → 補正なし（0）
    ///   攻撃側が前衛武器：
    ///     防御側が後衛武器・武装無し → 「武装無し扱い」で攻撃側有利
    ///     防御側も前衛武器           → 相性表（剣＞斧＞槍＞剣）で判定
    ///   有利なら +max(2, 攻撃側の技 − 防御側の速さ)
    ///   不利なら −max(2, 防御側の速さ − 攻撃側の技)
    /// </summary>
    public static int GetTriangleModifier(Unit attacker, Unit defender)
    {
        WeaponData atkWeapon = attacker.Weapon;
        if (atkWeapon == null || atkWeapon.category != WeaponCategory.Melee)
            return 0; // 後衛武器・武装無しの攻撃には三すくみが無い

        WeaponData defWeapon = defender.Weapon;
        bool defenderUnarmed = defWeapon == null || defWeapon.category == WeaponCategory.Ranged;

        if (defenderUnarmed || Beats(atkWeapon.type, defWeapon.type))
            return Mathf.Max(2, attacker.Skill - defender.Speed);   // 攻撃側が有利

        if (Beats(defWeapon.type, atkWeapon.type))
            return -Mathf.Max(2, defender.Speed - attacker.Skill);  // 攻撃側が不利

        return 0; // 同種など、相性なし
    }

    /// <summary>x が y に三すくみで勝つか（剣＞斧＞槍＞剣）。</summary>
    private static bool Beats(WeaponType x, WeaponType y)
    {
        return (x == WeaponType.Sword && y == WeaponType.Axe)
            || (x == WeaponType.Axe && y == WeaponType.Lance)
            || (x == WeaponType.Lance && y == WeaponType.Sword);
    }

    // ===== 挟撃（はさみ撃ち） =====

    /// <summary>挟撃に参加できるユニットか（前衛武器の装備者のみ）。</summary>
    public static bool IsPincerCapable(Unit unit)
    {
        return unit != null
            && unit.Weapon != null
            && unit.Weapon.category == WeaponCategory.Melee;
    }

    /// <summary>
    /// attackerCell から defender を近接攻撃したとき、挟撃してくれる味方を探す。
    /// 成立条件（すべて満たすとき、その味方を返す。不成立なら null）：
    ///   ・攻撃側が前衛武器で、defender の上下左右に隣接している（＝近接攻撃）
    ///   ・defender を挟んだ反対側のマスに、攻撃側と同じ陣営の別ユニットがいる
    ///   ・その味方も前衛武器を装備している（後衛武器・武装無しは参加できない）
    /// ※attackerCell を引数にしているのは、AI が「このマスに移動したら」という
    ///   仮定の位置で予測できるようにするため。実際の攻撃時は現在地を渡す。
    /// </summary>
    public static Unit FindPincerAlly(Vector2Int attackerCell, Unit attacker, Unit defender, GridManager grid)
    {
        if (!IsPincerCapable(attacker)) return null;

        Vector2Int diff = defender.GridPosition - attackerCell;
        if (Mathf.Abs(diff.x) + Mathf.Abs(diff.y) != 1) return null; // 隣接でない＝遠距離攻撃

        // 防御側を挟んだ反対側のマス
        TileData tile = grid.GetTile(defender.GridPosition + diff);
        Unit ally = tile != null ? tile.Occupant : null;

        if (ally == null || ally == attacker || ally.Faction != attacker.Faction) return null;
        if (!IsPincerCapable(ally)) return null;
        if (!CanEngage(ally, defender)) return null; // 飛翔中の敵を挟めるのは飛翔中の味方だけ（Phase 14）

        return ally;
    }

    /// <summary>
    /// defender への挟撃を無効化してくれる「ガード役」を探す（いなければ null）。
    /// ガード役の条件：defender の上下左右に隣接する、同じ陣営の「前衛武器を装備した歩兵」。
    /// ※ガード役自身が受ける挟撃は守れない（自分の隣に別のガード役が必要）。
    /// ※行動済みでも有効。敵味方どちらの陣営でも同じルール。
    /// ※防御側が飛翔中なら不成立（空中戦に地上の歩兵は届かない。Phase 14・作者合意）。
    /// </summary>
    public static Unit FindPincerGuard(Unit defender, GridManager grid)
    {
        if (defender.IsFlying) return null; // 飛翔中はガードされない（Phase 14）

        foreach (Vector2Int dir in Directions)
        {
            TileData tile = grid.GetTile(defender.GridPosition + dir);
            Unit guard = tile != null ? tile.Occupant : null;

            if (guard != null
                && guard != defender
                && guard.Faction == defender.Faction
                && guard.Class == UnitClass.Infantry
                && IsPincerCapable(guard))
            {
                return guard;
            }
        }
        return null;
    }

    /// <summary>defender への挟撃が無効化されるか（ガード役がいるか）。</summary>
    public static bool IsPincerNegated(Unit defender, GridManager grid)
    {
        return FindPincerGuard(defender, grid) != null;
    }

    // ===== 補助 =====

    /// <summary>マンハッタン距離（縦横のマス数の合計）。</summary>
    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
