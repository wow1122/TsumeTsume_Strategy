using UnityEngine;

/// <summary>
/// 敵AIの「行動の種類」。Phase 16 では Stay / Approach / Attack を使う。
/// Guard は Phase 19 で実装済み。TakeOff / LandAndWait は Phase 20 で使う予定（先に名前だけ用意）。
/// </summary>
public enum ActionKind
{
    Stay,        // その場で待機（動く価値のあるマスが無い）
    Approach,    // 攻撃できる相手がいないので、目標へ近づく
    Attack,      // 移動して（または その場から）攻撃する
    Guard,       // 味方の隣に立って挟撃を防ぐ（Phase 19）
    TakeOff,     // 飛翔を発動して移動する（Phase 20 で実装予定）
    LandAndWait, // 移動して着陸し、行動を終える（Phase 20 で実装予定）
}

/// <summary>
/// 敵1体の「これからやる行動」を表す入れ物。
/// EnemyAI.Evaluate（考える）が作り、Execute（動かす）が実行し、思考ログにも使う。
/// 「考える」と「動かす」を分けておくと、後のフェーズで判断材料が増えても
/// 実行部分を触らずに済み、思考の理由をログで説明できる。
/// </summary>
public struct ActionPlan
{
    public ActionKind kind;
    public Vector2Int standCell;  // 立つマス（動かないなら現在地）
    public Unit target;           // Attack のときの攻撃相手（それ以外は null）
    public int score;             // 攻撃の点数（ログ・デバッグ用）
    public string reason;         // 思考ログに出す日本語の説明
}
