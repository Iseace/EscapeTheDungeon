using UnityEngine;

/// <summary>
/// Represents a valid connection point on a room's edge where a corridor can attach
/// </summary>
public class ConnectionPoint
{
    public Vector2Int Position { get; private set; }
    public Direction Direction { get; private set; }
    public int QualityScore { get; private set; }
    public RoomNode ParentRoom { get; private set; }

    // The range of cells that this connection point covers
    public Vector2Int RangeStart { get; private set; }
    public Vector2Int RangeEnd { get; private set; }

    public ConnectionPoint(Vector2Int position, Direction direction, int qualityScore, RoomNode parentRoom, Vector2Int rangeStart, Vector2Int rangeEnd)
    {
        Position = position;
        Direction = direction;
        QualityScore = qualityScore;
        ParentRoom = parentRoom;
        RangeStart = rangeStart;
        RangeEnd = rangeEnd;
    }

    /// <summary>
    /// Check if this connection point can align with another
    /// </summary>
    public bool CanAlignWith(ConnectionPoint other)
    {
        // Connection points must face each other
        if (!AreOppositeDirections(this.Direction, other.Direction))
            return false;

        // Check if they can overlap in the perpendicular axis
        if (Direction == Direction.North || Direction == Direction.South)
        {
            // Horizontal alignment needed
            return !(RangeEnd.x < other.RangeStart.x || RangeStart.x > other.RangeEnd.x);
        }
        else
        {
            // Vertical alignment needed
            return !(RangeEnd.y < other.RangeStart.y || RangeStart.y > other.RangeEnd.y);
        }
    }

    private bool AreOppositeDirections(Direction dir1, Direction dir2)
    {
        return (dir1 == Direction.North && dir2 == Direction.South) ||
               (dir1 == Direction.South && dir2 == Direction.North) ||
               (dir1 == Direction.East && dir2 == Direction.West) ||
               (dir1 == Direction.West && dir2 == Direction.East);
    }

    public override string ToString()
    {
        return $"ConnectionPoint at {Position}, Direction: {Direction}, Quality: {QualityScore}";
    }
}

public enum Direction
{
    North,
    South,
    East,
    West
}
