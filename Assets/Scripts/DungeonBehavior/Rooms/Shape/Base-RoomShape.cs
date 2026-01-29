using UnityEngine;

public abstract class BaseRoomShape
{
    public abstract void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config);

    public virtual bool CanApply(int width, int height)
    {
        return width >= 6 && height >= 6;
    }

    protected void RemoveCells(DungeonGrid grid, int x1, int y1, int x2, int y2)
    {
        for (int x = x1; x < x2; x++)
        {
            for (int y = y1; y < y2; y++)
            {
                GridCell cell = grid.GetCell(new Vector2Int(x, y));
                if (cell != null && cell.Type == CellType.Floor)
                {
                    grid.SetCellType(new Vector2Int(x, y), CellType.Empty);
                }
            }
        }
    }

    protected int GetCutoutSize(int dimension, RoomShapeConfig config)
    {
        return Mathf.RoundToInt(dimension * Random.Range(config.cutoutMinSize, config.cutoutMaxSize));
    }
}