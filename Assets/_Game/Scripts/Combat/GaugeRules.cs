using UnityEngine;

/// <summary>
/// メモリゲージ（技 → 必殺技）と覚醒ゲージ（速さ → 再行動）のルールを一元管理する（フェーズ28）。
/// メモリゲージは 2026-10-24 の作者仕様変更でマナゲージから改名した（中身の仕組みは同じ）。
/// CombatRules / RescueRules / StaffRules / ExchangeRules と同じ「判定は1か所に集める」方針。
/// UIも敵AIも、ゲージまわりの数値と可否は必ずここを通して読む。
///
/// なぜ作ったか：技と速さは三すくみ補正の計算1か所でしか使われておらず、
/// キャラクターの個性としてほとんど働いていなかった。この2つに独自の役割を与えるのが目的。
///
///  ・メモリゲージ … マップ開始時に「技」の値ぶん貯まった状態で始まり、
///                   自陣営のフェイズ開始ごとに「魔力」に応じた量だけ増える。
///                   1本（30）貯めると必殺技（フェーズ29）が撃てる。
///  ・覚醒ゲージ … 戦闘で敵を倒すたびに、倒した側の全ユニットに「速さ」に応じた量が配られる。
///                 量は連続撃破の数で3段階（1体目＝速さの半分・2連続目＝速さ・3連続目以降＝速さの2倍。
///                 2026-10-25 作者仕様）。1本（100）貯めると再行動（フェーズ30）ができる。
///
/// 【注意】現行ユニットの能力値は 技1〜6・速さ1〜7・魔力0〜6 で、下の閾値にはほぼ届かない
/// （覚醒は 2026-10-25 の変更後でも、速さ7が敵を何体も続けて倒してやっと1本になる程度）。
/// 動作確認は専用の検証ステージ（Stage_GaugeTest）で行う。Stage_01 で遊べるようにするには、
/// ユニットの能力値を上げるか、下の定数を下げる調整が別途必要（作者と相談のうえで行う）。
/// </summary>
public static class GaugeRules
{
    // ===== 調整用の定数（バランスを変えるときはここだけ書き換える）=====
    // 敵AIの点数定数と同じく、数値は先頭の1ブロックに集めてある。

    // ── メモリゲージ（技 → 必殺技）──

    /// <summary>メモリゲージ1本ぶんの満タン値。</summary>
    public const int MaxMemory = 30;

    /// <summary>保持できるメモリの本数。</summary>
    public const int MaxMemoryStock = 2;

    /// <summary>メモリの毎ターンの上昇量（魔力が低いとき）。</summary>
    public const int MemoryGainLow = 1;

    /// <summary>魔力がこの値以上なら上昇量が MemoryGainMid になる。</summary>
    public const int MagicThresholdMid = 20;

    /// <summary>魔力が MagicThresholdMid 以上のときの上昇量。</summary>
    public const int MemoryGainMid = 2;

    /// <summary>魔力がこの値以上なら上昇量が MemoryGainHigh になる。</summary>
    public const int MagicThresholdHigh = 30;

    /// <summary>魔力が MagicThresholdHigh 以上のときの上昇量。</summary>
    public const int MemoryGainHigh = 3;

    /// <summary>必殺技のダメージ倍率（フェーズ29で使う）。</summary>
    public const float CriticalMultiplier = 2.0f;

    /// <summary>
    /// 必殺技で払うメモリ（1本＝満タン1回ぶん）。武器のメモリ消費はこれとは別に払う
    /// （＝必殺技は「1本＋その武器の消費」。2026-10-24 の既定）。
    /// </summary>
    public const int CriticalMemoryCost = MaxMemory;

    // ── 覚醒ゲージ（速さ → 再行動）──

    /// <summary>覚醒ゲージ1本ぶんの満タン値。</summary>
    public const int MaxMomentum = 100;

    /// <summary>保持できる覚醒の本数。</summary>
    public const int MaxMomentumStock = 2;

