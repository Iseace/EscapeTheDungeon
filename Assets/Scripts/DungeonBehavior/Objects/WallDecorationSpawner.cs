using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class WallDecoration
{
    public GameObject prefab;
    [Range(0f, 1f)] public float probability = 0.5f;
    [FormerlySerializedAs("heightOverride")]
    [Tooltip("Altura Y de esta decoración.")]
    public float height = 1.6f;
    [Tooltip("Rotación adicional en Euler para compensar pivots u orientación del prefab.")]
    public Vector3 rotationOffsetEuler = Vector3.zero;
}

public class WallDecorationSpawner
{
    private List<WallDecoration> currentDecorations;
    private static readonly Vector2Int[] Neigh =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    public void Spawn(
        DungeonGrid grid,
        Transform parent,
        Vector3 offset,
        List<WallDecoration> decorations,
        int spacing,
        float inwardOffset,
        HashSet<Vector2Int> doorCells)
    {
        if (grid == null || parent == null) return;
        if (decorations == null || decorations.Count == 0) return;

        var cells = grid.GetAllCells();
        if (cells.Count == 0) return;
        HashSet<Vector3Int> corners = WallCellAnalyzer.DetectCorners(grid, cells);

        currentDecorations = decorations;

        int xMin = int.MaxValue, xMax = int.MinValue, zMin = int.MaxValue, zMax = int.MinValue;
        foreach (var kvp in cells)
        {
            xMin = Mathf.Min(xMin, kvp.Key.x);
            xMax = Mathf.Max(xMax, kvp.Key.x);
            zMin = Mathf.Min(zMin, kvp.Key.y);
            zMax = Mathf.Max(zMax, kvp.Key.y);
        }

        GameObject root = new GameObject("WallDecorations");
        root.transform.SetParent(parent, false);

        spacing = Mathf.Max(1, spacing);
        float inward = Mathf.Max(0f, inwardOffset);

        // Horizontal edges (bottom and top per row)
        for (int z = zMin; z <= zMax; z++)
        {
            SpawnHorizontalRow(cells, corners, root.transform, offset, z, true, xMin, xMax, spacing, inward, doorCells);
            SpawnHorizontalRow(cells, corners, root.transform, offset, z, false, xMin, xMax, spacing, inward, doorCells);
        }

        // Vertical edges (left and right per column)
        for (int x = xMin; x <= xMax; x++)
        {
            SpawnVerticalCol(cells, corners, root.transform, offset, x, true, zMin, zMax, spacing, inward, doorCells);
            SpawnVerticalCol(cells, corners, root.transform, offset, x, false, zMin, zMax, spacing, inward, doorCells);
        }
    }

    private void SpawnHorizontalRow(
        Dictionary<Vector2Int, GridCell> cells,
        HashSet<Vector3Int> corners,
        Transform parent,
        Vector3 offset,
        int zRow,
        bool isBottomEdge,
        int xMin,
        int xMax,
        int spacing,
        float inward,
        HashSet<Vector2Int> doorCells)
    {
        int runStart = -1;
        for (int x = xMin; x <= xMax; x++)
        {
            bool walkable = IsWalkable(cells, x, zRow);
            bool neighborWalkable = IsWalkable(cells, x, isBottomEdge ? zRow - 1 : zRow + 1);
            bool needsWall = walkable && !neighborWalkable && !IsNearDoor(new Vector2Int(x, zRow), doorCells, cells);

            if (needsWall && runStart == -1)
                runStart = x;
            if ((!needsWall || x == xMax) && runStart != -1)
            {
                int runEnd = needsWall && x == xMax ? x : x - 1;
                int length = runEnd - runStart + 1;
                PlaceAlongHorizontalRun(parent, offset, zRow, isBottomEdge, runStart, length, spacing, inward, cells, doorCells, corners);
                runStart = -1;
            }
        }
    }

    private void PlaceAlongHorizontalRun(
        Transform parent,
        Vector3 offset,
        int zRow,
        bool isBottomEdge,
        int runStart,
        int length,
        int spacing,
        float inward,
        Dictionary<Vector2Int, GridCell> cells,
        HashSet<Vector2Int> doorCells,
        HashSet<Vector3Int> corners)
    {
        for (int i = 0; i < length; i++)
        {
            if (i % spacing != 0) continue;
            int xCell = runStart + i;
            var cell = cells[new Vector2Int(xCell, zRow)];
            if (cell.Type == CellType.Door) continue;
            if (IsAdjacentToCornerHorizontal(xCell, zRow, isBottomEdge, corners)) continue;

            WallDecoration deco = PickDecoration();
            if (deco == null || deco.prefab == null) continue;
            float decoHeight = Mathf.Max(0f, deco.height);

            float zPos = isBottomEdge ? zRow : zRow + 1f;
            Vector3 pos = new Vector3(xCell + 0.5f, decoHeight, zPos) + offset;
            Vector3 dir = isBottomEdge ? Vector3.forward : Vector3.back;
            pos += dir * inward;
            Quaternion baseRot = isBottomEdge ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            Quaternion rot = baseRot * Quaternion.Euler(deco.rotationOffsetEuler);

            Object.Instantiate(deco.prefab, pos, rot, parent);
        }
    }

