using UnityEngine;

/// <summary>
/// ユニットの兵種（クラス）。分類の階層（作者合意 2026-07-11／2026-08-05 に重装歩兵を追加）：
///   徒歩兵 ─ 歩兵（ゲーム上の呼称は「歩兵」）
///          └ 重装歩兵
///   騎乗兵 ─ 騎兵（輸送隊は騎兵の一種）
///          └ 飛行兵
/// 救出（Phase 11〜）や地形の通行（Phase 13〜）は、この兵種を条件にしている。
/// 「騎兵向け」のルールを書くときは IsCavalry() を使うと輸送隊にも自動で効く。
/// 同じように「歩兵向け」のルールは IsFoot()（＝徒歩。歩兵＋重装歩兵）を使う。
///
/// 【用語の決まり（2026-08-05 作者合意）】
/// 「歩兵」という語は通常歩兵だけを指す（ゲーム上の呼称と一致）。2つをまとめて指すときは
/// 必ず「徒歩」と呼ぶ。コードでも Class == UnitClass.Infantry と直接比べず、
/// IsFoot()（広義）か IsNormalInfantry()（狭義）のどちらかを書くこと。
/// そうすれば「どちらのつもりで書いたか」がコードに残り、読み違えが起きない。
///
/// 兵種データ（ClassData）導入後は、これは「移動タイプ」の分類として使われ、
/// 職業としての兵種（剣闘士・弓使など）と移動力は ClassData が持つ。
/// </summary>
public enum UnitClass
{
    // 【重要】並び順を変えない・途中に挿入しない。この値は .asset に数字で保存されている
    // （歩兵0・騎兵1・飛行兵2・輸送隊3・重装歩兵4）。順番を変えると既存ユニットの兵種が化ける。
    [InspectorName("歩兵")] Infantry,       // 歩兵（通常歩兵。徒歩の一種）
    [InspectorName("騎兵")] Cavalry,        // 騎兵（騎乗）
    [InspectorName("飛行兵")] Flier,        // 飛行兵（騎乗）
    [InspectorName("輸送隊")] Transporter,  // 輸送隊（騎乗・騎兵の一種）
    [InspectorName("重装歩兵")] Armor,      // 重装歩兵（徒歩の一種。2026-08-05 追加＝必ず末尾）
}

/// <summary>UnitClass への便利な問い合わせ（拡張メソッド）。</summary>
public static class UnitClassExtensions
{
    /// <summary>
    /// 徒歩系か（歩兵＋重装歩兵）。「まとめて歩兵」と言いたいときは必ずこれを使う。
    /// 地形・救出・ガードなど、これまで「歩兵」を条件にしていたルールはすべてこちら。
    /// </summary>
    public static bool IsFoot(this UnitClass unitClass)
    {
        return unitClass == UnitClass.Infantry || unitClass == UnitClass.Armor;
    }

    /// <summary>
    /// 通常歩兵か（重装歩兵を含まない＝ゲーム上の「歩兵」そのもの）。
    /// 「重装は除く」ルールを書くときだけ使う（現状は呼び出し元なし。
    /// 素の == を書かせず、狭義のつもりだと分かるようにするための入口）。
    /// </summary>
    public static bool IsNormalInfantry(this UnitClass unitClass)
    {
        return unitClass == UnitClass.Infantry;
    }

    /// <summary>騎乗ユニットか（徒歩系＝歩兵・重装歩兵 以外はすべて騎乗扱い。輸送隊も含む）。</summary>
    public static bool IsMounted(this UnitClass unitClass)
    {
        return !unitClass.IsFoot();
    }

    /// <summary>
    /// 飛行兵か（2026-10-24 作者仕様：飛翔の撤廃後、飛行兵は常に空を飛んで移動する）。
    /// 飛行兵は地形を無視して動き（全マスコスト1・屋内の壁だけ不可。TileData）、
    /// 地形防御を受けない（DamageCalculator）。騎乗の一種なので救出（容量1）は従来どおり。
    /// </summary>
    public static bool IsFlier(this UnitClass unitClass)
    {
        return unitClass == UnitClass.Flier;
    }

    /// <summary>騎兵系か（騎兵と、その一種である輸送隊。飛行兵は含まない）。</summary>
    public static bool IsCavalry(this UnitClass unitClass)
    {
        return unitClass == UnitClass.Cavalry || unitClass == UnitClass.Transporter;
    }

    /// <summary>移動タイプの日本語名（表示用）。</summary>
    public static string DisplayName(this UnitClass unitClass)
    {
        switch (unitClass)
        {
            case UnitClass.Cavalry: return "騎兵";
            case UnitClass.Flier: return "飛行兵";
            case UnitClass.Transporter: return "輸送隊";
            case UnitClass.Armor: return "重装歩兵";
            default: return "歩兵";
        }
    }
}
