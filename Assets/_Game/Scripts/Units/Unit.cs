using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 盤上に存在するユニット1体。能力値(UnitData)を参照し、
/// 自分のグリッド座標・現在HPを持ち、仮素材(色付き四角)で表示する。
/// 自軍は青、敵軍は赤で表示する。
/// 能力値は Initialize のときに UnitData からコピーした「ランタイム能力値」を使う。
/// （将来、一時的な強化・弱体や成長を入れるとき、元データ(アセット)を汚さずに書き換えられる）
/// </summary>
public class Unit : MonoBehaviour
{
    public UnitData Data { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public int CurrentHP { get; private set; }

    // ── ランタイム能力値（Initialize で UnitData からコピーされる）──
    public int MaxHP { get; private set; }        // HP（最大値）
    public int Strength { get; private set; }     // 力（物理攻撃）
    public int Magic { get; private set; }        // 魔力（魔法攻撃）
    public int Skill { get; private set; }        // 技（三すくみ補正・Phase 9 から使用）
    public int Speed { get; private set; }        // 速さ（三すくみ補正・Phase 9 から使用）
    public int Defense { get; private set; }      // 守備（物理防御）
    public int Resistance { get; private set; }   // 魔防（魔法防御）
    public int GroundMove { get; private set; }   // 移動力（地上を歩くとき）
    public int FlightMove { get; private set; }   // 移動力（飛翔中。飛行兵以外は地上と同じ値）

    /// <summary>
    /// いまの移動力。飛翔中は飛翔用の値を返す（2026-08-10 作者仕様。天馬兵は地上4/飛翔6、
    /// 竜騎兵は地上3/飛翔7）。移動力を読む側はこの窓口だけを見る約束で、
    /// これ1箇所で 移動範囲・再移動の残り予算・敵AI・情報パネルの表示が自動で追随する
    /// （装備武器を Weapon 窓口へ集めたのと同じ手口）。
    /// 【注意】「地上を歩いたときの距離」を測りたい場所（敵AIの着陸判断・陸路のターン数）は、
    /// 飛翔中に呼ばれても地上の値が要るので、この窓口ではなく GroundMove を読むこと。
    /// </summary>
    public int Move => IsFlying ? FlightMove : GroundMove;

    /// <summary>陣営（UnitData から取得）。</summary>
    public Faction Faction => Data.faction;

    /// <summary>兵種＝移動タイプ（UnitData から取得。兵種データがあればそちらが優先）。</summary>
    public UnitClass Class => Data.EffectiveClass;

    /// <summary>
    /// 特攻タグ（このユニットが受ける有効・特効の種類。兵種データ由来）。
    /// 判定は EffectiveRules が行う（包含関係＝亜竜は竜、は ClassTagExtensions.Matches が受け持つ）。
    /// </summary>
    public ClassTag Tags => Data.EffectiveTags;

    /// <summary>
    /// 戦闘に使える武器。武器はこの窓口から読む（Data.weapon の直読みはしない約束。
    /// フェーズ22からはランタイム装備 EquippedWeapon を指す）。
    /// 【フェーズ31】耐久が尽きた武器を装備しているときは null を返す＝「装備無し」扱い。
    /// これ1箇所で、攻撃不可・三すくみで不利・挟撃やガードの資格を失う・対空でなくなる・
    /// 静止撃ち有利が消える・敵AIが武装無しとして扱う、が全て自動で成立する
    /// （CombatRules / DamageCalculator / EnemyAI は全てこの窓口だけを見ているため）。
    /// 「何を装備しているか」を見せたいだけの表示は EquippedWeapon を読むこと。
    /// </summary>
    public WeaponData Weapon => IsEquippedWeaponWorn ? null : EquippedWeapon;

    /// <summary>このターンに行動済みか（移動・攻撃を終えたか）。</summary>
    public bool HasActed { get; private set; }

    /// <summary>まだ生存しているか。</summary>
    public bool IsAlive => CurrentHP > 0;

    // ── 所持品（フェーズ22〜）──
    // 所持品は UnitData.items（無ければ旧 weapon 欄）を Initialize でコピーした
    // ランタイム状態（ItemSlot）で持つ。道具の残り回数などユニットごとの消費を、
    // 元データ（アセット）を汚さずに管理する（ランタイム能力値と同じ考え方）。
    // 装備中の武器も同様にランタイム状態で、読む側は従来どおり Weapon プロパティだけを見る。

    /// <summary>所持品の一覧（ランタイム状態）。</summary>
    public List<ItemSlot> Items { get; } = new List<ItemSlot>();

    /// <summary>装備中の武器（null = 武装無し）。</summary>
    public WeaponData EquippedWeapon { get; private set; }

    /// <summary>
    /// 武器を装備する（フェーズ23の「所持品」コマンド）。行動は消費しない。
    /// 呼び出し側（BattleController）が所持品の中の装備可能な武器だけを渡すので、
    /// ここでは所持や兵種の可否は確認しない。読む側は従来どおり Weapon 窓口で自動追随する。
    ///
    /// 【警告】飛翔（StartFlight/CancelFlight）と同じ理由で、Equip / Unequip は
    /// 「純粋な状態変更」のまま保つこと（2026-08-09）。攻撃対象の計算と戦闘予測が
    /// probe（一時的に装備を差し替えて評価し、必ず元へ戻す。WeaponChoiceRules.WithWeapon）を
    /// 使っており、ここに副作用（ログ・演出・行動消費・耐久の増減など）を足すと、
    /// 予測を1回描くたびにそれが実行されて壊れる。ログや演出は呼び出し側に置くこと。
    /// </summary>
    public void Equip(WeaponData weapon)
    {
        EquippedWeapon = weapon;
    }

    /// <summary>
    /// 武装解除する（EquippedWeapon = null＝武装無し扱い）。攻撃コマンドが出なくなり、
    /// 前衛から攻撃されると不利判定、挟撃の資格も失う（既存の武装無しルールが自動で効く）。
    /// </summary>
    public void Unequip()
    {
        EquippedWeapon = null;
    }

    // ── 武器の耐久（フェーズ31）──
    // 耐久値そのものは所持品のランタイム状態（ItemSlot.UsesLeft）に入っている。
    // 攻撃1回・杖の回復1回ごとに1減り、0になると「傷んでいる」＝ Weapon が null を返すようになる。
    // そのとき装備は外れない（持ち替えずに補給を待てる＝作者合意）。
    // マップが変わると Initialize で所持品ごと作り直されるので、資源なしで満タンに戻る。
    //
    // 【警告】飛翔・ゲージと同じ理由で、ここは「純粋な状態変更」に保つこと。
    // 「誰の武器が減るか（自軍だけ）」やログは DurabilityRules と呼び出し側の担当。

    /// <summary>
    /// 装備中の武器のランタイム状態（耐久値の置き場）。武装無しなら null。
    /// ※同じ武器アセットを2本持っていると先頭の1本を指す（現状そういうユニットはいない）。
    ///   2本持ちを許すなら、装備をアセット参照から ItemSlot 参照へ変える必要がある。
    /// </summary>
    public ItemSlot EquippedSlot
    {
        get
        {
            if (EquippedWeapon == null) return null;
            foreach (ItemSlot slot in Items)
                if (slot.Item == EquippedWeapon) return slot;
            return null;
        }
    }

    /// <summary>装備中の武器が傷んでいる（耐久が尽きた）か。耐久なしの武器なら常に false。</summary>
    public bool IsEquippedWeaponWorn
    {
        get
        {
            ItemSlot slot = EquippedSlot;
            return slot != null && slot.HasDurability && slot.UsesLeft <= 0;
        }
    }

    /// <summary>
    /// 装備中の武器の耐久を1減らす（攻撃1回・杖の回復1回ぶん）。
    /// 【重要】UseTool と違い、0になっても所持品からは絶対に取り除かない。
    /// 耐久0の武器も「装備はできる・攻撃だけできない」が合意仕様で、補給で復活するため。
    /// 戻り値は減らしたあとの残り耐久。耐久なしの武器・武装無しなら -1（＝減らさなかった）。
    /// </summary>
    public int ConsumeWeaponUse()
    {
        ItemSlot slot = EquippedSlot;
        if (slot == null || !slot.HasDurability) return -1;

        if (slot.UsesLeft > 0) slot.UsesLeft--;
        return slot.UsesLeft;
    }

    /// <summary>
    /// 道具を使う（フェーズ24）。効果（今は使用者自身のHP回復のみ）を適用し、
    /// 残り回数を1減らす。0回になったら所持品リストから取り除く。
    /// 戻り値は実際に回復した量（ログ表示用）。使用の可否（HPが満タンでない等）と
    /// 「使うと行動終了」は呼び出し側（BattleController）が扱う。
    /// </summary>
    public int UseTool(ItemSlot slot)
    {
        int healed = 0;
        if (slot.Item is ToolData tool)
        {
            int before = CurrentHP;
            Heal(tool.healAmount);
            healed = CurrentHP - before;
        }

        slot.UsesLeft--;
        if (slot.UsesLeft <= 0) Items.Remove(slot);
        return healed;
    }

    /// <summary>
    /// 所持品を1つ捨てる（「持ち物」の「捨てる」）。所持品リストから取り除く。
    /// 捨てるのが装備中の武器なら武装解除も行う（持っていない武器は装備できないため）。
    /// ランタイム状態(Items)のみ変更で元データ(アセット)は汚さない＝その戦闘中だけ消え、
    /// 次の戦闘は Initialize で元の所持品に戻る。行動を消費するかは呼び出し側が扱う
    /// （合意：捨てるは行動を消費しない）。
    /// </summary>
    public void DiscardItem(ItemSlot slot)
    {
        if (slot.Item == EquippedWeapon) EquippedWeapon = null;
        Items.Remove(slot);
    }

    /// <summary>
    /// 所持品を1つ相手へ渡す（「交換」・フェーズ26）。装備中の武器を渡したときは
    /// 自動で武装解除する（「捨てる」と同じ扱い。持っていない武器は装備できないため）。
    /// 【重要】ItemSlot（ランタイム状態）をそのまま移すので、道具の残り使用回数は引き継がれる。
    /// 受け側で new ItemSlot(...) を作ると UsesLeft が maxUses に戻る＝
    /// 「渡すと使用回数が満タンに戻る裏技」になるので、必ずこの関数を通すこと。
    /// 受け側の空き（上限7）の確認と、行動を消費するかは呼び出し側（BattleController）が扱う
    /// （合意：交換は行動を消費しないが、1個でも動かすとその行動は取り消せなくなる）。
    /// </summary>
    public void TransferItemTo(ItemSlot slot, Unit receiver)
    {
        if (slot.Item == EquippedWeapon) EquippedWeapon = null;
        Items.Remove(slot);
        receiver.Items.Add(slot);
    }

    // ── 救出（Phase 11〜）──
    // 「運ぶ側」は Carried に相手を格納し、「運ばれる側」は IsCarried=true で
    // 非アクティブ化される（盤上から消え、マスの占有も明け渡す。名簿には残るので
    // 生存数には数えられ、全滅判定が誤動作しない）。

    /// <summary>いま格納している（救出中の）ユニットの一覧。</summary>
    public List<Unit> Carried { get; } = new List<Unit>();

    /// <summary>救出中か（誰かを格納しているか）。</summary>
    public bool IsRescuing => Carried.Count > 0;

    /// <summary>救出されて格納中か（盤上にいない）。</summary>
    public bool IsCarried { get; private set; }

    /// <summary>格納できる数。輸送隊=4（Phase 12）、他の騎乗=1、徒歩=0（救出できない）。</summary>
    public int CarryCapacity =>
        Class == UnitClass.Transporter ? 4 : (Class.IsMounted() ? 1 : 0);

    // ── 飛翔（Phase 14）──
    // 飛行兵専用の状態。飛翔中は地形（コスト・防御・通行不可）を無視して移動でき、
    // 戦闘は「飛翔中の相手」との間か「後衛武器による対空」しか成立しない（CombatRules.CanEngage）。
    // すり抜けの例外（飛翔中の敵・対空武器の敵は通れない）は MovementCalculator 参照。
    // 持続は発動ターンを含めて3ターン。自軍フェイズ開始ごとに TickFlight で数え、0で自動着地する。
    // 「着陸」コマンドで早めに降りることもできる（発動したターンは不可・着陸すると行動終了）。
    //
    // 【警告】StartFlight / CancelFlight は「純粋な状態変更」のまま保つこと（Phase 20）。
    // 敵AIが「飛んだら得か」を probe（お試し飛翔・お試し着地→評価→復元）で判定しており、
    // ここに副作用（ログ・演出・行動消費・占有の変更など）を足すと評価のたびに
    // それが実行されて probe が壊れる。演出やログは呼び出し側に置くこと。

    /// <summary>飛翔状態か。</summary>
    public bool IsFlying { get; private set; }

    /// <summary>飛翔状態の残りターン数（自軍フェイズ開始ごとに1減る）。</summary>
    public int FlightTurnsLeft { get; private set; }

    /// <summary>飛翔状態を開始する（発動ターンを1ターン目と数える）。</summary>
    public void StartFlight(int turns = 3)
    {
        IsFlying = true;
        FlightTurnsLeft = turns;
    }

    /// <summary>
    /// 飛翔状態を解除する。取り消し（発動した行動の中でのやり直し）と、
    /// 「着陸」コマンド（Phase 14・行動終了を伴う）の両方から呼ばれる。
    /// </summary>
    public void CancelFlight()
    {
        IsFlying = false;
        FlightTurnsLeft = 0;
    }

    /// <summary>自軍フェイズ開始時に呼ぶ。残りターンを減らし、0になったら自動着地する。</summary>
    public void TickFlight()
    {
        if (!IsFlying) return;

        FlightTurnsLeft--;
        if (FlightTurnsLeft <= 0)
        {
            IsFlying = false;
            FlightTurnsLeft = 0;
            Debug.Log($"{Data.unitName} は着地した");
        }
    }

    // ── 撃墜（2026-08-05 作者仕様）──
    // 対空武器（弓・魔導書・光魔法）の攻撃を受けた飛翔中のユニットは、その場に着地する。
    // 落下ダメージは無し。落ちた後は「次の自分のターンだけ飛翔できない」制限が付く
    // （飛翔は行動を消費しないコマンドなので、制限が無いと自分のターンにタダで飛び直せてしまい、
    //  撃墜された側の損失がほぼゼロになるため。作者合意）。
    // 撃墜されるかどうかの判定は CombatRules.ShootsDown に一元化してある。

    /// <summary>
    /// 撃墜されて飛翔できない残りフェイズ数。撃墜の瞬間に2が入り、
    /// 自陣営のフェイズ開始ごとに TickFlightBlock で1ずつ減る
    /// （次の自分のフェイズのあいだは1のまま＝そのターンは飛べない。その次の開始で0＝また飛べる）。
    /// </summary>
    public int FlightBlockedTurns { get; private set; }

    /// <summary>撃墜された直後で「飛翔」が使えない状態か。</summary>
    public bool IsFlightBlocked => FlightBlockedTurns > 0;

    /// <summary>
    /// 対空攻撃で撃墜される。飛翔を解除し、次の自分のターンは飛翔できない状態にする。
    /// 呼ぶのは CombatSystem.ResolveAttack だけ（ログは呼び出し側に置く）。
    /// ※StartFlight / CancelFlight と違い、敵AIの probe からは呼ばれないので副作用を持ってよい
    /// </summary>
    public void ShootDown()
    {
        CancelFlight();
        FlightBlockedTurns = 2;
    }

    /// <summary>自陣営フェイズ開始時に呼ぶ。撃墜による飛翔禁止の残りを1減らす。</summary>
    public void TickFlightBlock()
    {
        if (FlightBlockedTurns > 0) FlightBlockedTurns--;
    }

    // ── マナゲージ・気勢ゲージ（フェーズ28）──
    // 技と速さが三すくみ補正でしか使われていなかったので、この2つに独自の役割を与えるための仕組み。
    //  ・マナ … マップ開始時に「技」ぶん貯まっており、自陣営のフェイズ開始ごとに「魔力」に応じて増える。
    //           1本（30）で必殺技（フェーズ29）。
    //  ・気勢 … 敵を連続撃破すると、倒した側の全ユニットに「速さ÷2」ずつ配られる。
    //           1本（100）で再行動（フェーズ30）。
    // 蓄積量・可否のルール本体は Combat/GaugeRules に集約してある（CombatRules と同じ方針）。
    //
    // 【警告】飛翔と同じ理由で、ここは「純粋な状態変更」に保つこと。
    // ログ・演出・行動消費は呼び出し側（TurnManager / BattleController）に置く。

    /// <summary>マナゲージ（必殺技用）。Initialize で構築される。</summary>
    public GaugeState Mana { get; private set; }

    /// <summary>気勢ゲージ（再行動用）。Initialize で構築される。</summary>
    public GaugeState Momentum { get; private set; }

    /// <summary>このターンすでに再行動を使ったか（1ターン1回まで。フェーズ30）。</summary>
    public bool UsedRefreshThisTurn { get; private set; }

    /// <summary>マナを増やす（フェイズ開始時の自然増加）。</summary>
    public void AddMana(int amount)
    {
        Mana?.Add(amount);
    }

    /// <summary>マナを1本使う。使えたら true。</summary>
    public bool SpendMana()
    {
        return Mana != null && Mana.SpendStock();
    }

    /// <summary>気勢を増やす（連続撃破の配分）。</summary>
    public void AddMomentum(int amount)
    {
        Momentum?.Add(amount);
    }

    /// <summary>気勢を1本使う。使えたら true。</summary>
    public bool SpendMomentum()
    {
        return Momentum != null && Momentum.SpendStock();
    }

    /// <summary>再行動を使った印をつける（同じターンに2回は使えない）。</summary>
    public void MarkRefreshUsed()
    {
        UsedRefreshThisTurn = true;
    }

    /// <summary>再行動の「1ターン1回」の印を戻す（自陣営のフェイズ開始時）。</summary>
    public void ResetRefresh()
    {
        UsedRefreshThisTurn = false;
    }

    // ── 敵AIの性格（Phase 17）──
    // StageData の配置ごとに指定され、BattleSetup が SetAIProfile で書き込む（敵のみ）。
    // 待ち伏せ型（Ambush）は IsProvoked が立つまで一切動かない。挑発の判定と
    // MarkProvoked の呼び出しは EnemyAI 側が行う。一度立ったら戻す手段は無い（永続）。

    /// <summary>敵AIの性格。プレイヤーのユニットでは使わない（常に既定の突撃型）。</summary>
    public EnemyAIProfile AIProfile { get; private set; } = EnemyAIProfile.Assault;

    /// <summary>挑発済みか（待ち伏せ型が起動したか）。</summary>
    public bool IsProvoked { get; private set; }

    /// <summary>初期配置時に性格を設定する（BattleSetup が呼ぶ）。</summary>
    public void SetAIProfile(EnemyAIProfile profile)
    {
        AIProfile = profile;
    }

    /// <summary>挑発済みにする（永続。戻す手段は無い）。</summary>
    public void MarkProvoked()
    {
        IsProvoked = true;
    }

    // マップ上の絵のキャンバスの一辺（ピクセル）。試運転用スプライトは全て 32×32。
    private const float MapSpriteCanvasPixels = 32f;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;   // 行動済みで暗くする前の、元の色
    private GridManager grid;  // 戦闘不能時にマスの占有を外すため保持

    /// <summary>
    /// 生成直後に呼んで初期化する。
    /// 能力値のコピー・見た目の作成・盤上への配置・マスへの占有登録・名簿への登録まで行う。
    /// </summary>
    public void Initialize(UnitData data, GridManager grid, Vector2Int cell)
    {
        Data = data;
        GridPosition = cell;
        this.grid = grid;

        // 能力値を UnitData からコピー（ランタイム能力値）
        MaxHP = data.maxHP;
        Strength = data.strength;
        Magic = data.magic;
        Skill = data.skill;
        Speed = data.speed;
        Defense = data.defense;
        Resistance = data.resistance;
        GroundMove = data.EffectiveMove;         // 移動力は兵種データ優先（無ければ旧 move）
        FlightMove = data.EffectiveFlightMove;   // 飛翔中の移動力（未設定なら地上と同じ）
        CurrentHP = MaxHP;

        // ゲージを用意する（フェーズ28）。マナの初期値は技の値そのもので、
        // 30 に達した分は GaugeState 側で1本に変換される（技31 → 1本＋1）
        Mana = new GaugeState(GaugeRules.MaxMana, GaugeRules.MaxManaStock);
        Momentum = new GaugeState(GaugeRules.MaxMomentum, GaugeRules.MaxMomentumStock);
        Mana.Add(GaugeRules.InitialMana(this));
        UsedRefreshThisTurn = false;

        // 所持品をコピーし（ランタイム状態）、一番上の武器を初期装備にする（フェーズ22）。
        // 武器の耐久値も新品（上限）で入るので、マップが変わるたびに資源なしで全回復する（フェーズ31）
        Items.Clear();
        foreach (ItemData item in data.GetInitialItems())
            Items.Add(new ItemSlot(item));
        EquippedWeapon = data.GetInitialWeapon();

        // 初期装備が兵種の武器リストに無ければ警告（実行時側の保険。警告のみで続行）
        if (data.classData != null && EquippedWeapon != null && !data.classData.CanUse(EquippedWeapon.type))
        {
            Debug.LogWarning(
                $"ユニット「{data.unitName}」: 兵種「{data.classData.className}」は " +
                $"武器「{EquippedWeapon.weaponName}」を装備できません（警告のみ・動作は続行）。");
        }

        // 見た目：兵種データに絵があればそれを、無ければ陣営で色分けした四角（自軍=青 / 敵=赤）。
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        Sprite mapSprite = data.classData != null ? data.classData.GetMapSprite(data.faction) : null;
        transform.position = grid.CellToWorld(cell);
        if (mapSprite != null)
        {
            // 絵は無着色。キャンバス（32×32px）が1マスにぴったり収まる大きさにする。
            // Aseprite の取り込みは透明部分を切り詰めるので bounds ではなくキャンバスの寸法で測る
            // （基準点はキャンバス中央＝取り込み設定の Pivot Alignment: Center。マスの中心に絵の中心が来る）
            spriteRenderer.sprite = mapSprite;
            spriteRenderer.color = Color.white;
            float canvasWorldSize = MapSpriteCanvasPixels / mapSprite.pixelsPerUnit;
            transform.localScale = Vector3.one * (grid.cellSize / canvasWorldSize)*0.9f;
        }
        else
        {
            // 四角はグリッドのものを流用し、マスより少し小さく
            spriteRenderer.sprite = grid.SquareSprite;
            spriteRenderer.color = (data.faction == Faction.Player)
                ? new Color(0.30f, 0.50f, 1.00f)   // 青
                : new Color(1.00f, 0.40f, 0.40f);  // 赤
            transform.localScale = Vector3.one * grid.cellSize * 0.7f;
        }
        spriteRenderer.sortingOrder = 1;       // マス(0)より手前に表示
        baseColor = spriteRenderer.color;      // 元の色を覚えておく（行動済みの暗転の基準）
        HasActed = false;

        // このマスの「占有者」として登録（後の移動・戦闘判定で使う）
        TileData tile = grid.GetTile(cell);
        if (tile != null) tile.Occupant = this;

        // 全ユニットの名簿（レジストリ）に登録。勝敗判定・AI・対象探しはこの名簿を使う。
        UnitRegistry.Register(this);

        gameObject.name = $"Unit_{data.unitName}_{cell.x}_{cell.y}";
    }

    /// <summary>
    /// 指定マスへ移動する。元のマスの占有を解除し、移動先を占有する。
    /// 見た目（位置）も移す。
    /// </summary>
    public void MoveTo(GridManager grid, Vector2Int target)
    {
        // 元いたマスの占有を外す
        TileData oldTile = grid.GetTile(GridPosition);
        if (oldTile != null && oldTile.Occupant == this) oldTile.Occupant = null;

        // 新しいマスへ
        GridPosition = target;
        TileData newTile = grid.GetTile(target);
        if (newTile != null) newTile.Occupant = this;

        transform.position = grid.CellToWorld(target);
        gameObject.name = $"Unit_{Data.unitName}_{target.x}_{target.y}";
    }

    /// <summary>
    /// 行動済み状態を設定する。行動済みなら暗く表示し、
    /// フェイズが新しくなったら false に戻して元の色に戻す。
    /// </summary>
    public void SetActed(bool acted)
    {
        HasActed = acted;
        if (spriteRenderer != null)
        {
            // 行動済みは暗く（RGBだけ半分に。透明度は保つ）
            spriteRenderer.color = acted
                ? new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, baseColor.a)
                : baseColor;
        }
    }

