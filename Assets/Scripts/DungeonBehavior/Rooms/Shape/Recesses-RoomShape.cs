using UnityEngine;

public class RecessesRoomShape : BaseRoomShape
{
    public override void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;

        // Create multiple recesses
        for (int i = 0; i < config.recessCount; i++)
        {
            CreateRandomRecess(room, grid, width, height);
        }
    }

    private void CreateRandomRecess(RoomNode room, DungeonGrid grid, int width, int height)
    {
        // Select random side (0: bottom, 1: top, 2: left, 3: right)
        int side = Random.Range(0, 4);

        // Random recess dimensions
        int recessDepth = Random.Range(2, 4);
        int recessLength = Random.Range(3, Mathf.Max(4, (side < 2 ? width : height) / 3));

        switch (side)
        {
            case 0: // Bottom recess
                {
                    int startX = Random.Range(
                        room.BottomLeftAreaCorner.x + 2,
                        Mathf.Max(room.BottomLeftAreaCorner.x + 3, room.TopRightAreaCorner.x - recessLength - 2)
                    );
                    RemoveCells(grid,
                        startX,
                        room.BottomLeftAreaCorner.y,
                        startX + recessLength,
                        room.BottomLeftAreaCorner.y + recessDepth);
                }
                break;

            case 1: // Top recess
                {
                    int startX = Random.Range(
                        room.BottomLeftAreaCorner.x + 2,
                        Mathf.Max(room.BottomLeftAreaCorner.x + 3, room.TopRightAreaCorner.x - recessLength - 2)
                    );
                    RemoveCells(grid,
                        startX,
                        room.TopRightAreaCorner.y - recessDepth,
                        startX + recessLength,
                        room.TopRightAreaCorner.y);
                }
                break;

            case 2: // Left recess
                {
                    int startY = Random.Range(
                        room.BottomLeftAreaCorner.y + 2,
                        Mathf.Max(room.BottomLeftAreaCorner.y + 3, room.TopRightAreaCorner.y - recessLength - 2)
                    );
                    RemoveCells(grid,
                        room.BottomLeftAreaCorner.x,
                        startY,
                        room.BottomLeftAreaCorner.x + recessDepth,
                        startY + recessLength);
                }
                break;

            case 3: // Right recess
                {
                    int startY = Random.Range(
                        room.BottomLeftAreaCorner.y + 2,
                        Mathf.Max(room.BottomLeftAreaCorner.y + 3, room.TopRightAreaCorner.y - recessLength - 2)
                    );
                    RemoveCells(grid,
                        room.TopRightAreaCorner.x - recessDepth,
                        startY,
                        room.TopRightAreaCorner.x,
                        startY + recessLength);
                }
                break;
        }
    }
}