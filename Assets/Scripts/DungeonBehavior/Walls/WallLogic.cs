using System.Collections.Generic;
using UnityEngine;

public static class WallCellAnalyzer
{
    public static bool IsWalkable(GridCell cell)
    {
        return cell != null &&
               (cell.Type == CellType.Floor ||
                cell.Type == CellType.Corridor ||
                cell.Type == CellType.Door);
    }

    public static bool IsWall(GridCell cell)
    {
        return cell != null && cell.Type == CellType.Wall;
    }

    public static HashSet<Vector3Int> DetectCorners(
        DungeonGrid grid,
        Dictionary<Vector2Int, GridCell> _)
    {
        HashSet<Vector3Int> corners = new HashSet<Vector3Int>();

        foreach (var kvp in grid.GetAllCells())
        {
            Vector2Int pos = kvp.Key;

            if (IsCornerVertex(grid, pos.x, pos.y))
            {
                corners.Add(new Vector3Int(pos.x, 0, pos.y));
            }

            if (IsCornerVertex(grid, pos.x + 1, pos.y))
            {
                corners.Add(new Vector3Int(pos.x + 1, 0, pos.y));
            }

            if (IsCornerVertex(grid, pos.x, pos.y + 1))
            {
                corners.Add(new Vector3Int(pos.x, 0, pos.y + 1));
            }

            if (IsCornerVertex(grid, pos.x + 1, pos.y + 1))
            {
                corners.Add(new Vector3Int(pos.x + 1, 0, pos.y + 1));
            }
        }

        return corners;
    }

    private static bool IsCornerVertex(DungeonGrid grid, int vertexX, int vertexZ)
    {
        bool blWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX - 1, vertexZ - 1)));
        bool brWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX, vertexZ - 1)));
        bool tlWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX - 1, vertexZ)));
        bool trWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX, vertexZ)));

        int walkableCount = 0;
        if (blWalkable) walkableCount++;
        if (brWalkable) walkableCount++;
        if (tlWalkable) walkableCount++;
        if (trWalkable) walkableCount++;

        if (walkableCount == 1 || walkableCount == 3)
        {
            return true;
        }

        if (walkableCount == 2)
        {
            if (blWalkable && trWalkable && !brWalkable && !tlWalkable)
                return true;
            
            if (brWalkable && tlWalkable && !blWalkable && !trWalkable)
                return true;
        }

        return false;
    }

    public static (HashSet<Vector3Int> convex, HashSet<Vector3Int> concave) DetectCornersDetailed(
        DungeonGrid grid,
        Dictionary<Vector2Int, GridCell> _)
    {
        HashSet<Vector3Int> convexCorners = new HashSet<Vector3Int>();
        HashSet<Vector3Int> concaveCorners = new HashSet<Vector3Int>();

        foreach (var kvp in grid.GetAllCells())
        {
            Vector2Int pos = kvp.Key;

            CheckAndAddCornerByType(grid, pos.x, pos.y, convexCorners, concaveCorners);
            CheckAndAddCornerByType(grid, pos.x + 1, pos.y, convexCorners, concaveCorners);
            CheckAndAddCornerByType(grid, pos.x, pos.y + 1, convexCorners, concaveCorners);
            CheckAndAddCornerByType(grid, pos.x + 1, pos.y + 1, convexCorners, concaveCorners);
        }

        return (convexCorners, concaveCorners);
    }

    private static void CheckAndAddCornerByType(
        DungeonGrid grid, 
        int vertexX, 
        int vertexZ,
        HashSet<Vector3Int> convexCorners,
        HashSet<Vector3Int> concaveCorners)
    {
        Vector3Int vertex = new Vector3Int(vertexX, 0, vertexZ);

        if (convexCorners.Contains(vertex) || concaveCorners.Contains(vertex))
            return;

        bool blWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX - 1, vertexZ - 1)));
        bool brWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX, vertexZ - 1)));
        bool tlWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX - 1, vertexZ)));
        bool trWalkable = IsWalkable(grid.GetCell(new Vector2Int(vertexX, vertexZ)));

        int walkableCount = 0;
        if (blWalkable) walkableCount++;
        if (brWalkable) walkableCount++;
        if (tlWalkable) walkableCount++;
        if (trWalkable) walkableCount++;

        if (walkableCount == 1)
        {
            convexCorners.Add(vertex);
        }
        else if (walkableCount == 3)
        {
            concaveCorners.Add(vertex);
        }
        else if (walkableCount == 2)
        {
            if ((blWalkable && trWalkable && !brWalkable && !tlWalkable) ||
                (brWalkable && tlWalkable && !blWalkable && !trWalkable))
            {
                convexCorners.Add(vertex);
            }
        }
    }

    private static bool HasWallTop(DungeonGrid grid, Vector2Int pos)
    {
        GridCell topCell = grid.GetCell(pos + Vector2Int.up);
        return !IsWalkable(topCell);
    }

    private static bool HasWallBottom(DungeonGrid grid, Vector2Int pos)
    {
        GridCell bottomCell = grid.GetCell(pos + Vector2Int.down);
        return !IsWalkable(bottomCell);
    }

    private static bool HasWallRight(DungeonGrid grid, Vector2Int pos)
    {
        GridCell rightCell = grid.GetCell(pos + Vector2Int.right);
        return !IsWalkable(rightCell);
    }

    private static bool HasWallLeft(DungeonGrid grid, Vector2Int pos)
    {
        GridCell leftCell = grid.GetCell(pos + Vector2Int.left);
        return !IsWalkable(leftCell);
    }
}