    // ===== 救出の実務（格納・解放）=====

    /// <summary>
    /// target を格納する（救出・引き受けの実体）。
    /// target は非アクティブ化され、盤上から消える（占有の明け渡しは OnDisable が行う）。
    /// すでに非アクティブ（引き受けで別のユニットから移ってきた場合）でもそのまま使える。
    /// </summary>
    public void StoreUnit(Unit target)
    {
        Carried.Add(target);
        target.IsCarried = true;
        if (target.gameObject.activeSelf) target.gameObject.SetActive(false);
    }

    /// <summary>
    /// cargo を格納リストから外すだけ（盤上には置かない）。
    /// 引き受けで相手へ渡すときは、この後で相手の StoreUnit を呼ぶ。
    /// </summary>
    public void RemoveCargo(Unit cargo)
    {
        Carried.Remove(cargo);
        cargo.IsCarried = false;
    }

    /// <summary>
    /// cargo を指定マスへ降ろして盤上に戻す（降ろす・代わりに降ろすの実体）。
    /// 先に座標と占有を設定してからアクティブ化する（OnEnable の占有取得と整合する順序）。
    /// </summary>
    public void ReleaseUnitAt(Unit cargo, GridManager grid, Vector2Int cell)
    {
        RemoveCargo(cargo);
        cargo.MoveTo(grid, cell);
        cargo.gameObject.SetActive(true);
    }

