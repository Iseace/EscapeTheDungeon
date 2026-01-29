using UnityEngine;

public class CircularRoomShape : BaseRoomShape
{
    public override void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;

        // Calculate room center
        Vector2 center = new Vector2(
            (room.BottomLeftAreaCorner.x + room.TopRightAreaCorner.x) / 2f,
            (room.BottomLeftAreaCorner.y + room.TopRightAreaCorner.y) / 2f
        );

        // Calculate radius (use smaller dimension for a circle that fits)
        float radius = Mathf.Min(width, height) / 2f * Random.Range(0.85f, 0.95f);

        // Iterate through all cells and remove those outside the circle
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                Vector2Int cellPos = new Vector2Int(x, y);
                
                // Calculate distance from center to this cell
                Vector2 cellCenter = new Vector2(x + 0.5f, y + 0.5f);
                float distance = Vector2.Distance(center, cellCenter);

                // Remove cells outside the radius
                if (distance > radius)
                {
                    GridCell cell = grid.GetCell(cellPos);
                    if (cell != null && cell.Type == CellType.Floor)
                    {
                        grid.SetCellType(cellPos, CellType.Empty);
                    }
                }
            }
        }
    }

    public override bool CanApply(int width, int height)
    {
        return width >= 8 && height >= 8 && Mathf.Abs(width - height) <= 4;
    }
}