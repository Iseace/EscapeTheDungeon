using System.Collections.Generic;
using UnityEngine;

public enum CellType
{
    Empty,
    Floor,
    Wall,
    Corridor,
    Door
}

public class GridCell
{
    public Vector2Int Position { get; set; }
    public CellType Type { get; set; }
    public RoomNode ParentRoom { get; set; }
    public bool IsOccupied { get; set; }
    public GameObject PlacedObject { get; set; }

    public GridCell(Vector2Int position)
    {
        Position = position;
        Type = CellType.Empty;
        IsOccupied = false;
    }
}

public class DungeonGrid
{
    private Dictionary<Vector2Int, GridCell> grid;
    private int width;
    private int height;

    public DungeonGrid(int width, int height)
    {
        this.width = width;
        this.height = height;
        grid = new Dictionary<Vector2Int, GridCell>();
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector2Int pos = new Vector2Int(x, z);
                grid[pos] = new GridCell(pos);
            }
        }
    }

    public GridCell GetCell(Vector2Int position)
    {
        if (grid.ContainsKey(position))
            return grid[position];
        return null;
    }

    public void SetCellType(Vector2Int position, CellType type, RoomNode parentRoom = null)
    {
        GridCell cell = GetCell(position);
        if (cell != null)
        {
            cell.Type = type;
            cell.ParentRoom = parentRoom;
        }
    }

    public bool IsCellAvailable(Vector2Int position)
    {
        GridCell cell = GetCell(position);
        return cell != null && cell.Type == CellType.Floor && !cell.IsOccupied;
    }

    public List<Vector2Int> GetAvailableCellsInRoom(RoomNode room)
    {
        List<Vector2Int> availableCells = new List<Vector2Int>();

        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int z = room.BottomLeftAreaCorner.y; z < room.TopRightAreaCorner.y; z++)
            {
                Vector2Int pos = new Vector2Int(x, z);
                if (IsCellAvailable(pos))
                {
                    availableCells.Add(pos);
                }
            }
        }

        return availableCells;
    }

    public void OccupyCell(Vector2Int position, GameObject obj)
    {
        GridCell cell = GetCell(position);
        if (cell != null)
        {
            cell.IsOccupied = true;
            cell.PlacedObject = obj;
        }
    }

    public void ClearCell(Vector2Int position)
    {
        GridCell cell = GetCell(position);
        if (cell != null)
        {
            cell.IsOccupied = false;
            cell.PlacedObject = null;
        }
    }

    public Dictionary<Vector2Int, GridCell> GetAllCells()
    {
        return grid;
    }
}