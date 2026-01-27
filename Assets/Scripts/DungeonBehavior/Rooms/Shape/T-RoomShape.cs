using UnityEngine;

public class TShapedRoom : BaseRoomShape
{
    public override void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;

        // Decide if T is horizontal or vertical
        bool horizontal = Random.value > 0.5f;

        int cutSize = Mathf.RoundToInt((horizontal ? height : width) * 
            Random.Range(config.cutoutMinSize, config.cutoutMaxSize));

        if (horizontal)
        {
            ApplyHorizontalT(room, grid, width, cutSize);
        }
        else
        {
            ApplyVerticalT(room, grid, height, cutSize);
        }
    }

    private void ApplyHorizontalT(RoomNode room, DungeonGrid grid, int width, int cutSize)
    {
        // Horizontal T: cut top or bottom corners
        if (Random.value > 0.5f)
        {
            // Cut top corners
            RemoveCells(grid,
                room.BottomLeftAreaCorner.x,
                room.TopRightAreaCorner.y - cutSize,
                room.BottomLeftAreaCorner.x + width / 3,
                room.TopRightAreaCorner.y);

            RemoveCells(grid,
                room.TopRightAreaCorner.x - width / 3,
                room.TopRightAreaCorner.y - cutSize,
                room.TopRightAreaCorner.x,
                room.TopRightAreaCorner.y);
        }
        else
        {
            // Cut bottom corners
            RemoveCells(grid,
                room.BottomLeftAreaCorner.x,
                room.BottomLeftAreaCorner.y,
                room.BottomLeftAreaCorner.x + width / 3,
                room.BottomLeftAreaCorner.y + cutSize);

            RemoveCells(grid,
                room.TopRightAreaCorner.x - width / 3,
                room.BottomLeftAreaCorner.y,
                room.TopRightAreaCorner.x,
                room.BottomLeftAreaCorner.y + cutSize);
        }
    }

    private void ApplyVerticalT(RoomNode room, DungeonGrid grid, int height, int cutSize)
    {
        // Vertical T: cut left or right corners
        if (Random.value > 0.5f)
        {
            // Cut left corners
            RemoveCells(grid,
                room.BottomLeftAreaCorner.x,
                room.BottomLeftAreaCorner.y,
                room.BottomLeftAreaCorner.x + cutSize,
                room.BottomLeftAreaCorner.y + height / 3);

            RemoveCells(grid,
                room.BottomLeftAreaCorner.x,
                room.TopRightAreaCorner.y - height / 3,
                room.BottomLeftAreaCorner.x + cutSize,
                room.TopRightAreaCorner.y);
        }
        else
        {
            // Cut right corners
            RemoveCells(grid,
                room.TopRightAreaCorner.x - cutSize,
                room.BottomLeftAreaCorner.y,
                room.TopRightAreaCorner.x,
                room.BottomLeftAreaCorner.y + height / 3);

            RemoveCells(grid,
                room.TopRightAreaCorner.x - cutSize,
                room.TopRightAreaCorner.y - height / 3,
                room.TopRightAreaCorner.x,
                room.TopRightAreaCorner.y);
        }
    }
}