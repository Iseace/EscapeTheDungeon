using UnityEngine;

public class CrossShapedRoom : BaseRoomShape
{
    public override void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;

        // Cross uses smaller cuts to maintain connectivity
        int cutWidth = Mathf.RoundToInt(width * Random.Range(config.cutoutMinSize * 0.7f, config.cutoutMaxSize * 0.7f));
        int cutHeight = Mathf.RoundToInt(height * Random.Range(config.cutoutMinSize * 0.7f, config.cutoutMaxSize * 0.7f));

        // Cut all 4 corners
        // Bottom-Left corner
        RemoveCells(grid,
            room.BottomLeftAreaCorner.x,
            room.BottomLeftAreaCorner.y,
            room.BottomLeftAreaCorner.x + cutWidth,
            room.BottomLeftAreaCorner.y + cutHeight);

        // Bottom-Right corner
        RemoveCells(grid,
            room.TopRightAreaCorner.x - cutWidth,
            room.BottomLeftAreaCorner.y,
            room.TopRightAreaCorner.x,
            room.BottomLeftAreaCorner.y + cutHeight);

        // Top-Left corner
        RemoveCells(grid,
            room.BottomLeftAreaCorner.x,
            room.TopRightAreaCorner.y - cutHeight,
            room.BottomLeftAreaCorner.x + cutWidth,
            room.TopRightAreaCorner.y);

        // Top-Right corner
        RemoveCells(grid,
            room.TopRightAreaCorner.x - cutWidth,
            room.TopRightAreaCorner.y - cutHeight,
            room.TopRightAreaCorner.x,
            room.TopRightAreaCorner.y);
    }

    public override bool CanApply(int width, int height)
    {
        // Cross needs at least 8x8 to look good
        return width >= 8 && height >= 8;
    }
}