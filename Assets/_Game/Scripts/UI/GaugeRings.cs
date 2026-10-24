using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 盤上のユニットのマスに出す、リング状のメモリ・覚醒ゲージ（2026-10-10 作者仕様・IMGUI）。
/// GaugeBars（ユニット情報パネルの横棒）と同じく MonoBehaviour ではない描画の共通処理で、
/// Unit.OnGUI から呼ぶ。
///
/// 見た目（作者仕様）:
///  ・マスの左下隅にメモリ、右下隅に覚醒（2026-10-11 に上隅から変更）。外円の直径はマスの一辺の 1/5
///  ・HP ゲージはリングと重ならないよう、リング2つぶんの直径だけ短くして中央に置く（Unit.OnGUI）
///  ・ユニットの絵と重なってもリングが上に見える（OnGUI の描画は常にスプライトより手前）
///  ・リングの一番上から**反時計回り**に、貯まったぶんだけ色が伸びる
///  ・色はユニット情報のゲージと同じ（GaugeBars.MemoryColor / MomentumColor）
///  ・貯まりきって保持している本数（ストック）は、リングの真上にリングと同じ大きさの数字で出す
///    （下隅に移したので、真下だとマスの外にはみ出るため 2026-10-11 に真上へ変更）
///
/// 既定として決めたこと（作者に伝達済み・変えるならここの定数）:
///  ・リングの太さ＝半径の半分（InnerRadiusRatio）。未蓄積の部分は GaugeBars.BackColor
///  ・ストックは1本以上のときだけ出す。白い数字に黒い影
///
/// IMGUI には円弧を描く命令が無いので、リングは「直径・値・最大値・色」ごとに小さなテクスチャを
/// 作ってキャッシュし、GUI.DrawTexture で貼る（値は整数なので作る枚数は高々数百枚で済む）。
/// </summary>
public static class GaugeRings
{
    /// <summary>外円の直径を、マスの一辺の何分の1にするか。</summary>
    private const float DiameterRatio = 1f / 5f;

    /// <summary>内円の半径 ÷ 外円の半径（0.5 ＝ リングの太さは半径の半分）。</summary>
    private const float InnerRadiusRatio = 0.5f;

    /// <summary>これより小さい直径にはしない（ピクセル。小さな画面でも形が分かるように）。</summary>
    private const int MinDiameter = 5;

    /// <summary>リングをマスの角から離す余白（ピクセル）。</summary>
    private const float CornerMargin = 1f;

    /// <summary>ふちを滑らかにするための、1ピクセルあたりの判定点の数（一辺）。</summary>
    private const int Supersample = 4;

    /// <summary>キャッシュがこれを超えたら一度全部捨てる（画面サイズを何度も変えたとき用の保険）。</summary>
    private const int MaxCachedTextures = 512;

    private static readonly Dictionary<(int diameter, int value, int max, Color32 color), Texture2D> cache =
        new Dictionary<(int, int, int, Color32), Texture2D>();

    /// <summary>cell（マスの画面上の矩形）に描くときのリングの外円の直径（ピクセル）。</summary>
    public static int GetDiameter(Rect cell)
    {
        return Mathf.Max(MinDiameter, Mathf.RoundToInt(cell.width * DiameterRatio));
    }

    /// <summary>
    /// cell（マスの画面上の矩形・GUI 座標。見えているマスの内側 inset ぶんを除いて使う）の下隅に、
    /// unit のメモリ（左下）と覚醒（右下）のリングを描く。
    /// </summary>
    public static void Draw(Rect cell, float inset, Unit unit)
    {
        if (unit == null) return;

        int d = GetDiameter(cell);
        float top = Mathf.Round(cell.yMax - inset - CornerMargin - d);
        float left = Mathf.Round(cell.x + inset + CornerMargin);
        float right = Mathf.Round(cell.xMax - inset - CornerMargin - d);

        DrawOne(new Rect(left, top, d, d), unit.Memory, GaugeBars.MemoryColor);
        DrawOne(new Rect(right, top, d, d), unit.Momentum, GaugeBars.MomentumColor);
    }

