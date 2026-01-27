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

        // Create corridors connecting the rooms (NOW with updated bounds)
        CorridorsGenerator corridorGenerator = new CorridorsGenerator();
        var corridorList = corridorGenerator.CreateCorridor(allNodesCollection, corridorWidth);

        // Update grid with corridors
        UpdateGridWithCorridors(corridorList);

        return new List<Node>(RoomList).Concat(corridorList).ToList();
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

    private void UpdateGridWithCorridors(List<Node> corridors)
    {
        foreach (var corridor in corridors)
        {
            // Mark corridor cells
            for (int x = corridor.BottomLeftAreaCorner.x; x < corridor.TopRightAreaCorner.x; x++)
            {
                for (int y = corridor.BottomLeftAreaCorner.y; y < corridor.TopRightAreaCorner.y; y++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    GridCell cell = Grid.GetCell(pos);

                    if (cell != null && cell.Type != CellType.Floor)
                    {
                        Grid.SetCellType(pos, CellType.Corridor);
                    }
                }
            }

            // Mark corridor walls
            MarkCorridorWalls(corridor);
        }
    }

    private void MarkCorridorWalls(Node corridor)
    {
        int leftX = corridor.BottomLeftAreaCorner.x - 1;
        int rightX = corridor.TopRightAreaCorner.x;
        int bottomY = corridor.BottomLeftAreaCorner.y - 1;
        int topY = corridor.TopRightAreaCorner.y;

        // Bottom wall (excluding corners)
        for (int x = corridor.BottomLeftAreaCorner.x + 1; x < corridor.TopRightAreaCorner.x - 1; x++)
        {
            Vector2Int pos = new Vector2Int(x, bottomY);
            GridCell cell = Grid.GetCell(pos);
            
            // Only mark if not already a room
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
            }
        }

        // Top wall (excluding corners)
        for (int x = corridor.BottomLeftAreaCorner.x + 1; x < corridor.TopRightAreaCorner.x - 1; x++)
        {
            Vector2Int pos = new Vector2Int(x, topY);
            GridCell cell = Grid.GetCell(pos);
            
            // Only mark if not already a room
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
            }
        }

        // Left wall (excluding corners)
        for (int y = corridor.BottomLeftAreaCorner.y + 1; y < corridor.TopRightAreaCorner.y - 1; y++)
        {
            Vector2Int pos = new Vector2Int(leftX, y);
            GridCell cell = Grid.GetCell(pos);
            
            // Only mark if not already a room
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
            }
        }

        // Right wall (excluding corners)
        for (int y = corridor.BottomLeftAreaCorner.y + 1; y < corridor.TopRightAreaCorner.y - 1; y++)
        {
            Vector2Int pos = new Vector2Int(rightX, y);
            GridCell cell = Grid.GetCell(pos);
            
            // Only mark if not already a room
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
            }
        }
    }
}