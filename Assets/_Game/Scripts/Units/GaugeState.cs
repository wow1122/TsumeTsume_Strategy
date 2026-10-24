/// <summary>
/// ゲージ1本ぶんの「ランタイム状態」（フェーズ28）。メモリゲージと気勢ゲージが共用する。
///
/// 値（Value）が満タン（Max）に達したらストック（使える本数）へ1本ずつ繰り上げ、
/// 超過した分は次のゲージへ持ち越す。ストックが上限（MaxStock）に達しているときは
/// 値が Max で頭打ちになる（それ以上の上昇は捨てる）。
///
/// アセット（UnitData）は全ユニット共有の定義なので、ユニットごとの蓄積はこちらで持つ
/// （ItemSlot・Unit のランタイム能力値と同じ考え方）。
///
/// 【警告】ここは「純粋な状態変更」に保つこと。ログ・演出・行動消費は呼び出し側に置く
/// （理由は Unit.cs の飛翔まわりの警告コメントと同じ。敵AIが状態を試算する probe を壊さないため）。
/// </summary>
public class GaugeState
{
    /// <summary>いまのゲージ値（0〜Max）。</summary>
    public int Value { get; private set; }

    /// <summary>貯まったゲージの本数（0〜MaxStock）。1本消費して能力を使う。</summary>
    public int Stock { get; private set; }

    /// <summary>ゲージ1本ぶんの満タン値。</summary>
    public int Max { get; }

    /// <summary>保持できる本数の上限。</summary>
    public int MaxStock { get; }

    /// <summary>1本でも使える状態か。</summary>
    public bool HasStock => Stock > 0;

    /// <summary>ストックが上限まで貯まっているか（これ以上ゲージが伸びない状態）。</summary>
    public bool IsFull => Stock >= MaxStock && Value >= Max;

    /// <summary>
    /// 貯まっている量の合計＝本数×満タン値＋いまの値（例：1本＋3 なら 33）。
    /// 武器のメモリ消費は本数も崩してこの合計から払う（2026-10-24 作者合意）。
    /// </summary>
    public int Total => Stock * Max + Value;

    public GaugeState(int max, int maxStock)
    {
        Max = max;
        MaxStock = maxStock;
    }

    /// <summary>
    /// ゲージを増やす。Max に達するごとにストックへ1本繰り上げ、超過分は次のゲージに残す。
    /// ストックが上限のときは Max で頭打ち（超過分は捨てる）。
    /// </summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;

        Value += amount;

        while (Value >= Max && Stock < MaxStock)
        {
            Value -= Max;
            Stock++;
        }

        // ストックが上限のときは満タンで止める（あふれた分は捨てる）
        if (Stock >= MaxStock && Value > Max) Value = Max;
    }

    /// <summary>ストックを1本使う。使えたら true、貯まっていなければ false。</summary>
    public bool SpendStock()
    {
        if (Stock <= 0) return false;
        Stock--;
        return true;
    }

    /// <summary>
    /// 合計（Total）から amount を払う。本数も崩して払い、払った後は
    /// 「本数＝残りの合計÷満タン値（上限まで）・値＝その余り」に並べ直す
    /// （例：1本＋3＝33 から5を払うと 0本＋28。2026-10-24 作者合意）。
    /// 足りなければ何もせず false。0以下の量は払うものが無いので true。
    /// </summary>
    public bool Spend(int amount)
    {
        if (amount <= 0) return true;

        int total = Total;
        if (total < amount) return false;

        total -= amount;
        Stock = System.Math.Min(MaxStock, total / Max);
        Value = total - Stock * Max;
        return true;
    }
}
