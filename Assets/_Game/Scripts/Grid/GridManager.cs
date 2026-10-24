using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// マスのハイライトの「種類」。1マスに複数の種類を重ねられ（多層管理）、
/// 表示は優先度の高い順（Selection > TargetChoice > AttackRange > HealRange > MoveRange >
/// 基本色）。ある種類だけを消すと、下に重なっていた種類の色が現れる。
/// ※フェーズ32の補給圏（SupplyZone・薄紫）は 2026-10-24 に補給ごと撤廃した。
/// </summary>
[System.Flags]
public enum HighlightKind
{
    None = 0,
    MoveRange = 1 << 0,    // 移動できるマス（水色）
    AttackRange = 1 << 1,  // 攻撃が届くマス（橙。将来の射程表示用）
    TargetChoice = 1 << 2, // 攻撃対象の候補（赤）
    Selection = 1 << 3,    // 選択中ユニットのマス（黄）
    HealRange = 1 << 4,    // 杖が届くマス・回復対象のマス（緑・Phase 25）
}

/// <summary>
/// マス目マップを管理する「盤面」スクリプト。
///  ・指定した幅×高さのマスを「仮素材（色付きの四角）」で生成する
///    （大きさは StageData に指定があればそちらを優先。Phase 15 からステージごとに可変）
///  ・カメラを盤面全体が収まる位置・表示範囲に自動調整する（Phase 15）
///  ・ワールド座標 ⇔ グリッド座標 の変換を行う
///  ・マスの色（ハイライト）を変える道具を提供する
/// クリックや選択の操作は BattleController が担当します（役割分担）。
/// グリッドの中心が、この GameObject の位置に来るように配置されます。
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("グリッドの大きさ（StageData に指定があればそちらを優先）")]
    public int width = 10;          // 横のマス数（ステージ側の指定が無いときの既定値）
    public int height = 10;         // 縦のマス数（ステージ側の指定が無いときの既定値）
    public float cellSize = 1f;     // 1マスの大きさ（ワールド単位）

    [Header("カメラ")]
    [Tooltip("カメラを盤面に合わせるときの、盤面の外側に足す余白（マス数）")]
    public float cameraMargin = 1f;

    [Tooltip("盤面を映す画面の横の割合。残りの右側はコマンド一覧・各種パネルの置き場になる（2026-08-15）")]
    [Range(0.3f, 1f)]
    // 2026-08-16：作者の指示で盤面をさらに左へ寄せるため 0.65 → 0.58。
    // 高さ側で大きさが決まっているステージでは盤面は縮まず、位置だけ左へ動く
    public float boardAreaWidth = 0.58f;

    [Tooltip("盤面の下に空ける画面の縦の割合。ここに地形情報の帯を出す（2026-08-15。" +
             "帯の高さは約56pxなので、狭くしすぎると帯が画面からはみ出す）")]
    [Range(0f, 0.3f)]
    public float boardBottomArea = 0.10f;

    [Header("見た目（仮素材）")]
    [Tooltip("市松模様の暗い方のマスに掛ける明るさ（地形色 × この値。1で市松なし）")]
    [Range(0.5f, 1f)]
    public float checkerDarken = 0.85f;
    [Tooltip("ハイライト色を地形色にどれだけ混ぜるか（1で地形色を完全に上書き）")]
    [Range(0f, 1f)]
    public float highlightBlend = 0.7f;

    [Header("ハイライト色")]
    [Tooltip("選択中ユニットのマス")]
    public Color selectionColor = new Color(1f, 0.9f, 0.4f);      // 黄
    [Tooltip("攻撃対象の候補のマス")]
    public Color targetChoiceColor = new Color(1f, 0.45f, 0.45f); // 赤
    [Tooltip("攻撃が届くマス（移動範囲の外側の、武器で届く範囲）")]
    public Color attackRangeColor = new Color(1f, 0.5f, 0.5f);    // 赤
    [Tooltip("移動できるマス")]
    public Color moveRangeColor = new Color(0.5f, 0.8f, 1f);      // 水色
    [Tooltip("杖が届くマス・回復対象のマス（Phase 25）")]
    public Color healRangeColor = new Color(0.5f, 0.9f, 0.5f);    // 緑

    // ── 内部データ ──
    private TileData[,] tiles;                 // 各マスの論理データ
    private SpriteRenderer[,] cellRenderers;   // 各マスの見た目（色を変える用）
    private HighlightKind[,] highlights;       // 各マスに重なっているハイライトの種類
    private Sprite squareSprite;               // コードで生成する白い四角スプライト
    private Vector3 originWorld;               // マス(0,0)の中心のワールド座標

    /// <summary>ユニットなど他スクリプトが流用できる共通の四角スプライト。</summary>
    public Sprite SquareSprite => squareSprite;

    // グリッドは Awake で作る。こうすると、他のスクリプトが Start で
    // ユニットを配置するときには、すでに盤面が完成している（順序の保証）。
    void Awake()
    {
        ApplyStageSize();
        squareSprite = CreateSquareSprite();
        BuildGrid();
        FitCamera();
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    // 画面サイズ（縦横比）が変わったらカメラを合わせ直す。
    // 盤面を「画面の左◯％」に収める計算が縦横比に依存するようになったため（2026-08-15）
    private int lastScreenWidth;
    private int lastScreenHeight;

    void Update()
    {
        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        FitCamera();
    }

    /// <summary>
    /// ステージ（StageData）に盤面サイズの指定があれば width / height を上書きする（Phase 15）。
    /// BattleSetup が参照しているステージを見る。指定が 0 以下なら Inspector の値のまま。
    /// </summary>
    private void ApplyStageSize()
    {
        BattleSetup setup = FindAnyObjectByType<BattleSetup>();
        if (setup == null || setup.stage == null) return;

        if (setup.stage.gridWidth > 0) width = setup.stage.gridWidth;
        if (setup.stage.gridHeight > 0) height = setup.stage.gridHeight;
    }

    /// <summary>
    /// メインカメラを、盤面が「画面の左寄り・下に帯ぶんを空けた領域」にちょうど収まるように合わせる。
    /// 盤面サイズがステージごとに変わっても、シーンのカメラを手で直す必要がない（Phase 15）。
    ///
    /// 2026-08-15：画面いっぱいではなく、左 boardAreaWidth ぶん・下から boardBottomArea を除いた
    /// 矩形に収めるようにした。空いた右側はコマンド一覧・戦闘予測などのパネル置き場。
    /// 空いた下側はもともと地形情報の帯だったが、2026-08-16 にカーソル追従へ変えたので今は余白。
    /// カメラの描画範囲（cam.rect）は変えない＝領域外が塗られずゴミが残るのを避けるため、
    /// 表示倍率（orthographicSize）と位置だけで寄せている。
    /// </summary>
    private void FitCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        // 盤面（＋外側の余白）の半分の大きさ
        float halfBoardH = height * cellSize * 0.5f + cameraMargin * cellSize;
        float halfBoardW = width * cellSize * 0.5f + cameraMargin * cellSize;

        // 盤面を映す領域の大きさ（画面に対する割合）
        float w = Mathf.Clamp(boardAreaWidth, 0.3f, 1f);
        float h = Mathf.Clamp(1f - boardBottomArea, 0.3f, 1f);

        // orthographicSize は「画面の縦半分に映るワールドの長さ」。
        // 盤面の縦が領域の縦に収まる大きさと、横が収まる大きさの大きい方にする
        float size = Mathf.Max(halfBoardH / h, halfBoardW / (cam.aspect * w));
        cam.orthographicSize = size;

        // カメラの中心＝画面の中心なので、「領域の中心」との差だけカメラをずらすと
        // 盤面の中心が領域の中心に来る（ビューポート座標：左下(0,0)〜右上(1,1)）
        float viewX = w * 0.5f;
        float viewY = boardBottomArea + h * 0.5f;
        float dx = (viewX - 0.5f) * 2f * size * cam.aspect;
        float dy = (viewY - 0.5f) * 2f * size;

        Vector3 pos = cam.transform.position; // Z はシーンの値のまま（盤面より手前）
        cam.transform.position = new Vector3(transform.position.x - dx,
                                             transform.position.y - dy,
                                             pos.z);
    }

    // ===== グリッド生成 =====

    private void BuildGrid()
    {
        tiles = new TileData[width, height];
        cellRenderers = new SpriteRenderer[width, height];
        highlights = new HighlightKind[width, height];

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

    // 地形が未設定（ApplyTerrain 前や記号不明）のときに使う平地相当の色
    private static readonly Color FallbackColor = new Color(0.85f, 0.85f, 0.85f);

    /// <summary>マスの基本色（地形色 × 市松模様の明暗）を返す。</summary>
    private Color GetDefaultColor(int x, int y)
    {
        TerrainDef terrain = tiles[x, y].Terrain;
        Color baseColor = terrain != null ? terrain.color : FallbackColor;
        if ((x + y) % 2 == 0) return baseColor;

        // 暗い方のマスは地形色を少し暗くする（市松模様で座標が読みやすくなる）
        Color dark = baseColor * checkerDarken;
        dark.a = 1f;
        return dark;
    }

    // ===== 地形の適用（Phase 13） =====

    /// <summary>
    /// StageData の文字マップ（terrainRows）を読んで、各マスに地形を割り当てて色を塗る。
    /// BattleSetup が Start で呼ぶ（グリッドは Awake で生成済みなので順序は安全）。
    /// 行数・文字数の不一致や未知の記号は警告を出して「平地扱い」にする。
    /// </summary>
    public void ApplyTerrain(StageData stage)
    {
        if (stage == null || stage.terrainTable == null)
        {
            Debug.LogWarning("GridManager: StageData か TerrainTable が未設定のため、全マスを平地扱いにします。");
            return;
        }

        TerrainTable table = stage.terrainTable;

        if (stage.terrainRows == null || stage.terrainRows.Count != height)
        {
            int rows = stage.terrainRows != null ? stage.terrainRows.Count : 0;
            Debug.LogWarning($"GridManager: terrainRows の行数({rows})が盤面の高さ({height})と違います。全マスを平地扱いにします。");
            return;
        }

        for (int y = 0; y < height; y++)
        {
            // リストの先頭が「盤面の一番上の行」なので、y 座標（下が0）とは上下逆になる
            string row = stage.terrainRows[height - 1 - y];

            if (row == null || row.Length != width)
            {
                Debug.LogWarning($"GridManager: terrainRows の {height - 1 - y} 行目の文字数が盤面の幅({width})と違います。この行は平地扱いにします。");
                for (int x = 0; x < width; x++) tiles[x, y].Terrain = table.DefaultTerrain;
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                TerrainDef def = table.FindBySymbol(row[x]);
                if (def == null)
                {
                    Debug.LogWarning($"GridManager: マス({x},{y}) の記号 '{row[x]}' が TerrainTable にありません。平地扱いにします。");
                    def = table.DefaultTerrain;
                }
                tiles[x, y].Terrain = def;
            }
        }

        // 地形色で全マスを塗り直す
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                RepaintCell(x, y);
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

    /// <summary>指定マスに指定種類のハイライトを重ねる。</summary>
    public void AddHighlight(Vector2Int cell, HighlightKind kind)
    {
        if (!IsInside(cell)) return;
        highlights[cell.x, cell.y] |= kind;
        RepaintCell(cell.x, cell.y);
    }

    /// <summary>指定マスから指定種類のハイライトだけを外す（他の種類は残る）。</summary>
    public void RemoveHighlight(Vector2Int cell, HighlightKind kind)
    {
        if (!IsInside(cell)) return;
        highlights[cell.x, cell.y] &= ~kind;
        RepaintCell(cell.x, cell.y);
    }

    /// <summary>盤面全体から指定種類のハイライトだけを消す（例：移動範囲だけ消す）。</summary>
    public void ClearHighlights(HighlightKind kind)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                highlights[x, y] &= ~kind;
                RepaintCell(x, y);
            }
        }
    }

    /// <summary>全マスの全ハイライトを消して、元の市松模様に戻す。</summary>
    public void ResetAllHighlights()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                highlights[x, y] = HighlightKind.None;
                RepaintCell(x, y);
            }
        }
    }

    /// <summary>そのマスの表示色を、重なっているハイライトの優先度に従って塗り直す。</summary>
    private void RepaintCell(int x, int y)
    {
        cellRenderers[x, y].color = ResolveColor(x, y);
    }

    /// <summary>
    /// 重なっているハイライトのうち最も優先度の高い色を、地形色に混ぜて返す（無ければ地形色そのまま）。
    /// 完全な上書きではなく Color.Lerp で混ぜることで、ハイライト中でも下の地形が判別できる。
    /// </summary>
    private Color ResolveColor(int x, int y)
    {
        Color baseColor = GetDefaultColor(x, y);

        HighlightKind kinds = highlights[x, y];
        Color highlightColor;
        if ((kinds & HighlightKind.Selection) != 0) highlightColor = selectionColor;
        else if ((kinds & HighlightKind.TargetChoice) != 0) highlightColor = targetChoiceColor;
        else if ((kinds & HighlightKind.AttackRange) != 0) highlightColor = attackRangeColor;
        else if ((kinds & HighlightKind.HealRange) != 0) highlightColor = healRangeColor;
        else if ((kinds & HighlightKind.MoveRange) != 0) highlightColor = moveRangeColor;
        else return baseColor;

        return Color.Lerp(baseColor, highlightColor, highlightBlend);
    }

    // ===== カーソル下のマスの地形情報と座標（盤面の下の帯。2026-08-17 作者仕様） =====

    /// <summary>地形情報の帯の内側の余白。</summary>
    private const float TerrainInfoPadding = 8f;

    /// <summary>地形情報の帯の文字1行の高さ（帯はこの1行ぶん＋余白の厚み）。</summary>
    private const float TerrainInfoLineHeight = 20f;

    /// <summary>盤面の下端と帯のあいだに空ける隙間。</summary>
    private const float TerrainInfoGap = 8f;

    /// <summary>
    /// 帯の右端に空ける座標のぶんの幅（2桁の「(13,11)」＝半角7文字ぶん＋地形名との隙間）。
    /// 桁数で変えず固定にしてあるのは、カーソルを動かすたびに地形名に使える幅が
    /// 変わらないようにするため。
    /// </summary>
    private const float TerrainInfoCoordWidth = 60f;

    /// <summary>帯の文字の大きさ。</summary>
    private const int TerrainInfoFontSize = 14;

    /// <summary>
    /// 直近に描いた地形情報の帯（GUI 座標。描かなかったフレームは Rect.zero）。
    /// 他のUIがこの帯を避けるために公開している
    /// （2026-08-17。BattleForecast.PanelRect と同じ「描いた矩形を公開する」作法）。
    /// </summary>
    public Rect TerrainInfoRect { get; private set; }

    /// <summary>
    /// カーソルが指しているマスの地形を、**盤面のすぐ下の帯**に1行で出す。
    /// 左に 地形名・移動コスト（騎兵との差があれば併記）・地形防御、**右端に座標**。
    ///
    /// 帯は**盤面の真下に固定**で、大きさも一定（**横幅は盤面と同じ**・高さは1行）。
    /// カーソルでも他のUIでも動かない。盤面いっぱいの幅があるので、
    /// 一番長い「山　移動コスト 3（騎兵は通行不可）　地形防御 +2」でも切れない。
    /// 盤上のメニュー（行動メニュー・コマンド一覧・持ち物・貨物）は UI/MenuPlacement によって
    /// 盤面の中に収まるので、帯と重なることはない（2026-08-17 作者指示）。
    ///
    /// 経緯: 2026-08-15 は盤面の真下の帯 → 08-16 カーソル追従 → 08-17 盤上の4隅
    /// → 位置が入れ替わりすぎるという作者の指摘で帯へ戻した。
    /// </summary>
    void OnGUI()
    {
        TerrainInfoRect = new Rect(); // 描かなかったときは「避ける相手は無い」扱いに戻す

        if (tiles == null || Mouse.current == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(mousePos);
        if (!TryWorldToCell(world, out Vector2Int cell)) return;

        Rect board = GetBoardScreenRect();
        if (board.width <= 0f) return;

        float boxWidth = board.width; // 盤面と同じ横幅（2026-08-17 作者指示）
        float boxHeight = TerrainInfoLineHeight + TerrainInfoPadding * 2f;
        float boxY = Mathf.Min(board.yMax + TerrainInfoGap, Mathf.Max(0f, Screen.height - boxHeight));

        var rect = new Rect(board.x, boxY, boxWidth, boxHeight);
        TerrainInfoRect = rect;
        GUI.Box(rect, GUIContent.none);

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = TerrainInfoFontSize,
        };
        float innerX = rect.x + TerrainInfoPadding;
        float innerY = rect.y + TerrainInfoPadding;
        float innerWidth = boxWidth - TerrainInfoPadding * 2f;
        float textWidth = Mathf.Max(0f, innerWidth - TerrainInfoCoordWidth);

        // 盤面と同じ幅があるので普通は切れないが、ウィンドウが極端に小さいときの保険として
        // 入りきらない説明は「…」で丸める（半角1文字ぶん＝文字の大きさの半分で数える）
        int limit = Mathf.Max(1, Mathf.FloorToInt(textWidth / (TerrainInfoFontSize * 0.5f)));
        string info = UnitInfoLines.Truncate(BuildTerrainInfo(GetTile(cell)), limit);

        // 地形の説明は左詰め、座標は帯の右端に右詰め（2026-08-17 の作者指示）
        GUI.Label(new Rect(innerX, innerY, textWidth, TerrainInfoLineHeight), info, style);

        var coordStyle = new GUIStyle(style) { alignment = TextAnchor.MiddleRight };
        GUI.Label(new Rect(innerX, innerY, innerWidth, TerrainInfoLineHeight),
                  $"({cell.x},{cell.y})", coordStyle);
    }

    /// <summary>
    /// 盤面が画面のどこに映っているかを GUI 座標（左上原点・Y は下向き）で返す。
    /// 地形情報の枠を盤上の4隅に置く（＋カーソルがどの四分円にいるかを測る）のと、
    /// 戦闘予測パネルを盤面の隅に置くのに使う（BattleForecast が読む）。
    /// カメラが無いときは Rect.zero（呼び出し側は width で判定する）。
    /// </summary>
    public Rect GetBoardScreenRect() =>
        GetCellsScreenRect(new Vector2Int(0, 0), new Vector2Int(width - 1, height - 1));

    /// <summary>
    /// min〜max のマスの範囲（両端を含む）が画面のどこに映っているかを GUI 座標で返す。
    /// 戦闘予測パネルが「攻撃するユニットと選べる相手が乗っている範囲」を避けるのに使う
    /// （2026-08-17）。カメラが無いときは Rect.zero。
    /// </summary>
    public Rect GetCellsScreenRect(Vector2Int min, Vector2Int max)
    {
        Camera cam = Camera.main;
        if (cam == null) return new Rect();

        // 左下マスの左下角と、右上マスの右上角をスクリーン座標へ
        float half = cellSize * 0.5f;
        Vector3 bottomLeft = CellToWorld(min) - new Vector3(half, half, 0f);
        Vector3 topRight = CellToWorld(max) + new Vector3(half, half, 0f);

        Vector3 a = cam.WorldToScreenPoint(bottomLeft);
        Vector3 b = cam.WorldToScreenPoint(topRight);

        // スクリーン座標は左下原点・Y 上向きなので、GUI 座標へ反転する
        float top = Screen.height - b.y;
        float bottom = Screen.height - a.y;
        return new Rect(a.x, top, b.x - a.x, bottom - top);
    }

    /// <summary>
    /// 帯に出す地形情報を1行で作る（Phase 15 の形に戻したもの）。
    /// 地形名・移動コスト（騎兵との差があれば括弧で併記）・地形防御。
    /// 地上のユニットが立てない壁・城壁は地形防御を出さない（城壁に立てる飛行兵は地形防御を受けない。2026-10-24）。
    /// 飛行兵はどの地形もコスト1なので、ここには書かない。座標は出さない（2026-08-17 作者指示）。
    /// </summary>
    private static string BuildTerrainInfo(TileData tile)
    {
        string name = tile.Terrain != null ? tile.Terrain.displayName : "平地";

        if (!tile.IsWalkable)
        {
            return tile.CanFlyOver
                ? $"{name}　通行不可（飛行兵は可）"
                : $"{name}　通行不可";
        }

        // 騎兵（輸送隊も）だけ通れない・コストが違う地形は、その差も表示する
        string cost;
        if (!tile.IsWalkableFor(UnitClass.Cavalry))
            cost = $"移動コスト {tile.MoveCost}（騎兵は通行不可）";
        else if (tile.MoveCostFor(UnitClass.Cavalry) != tile.MoveCost)
            cost = $"移動コスト {tile.MoveCost}（騎兵 {tile.MoveCostFor(UnitClass.Cavalry)}）";
        else
            cost = $"移動コスト {tile.MoveCost}";

        return $"{name}　{cost}　地形防御 +{tile.DefenseBonus}";
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