    /// <summary>ダメージを受ける。HPが0になったら戦闘不能。</summary>
    public void TakeDamage(int amount)
    {
        CurrentHP -= amount;
        if (CurrentHP < 0) CurrentHP = 0;
        if (CurrentHP == 0) Die();
    }

    /// <summary>
    /// HPを回復する（最大HPを超えない。0以下の量は無視）。TakeDamage の対。
    /// 道具（傷薬・フェーズ24）や杖（Phase 25 予定）の回復の共通の土台。
    /// </summary>
    public void Heal(int amount)
    {
        if (amount <= 0) return;
        CurrentHP += amount;
        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
    }

    /// <summary>戦闘不能：マスの占有を外し、名簿からも外して盤上から消える。</summary>
    private void Die()
    {
        if (grid != null)
        {
            TileData tile = grid.GetTile(GridPosition);
            if (tile != null && tile.Occupant == this) tile.Occupant = null;
        }

        // 貨物は全員生存し、死亡マス → 隣接 → さらに外側の最寄り空きマスの順に降ろされる
        // （Phase 11 合意(a) + Phase 12 合意(b)）。自分の占有を外した後なので、死亡マスは空いている。
        // 置き場は貨物ごとの兵種で判定する（騎乗の貨物は山に置けない等。Phase 15）
        if (IsRescuing && grid != null)
        {
            foreach (Unit cargo in new List<Unit>(Carried))
            {
                Vector2Int? cell = RescueRules.FindReleaseCell(GridPosition, cargo, grid);
                if (cell == null) continue; // 置き場が無い（下の警告でまとめて知らせる）

                ReleaseUnitAt(cargo, grid, cell.Value);
                Debug.Log($"{Data.unitName} が倒れ、{cargo.Data.unitName} は {cell.Value} に降ろされた");
            }

            // 盤面が埋まり尽くして置き場が無い場合の保険（通常は起こらない）
            if (IsRescuing)
                Debug.LogWarning($"{Data.unitName} の貨物 {Carried.Count} 体は空きマスが無く降ろせなかった");
        }

        UnitRegistry.NotifyDeath(this); // 味方輸送隊の死亡＝敗北（Phase 12）を勝敗判定へ伝える
        UnitRegistry.Unregister(this);
        Debug.Log($"{Data.unitName} は戦闘不能になった");
        Destroy(gameObject);
    }

