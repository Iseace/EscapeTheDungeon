using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Analyzes a room and generates valid connection points on its edges
/// </summary>
public static class RoomConnectionAnalyzer
{
    private const int MIN_CONNECTION_WIDTH = 5; // Minimum continuous space required for corridor connection
    private const int DISTANCE_FROM_CORNER = 2; // Minimum distance from room corners

    /// <summary>
    /// Analyzes a room and returns all valid connection points
    /// </summary>
    public static List<ConnectionPoint> AnalyzeRoom(RoomNode room, DungeonGrid grid)
    {
        List<ConnectionPoint> connectionPoints = new List<ConnectionPoint>();

        // Analyze each edge of the room
        connectionPoints.AddRange(AnalyzeNorthEdge(room, grid));
        connectionPoints.AddRange(AnalyzeSouthEdge(room, grid));
        connectionPoints.AddRange(AnalyzeEastEdge(room, grid));
        connectionPoints.AddRange(AnalyzeWestEdge(room, grid));

        return connectionPoints;
    }

    private static List<ConnectionPoint> AnalyzeNorthEdge(RoomNode room, DungeonGrid grid)
    {
        List<ConnectionPoint> points = new List<ConnectionPoint>();
        int edgeY = room.TopRightAreaCorner.y - 1; // Top edge of the room floor

        int startX = room.BottomLeftAreaCorner.x + DISTANCE_FROM_CORNER;
        int endX = room.TopRightAreaCorner.x - DISTANCE_FROM_CORNER;

        FindConnectionSegments(startX, endX, edgeY, true, Direction.North, room, grid, points);

        return points;
    }

    private static List<ConnectionPoint> AnalyzeSouthEdge(RoomNode room, DungeonGrid grid)
    {
        List<ConnectionPoint> points = new List<ConnectionPoint>();
        int edgeY = room.BottomLeftAreaCorner.y; // Bottom edge of the room floor

        int startX = room.BottomLeftAreaCorner.x + DISTANCE_FROM_CORNER;
        int endX = room.TopRightAreaCorner.x - DISTANCE_FROM_CORNER;

        FindConnectionSegments(startX, endX, edgeY, true, Direction.South, room, grid, points);

        return points;
    }

    private static List<ConnectionPoint> AnalyzeEastEdge(RoomNode room, DungeonGrid grid)
    {
        List<ConnectionPoint> points = new List<ConnectionPoint>();
        int edgeX = room.TopRightAreaCorner.x - 1; // Right edge of the room floor

        int startY = room.BottomLeftAreaCorner.y + DISTANCE_FROM_CORNER;
        int endY = room.TopRightAreaCorner.y - DISTANCE_FROM_CORNER;

        FindConnectionSegments(startY, endY, edgeX, false, Direction.East, room, grid, points);

        return points;
    }

    private static List<ConnectionPoint> AnalyzeWestEdge(RoomNode room, DungeonGrid grid)
    {
        List<ConnectionPoint> points = new List<ConnectionPoint>();
        int edgeX = room.BottomLeftAreaCorner.x; // Left edge of the room floor

        int startY = room.BottomLeftAreaCorner.y + DISTANCE_FROM_CORNER;
        int endY = room.TopRightAreaCorner.y - DISTANCE_FROM_CORNER;

        FindConnectionSegments(startY, endY, edgeX, false, Direction.West, room, grid, points);

        return points;
    }

    /// <summary>
    /// Finds continuous segments of valid floor cells along an edge
    /// </summary>
    private static void FindConnectionSegments(int start, int end, int fixedCoord, bool isHorizontal, 
        Direction direction, RoomNode room, DungeonGrid grid, List<ConnectionPoint> points)
    {
        int segmentStart = -1;
        int segmentLength = 0;

        for (int i = start; i < end; i++)
        {
            Vector2Int checkPos = isHorizontal ? new Vector2Int(i, fixedCoord) : new Vector2Int(fixedCoord, i);
            GridCell cell = grid.GetCell(checkPos);

            // Check if this cell is valid floor space
            if (cell != null && cell.Type == CellType.Floor && cell.ParentRoom == room)
            {
                if (segmentStart == -1)
                {
                    segmentStart = i;
                    segmentLength = 1;
                }
                else
                {
                    segmentLength++;
                }
            }
            else
            {
                // End of a segment, check if it's valid
                if (segmentLength >= MIN_CONNECTION_WIDTH)
                {
                    CreateConnectionPoint(segmentStart, segmentLength, fixedCoord, isHorizontal, 
                        direction, room, points);
                }
                segmentStart = -1;
                segmentLength = 0;
            }
        }

        // Check the last segment
        if (segmentLength >= MIN_CONNECTION_WIDTH)
        {
            CreateConnectionPoint(segmentStart, segmentLength, fixedCoord, isHorizontal, 
                direction, room, points);
        }
    }

