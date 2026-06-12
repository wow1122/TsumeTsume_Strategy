using UnityEngine;

/// <summary>
/// マス目マップを管理する「盤面」スクリプト。
///  ・指定した幅×高さのマスを「仮素材（色付きの四角）」で生成する
///  ・ワールド座標 ⇔ グリッド座標 の変換を行う
///  ・マスの色（ハイライト）を変える道具を提供する
/// クリックや選択の操作は BattleController が担当します（役割分担）。
/// グリッドの中心が、この GameObject の位置に来るように配置されます。
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("グリッドの大きさ")]
    public int width = 10;          // 横のマス数
    public int height = 10;         // 縦のマス数
    public float cellSize = 1f;     // 1マスの大きさ（ワールド単位）

    [Header("見た目（仮素材）")]
    [Tooltip("市松模様の明るい方の色")]
    public Color colorLight = new Color(0.85f, 0.85f, 0.85f);
    [Tooltip("市松模様の暗い方の色")]
    public Color colorDark = new Color(0.70f, 0.70f, 0.70f);

    // ── 内部データ ──
    private TileData[,] tiles;                 // 各マスの論理データ
    private SpriteRenderer[,] cellRenderers;   // 各マスの見た目（色を変える用）
    private Sprite squareSprite;               // コードで生成する白い四角スプライト
    private Vector3 originWorld;               // マス(0,0)の中心のワールド座標

    /// <summary>ユニットなど他スクリプトが流用できる共通の四角スプライト。</summary>
    public Sprite SquareSprite => squareSprite;

    // グリッドは Awake で作る。こうすると、他のスクリプトが Start で
    // ユニットを配置するときには、すでに盤面が完成している（順序の保証）。
    void Awake()
    {
        squareSprite = CreateSquareSprite();
        BuildGrid();
    }

    // ===== グリッド生成 =====

    private void BuildGrid()
    {
        tiles = new TileData[width, height];
        cellRenderers = new SpriteRenderer[width, height];

        // グリッドの中心がこの GameObject の位置に来るよう、左下マスの基準点を計算
        originWorld = transform.position
            - new Vector3((width - 1) * cellSize * 0.5f,
                          (height - 1) * cellSize * 0.5f,
                          0f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                tiles[x, y] = new TileData(new Vector2Int(x, y));
                cellRenderers[x, y] = CreateCellVisual(x, y);
            }
        }
    }

    /// <summary>1マス分の見た目（SpriteRenderer 付き GameObject）を作る。</summary>
    private SpriteRenderer CreateCellVisual(int x, int y)
    {
        var cell = new GameObject($"Cell_{x}_{y}");
        cell.transform.SetParent(transform);
        cell.transform.position = CellToWorld(new Vector2Int(x, y));
        // マスの間にわずかな隙間を作り、グリッドの線が見えるようにする
        cell.transform.localScale = Vector3.one * cellSize * 0.95f;

        var sr = cell.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = GetDefaultColor(x, y);
        return sr;
    }

    /// <summary>市松模様になるよう、マスの基本色を返す。</summary>
    private Color GetDefaultColor(int x, int y)
    {
        return ((x + y) % 2 == 0) ? colorLight : colorDark;
    }

    // ===== 座標変換・問い合わせ =====

    /// <summary>グリッド座標 → ワールド座標（マスの中心）。</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return originWorld + new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);
    }

    /// <summary>ワールド座標 → グリッド座標。範囲内なら true。</summary>
    public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
    {
        Vector3 local = world - originWorld;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.y / cellSize);
        cell = new Vector2Int(x, y);
        return IsInside(cell);
    }

    /// <summary>そのグリッド座標が盤面の中に収まっているか。</summary>
    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    /// <summary>指定マスの論理データを取得（範囲外なら null）。</summary>
    public TileData GetTile(Vector2Int cell)
    {
        return IsInside(cell) ? tiles[cell.x, cell.y] : null;
    }

    // ===== ハイライト（色変更）の道具 =====

    /// <summary>全マスの色を、元の市松模様に戻す（ハイライトを消す）。</summary>
    public void ResetAllHighlights()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cellRenderers[x, y].color = GetDefaultColor(x, y);
    }

    /// <summary>指定マスを好きな色に塗る（ハイライト表示）。</summary>
    public void SetHighlight(Vector2Int cell, Color color)
    {
        if (IsInside(cell))
            cellRenderers[cell.x, cell.y].color = color;
    }

    // ===== 仮素材スプライトの生成 =====

    /// <summary>
    /// コード上で 1×1 の白い四角スプライトを作る。
    /// これで外部の画像ファイルを用意しなくても四角マスを描けます（仮素材）。
    /// </summary>
    private Sprite CreateSquareSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        // pixelsPerUnit = 1 にすることで、1×1テクスチャ＝1ワールド単位の四角になる
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
