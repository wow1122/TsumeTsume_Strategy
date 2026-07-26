using UnityEngine;

/// <summary>
/// 「1ユニットの1回の行動」の文脈をまとめて持つ入れ物。
/// どのユニットが・どこから動いて・いま移動済みかを記録し、
/// 移動の取り消し（RevertMove）はここが受け持つ。
///
/// Phase 11 から：救出系コマンドの「1行動1回」制限フラグと、
/// 救出後の再移動（残り移動力）・取り消し不能点（Commit）もここで管理する。
/// </summary>
public class ActionContext
{
    /// <summary>行動中のユニット。</summary>
    public readonly Unit unit;

    /// <summary>移動の取り消しで戻るマス（Commit すると現在地に更新される）。</summary>
    public Vector2Int originCell { get; private set; }

    /// <summary>移動したか（false = その場で静止。後衛武器の射程拡張の判定に使う）。</summary>
    public bool hasMoved;

    /// <summary>この行動で使った移動コストの累計（再移動の予算計算に使う）。</summary>
    public int moveCostUsed;

    // ── 救出系コマンドの「1回の行動につき1回」制限（仕様8）──
    public bool usedRescue;     // 救出を使ったか（以後この行動は再移動→待機のみ。飛行兵は飛翔も可）

    /// <summary>引き受けを使ったか（以後は降ろす/待機のみ＝合意(f)）。</summary>
    public bool usedTakeOver { get; private set; }

    /// <summary>引き受けを実行したマス（usedTakeOver のときだけ意味を持つ）。</summary>
    public Vector2Int takeOverCell { get; private set; }

    /// <summary>降ろすを使ったか（以後は残り移動力での再移動→待機のみ。2026-07-26 作者仕様）。</summary>
    public bool usedDrop { get; private set; }

    /// <summary>引き受けの使用と、それを実行したマスを記録する（ExecuteTakeOver から呼ぶ）。</summary>
    public void MarkTakeOver()
    {
        usedTakeOver = true;
        takeOverCell = unit.GridPosition;
    }

    /// <summary>
    /// 引き受けを実行したマスの上にいるか。
    /// 引き受けた行動中に「降ろす」を選べるのはこのマスの上だけ（2026-07-26 作者仕様）。
    /// </summary>
    public bool IsAtTakeOverCell => unit.GridPosition == takeOverCell;

    /// <summary>降ろすの使用を記録する（ExecuteDrop から呼ぶ）。</summary>
    public void MarkDrop()
    {
        usedDrop = true;
    }

    // ── 飛翔（Phase 14）──
    // 飛翔を使うと「その時点の位置・消費コスト・移動済みか」を柔らかい中間点として覚える。
    // メニューからの取り消しはまず中間点まで戻り（飛翔後の再移動だけを取り消す）、
    // さらに取り消すと飛翔自体を解除する（1段階ずつ戻る。Commit と違って巻き戻せる）。

    /// <summary>この行動で飛翔を使ったか（発動ターンは「着陸」不可、の判定にも使う）。</summary>
    public bool usedFlight { get; private set; }

    private Vector2Int flightCell;   // 飛翔した位置（取り消しの中間点）
    private int flightCost;          // 飛翔した時点までに使っていた移動コスト
    private bool flightHadMoved;     // 飛翔する前に移動していたか

    /// <summary>飛翔の使用を記録し、いまの位置を取り消しの中間点にする（ExecuteFlight から呼ぶ）。</summary>
    public void MarkFlight()
    {
        usedFlight = true;
        flightCell = unit.GridPosition;
        flightCost = moveCostUsed;
        flightHadMoved = hasMoved;
    }

    /// <summary>飛翔の取り消し（中間点の破棄）。位置の巻き戻しは先に RevertMove で済ませておくこと。</summary>
    public void UnmarkFlight()
    {
        usedFlight = false;
    }

