using UnityEngine;

/// <summary>
/// 盤上のユニットの隣に出すメニュー（行動メニュー／コマンド一覧・持ち物・貨物リスト）の
/// 置き場所を決める共通処理（2026-08-17 作者指示）。
/// UnitInfoLines・GaugeBars と同じく MonoBehaviour ではない「共通処理」の置き場。
///
/// **これらのメニューは盤面の外に出さない**のが決まり。
/// 以前は画面いっぱいを基準に回り込み・押し込みをしていたので、盤面の下側のマスで開くと
/// 盤面の下の帯（地形情報）に重なっていた。基準を盤面の矩形に変えることで、
/// 帯や右側のパネルの領域へメニューがはみ出さなくなる。
///
/// ※画面の隅に固定で出すもの（持ち物の詳細パネル・戦闘予測・ユニット情報）は対象外。
///   ここが面倒を見るのは「盤上のユニットの隣に出るもの」だけ。
/// </summary>
public static class MenuPlacement
{
    // 盤面の矩形を毎回探すのは無駄なので一度だけ引いて覚える。
    // Play を止めても静的フィールドは残るので、UnitRegistry と同じ作法で入り直しのたびに戻す
    private static GridManager grid;

    [RuntimeInitializeOnLoadMethod]
    private static void ResetStatics() => grid = null;

    /// <summary>
    /// メニューを収める範囲＝盤面が画面に映っている矩形（GUI 座標）。
    /// 盤面が見つからないときは画面全体（従来の挙動）にしておく。
    /// </summary>
    public static Rect Bounds
    {
        get
        {
            if (grid == null) grid = Object.FindAnyObjectByType<GridManager>();

            Rect board = grid != null ? grid.GetBoardScreenRect() : new Rect();
            return board.width > 0f ? board : new Rect(0f, 0f, Screen.width, Screen.height);
        }
    }

    /// <summary>
    /// 基準ユニットの右隣に width×height のメニューを置いた矩形を返す。
    /// 盤面の右端をはみ出すなら左隣へ回り込み、縦はユニットの中心に合わせてから範囲へ押し込む。
    /// center / right は「ユニットの中心」と「1マス右」のスクリーン座標。
    /// altX には回り込み先（反対の隣）の x を返す＝呼び出し側が別の逃がし方に使う。
    /// </summary>
    public static Rect Beside(Vector3 center, Vector3 right, float width, float height, out float altX)
    {
        Rect bounds = Bounds;

        altX = center.x - (right.x - center.x) - width;                  // 左隣
        float x = (right.x + width > bounds.xMax) ? altX : right.x;

        // GUI座標は左上原点・Y下向きなので、スクリーンYを反転。縦はユニットの中心に合わせる
        float y = Screen.height - center.y - height * 0.5f;

        return Clamp(new Rect(x, y, width, height));
    }

    /// <summary>矩形を盤面の中へ押し込む（範囲より大きいときは左上に寄る）。</summary>
    public static Rect Clamp(Rect rect)
    {
        Rect bounds = Bounds;

        rect.x = Mathf.Clamp(rect.x, bounds.x, Mathf.Max(bounds.x, bounds.xMax - rect.width));
        rect.y = Mathf.Clamp(rect.y, bounds.y, Mathf.Max(bounds.y, bounds.yMax - rect.height));
        return rect;
    }
}
