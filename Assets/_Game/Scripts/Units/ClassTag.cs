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
    // 名前の変更（2026-10-24 作者指示）: 天馬 → 白翼、亜竜 → 竜騎兵。数字の値は変えていないのでアセットは無傷
    [InspectorName("騎馬")] Horse       = 1 << 0, // 騎馬特攻を受ける
    [InspectorName("白翼")] WhiteWing   = 1 << 1, // 白翼特攻を受ける（スカイナイト・聖白翼兵・金鵄兵。旧「天馬」）
    [InspectorName("竜")]   Dragon      = 1 << 2, // 竜特攻を受ける（竜そのもの。将来のユニット用）
    [InspectorName("重装")] Armor       = 1 << 3, // 重装特攻を受ける（2026-08-05 追加）
    [InspectorName("竜騎兵")] DragonRider = 1 << 4, // 竜騎兵特攻を受ける（ドラゴンナイト・大竜騎兵・魔竜騎兵。竜に含まれる。2026-08-06 追加＝必ず末尾。旧「亜竜」）
}

/// <summary>
/// 特攻タグの包含関係と重ね合わせ（2026-08-06 作者仕様）。
///
/// 【包含関係】竜騎兵属性は竜属性の一種。竜騎兵タグを持つ兵種は、竜を狙う武器の対象にもなる（逆は成り立たない）。
/// これにより「斧の有効は竜騎兵にだけ効き、魔導書と竜特効の武器は竜＝竜騎兵も含めて効く」が表せる。
///
/// 包含関係を持つのはコードのこの1か所だけ。兵種アセットには竜騎兵だけを付ける（竜は付けない）。
/// 両方付けても結果は同じだが、付け忘れで挙動が変わらないよう、判定はここに集めてある。
/// 将来ほかのタグに階層を作りたくなったら Expand に1行足す。
/// </summary>
public static class ClassTagExtensions
{
    /// <summary>
    /// 包含関係を反映したタグに広げる（竜騎兵 → 竜騎兵＋竜）。
    /// 「このユニットは何の特攻を受けるか」を判定する直前に必ず通す。
    /// </summary>
    public static ClassTag Expand(this ClassTag tags)
    {
        if ((tags & ClassTag.DragonRider) != 0) tags |= ClassTag.Dragon;
        return tags;
    }

    /// <summary>
    /// 武器が狙うタグを「実際に当たるタグ」へ広げる（竜を狙う武器は竜騎兵にも当たる）。
    /// 上の Expand と同じ包含関係を、武器の側から見たもの。
    /// 判定には要らない（Expand だけで足りる）が、表示で
    /// 「この武器は何に効くのか」を並べるときに、こちらの向きが必要になる。
    /// </summary>
    public static ClassTag Cover(this ClassTag targetTags)
    {
        if ((targetTags & ClassTag.Dragon) != 0) targetTags |= ClassTag.DragonRider;
        return targetTags;
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

    /// <summary>
    /// タグの日本語名（表示用。複数持っていれば「騎馬・重装」のように並べる）。
    /// shortForm は武器の説明のように幅の狭い場所だけで使う短い呼び名（竜騎兵 → 竜騎。ほかは同じ）。
    /// 「竜騎」は他の作品の題名と被るので、短い表記が要る場所に限って使う（2026-10-24 作者指示）。
    /// 戦闘予測の注記などは正式名のまま（shortForm を渡さない）。
    /// </summary>
    public static string DisplayName(this ClassTag tags, bool shortForm = false)
    {
        if (tags == ClassTag.None) return "なし";

        var names = new System.Collections.Generic.List<string>();
        if ((tags & ClassTag.Horse) != 0) names.Add("騎馬");
        if ((tags & ClassTag.WhiteWing) != 0) names.Add("白翼");
        if ((tags & ClassTag.Dragon) != 0) names.Add("竜");
        if ((tags & ClassTag.Armor) != 0) names.Add("重装");
        if ((tags & ClassTag.DragonRider) != 0) names.Add(shortForm ? "竜騎" : "竜騎兵");
        return string.Join("・", names);
    }
}
