using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Smart corridor generator that uses connection points to create corridors
/// Ensures minimum space requirements and avoids gaps in rooms
/// </summary>
public class SmartCorridorGenerator
{
    private DungeonGrid grid;
    private int corridorWidth;
    private const int MIN_CORRIDOR_WIDTH = 5;

    public SmartCorridorGenerator(DungeonGrid grid, int corridorWidth)
    {
        this.grid = grid;
        this.corridorWidth = Mathf.Max(MIN_CORRIDOR_WIDTH, corridorWidth);
    }

    /// <summary>
    /// Creates corridors between rooms using connection points
    /// </summary>
    public List<CorridorData> CreateCorridorsWithConnectionPoints(List<RoomNode> allNodesCollection)
    {
        List<CorridorData> corridors = new List<CorridorData>();

        // First, analyze all rooms and generate their connection points
        Dictionary<RoomNode, List<ConnectionPoint>> roomConnectionPoints = new Dictionary<RoomNode, List<ConnectionPoint>>();

        foreach (var node in allNodesCollection)
        {
            if (node.ChildrenNodeList.Count == 0) // Only analyze leaf rooms
            {
                List<ConnectionPoint> points = RoomConnectionAnalyzer.AnalyzeRoom(node, grid);
                roomConnectionPoints[node] = points;
            }
        }

        // Create corridors between connected rooms
        Queue<RoomNode> structuresToCheck = new Queue<RoomNode>(
            new List<RoomNode>(allNodesCollection)
        );

        while (structuresToCheck.Count > 0)
        {
            var node = structuresToCheck.Dequeue();
            
            if (node.ChildrenNodeList.Count == 0)
                continue;

            // Get the two children rooms
            Node child1 = node.ChildrenNodeList[0];
            Node child2 = node.ChildrenNodeList[1];

            // Get leaf rooms for both children
            List<Node> child1Rooms = StructureHelper.TraverseGraphToExtractLowestLeafes(child1);
            List<Node> child2Rooms = StructureHelper.TraverseGraphToExtractLowestLeafes(child2);

            // Find the best pair of rooms to connect
            CorridorData corridor = FindBestCorridorConnection(child1Rooms, child2Rooms, roomConnectionPoints);
            
            if (corridor != null)
            {
                corridors.Add(corridor);
            }
            else
            {
                Debug.LogWarning($"Failed to create corridor between children of node at layer {node.TreeLayerIndex}");
            }
        }

        return corridors;
    }

    /// <summary>
    /// Finds the best corridor connection between two sets of rooms
    /// </summary>
    private CorridorData FindBestCorridorConnection(List<Node> rooms1, List<Node> rooms2, 
        Dictionary<RoomNode, List<ConnectionPoint>> connectionPoints)
    {
        CorridorData bestCorridor = null;
        float bestDistance = float.MaxValue;

        foreach (Node room1Node in rooms1)
        {
            if (!(room1Node is RoomNode room1) || !connectionPoints.ContainsKey(room1))
                continue;

            foreach (Node room2Node in rooms2)
            {
                if (!(room2Node is RoomNode room2) || !connectionPoints.ContainsKey(room2))
                    continue;

                // Find best connection pair between these two rooms
                var (point1, point2) = RoomConnectionAnalyzer.FindBestConnectionPair(
                    connectionPoints[room1], connectionPoints[room2]);

                if (point1 != null && point2 != null)
                {
                    // Calculate corridor distance
                    float distance = Vector2Int.Distance(point1.Position, point2.Position);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestCorridor = CreateCorridorData(point1, point2, room1, room2);
                    }
                }
            }
        }

