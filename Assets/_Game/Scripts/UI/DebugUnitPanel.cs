using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// デバッグ・テスト用のユニット情報パネル（IMGUI・画面右上）。
/// 盤上のユニット（敵味方どちらでも）をクリックすると、能力値・武器・状態を表示する。
/// 空きマスをクリックすると閉じる。盤外クリックでは表示を保つ。
/// BattleController の操作（選択・メニュー）とは独立に自分でクリックを読むだけなので、
/// 敵フェイズ中やメニュー表示中でも確認できる。ゲームの状態は一切変えない。
/// あくまで開発中の確認用 — 本番のユニットUIは素材差し替え時期に uGUI でまとめて作る方針。
/// BattleController が自動生成する（シーンへの追加作業は不要）。
/// </summary>
public class DebugUnitPanel : MonoBehaviour
{
    [Tooltip("パネルを表示するか（デバッグ用UIなので、邪魔なときはここでOFF）")]
    public bool panelEnabled = true;

    [Header("見た目")]
    [Tooltip("パネルの幅（ピクセル）")]
    public float panelWidth = 340f;
    [Tooltip("1行の高さ（ピクセル）")]
    public float lineHeight = 24f;
    [Tooltip("パネルの枠と文字の間の余白（ピクセル）")]
    public float padding = 10f;
    [Tooltip("文字の大きさ")]
    public int fontSize = 14;

    private GridManager grid;
    private Unit unit; // いま表示しているユニット（null = 非表示）

    void Start()
    {
        grid = FindAnyObjectByType<GridManager>();
    }

    void Update()
    {
        if (grid == null || Mouse.current == null || Camera.main == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // クリックしたマスのユニットを表示対象にする（BattleController と同じ座標変換の手順）。
        // 盤外クリックは無視して表示を保ち、空きマスのクリックで閉じる。
        // ※IMGUIのボタン（ターン終了・行動メニュー）はクリックを横取りしないため、
        //   ボタンの下のマスにも反応して表示が変わることがある（デバッグ用UIなので許容）
        Vector3 world = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        if (!grid.TryWorldToCell(world, out Vector2Int cell)) return;

        TileData tile = grid.GetTile(cell);
        unit = tile != null ? tile.Occupant : null;
    }

    void OnGUI()
    {
        if (!panelEnabled) return;
        if (unit == null || !unit.IsAlive) return; // 倒れて Destroy されたら自動で閉じる

        List<string> lines = BuildLines();

        // 画面右上に描く（左上のターン表示・左下の地形情報・右下の戦闘予測と重ならない位置）
        float panelHeight = (lines.Count + 1) * lineHeight + padding * 2f; // +1 は見出しの行
        var rect = new Rect(Screen.width - panelWidth - 12f, 10f, panelWidth, panelHeight);

        var boxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
        };
        GUI.Box(rect, "ユニット情報（デバッグ用）", boxStyle);

        var lineStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
        for (int i = 0; i < lines.Count; i++)
        {
            GUI.Label(new Rect(rect.x + padding,
                               rect.y + padding + (i + 1) * lineHeight,
                               panelWidth - padding * 2f, lineHeight),
                      lines[i], lineStyle);
        }
    }

    /// <summary>表示する行のリストを組み立てる（行数はユニットの状態で変わる）。</summary>
    private List<string> BuildLines()
    {
        var lines = new List<string>();

        string side = unit.Faction == Faction.Player ? "自軍" : "敵軍";
        lines.Add($"{unit.Data.unitName}（{side}・{ClassName(unit)}）({unit.GridPosition.x},{unit.GridPosition.y})");
        lines.Add($"HP {unit.CurrentHP}／{unit.MaxHP}");
        lines.Add($"力{unit.Strength}　魔力{unit.Magic}　技{unit.Skill}　速さ{unit.Speed}");
        lines.Add($"守備{unit.Defense}　魔防{unit.Resistance}　移動{unit.Move}");
        lines.Add(BuildWeaponLine());
        AddItemLines(lines);
        lines.Add(BuildStateLine());

        if (unit.Faction == Faction.Enemy)
            lines.Add(BuildAILine());

        // 挟撃まわり（該当するときだけ）— 敵AIの守り配置・ガード役潰しを観察するときに便利
        if (CombatRules.IsGuardingSomeone(unit, grid))
            lines.Add("誰かの挟撃ガード役になっている");
        if (CombatRules.IsPincerNegated(unit, grid))
            lines.Add("ガードで挟撃を無効化されている");

        return lines;
    }

    /// <summary>武器の行。「武器: 槍（前衛・威力5・射程1）」のような形式。</summary>
    private string BuildWeaponLine()
    {
        WeaponData w = unit.Weapon;
        if (w == null) return "武器: なし（攻撃不可）";

        string cat = w.category == WeaponCategory.Melee ? "前衛" : "後衛";
        string range = w.minRange == w.maxRange ? $"{w.maxRange}" : $"{w.minRange}〜{w.maxRange}";
        return $"武器: {w.weaponName}（{cat}・威力{w.might}・射程{range}）";
    }

    /// <summary>持ち物の行を追加する（フェーズ22）。装備中の武器は「装備中」、道具は残り回数を添える。</summary>
    private void AddItemLines(List<string> lines)
    {
        if (unit.Items.Count == 0)
        {
            lines.Add("持ち物: なし");
            return;
        }

        lines.Add($"持ち物（{unit.Items.Count}／{UnitData.InventoryCapacity}）:");
        foreach (ItemSlot slot in unit.Items)
        {
            string note = "";
            if (slot.Item is ToolData) note = $"（残り{slot.UsesLeft}回）";
            else if (slot.Item == unit.Weapon) note = "（装備中）";
            lines.Add($"・{slot.Item.DisplayName}{note}");
        }
    }

    /// <summary>状態の行。行動済みかどうかに、飛翔・救出・格納の状態を「・」でつなげる。</summary>
    private string BuildStateLine()
    {
        var parts = new List<string> { unit.HasActed ? "行動済み" : "未行動" };

        if (unit.IsFlying) parts.Add($"飛翔中（残り{unit.FlightTurnsLeft}ターン）");
        if (unit.IsRescuing)
        {
            var names = new List<string>();
            foreach (Unit cargo in unit.Carried) names.Add(cargo.Data.unitName);
            parts.Add($"救出中: {string.Join("、", names)}");
        }
        if (unit.IsCarried) parts.Add("格納中（盤上にいない）");

        return "状態: " + string.Join("・", parts);
    }

    /// <summary>敵AIの行。性格と、待ち伏せ型なら挑発されたかどうかも出す。</summary>
    private string BuildAILine()
    {
        if (unit.AIProfile == EnemyAIProfile.Ambush)
            return unit.IsProvoked ? "AI: 待ち伏せ型（挑発済み＝突撃化）" : "AI: 待ち伏せ型（未挑発）";
        return "AI: 突撃型";
    }

    /// <summary>兵種の表示名。兵種データがあればその名前（例：剣闘士）、無ければ移動タイプ名。</summary>
    private static string ClassName(Unit u)
    {
        if (u.Data.classData != null) return u.Data.classData.className;
        return u.Class.DisplayName();
    }
}
