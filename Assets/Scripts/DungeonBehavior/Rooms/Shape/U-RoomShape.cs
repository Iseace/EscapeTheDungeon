using UnityEngine;

public class UShapedRoom : BaseRoomShape
{
    public override void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;

        // Select which side to cut (0: top, 1: bottom, 2: left, 3: right)
        int sideToCut = Random.Range(0, 4);

        // U-shape cuts deeper and wider than other shapes
        int cutWidth = Mathf.RoundToInt(width * Random.Range(0.3f, 0.5f));
        int cutHeight = Mathf.RoundToInt(height * Random.Range(0.3f, 0.5f));

        switch (sideToCut)
        {
            case 0: // Cut from top - U opens upward
                {
                    int startX = room.BottomLeftAreaCorner.x + width / 4;
                    int endX = room.TopRightAreaCorner.x - width / 4;
                    RemoveCells(grid,
                        startX,
                        room.TopRightAreaCorner.y - cutHeight,
                        endX,
                        room.TopRightAreaCorner.y);
                }
                break;

            case 1: // Cut from bottom - U opens downward
                {
                    int startX = room.BottomLeftAreaCorner.x + width / 4;
                    int endX = room.TopRightAreaCorner.x - width / 4;
                    RemoveCells(grid,
                        startX,
                        room.BottomLeftAreaCorner.y,
                        endX,
                        room.BottomLeftAreaCorner.y + cutHeight);
                }
                break;

            case 2: // Cut from left - U opens left
                {
                    int startY = room.BottomLeftAreaCorner.y + height / 4;
                    int endY = room.TopRightAreaCorner.y - height / 4;
                    RemoveCells(grid,
                        room.BottomLeftAreaCorner.x,
                        startY,
                        room.BottomLeftAreaCorner.x + cutWidth,
                        endY);
                }
                break;

            case 3: // Cut from right - U opens right
                {
                    int startY = room.BottomLeftAreaCorner.y + height / 4;
                    int endY = room.TopRightAreaCorner.y - height / 4;
                    RemoveCells(grid,
                        room.TopRightAreaCorner.x - cutWidth,
                        startY,
                        room.TopRightAreaCorner.x,
                        endY);
                }
                break;
        }
    }

    public override bool CanApply(int width, int height)
    {
        // U-shape needs larger rooms
        return width >= 8 && height >= 8;
    }
}