    private void SpawnVerticalCol(
        Dictionary<Vector2Int, GridCell> cells,
        HashSet<Vector3Int> corners,
        Transform parent,
        Vector3 offset,
        int xCol,
        bool isLeftEdge,
        int zMin,
        int zMax,
        int spacing,
        float inward,
        HashSet<Vector2Int> doorCells)
    {
        int runStart = -1;
        for (int z = zMin; z <= zMax; z++)
        {
            bool walkable = IsWalkable(cells, xCol, z);
            bool neighborWalkable = IsWalkable(cells, isLeftEdge ? xCol - 1 : xCol + 1, z);
            bool needsWall = walkable && !neighborWalkable && !IsNearDoor(new Vector2Int(xCol, z), doorCells, cells);

            if (needsWall && runStart == -1)
                runStart = z;
            if ((!needsWall || z == zMax) && runStart != -1)
            {
                int runEnd = needsWall && z == zMax ? z : z - 1;
                int length = runEnd - runStart + 1;
                PlaceAlongVerticalRun(parent, offset, xCol, isLeftEdge, runStart, length, spacing, inward, cells, doorCells, corners);
                runStart = -1;
            }
        }
    }

    private void PlaceAlongVerticalRun(
        Transform parent,
        Vector3 offset,
        int xCol,
        bool isLeftEdge,
        int runStart,
        int length,
        int spacing,
        float inward,
        Dictionary<Vector2Int, GridCell> cells,
        HashSet<Vector2Int> doorCells,
        HashSet<Vector3Int> corners)
    {
        for (int i = 0; i < length; i++)
        {
            if (i % spacing != 0) continue;
            int zCell = runStart + i;
            var cell = cells[new Vector2Int(xCol, zCell)];
            if (cell.Type == CellType.Door) continue;
            if (IsAdjacentToCornerVertical(xCol, zCell, isLeftEdge, corners)) continue;

            WallDecoration deco = PickDecoration();
            if (deco == null || deco.prefab == null) continue;
            float decoHeight = Mathf.Max(0f, deco.height);

            float xPos = isLeftEdge ? xCol : xCol + 1f;
            Vector3 pos = new Vector3(xPos, decoHeight, zCell + 0.5f) + offset;
            Vector3 dir = isLeftEdge ? Vector3.right : Vector3.left;
            pos += dir * inward;
            Quaternion baseRot = isLeftEdge ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(0f, -90f, 0f);
            Quaternion rot = baseRot * Quaternion.Euler(deco.rotationOffsetEuler);

            Object.Instantiate(deco.prefab, pos, rot, parent);
        }
    }

    private bool IsWalkable(Dictionary<Vector2Int, GridCell> cells, int x, int z)
    {
        if (cells.TryGetValue(new Vector2Int(x, z), out var cell))
        {
            return WallCellAnalyzer.IsWalkable(cell);
        }
        return false;
    }

    private bool IsAdjacentToCornerHorizontal(int xCell, int zRow, bool isBottomEdge, HashSet<Vector3Int> corners)
    {
        if (corners == null || corners.Count == 0) return false;
        int zEdge = isBottomEdge ? zRow : zRow + 1;
        return corners.Contains(new Vector3Int(xCell, 0, zEdge)) ||
               corners.Contains(new Vector3Int(xCell + 1, 0, zEdge));
    }

    private bool IsAdjacentToCornerVertical(int xCol, int zCell, bool isLeftEdge, HashSet<Vector3Int> corners)
    {
        if (corners == null || corners.Count == 0) return false;
        int xEdge = isLeftEdge ? xCol : xCol + 1;
        return corners.Contains(new Vector3Int(xEdge, 0, zCell)) ||
               corners.Contains(new Vector3Int(xEdge, 0, zCell + 1));
    }
    private bool IsNearDoor(Vector2Int cell, HashSet<Vector2Int> doorCells, Dictionary<Vector2Int, GridCell> cells)
    {
        if (doorCells == null || doorCells.Count == 0) return false;
        if (doorCells.Contains(cell)) return true;
        foreach (var d in Neigh)
        {
            var np = cell + d;
            if (doorCells.Contains(np)) return true;
            if (cells.TryGetValue(np, out var c) && c.Type == CellType.Door) return true;
        }
        return false;
    }

    private WallDecoration PickDecoration()
    {
        if (currentDecorations == null || currentDecorations.Count == 0) return null;

        int safety = 8;
        for (int i = 0; i < safety; i++)
        {
            var entry = currentDecorations[Random.Range(0, currentDecorations.Count)];
            if (entry.prefab == null) continue;
            if (Random.value <= entry.probability) return entry;
        }
        return null;
    }
}
