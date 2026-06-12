using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 新 Input System

/// <summary>
/// プレイヤーの操作を受け持つ司令塔。
///  ・味方ユニットをクリックで選択 → 移動できるマスを青く表示
///  ・光ったマスをクリック → そこへ移動
///  ・範囲外や盤外をクリック → 選択解除
/// 盤面の情報は GridManager に問い合わせ、範囲計算は MovementCalculator に任せる。
/// </summary>
public class BattleController : MonoBehaviour
{
    [Tooltip("グリッド（GridManager）への参照")]
    public GridManager grid;

    [Header("ハイライト色")]
    [Tooltip("選択中ユニットのマス")]
    public Color selectColor = new Color(1f, 0.9f, 0.4f);   // 黄
    [Tooltip("移動できるマス")]
    public Color moveRangeColor = new Color(0.5f, 0.8f, 1f); // 水色

    private Unit selectedUnit;                 // 選択中のユニット（無ければ null）
    private HashSet<Vector2Int> reachableCells; // 現在の移動可能マス

    void Update()
    {
        if (grid == null || Mouse.current == null || Camera.main == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(screenPos);

            if (grid.TryWorldToCell(worldPos, out Vector2Int cell))
                OnCellClicked(cell);
            else
                Deselect(); // 盤の外をクリックしたら選択解除
        }
    }

    private void OnCellClicked(Vector2Int cell)
    {
        TileData tile = grid.GetTile(cell);
        Unit clickedUnit = tile != null ? tile.Occupant : null;

        if (selectedUnit == null)
        {
            // 未選択：味方ユニットをクリックしたら選択する
            if (clickedUnit != null && clickedUnit.Faction == Faction.Player)
                Select(clickedUnit);
        }
        else
        {
            // 選択中
            if (reachableCells != null && reachableCells.Contains(cell))
            {
                // 移動できるマスをクリック → 移動して選択解除
                selectedUnit.MoveTo(grid, cell);
                Deselect();
            }
            else if (clickedUnit != null && clickedUnit.Faction == Faction.Player)
            {
                // 別の味方をクリック → そちらに選択を切り替え
                Select(clickedUnit);
            }
            else
            {
                // それ以外 → 選択解除
                Deselect();
            }
        }
    }

    /// <summary>ユニットを選択し、移動範囲を表示する。</summary>
    private void Select(Unit unit)
    {
        Deselect(); // 前の表示を消してから

        selectedUnit = unit;
        reachableCells = MovementCalculator.GetReachableCells(grid, unit);

        grid.SetHighlight(unit.GridPosition, selectColor);
        foreach (Vector2Int c in reachableCells)
            grid.SetHighlight(c, moveRangeColor);

        Debug.Log($"選択: {unit.Data.unitName}（移動できるマス {reachableCells.Count} 個）");
    }

    /// <summary>選択を解除し、ハイライトを消す。</summary>
    private void Deselect()
    {
        grid.ResetAllHighlights();
        selectedUnit = null;
        reachableCells = null;
    }
}
