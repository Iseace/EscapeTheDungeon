using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DugeonGenerator
{
    List<RoomNode> allNodesCollection = new List<RoomNode>(); // Collection of all room nodes in the dungeon tree
    private int dungeonWidth;
    private int dungeonLength;

    public DungeonGrid Grid { get; private set; }
    public List<RoomNode> RoomList { get; private set; }
    
    /// <summary>
    /// Gets the offset needed to center the dungeon at (0, 0, 0)
    /// </summary>
    public Vector3 GetCenterOffset()
    {
        return new Vector3(-dungeonWidth / 2f, 0, -dungeonLength / 2f);
    }
    
    public DugeonGenerator(int dungeonWidth, int dungeonLength)
    {
        this.dungeonWidth = dungeonWidth;
        this.dungeonLength = dungeonLength;
        Grid = new DungeonGrid(dungeonWidth, dungeonLength);
    }

    public List<Node> CalculateDungeon(
        int maxIterations,
        int roomWidthMin,
        int roomLengthMin,
        float roomBottomCornerModifier,
        float roomTopCornerMidifier,
        int roomOffset,
        int corridorWidth,
        RoomShapeConfig roomShapeConfig = null)
    {
        // Divide dungeon space using Binary Space Partitioning
        BinarySpacePartitioner bsp = new BinarySpacePartitioner(dungeonWidth, dungeonLength);
        allNodesCollection = bsp.PrepareNodesCollection(maxIterations, roomWidthMin, roomLengthMin);

        // Get the smallest divided spaces (leaf nodes)
        List<Node> roomSpaces = StructureHelper.TraverseGraphToExtractLowestLeafes(bsp.RootNode);

        // Generate actual rooms within those spaces
        RoomGenerator roomGenerator = new RoomGenerator(maxIterations, roomLengthMin, roomWidthMin);
        RoomList = roomGenerator.GenerateRoomsInGivenSpaces(
            roomSpaces,
            roomBottomCornerModifier,
            roomTopCornerMidifier,
            roomOffset
        );

        // Update grid with rooms (basic rectangular shape)
        UpdateGridWithRooms(RoomList);

        // Apply varied shapes to rooms if config is provided
        if (roomShapeConfig != null)
        {
            ApplyRoomShapes(RoomList, roomShapeConfig);
            // IMPORTANT: Recalculate room bounds after applying shapes
            UpdateRoomBounds(RoomList);
        }

        // Create corridors connecting the rooms using smart corridor generator
        SmartCorridorGenerator smartCorridorGen = new SmartCorridorGenerator(Grid, corridorWidth);
        var smartCorridors = smartCorridorGen.CreateCorridorsWithConnectionPoints(allNodesCollection);
        UpdateGridWithSmartCorridors(smartCorridors);

        return new List<Node>(RoomList);
    }

    /// <summary>
    /// Updates room bounds based on actual grid after applying shapes
    /// </summary>
    private void UpdateRoomBounds(List<RoomNode> rooms)
    {
        foreach (var room in rooms)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            
            bool foundAnyFloor = false;

            // Find actual bounds of Floor cells for this room
            for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
            {
                for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    GridCell cell = Grid.GetCell(pos);

                    if (cell != null && cell.Type == CellType.Floor && cell.ParentRoom == room)
                    {
                        foundAnyFloor = true;
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                    }
                }
            }

            // Update bounds if cells were found
            if (foundAnyFloor)
            {
                room.BottomLeftAreaCorner = new Vector2Int(minX, minY);
                room.TopRightAreaCorner = new Vector2Int(maxX + 1, maxY + 1);
                room.BottomRightAreaCorner = new Vector2Int(maxX + 1, minY);
                room.TopLeftAreaCorner = new Vector2Int(minX, maxY + 1);
            }
        }
    }

    private void ApplyRoomShapes(List<RoomNode> rooms, RoomShapeConfig config)
    {
        foreach (var room in rooms)
        {
            // Apply varied shape to each room
            RoomShapeModifier.ApplyShape(room, Grid, config);
        }
    }

    private void UpdateGridWithRooms(List<RoomNode> rooms)
    {
        foreach (var room in rooms)
        {
            for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
            {
                for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    Grid.SetCellType(pos, CellType.Floor, room);
                }
            }

            // Mark walls
            MarkWalls(room);
        }
    }

    private void MarkWalls(RoomNode room)
    {
        int leftX = room.BottomLeftAreaCorner.x - 1;
        int rightX = room.TopRightAreaCorner.x;
        int bottomY = room.BottomLeftAreaCorner.y - 1;
        int topY = room.TopRightAreaCorner.y;

        // Bottom wall (excluding corners)
        for (int x = room.BottomLeftAreaCorner.x + 1; x < room.TopRightAreaCorner.x - 1; x++)
        {
            Grid.SetCellType(new Vector2Int(x, bottomY), CellType.Wall);
        }

        // Top wall (excluding corners)
        for (int x = room.BottomLeftAreaCorner.x + 1; x < room.TopRightAreaCorner.x - 1; x++)
        {
            Grid.SetCellType(new Vector2Int(x, topY), CellType.Wall);
        }

        // Left wall (excluding corners)
        for (int y = room.BottomLeftAreaCorner.y + 1; y < room.TopRightAreaCorner.y - 1; y++)
        {
            Grid.SetCellType(new Vector2Int(leftX, y), CellType.Wall);
        }

        // Right wall (excluding corners)
        for (int y = room.BottomLeftAreaCorner.y + 1; y < room.TopRightAreaCorner.y - 1; y++)
        {
            Grid.SetCellType(new Vector2Int(rightX, y), CellType.Wall);
        }
    }



    /// <summary>
    /// Updates the grid with smart corridors generated from connection points
    /// </summary>
    private void UpdateGridWithSmartCorridors(List<CorridorData> corridors)
    {
        foreach (var corridor in corridors)
        {
            // Process each segment of the corridor
            foreach (var segment in corridor.PathSegments)
            {
                // Mark corridor floor cells
                for (int x = segment.Start.x; x <= segment.End.x; x++)
                {
                    for (int y = segment.Start.y; y <= segment.End.y; y++)
                    {
                        Vector2Int pos = new Vector2Int(x, y);
                        GridCell cell = Grid.GetCell(pos);

                        // Only mark if not already a room floor
                        if (cell != null && cell.Type != CellType.Floor)
                        {
                            Grid.SetCellType(pos, CellType.Corridor);
                        }
                    }
                }

                // Mark walls around the segment
                MarkSmartCorridorWalls(segment);
            }
        }
    }

    /// <summary>
    /// Marks walls around a corridor segment
    /// </summary>
    private void MarkSmartCorridorWalls(CorridorSegment segment)
    {
        if (segment.IsHorizontal)
        {
            // Mark walls above and below
            for (int x = segment.Start.x; x <= segment.End.x; x++)
            {
                // Bottom wall
                Vector2Int bottomPos = new Vector2Int(x, segment.Start.y - 1);
                GridCell bottomCell = Grid.GetCell(bottomPos);
                if (bottomCell != null && bottomCell.Type != CellType.Floor && bottomCell.Type != CellType.Corridor)
                {
                    Grid.SetCellType(bottomPos, CellType.Wall);
                }

                // Top wall
                Vector2Int topPos = new Vector2Int(x, segment.End.y + 1);
                GridCell topCell = Grid.GetCell(topPos);
                if (topCell != null && topCell.Type != CellType.Floor && topCell.Type != CellType.Corridor)
                {
                    Grid.SetCellType(topPos, CellType.Wall);
                }
            }
        }
        else
        {
            // Mark walls left and right
            for (int y = segment.Start.y; y <= segment.End.y; y++)
            {
                // Left wall
                Vector2Int leftPos = new Vector2Int(segment.Start.x - 1, y);
                GridCell leftCell = Grid.GetCell(leftPos);
                if (leftCell != null && leftCell.Type != CellType.Floor && leftCell.Type != CellType.Corridor)
                {
                    Grid.SetCellType(leftPos, CellType.Wall);
                }

                // Right wall
                Vector2Int rightPos = new Vector2Int(segment.End.x + 1, y);
                GridCell rightCell = Grid.GetCell(rightPos);
                if (rightCell != null && rightCell.Type != CellType.Floor && rightCell.Type != CellType.Corridor)
                {
                    Grid.SetCellType(rightPos, CellType.Wall);
                }
            }
        }
    }
}