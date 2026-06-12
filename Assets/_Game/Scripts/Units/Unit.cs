using UnityEngine;

/// <summary>
/// 盤上に存在するユニット1体。能力値(UnitData)を参照し、
/// 自分のグリッド座標・現在HPを持ち、仮素材(色付き四角)で表示する。
/// 自軍は青、敵軍は赤で表示する。
/// </summary>
public class Unit : MonoBehaviour
{
    public UnitData Data { get; private set; }
    public Vector2Int GridPosition { get; private set; }
    public int CurrentHP { get; private set; }

    /// <summary>陣営（UnitData から取得）。</summary>
    public Faction Faction => Data.faction;

    private SpriteRenderer spriteRenderer;

    /// <summary>
    /// 生成直後に呼んで初期化する。
    /// 見た目の作成・盤上への配置・マスへの占有登録まで行う。
    /// </summary>
    public void Initialize(UnitData data, GridManager grid, Vector2Int cell)
    {
        Data = data;
        CurrentHP = data.maxHP;
        GridPosition = cell;

        // 見た目：陣営で色分け（自軍=青 / 敵=赤）。スプライトはグリッドのものを流用。
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = grid.SquareSprite;
        spriteRenderer.color = (data.faction == Faction.Player)
            ? new Color(0.30f, 0.50f, 1.00f)   // 青
            : new Color(1.00f, 0.40f, 0.40f);  // 赤
        spriteRenderer.sortingOrder = 1;       // マス(0)より手前に表示

        // 盤上の位置とサイズ（マスより少し小さく）
        transform.position = grid.CellToWorld(cell);
        transform.localScale = Vector3.one * grid.cellSize * 0.7f;

        // このマスの「占有者」として登録（後の移動・戦闘判定で使う）
        TileData tile = grid.GetTile(cell);
        if (tile != null) tile.Occupant = this;

        gameObject.name = $"Unit_{data.unitName}_{cell.x}_{cell.y}";
    }
}