    /// <summary>
    /// 取り消し不能になったか。救出・引き受けを実行すると true になり、
    /// 以後は選択解除も元の位置への取り消しもできない（必ず待機等で行動を終える）。
    /// </summary>
    public bool committed { get; private set; }

    /// <summary>再移動に使える残り移動力（救出後の MoveSelect の予算）。</summary>
    public int RemainingMove => Mathf.Max(0, unit.Move - moveCostUsed);

    // Commit 時点の移動コスト。取り消しはこの値まで巻き戻す（0までではなく）
    private int committedCost;

    // Commit 時点で「もう移動していた」か。取り消し（RevertMove）でこの値へ戻す。
    // 救出・引き受け・降ろすは false（＝従来どおり移動済みを解除する）。
    // 交換だけ true になりうる（下の Commit の説明を参照）。
    private bool committedHasMoved;

    public ActionContext(Unit unit)
    {
        this.unit = unit;
        originCell = unit.GridPosition;
        hasMoved = false;
        moveCostUsed = 0;
    }

    /// <summary>
    /// ここまでの移動を確定して「取り消し不能点」を作る（救出・引き受け・降ろす・交換の実行時に呼ぶ）。
    /// 以後の RevertMove は、行動開始位置ではなく「この時点の位置」へ戻る。
    ///
    /// keepHasMoved（フェーズ26で追加・既定 false ＝ 従来の3か所は挙動不変）：
    /// 通常は「この位置から再出発する」意味で移動済みを解除する。ところが交換は
    /// 行動を消費しない＝確定した後にそのまま攻撃できるので、ここで移動済みを解除すると
    /// 「移動した弓兵が交換すると、移動していないことになって射程が伸びる」
    /// （後衛武器は移動後だと最小射程ちょうど）という抜け穴になる。
    /// 交換からは keepHasMoved: true で呼び、移動済みを引き継ぐ。
    /// 飛翔が flightHadMoved を覚えて RevertMove で戻すのと同じ考え方。
    /// </summary>
    public void Commit(bool keepHasMoved = false)
    {
        committed = true;
        originCell = unit.GridPosition;
        committedCost = moveCostUsed;
        committedHasMoved = keepHasMoved && hasMoved;
        hasMoved = committedHasMoved;

        // 飛翔の「柔らかい中間点」も、この位置まで引き上げる（フェーズ26で修正）。
        // これが無いと RevertMove が中間点を優先するため、
        // 「飛翔 → 移動 → 引き受け（または交換）→ 取り消し」で飛翔した位置まで戻ってしまい、
        // すでに使った移動力が復活する＝取り消し不能点を作った意味が無くなる。
        // 飛翔そのものの解除（CancelFlightAndReturn）は従来どおりできる
        // （「取り消し不能でも飛翔の取り消しだけは起こる」という飛翔の合意仕様は変えない）。
        if (usedFlight)
        {
            flightCell = unit.GridPosition;
            flightCost = moveCostUsed;
            flightHadMoved = committedHasMoved;
        }
    }

    /// <summary>
    /// 移動を取り消して戻す（Commit 済みならその時点の位置まで）。
    /// 飛翔を使った後なら、まず飛翔した位置（中間点）まで＝飛翔後の再移動だけを取り消す。
    /// マスの占有の解除・復元は Unit.MoveTo が行うので、戻した後は
    /// 他のユニットが元の移動先マスへ普通に移動できる。
    /// </summary>
    public void RevertMove(GridManager grid)
    {
        if (usedFlight)
        {
            // 飛翔後の再移動だけを取り消して、飛翔した位置まで戻す
            if (unit.GridPosition != flightCell) unit.MoveTo(grid, flightCell);
            moveCostUsed = flightCost;
            hasMoved = flightHadMoved; // 飛翔前に移動していたなら「移動済み」のまま
            return;
        }

        if (!hasMoved) return;
        unit.MoveTo(grid, originCell);
        hasMoved = committedHasMoved; // 通常は false。交換の後だけ「移動済み」を保つ（Commit の説明を参照）
        moveCostUsed = committedCost;
    }
}