    // ── 非アクティブ化とマス占有の整合 ──
    // ユニットが非アクティブ化されて盤上から見えなくなるとき（動作確認テストや、
    // 将来の救出での「格納」）、マスの占有が残ると「見えない壁」ができて
    // 移動範囲がおかしくなる。そこで無効化で占有を明け渡し、有効化で取り直す。

    void OnEnable()
    {
        if (grid == null) return; // 生成直後（Initialize 前）はまだ盤面を知らない

        TileData tile = grid.GetTile(GridPosition);
        if (tile == null) return;

        if (tile.Occupant == null)
        {
            tile.Occupant = this;
        }
        else if (tile.Occupant != this)
        {
            Debug.LogWarning($"{Data.unitName} を再アクティブ化しましたが、マス {GridPosition} は先に {tile.Occupant.Data.unitName} が占有しています。");
        }
    }

    void OnDisable()
    {
        if (grid == null) return;

        TileData tile = grid.GetTile(GridPosition);
        if (tile != null && tile.Occupant == this) tile.Occupant = null;
    }

    // 何らかの理由で破棄されたときの保険（名簿から二重に外しても害はない）
    void OnDestroy()
    {
        UnitRegistry.Unregister(this);
    }

    // ===== 盤上の HP ゲージ（2026-10-10 作者仕様）=====
    // ユニットのいるマスの最下部に、残りHPの割合を横棒で出す（味方=青／敵=赤）。
    // HP の数字は出さない（以前はユニットの絵の上に数字を出していたが廃止）。
    private const float HpBarWidthRatio = 0.85f;    // マスの幅に対するゲージの長さ
    private const float HpBarHeightRatio = 0.10f;   // マスの高さに対するゲージの太さ
    private const float HpBarMinHeight = 3f;        // 小さい画面でも見える最低の太さ（ピクセル）
    private static readonly Color HpBarPlayerColor = new Color(0.30f, 0.55f, 1.00f);  // 青
    private static readonly Color HpBarEnemyColor = new Color(1.00f, 0.30f, 0.30f);   // 赤
    private static readonly Color HpBarBackColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);

    // 各ユニットの HP ゲージとマナ・気勢のリングを描く（簡易・Canvas不要）。
    // 「救」「飛x2」の印も以前はここで絵の上に出していたが、ユニット情報パネルの状態枠
    // （飛翔の残りターン・救出中の相手の名前）で確認できるので廃止した（2026-10-10 作者合意）。
    void OnGUI()
    {
        if (Camera.main == null || grid == null) return;
        if (Camera.main.WorldToScreenPoint(transform.position).z <= 0) return; // カメラの後ろなら描かない

        // HP ゲージ：マスの画面上の矩形（GUI 座標）の最下部に置く。
        // 見えているマスは 0.95 倍（GridManager.CreateCellVisual の隙間）なので、その内側に収める
        Rect cell = grid.GetCellsScreenRect(GridPosition, GridPosition);
        float inset = cell.height * 0.025f;
        float barHeight = Mathf.Max(HpBarMinHeight, Mathf.Round(cell.height * HpBarHeightRatio));
        float barWidth = Mathf.Round(cell.width * HpBarWidthRatio);
        var bar = new Rect(Mathf.Round(cell.center.x - barWidth * 0.5f),
                           Mathf.Round(cell.yMax - inset - barHeight - 1f),
                           barWidth, barHeight);
        float ratio = MaxHP > 0 ? Mathf.Clamp01((float)CurrentHP / MaxHP) : 0f;
        Color fill = Data != null && Data.faction == Faction.Player ? HpBarPlayerColor : HpBarEnemyColor;

        GaugeBars.FillRect(bar, HpBarBackColor);
        if (ratio > 0f)
            GaugeBars.FillRect(new Rect(bar.x, bar.y, Mathf.Max(1f, Mathf.Round(bar.width * ratio)), bar.height), fill);

        // マナ（左上隅）・気勢（右上隅）のリング状ゲージとストック数（2026-10-10 作者仕様）
        GaugeRings.Draw(cell, inset, this);
    }
}
