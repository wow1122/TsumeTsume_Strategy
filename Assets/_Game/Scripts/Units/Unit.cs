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
        var rect = new Rect(sp.x - 25, Screen.height - sp.y - 12, 50, 22);
        GUI.Label(rect, CurrentHP.ToString(), style);
    }
}
