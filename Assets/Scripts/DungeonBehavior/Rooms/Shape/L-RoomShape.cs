using UnityEngine;

public class LShapedRoom : BaseRoomShape
{
    public override void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;

        // Select which corner to cut (0-3: BL, BR, TL, TR)
        int cornerToCut = Random.Range(0, 4);

        // Calculate cutout size
        int cutWidth = GetCutoutSize(width, config);
        int cutHeight = GetCutoutSize(height, config);

        switch (cornerToCut)
        {
            case 0: // Bottom-Left
                RemoveCells(grid,
                    room.BottomLeftAreaCorner.x,
                    room.BottomLeftAreaCorner.y,
                    room.BottomLeftAreaCorner.x + cutWidth,
                    room.BottomLeftAreaCorner.y + cutHeight);
                break;

            case 1: // Bottom-Right
                RemoveCells(grid,
                    room.TopRightAreaCorner.x - cutWidth,
                    room.BottomLeftAreaCorner.y,
                    room.TopRightAreaCorner.x,
                    room.BottomLeftAreaCorner.y + cutHeight);
                break;

            case 2: // Top-Left
                RemoveCells(grid,
                    room.BottomLeftAreaCorner.x,
                    room.TopRightAreaCorner.y - cutHeight,
                    room.BottomLeftAreaCorner.x + cutWidth,
                    room.TopRightAreaCorner.y);
                break;

            case 3: // Top-Right
                RemoveCells(grid,
                    room.TopRightAreaCorner.x - cutWidth,
                    room.TopRightAreaCorner.y - cutHeight,
                    room.TopRightAreaCorner.x,
                    room.TopRightAreaCorner.y);
                break;
        }
    }
}