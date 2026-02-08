using System.Collections.Generic;
using UnityEngine;

public class DungeonPostProcessResult
{
    public readonly List<Vector2Int> DoorCells = new List<Vector2Int>();
    public readonly List<Vector2Int> RoomEntrances = new List<Vector2Int>();
    public readonly List<Vector2Int> DeadEnds = new List<Vector2Int>();
    public readonly List<Vector2Int> Intersections = new List<Vector2Int>();
}

/// <summary>
/// Post-procesa la malla de celdas para etiquetar puertas, entradas de sala y nodos clave.
/// Esto deja la rejilla lista para pasos de decorado (ej: colocar puertas, props en cul-de-sac, etc.).
/// </summary>
public class DungeonPostProcessor
{
    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    public DungeonPostProcessResult Process(DungeonGrid grid, List<RoomNode> rooms)
    {
        var result = new DungeonPostProcessResult();
        if (grid == null) return result;

        MarkDoors(grid, result);
        AnalyzeConnectivity(grid, result);

        return result;
    }

    private void MarkDoors(DungeonGrid grid, DungeonPostProcessResult result)
    {
        var allCells = grid.GetAllCells();
        HashSet<Vector2Int> doorCandidates = new HashSet<Vector2Int>();
        HashSet<Vector2Int> roomEntrances = new HashSet<Vector2Int>();

        foreach (var kvp in allCells)
        {
            if (kvp.Value.Type != CellType.Corridor) continue;

            foreach (var dir in Directions)
            {
                Vector2Int neighborPos = kvp.Key + dir;
                var neighbor = grid.GetCell(neighborPos);
                if (neighbor != null && neighbor.Type == CellType.Floor && neighbor.ParentRoom != null)
                {
                    doorCandidates.Add(kvp.Key);
                    roomEntrances.Add(neighborPos);
                    break;
                }
            }
        }

        foreach (var pos in doorCandidates)
        {
            var cell = grid.GetCell(pos);
            if (cell == null) continue;
            cell.Type = CellType.Door;
            result.DoorCells.Add(pos);
        }

        result.RoomEntrances.AddRange(roomEntrances);
    }

    private void AnalyzeConnectivity(DungeonGrid grid, DungeonPostProcessResult result)
    {
        var allCells = grid.GetAllCells();

        foreach (var kvp in allCells)
        {
            var cell = kvp.Value;
            if (!WallCellAnalyzer.IsWalkable(cell)) continue;

            int neighbors = 0;
            foreach (var dir in Directions)
            {
                var neighbor = grid.GetCell(kvp.Key + dir);
                if (WallCellAnalyzer.IsWalkable(neighbor))
                {
                    neighbors++;
                }
            }

            if (neighbors == 1)
            {
                result.DeadEnds.Add(kvp.Key);
            }
            else if (neighbors >= 3)
            {
                result.Intersections.Add(kvp.Key);
            }
        }
    }
}