    /// <summary>
    /// Creates a connection point from a valid segment
    /// </summary>
    private static void CreateConnectionPoint(int segmentStart, int segmentLength, int fixedCoord, 
        bool isHorizontal, Direction direction, RoomNode room, List<ConnectionPoint> points)
    {
        // Calculate the center position of the segment
        int centerOffset = segmentStart + segmentLength / 2;
        
        Vector2Int position = isHorizontal 
            ? new Vector2Int(centerOffset, fixedCoord) 
            : new Vector2Int(fixedCoord, centerOffset);

        Vector2Int rangeStart = isHorizontal 
            ? new Vector2Int(segmentStart, fixedCoord) 
            : new Vector2Int(fixedCoord, segmentStart);

        Vector2Int rangeEnd = isHorizontal 
            ? new Vector2Int(segmentStart + segmentLength - 1, fixedCoord) 
            : new Vector2Int(fixedCoord, segmentStart + segmentLength - 1);

        // Quality score based on segment length (longer = better)
        int qualityScore = Mathf.Min(100, segmentLength * 10);

        ConnectionPoint point = new ConnectionPoint(position, direction, qualityScore, room, rangeStart, rangeEnd);
        points.Add(point);
    }

    /// <summary>
    /// Gets the best connection point from a list based on quality score
    /// </summary>
    public static ConnectionPoint GetBestConnectionPoint(List<ConnectionPoint> points)
    {
        if (points == null || points.Count == 0)
            return null;

        ConnectionPoint best = points[0];
        foreach (var point in points)
        {
            if (point.QualityScore > best.QualityScore)
                best = point;
        }
        return best;
    }

    /// <summary>
    /// Finds the best pair of connection points between two rooms
    /// </summary>
    public static (ConnectionPoint point1, ConnectionPoint point2) FindBestConnectionPair(
        List<ConnectionPoint> room1Points, List<ConnectionPoint> room2Points)
    {
        ConnectionPoint bestPoint1 = null;
        ConnectionPoint bestPoint2 = null;
        int bestScore = -1;

        foreach (var point1 in room1Points)
        {
            foreach (var point2 in room2Points)
            {
                if (point1.CanAlignWith(point2))
                {
                    int combinedScore = point1.QualityScore + point2.QualityScore;
                    
                    // Bonus for better alignment
                    int alignmentBonus = CalculateAlignmentBonus(point1, point2);
                    combinedScore += alignmentBonus;

                    if (combinedScore > bestScore)
                    {
                        bestScore = combinedScore;
                        bestPoint1 = point1;
                        bestPoint2 = point2;
                    }
                }
            }
        }

        return (bestPoint1, bestPoint2);
    }

    private static int CalculateAlignmentBonus(ConnectionPoint point1, ConnectionPoint point2)
    {
        // Calculate how well aligned the points are
        if (point1.Direction == Direction.North || point1.Direction == Direction.South)
        {
            // Horizontal alignment - check X overlap
            int overlapStart = Mathf.Max(point1.RangeStart.x, point2.RangeStart.x);
            int overlapEnd = Mathf.Min(point1.RangeEnd.x, point2.RangeEnd.x);
            int overlap = Mathf.Max(0, overlapEnd - overlapStart + 1);
            return overlap * 5; // Bonus for overlap width
        }
        else
        {
            // Vertical alignment - check Y overlap
            int overlapStart = Mathf.Max(point1.RangeStart.y, point2.RangeStart.y);
            int overlapEnd = Mathf.Min(point1.RangeEnd.y, point2.RangeEnd.y);
            int overlap = Mathf.Max(0, overlapEnd - overlapStart + 1);
            return overlap * 5; // Bonus for overlap height
        }
    }
}
