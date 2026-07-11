using UnityEngine;

/// <summary>
/// 「1ユニットの1回の行動」の文脈をまとめて持つ入れ物。
/// どのユニットが・どこから動いて・いま移動済みかを記録し、
/// 移動の取り消し（RevertMove）はここが受け持つ。
/// ※ Phase 11 の救出コマンドでは「この行動で救出/引き受けを使ったか」等の
///   フラグや、再移動用の残り移動力（moveCostUsed から計算）をここに足していく。
/// </summary>
public class ActionContext
{
    /// <summary>行動中のユニット。</summary>
    public readonly Unit unit;

    /// <summary>移動前にいたマス（取り消しの戻り先）。</summary>
    public readonly Vector2Int originCell;

    /// <summary>移動したか（false = その場で静止。後衛武器の射程拡張の判定に使う）。</summary>
    public bool hasMoved;

    /// <summary>使った移動コスト（Phase 11 の再移動用。今のフェーズでは常に 0）。</summary>
    public int moveCostUsed;

    public ActionContext(Unit unit)
    {
        this.unit = unit;
        originCell = unit.GridPosition;
        hasMoved = false;
        moveCostUsed = 0;
    }

    /// <summary>
    /// 移動を取り消して元のマスへ戻す。
    /// マスの占有の解除・復元は Unit.MoveTo が行うので、戻した後は
    /// 他のユニットが元の移動先マスへ普通に移動できる。
    /// </summary>
    public void RevertMove(GridManager grid)
    {
        if (!hasMoved) return;
        unit.MoveTo(grid, originCell);
        hasMoved = false;
        moveCostUsed = 0;
    }
}
