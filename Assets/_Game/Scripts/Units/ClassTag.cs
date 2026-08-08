using System;
using UnityEngine;

/// <summary>
/// 兵種の特攻タグ（この兵種が「受ける」特攻の種類。複数指定可）。
/// 例：騎馬特攻の武器は、騎馬タグを持つ兵種に有効・特効のダメージを与える
/// （戦闘への反映は EffectiveRules。2026-08-06「有効と特効」で実装）。
///
/// タグは移動タイプ（UnitClass）とは別の軸。たとえば鎧騎馬兵は移動タイプが「騎兵」なので
/// 移動タイプでは重装と表せないが、重装特攻は受けてほしい——タグならそれが書ける
/// （騎馬タグと重装タグの両方を付ける。2026-08-05）。
/// </summary>
[Flags]
public enum ClassTag
{
    // 【重要】値は .asset に数字で保存されている。追加は必ず末尾（途中に入れると既存の兵種が化ける）
    None = 0,
    [InspectorName("騎馬")] Horse     = 1 << 0, // 騎馬特攻を受ける
    [InspectorName("天馬")] Pegasus   = 1 << 1, // 天馬特攻を受ける
    [InspectorName("竜")]   Dragon    = 1 << 2, // 竜特攻を受ける（竜そのもの。将来のユニット用）
    [InspectorName("重装")] Armor     = 1 << 3, // 重装特攻を受ける（2026-08-05 追加）
    [InspectorName("亜竜")] SubDragon = 1 << 4, // 亜竜＝竜騎兵が乗る飛竜（2026-08-06 追加＝必ず末尾）
}

/// <summary>
/// 特攻タグの包含関係と重ね合わせ（2026-08-06 作者仕様）。
///
/// 【包含関係】亜竜は竜の一種。亜竜タグを持つ兵種は、竜を狙う武器の対象にもなる（逆は成り立たない）。
/// これにより「斧の有効は亜竜にだけ効き、魔導書と竜狩り武器は竜＝亜竜も含めて効く」が表せる。
///
/// 包含関係を持つのはコードのこの1か所だけ。兵種アセットには亜竜だけを付ける（竜は付けない）。
/// 両方付けても結果は同じだが、付け忘れで挙動が変わらないよう、判定はここに集めてある。
/// 将来ほかのタグに階層を作りたくなったら Expand に1行足す。
/// </summary>
public static class ClassTagExtensions
{
    /// <summary>
    /// 包含関係を反映したタグに広げる（亜竜 → 亜竜＋竜）。
    /// 「このユニットは何の特攻を受けるか」を判定する直前に必ず通す。
    /// </summary>
    public static ClassTag Expand(this ClassTag tags)
    {
        if ((tags & ClassTag.SubDragon) != 0) tags |= ClassTag.Dragon;
        return tags;
    }

    /// <summary>
    /// ユニットのタグが、武器の狙うタグに当てはまるか（包含関係を考慮する）。
    /// unitTags はユニットの兵種が持つタグ、targetTags は武器が狙うタグ。
    /// targetTags が空（None）なら常に false ＝ 相性なし。
    /// </summary>
    public static bool Matches(ClassTag unitTags, ClassTag targetTags)
    {
        if (targetTags == ClassTag.None) return false;
        return (unitTags.Expand() & targetTags) != ClassTag.None;
    }

    /// <summary>タグの日本語名（表示用。複数持っていれば「騎馬・重装」のように並べる）。</summary>
    public static string DisplayName(this ClassTag tags)
    {
        if (tags == ClassTag.None) return "なし";

        var names = new System.Collections.Generic.List<string>();
        if ((tags & ClassTag.Horse) != 0) names.Add("騎馬");
        if ((tags & ClassTag.Pegasus) != 0) names.Add("天馬");
        if ((tags & ClassTag.Dragon) != 0) names.Add("竜");
        if ((tags & ClassTag.Armor) != 0) names.Add("重装");
        if ((tags & ClassTag.SubDragon) != 0) names.Add("亜竜");
        return string.Join("・", names);
    }
}
