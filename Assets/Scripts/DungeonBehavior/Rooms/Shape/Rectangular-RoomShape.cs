using UnityEngine;

public class RectangularRoomShape : BaseRoomShape
{
    public override void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        // Room stays as original rectangle
    }

    public override bool CanApply(int width, int height)
    {
        // Can always apply rectangular shape
        return true;
    }
}