        return bestCorridor;
    }

    /// <summary>
    /// Creates corridor data from two connection points
    /// </summary>
    private CorridorData CreateCorridorData(ConnectionPoint point1, ConnectionPoint point2, 
        RoomNode room1, RoomNode room2)
    {
        CorridorData corridor = new CorridorData
        {
            StartRoom = room1,
            EndRoom = room2,
            StartPoint = point1,
            EndPoint = point2
        };

        // Determine corridor path based on connection point directions
        if (AreDirectlyAligned(point1, point2))
        {
            // Straight corridor
            corridor.PathSegments = CreateStraightPath(point1, point2);
            corridor.IsLShaped = false;
        }
        else
        {
            // L-shaped corridor
            corridor.PathSegments = CreateLShapedPath(point1, point2);
            corridor.IsLShaped = true;
        }

        return corridor;
    }

    /// <summary>
    /// Check if two connection points are directly aligned (can connect with straight corridor)
    /// </summary>
    private bool AreDirectlyAligned(ConnectionPoint point1, ConnectionPoint point2)
    {
        // Horizontal alignment
        if ((point1.Direction == Direction.East && point2.Direction == Direction.West) ||
            (point1.Direction == Direction.West && point2.Direction == Direction.East))
        {
            return Mathf.Abs(point1.Position.y - point2.Position.y) <= corridorWidth;
        }

        // Vertical alignment
        if ((point1.Direction == Direction.North && point2.Direction == Direction.South) ||
            (point1.Direction == Direction.South && point2.Direction == Direction.North))
        {
            return Mathf.Abs(point1.Position.x - point2.Position.x) <= corridorWidth;
        }

        return false;
    }

    /// <summary>
    /// Creates a straight corridor path between two aligned points
    /// </summary>
    private List<CorridorSegment> CreateStraightPath(ConnectionPoint point1, ConnectionPoint point2)
    {
        List<CorridorSegment> segments = new List<CorridorSegment>();

        Vector2Int start = point1.Position;
        Vector2Int end = point2.Position;

        // Calculate the corridor bounds
        if (point1.Direction == Direction.East || point1.Direction == Direction.West)
        {
            // Horizontal corridor
            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int centerY = (start.y + end.y) / 2;

            segments.Add(new CorridorSegment
            {
                Start = new Vector2Int(minX, centerY - corridorWidth / 2),
                End = new Vector2Int(maxX, centerY + corridorWidth / 2),
                IsHorizontal = true
            });
        }
        else
        {
            // Vertical corridor
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);
            int centerX = (start.x + end.x) / 2;

            segments.Add(new CorridorSegment
            {
                Start = new Vector2Int(centerX - corridorWidth / 2, minY),
                End = new Vector2Int(centerX + corridorWidth / 2, maxY),
                IsHorizontal = false
            });
        }

        return segments;
    }

    /// <summary>
    /// Creates an L-shaped corridor path between two non-aligned points
    /// </summary>
    private List<CorridorSegment> CreateLShapedPath(ConnectionPoint point1, ConnectionPoint point2)
    {
        List<CorridorSegment> segments = new List<CorridorSegment>();

        // Determine the corner position
        Vector2Int cornerPos = CalculateCornerPosition(point1, point2);

        // Create first segment (from point1 to corner) - uses point1's direction
        segments.Add(CreateSegmentToCorner(point1.Position, cornerPos, point1.Direction));

        // Create second segment (from corner to point2) - direction is perpendicular to point1's direction
        Direction secondSegmentDirection = GetPerpendicularDirection(point1.Direction);
        segments.Add(CreateSegmentToCorner(cornerPos, point2.Position, secondSegmentDirection));

        return segments;
    }

    private Direction GetPerpendicularDirection(Direction dir)
    {
        // If the first segment is vertical (North/South), the second must be horizontal
        if (dir == Direction.North || dir == Direction.South)
        {
            return Direction.East; // Use East to represent horizontal direction
        }
        else
        {
            return Direction.North; // Use North to represent vertical direction
        }
    }

    private Vector2Int CalculateCornerPosition(ConnectionPoint point1, ConnectionPoint point2)
    {
        // The corner is typically at the intersection of the two directions
        if (point1.Direction == Direction.North || point1.Direction == Direction.South)
        {
            // Point1 is vertical, point2 is horizontal
            return new Vector2Int(point1.Position.x, point2.Position.y);
        }
        else
        {
            // Point1 is horizontal, point2 is vertical
            return new Vector2Int(point2.Position.x, point1.Position.y);
        }
    }

    private CorridorSegment CreateSegmentToCorner(Vector2Int start, Vector2Int corner, Direction direction)
    {
        CorridorSegment segment = new CorridorSegment();

        if (direction == Direction.North || direction == Direction.South)
        {
            // Vertical segment
            int minY = Mathf.Min(start.y, corner.y);
            int maxY = Mathf.Max(start.y, corner.y);
            int centerX = start.x;

            segment.Start = new Vector2Int(centerX - corridorWidth / 2, minY);
            segment.End = new Vector2Int(centerX + corridorWidth / 2, maxY);
            segment.IsHorizontal = false;
        }
        else
        {
            // Horizontal segment
            int minX = Mathf.Min(start.x, corner.x);
            int maxX = Mathf.Max(start.x, corner.x);
            int centerY = start.y;

            segment.Start = new Vector2Int(minX, centerY - corridorWidth / 2);
            segment.End = new Vector2Int(maxX, centerY + corridorWidth / 2);
            segment.IsHorizontal = true;
        }

        return segment;
    }
}

/// <summary>
/// Data structure representing a complete corridor connection
/// </summary>
public class CorridorData
{
    public RoomNode StartRoom { get; set; }
    public RoomNode EndRoom { get; set; }
    public ConnectionPoint StartPoint { get; set; }
    public ConnectionPoint EndPoint { get; set; }
    public List<CorridorSegment> PathSegments { get; set; }
    public bool IsLShaped { get; set; }

    public CorridorData()
    {
        PathSegments = new List<CorridorSegment>();
    }
}

/// <summary>
/// Represents a segment of a corridor (straight line)
/// </summary>
public class CorridorSegment
{
    public Vector2Int Start { get; set; }
    public Vector2Int End { get; set; }
    public bool IsHorizontal { get; set; }

    public override string ToString()
    {
        return $"Segment from {Start} to {End} ({(IsHorizontal ? "Horizontal" : "Vertical")})";
    }
}