    // 撃破1回で増える覚醒＝速さ × 下の割合（％。切り捨て）。連続撃破の数で3段階（2026-10-25 作者仕様）。
    // 連鎖が切れたら（倒せなかった戦闘・フェイズの切り替わり）、次の撃破は1体目の割合に戻る。

    /// <summary>1体目（連続していない撃破）の覚醒の上昇率（％）。50＝速さの半分。</summary>
    public const int MomentumPercentFirst = 50;

    /// <summary>2連続目の撃破の覚醒の上昇率（％）。100＝速さと同じ。</summary>
    public const int MomentumPercentChain2 = 100;

    /// <summary>3連続目以降の撃破の覚醒の上昇率（％）。200＝速さの2倍。これより先は増えない。</summary>
    public const int MomentumPercentChain3Plus = 200;

    // ===== 連続撃破（連鎖）の管理 =====
    // 「戦闘1回（CombatSystem.ResolveAttack 1回。挟撃のとどめを含む）で相手を倒したか」を
    // 数える。倒せなかった戦闘とフェイズの切り替わりで連鎖は切れる。

    private static Faction chainFaction;
    private static int chainCount;

    // Enter Play Mode Options（ドメインリロード無効）でも前回プレイの連鎖を
    // 引きずらないよう、プレイ開始のたびにリセットする（UnitRegistry / EnemyAI と同じ作法）。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        chainFaction = Faction.Player;
        chainCount = 0;
    }

    /// <summary>いま何連続で撃破しているか（ログ・デバッグ表示用）。</summary>
    public static int ChainCount => chainCount;

    /// <summary>連鎖を切る（フェイズの切り替わりで TurnManager が呼ぶ）。</summary>
    public static void ResetChain()
    {
        chainCount = 0;
    }

    /// <summary>
    /// 戦闘1回の結果を受け取って連鎖を進める／切る（CombatSystem.ResolveAttack が呼ぶ）。
    /// 撃破するたびに、倒した側の全生存ユニットへ覚醒を配る。量は連続撃破の数で決まる
    /// （1体目＝速さの半分・2連続目＝速さ・3連続目以降＝速さの2倍。作者仕様 2026-10-25）。
    /// ※旧版（2026-07-26）は「2連続目から、撃破1回につき速さ÷2」で、1体目では配らなかった。
    /// </summary>
    public static void NotifyBattleResult(Faction attackerFaction, bool killed)
    {
        if (!killed)
        {
            chainCount = 0;
            return;
        }

        // 陣営が変わったら数え直し（フェイズをまたいで繋がらないようにする保険）
        if (chainCount > 0 && chainFaction != attackerFaction) chainCount = 0;
        chainFaction = attackerFaction;
        chainCount++;

        AwardMomentum(attackerFaction, chainCount);

        // 割合が100の倍数でないとき（1体目の50％など）だけ「切り捨て」と書き添える
        int percent = MomentumPercent(chainCount);
        string chainLabel = chainCount >= 2 ? $"{chainCount}連続撃破" : "撃破";
        string rounding = percent % 100 != 0 ? "（切り捨て）" : "";
        Debug.Log($"覚醒！ {chainLabel} → {FactionLabel(attackerFaction)}の全ユニットの覚醒ゲージが" +
                  $"「速さの{percent}％{rounding}」ずつ貯まった");
    }

    /// <summary>
    /// 指定陣営の全生存ユニットに、chain 連続目の撃破1回分の覚醒を配る（量は各自の速さで決まる）。
    /// 格納中（救出されて盤上にいない）の貨物にも入るので、UnitRegistry.All を使う
    /// （GetUnits は盤上のユニットしか返さない）。
    /// </summary>
    private static void AwardMomentum(Faction faction, int chain)
    {
        foreach (Unit u in UnitRegistry.All)
        {
            if (u == null || !u.IsAlive || u.Faction != faction) continue;
            u.AddMomentum(MomentumGain(u, chain));
        }
    }

    // ===== メモリ =====

    /// <summary>
    /// マップ開始時のメモリ（作者仕様：技の値をそのまま入れる）。
    /// 30 に達した分は GaugeState 側で1本に変換され、超過分が残る（技31 → 1本＋1）。
    /// </summary>
    public static int InitialMemory(Unit unit)
    {
        return unit != null ? Mathf.Max(0, unit.Skill) : 0;
    }

    /// <summary>自陣営のフェイズ開始1回ぶんのメモリ上昇量（魔力で3段階）。</summary>
    public static int MemoryGainPerTurn(Unit unit)
    {
        if (unit == null) return 0;
        if (unit.Magic >= MagicThresholdHigh) return MemoryGainHigh;
        if (unit.Magic >= MagicThresholdMid) return MemoryGainMid;
        return MemoryGainLow;
    }

    /// <summary>
    /// 指定陣営の全生存ユニットのメモリを1回ぶん増やす（フェイズ開始時に TurnManager が呼ぶ）。
    /// 覚醒と同じく、格納中の貨物にも入る。
    /// </summary>
    public static void TickMemory(Faction faction)
    {
        foreach (Unit u in UnitRegistry.All)
        {
            if (u == null || !u.IsAlive || u.Faction != faction) continue;
            u.AddMemory(MemoryGainPerTurn(u));
        }
    }

    // ===== 覚醒 =====

    /// <summary>
    /// chain 連続目の撃破1回で増える覚醒＝速さ × MomentumPercent(chain) ÷ 100（切り捨て）。
    /// 例：速さ7なら 1体目3・2連続目7・3連続目以降14。
    /// </summary>
    public static int MomentumGain(Unit unit, int chain)
    {
        if (unit == null) return 0;
        return Mathf.Max(0, unit.Speed * MomentumPercent(chain) / 100);
    }

    /// <summary>chain 連続目の撃破の上昇率（％）。1以下は1体目扱い、3以上はすべて3連続目と同じ。</summary>
    private static int MomentumPercent(int chain)
    {
        if (chain >= 3) return MomentumPercentChain3Plus;
        if (chain == 2) return MomentumPercentChain2;
        return MomentumPercentFirst;
    }

    // ===== コマンドの可否 =====

    /// <summary>
    /// 必殺技（メモリ1本消費）を撃てるだけのメモリがあるか（フェーズ29）。
    /// 2026-10-24 から武器の消費も一緒に払うので、どの武器で撃てるかは
    /// WeaponChoiceRules に extraCost（＝CriticalMemoryCost）を渡して別に確かめる。
    /// </summary>
    public static bool CanUseCritical(Unit unit)
    {
        return unit != null && unit.IsAlive && unit.Memory != null && unit.Memory.Total >= CriticalMemoryCost;
    }

    /// <summary>
    /// 再行動（覚醒1本消費）が使える状態か（フェーズ30）。
    /// 行動済みのユニットだけが対象。覚醒が残っていれば、同じターンに何回でも使える
    /// （2026-10-25 作者指示で「1ターン1回」の制限を外した。制限が要るときは作者から相談する）。
    /// </summary>
    public static bool CanRefresh(Unit unit)
    {
        return unit != null && unit.IsAlive && unit.HasActed
               && unit.Momentum != null && unit.Momentum.HasStock;
    }

    /// <summary>
    /// その陣営に「まだ再行動できるユニット」が盤上にいるか（フェーズ30）。
    /// 味方全員が行動済みでもターンを自動終了させないための判定に使う。
    /// </summary>
    public static bool AnyoneCanRefresh(Faction faction)
    {
        foreach (Unit u in UnitRegistry.GetUnits(faction))
            if (CanRefresh(u)) return true;
        return false;
    }

    private static string FactionLabel(Faction faction)
    {
        return faction == Faction.Player ? "自軍" : "敵軍";
    }
}
