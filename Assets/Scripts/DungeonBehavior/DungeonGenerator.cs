using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DugeonGenerator
{
    List<RoomNode> allNodesCollection = new List<RoomNode>();
    private int dungeonWidth;
    private int dungeonLength;

    // Nuevas propiedades
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
        RoomTypeConfiguration roomTypeConfig = null)
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

        // Asignar tipos a las habitaciones
        if (roomTypeConfig != null)
        {
            RoomTypeAssigner typeAssigner = new RoomTypeAssigner(roomTypeConfig);
            typeAssigner.AssignRoomTypes(RoomList);
        }

        // Actualizar el grid con las habitaciones
        UpdateGridWithRooms(RoomList);

        // Create corridors connecting the rooms
        CorridorsGenerator corridorGenerator = new CorridorsGenerator();
        var corridorList = corridorGenerator.CreateCorridor(allNodesCollection, corridorWidth);

        // Actualizar el grid con los corredores
        UpdateGridWithCorridors(corridorList);

        return new List<Node>(RoomList).Concat(corridorList).ToList();
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

            // Marcar paredes
            MarkWalls(room);
        }
    }

    private void MarkWalls(RoomNode room)
    {
        // Pared inferior
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            Grid.SetCellType(new Vector2Int(x, room.BottomLeftAreaCorner.y - 1), CellType.Wall);
        }

        // Pared superior
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            Grid.SetCellType(new Vector2Int(x, room.TopRightAreaCorner.y), CellType.Wall);
        }

        // Pared izquierda
        for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
        {
            Grid.SetCellType(new Vector2Int(room.BottomLeftAreaCorner.x - 1, y), CellType.Wall);
        }

        // Pared derecha
        for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
        {
            Grid.SetCellType(new Vector2Int(room.TopRightAreaCorner.x, y), CellType.Wall);
        }
    }

    private void UpdateGridWithCorridors(List<Node> corridors)
    {
        foreach (var corridor in corridors)
        {
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
        }
    }

    public RoomNode GetRoomByType(RoomType type)
    {
        return RoomList?.FirstOrDefault(r => r.RoomType == type);
    }

    public List<RoomNode> GetRoomsByType(RoomType type)
    {
        return RoomList?.Where(r => r.RoomType == type).ToList() ?? new List<RoomNode>();
    }
}