    /// <summary>リング1つと、その真上のストック数を描く。</summary>
    private static void DrawOne(Rect ring, GaugeState gauge, Color fillColor)
    {
        if (gauge == null) return;

        int d = Mathf.RoundToInt(ring.width);
        GUI.DrawTexture(ring, GetTexture(d, gauge.Value, gauge.Max, fillColor));

        if (gauge.Stock <= 0) return;

        // 数字の高さをリングの直径に合わせる。IMGUI の fontSize は文字の枠の大きさで、
        // 数字の見た目の高さはその7割ほどなので、直径 ÷ 0.7 を目安にする
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.Max(6, Mathf.RoundToInt(d / 0.7f)),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            clipping = TextClipping.Overflow,
        };
        // 横はリングより広く取って、2桁でも欠けないようにする（中心はリングと揃える）
        var label = new Rect(ring.center.x - d, ring.y - d, d * 2f, d);
        string text = gauge.Stock.ToString();

        style.normal.textColor = Color.black;   // 影（盤面の色に埋もれないように）
        GUI.Label(new Rect(label.x + 1f, label.y + 1f, label.width, label.height), text, style);
        style.normal.textColor = Color.white;
        GUI.Label(label, text, style);
    }

    /// <summary>直径・値・最大値・色に対応するリングのテクスチャを返す（無ければ作ってキャッシュ）。</summary>
    private static Texture2D GetTexture(int diameter, int value, int max, Color fillColor)
    {
        var key = (diameter, value, max, (Color32)fillColor);
        if (cache.TryGetValue(key, out Texture2D cached) && cached != null) return cached;

        if (cache.Count >= MaxCachedTextures) ClearCache();

        Texture2D tex = BuildTexture(diameter, max > 0 ? Mathf.Clamp01((float)value / max) : 0f, fillColor);
        cache[key] = tex;
        return tex;
    }

    /// <summary>
    /// リングのテクスチャを作る。各ピクセルを Supersample×Supersample 点で調べ、
    /// リングの内側に入った点の割合を不透明度にする（ふちが滑らかになる）。
    /// 色は「一番上から反時計回りに測った角度」が ratio×360° 未満なら塗り色、それ以外は背景色。
    /// </summary>
    private static Texture2D BuildTexture(int d, float ratio, Color fillColor)
    {
        var tex = new Texture2D(d, d, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };

        float outer = d * 0.5f;
        float inner = outer * InnerRadiusRatio;
        float fillAngle = ratio * Mathf.PI * 2f;
        Color backColor = GaugeBars.BackColor;
        var pixels = new Color[d * d];

        for (int py = 0; py < d; py++)
        {
            for (int px = 0; px < d; px++)
            {
                int inside = 0, filled = 0;
                for (int sy = 0; sy < Supersample; sy++)
                {
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        // テクスチャは左下原点・Y 上向き（GUI.DrawTexture でもそのまま上が上になる）
                        float x = px + (sx + 0.5f) / Supersample - outer;
                        float y = py + (sy + 0.5f) / Supersample - outer;
                        float r = Mathf.Sqrt(x * x + y * y);
                        if (r > outer || r < inner) continue;
                        inside++;

                        // 真上を 0 として反時計回りに増える角度（左＝90°、下＝180°、右＝270°）
                        float a = Mathf.Atan2(-x, y);
                        if (a < 0f) a += Mathf.PI * 2f;
                        if (a < fillAngle) filled++;
                    }
                }

                if (inside == 0)
                {
                    pixels[py * d + px] = Color.clear;
                    continue;
                }

                // 塗りと背景が混ざるピクセル（塗りの先端）は多い方の色にする
                Color c = filled * 2 >= inside ? fillColor : backColor;
                c.a *= (float)inside / (Supersample * Supersample);
                pixels[py * d + px] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    /// <summary>作ったテクスチャを全部破棄する。</summary>
    private static void ClearCache()
    {
        foreach (Texture2D tex in cache.Values)
            if (tex != null) Object.Destroy(tex);
        cache.Clear();
    }

    // Domain Reload を切った Play でも前回のテクスチャを持ち越さない（UnitRegistry と同じ作法）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cache.Clear();
    }
}
