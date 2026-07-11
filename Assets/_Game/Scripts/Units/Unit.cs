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
    public int Move { get; private set; }         // 移動力

    /// <summary>陣営（UnitData から取得）。</summary>
    public Faction Faction => Data.faction;

    /// <summary>兵種（UnitData から取得）。</summary>
    public UnitClass Class => Data.unitClass;

    /// <summary>装備中の武器。武器はこの窓口から読む（Data.weapon の直読みはしない約束）。</summary>
    public WeaponData Weapon => Data.weapon;

    /// <summary>このターンに行動済みか（移動・攻撃を終えたか）。</summary>
    public bool HasActed { get; private set; }

    /// <summary>まだ生存しているか。</summary>
    public bool IsAlive => CurrentHP > 0;

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

    /// <summary>格納できる数。輸送隊=4（Phase 12）、他の騎乗=1、歩兵=0（救出できない）。</summary>
    public int CarryCapacity =>
        Class == UnitClass.Transporter ? 4 : (Class.IsMounted() ? 1 : 0);

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
        Move = data.move;
        CurrentHP = MaxHP;

        // 見た目：陣営で色分け（自軍=青 / 敵=赤）。スプライトはグリッドのものを流用。
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = grid.SquareSprite;
        spriteRenderer.color = (data.faction == Faction.Player)
            ? new Color(0.30f, 0.50f, 1.00f)   // 青
            : new Color(1.00f, 0.40f, 0.40f);  // 赤
        spriteRenderer.sortingOrder = 1;       // マス(0)より手前に表示
        baseColor = spriteRenderer.color;      // 元の色を覚えておく
        HasActed = false;

        // 盤上の位置とサイズ（マスより少し小さく）
        transform.position = grid.CellToWorld(cell);
        transform.localScale = Vector3.one * grid.cellSize * 0.7f;

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
        if (IsRescuing && grid != null)
        {
            List<Vector2Int> cells = RescueRules.FindReleaseCells(GridPosition, Carried.Count, grid);
            foreach (Vector2Int cell in cells)
            {
                Unit cargo = Carried[0];
                ReleaseUnitAt(cargo, grid, cell);
                Debug.Log($"{Data.unitName} が倒れ、{cargo.Data.unitName} は {cell} に降ろされた");
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

    // 各ユニットの頭上あたりに現在HPを表示する（簡易・Canvas不要）。
    void OnGUI()
    {
        if (Camera.main == null) return;

        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position);
        if (sp.z <= 0) return; // カメラの後ろなら描かない

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;

        // GUI座標は左上原点・Y下向きなので、スクリーンYを反転する
        // 救出中は HP の横に「救」を添える（例: 「20 救」。複数格納中は「救x2」のように数も出す）
        string label = CurrentHP.ToString();
        if (IsRescuing)
            label += Carried.Count > 1 ? $" 救x{Carried.Count}" : " 救";
        var rect = new Rect(sp.x - 30, Screen.height - sp.y - 12, 60, 22);
        GUI.Label(rect, label, style);
    }
}
