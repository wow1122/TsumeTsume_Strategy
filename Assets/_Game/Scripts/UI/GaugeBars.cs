using UnityEngine;

/// <summary>
/// マナゲージ・気勢ゲージのバーを描く共通部品（フェーズ28・IMGUI）。
/// UnitInfoLines と同じく MonoBehaviour ではない「描画の共通処理」で、
/// デバッグ用パネル（右上）と対象選択のユニット情報パネル（右下）の両方から呼ぶ。
///
/// 見た目（作者仕様）:
///  ・ユニット情報の文字行の下に、マナ（明るい緑）→ 気勢（明るい青）の順で2本
///  ・貯まっているぶんだけ、バーの左端から色が伸びる
///  ・値10ごとに黒い縦線（目盛り）が入る
///  ・バーの右隣に、貯まった本数（ストック）と現在値のカウンターを出す
///
/// 塗りつぶしには Unity 組み込みの白1x1テクスチャ（Texture2D.whiteTexture）に色を掛けて使う。
/// テクスチャを自前で作らないので後始末が要らない。
/// </summary>
public static class GaugeBars
{
    // ===== 見た目の定数 =====

    /// <summary>バー1本の高さ（ピクセル）。</summary>
    private const float BarHeight = 14f;

    /// <summary>バー1本ぶんの下の余白（ピクセル）。</summary>
    private const float BarGap = 6f;

    /// <summary>左のラベル（「マナ」「気勢」）の幅。</summary>
    private const float LabelWidth = 38f;

    /// <summary>右のカウンター文字の幅。</summary>
    private const float CounterWidth = 96f;

    /// <summary>ラベル・バー・カウンターの間隔。</summary>
    private const float Spacing = 4f;

    /// <summary>目盛りの黒線を入れる間隔（ゲージの値）。</summary>
    private const int TickStep = 10;

    private static readonly Color ManaColor = new Color(0.30f, 0.95f, 0.40f);      // 明るい緑
    private static readonly Color MomentumColor = new Color(0.35f, 0.70f, 1.00f);  // 明るい青
    private static readonly Color BackColor = new Color(0.12f, 0.12f, 0.12f, 0.90f);
    private static readonly Color FrameColor = new Color(0.65f, 0.65f, 0.65f);
    private static readonly Color TickColor = Color.black;

    /// <summary>ゲージ2本ぶんの高さ。パネルの高さ計算に足す。</summary>
    public static float TotalHeight => (BarHeight + BarGap) * 2f;

    /// <summary>
    /// 指定した領域（左上を起点）に、そのユニットのゲージ2本を描く。
    /// 戻り値は使った高さ（＝TotalHeight）。
    /// </summary>
    public static float Draw(Rect area, Unit unit, int fontSize)
    {
        if (unit == null) return 0f;

        float y = area.y;
        DrawOne(new Rect(area.x, y, area.width, BarHeight), "マナ", unit.Mana, ManaColor, fontSize);

        y += BarHeight + BarGap;
        DrawOne(new Rect(area.x, y, area.width, BarHeight), "気勢", unit.Momentum, MomentumColor, fontSize);

        return TotalHeight;
    }

    /// <summary>バー1本を描く（ラベル・バー本体・目盛り・カウンター）。</summary>
    private static void DrawOne(Rect row, string label, GaugeState gauge, Color fillColor, int fontSize)
    {
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, fontSize - 2),
            alignment = TextAnchor.MiddleLeft,
        };

        // 左のラベル
        GUI.Label(new Rect(row.x, row.y, LabelWidth, row.height), label, labelStyle);

        float barX = row.x + LabelWidth + Spacing;
        float barWidth = row.width - LabelWidth - CounterWidth - Spacing * 2f;
        if (barWidth < 20f) barWidth = 20f;
        var bar = new Rect(barX, row.y, barWidth, row.height);

        // ゲージが未構築（Initialize 前）のときは空のバーだけ描く
        if (gauge == null)
        {
            FillRect(bar, BackColor);
            DrawFrame(bar, FrameColor);
            return;
        }

        // 背景 → 貯まったぶんの塗り → 目盛り → 枠 の順に重ねる
        FillRect(bar, BackColor);

        float ratio = gauge.Max > 0 ? Mathf.Clamp01((float)gauge.Value / gauge.Max) : 0f;
        if (ratio > 0f)
            FillRect(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), fillColor);

        for (int v = TickStep; v < gauge.Max; v += TickStep)
        {
            float x = bar.x + bar.width * ((float)v / gauge.Max);
            FillRect(new Rect(x, bar.y, 1f, bar.height), TickColor);
        }

        DrawFrame(bar, FrameColor);

        // 右のカウンター（貯まった本数と現在値）
        var counterStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(10, fontSize - 2),
            alignment = TextAnchor.MiddleLeft,
        };
        string counter = $"{gauge.Stock}本 {gauge.Value}／{gauge.Max}";
        GUI.Label(new Rect(bar.xMax + Spacing, row.y, CounterWidth, row.height), counter, counterStyle);
    }

    /// <summary>単色で矩形を塗る。</summary>
    private static void FillRect(Rect rect, Color color)
    {
        Color prev = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    /// <summary>1ピクセルの枠線を描く（塗りの矩形4本で表現）。</summary>
    private static void DrawFrame(Rect rect, Color color)
    {
        FillRect(new Rect(rect.x, rect.y, rect.width, 1f), color);                 // 上
        FillRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);         // 下
        FillRect(new Rect(rect.x, rect.y, 1f, rect.height), color);                // 左
        FillRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);        // 右
    }
}
