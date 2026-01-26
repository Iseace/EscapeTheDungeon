using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DugeonGenerator
{
    List<RoomNode> allNodesCollection = new List<RoomNode>(); // Collection of all room nodes in the dungeon tree
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
        RoomTypeConfiguration roomTypeConfig = null,
        RoomShapeConfig roomShapeConfig = null,
        PrefabRoomConfiguration prefabRoomConfig = null)
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

        // Asignar prefabs a las habitaciones (antes de actualizar grid)
        if (prefabRoomConfig != null)
        {
            PrefabRoomApplicator prefabApplicator = new PrefabRoomApplicator(prefabRoomConfig);
            prefabApplicator.AssignPrefabsToRooms(RoomList);
        }

        // Actualizar el grid con las habitaciones (forma rectangular básica)
        UpdateGridWithRooms(RoomList);

        // Aplicar formas variadas a las habitaciones (solo a las que NO tienen prefab)
        if (roomShapeConfig != null)
        {
            ApplyRoomShapes(RoomList, roomShapeConfig);
            // IMPORTANTE: Recalcular los límites de las habitaciones después de aplicar formas
            UpdateRoomBounds(RoomList);
        }

        // Create corridors connecting the rooms (AHORA con los límites actualizados)
        CorridorsGenerator corridorGenerator = new CorridorsGenerator();
        var corridorList = corridorGenerator.CreateCorridor(allNodesCollection, corridorWidth);

        // Actualizar el grid con los corredores
        UpdateGridWithCorridors(corridorList);

        return new List<Node>(RoomList).Concat(corridorList).ToList();
    }

    /// <summary>
    /// Actualiza los límites de las habitaciones basándose en el grid real después de aplicar formas
    /// </summary>
    private void UpdateRoomBounds(List<RoomNode> rooms)
    {
        foreach (var room in rooms)
        {
            // Si tiene prefab asignado, no recalcular
            if (room.AssignedPrefab != null)
                continue;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            
            bool foundAnyFloor = false;

            // Buscar los límites reales de las celdas Floor de esta habitación
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

            // Actualizar los límites si se encontraron celdas
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
            // No aplicar formas variadas a habitaciones con prefab asignado
            if (room.AssignedPrefab != null)
                continue;
                
            // Aplicar forma variada a cada habitación
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

            // Marcar paredes
            MarkWalls(room);
        }
    }

    private void MarkWalls(RoomNode room)
    {
        int leftX = room.BottomLeftAreaCorner.x - 1;
        int rightX = room.TopRightAreaCorner.x;
        int bottomY = room.BottomLeftAreaCorner.y - 1;
        int topY = room.TopRightAreaCorner.y;

        // Pared inferior (desde x+1 hasta x-2 para excluir esquinas)
        for (int x = room.BottomLeftAreaCorner.x + 1; x < room.TopRightAreaCorner.x - 1; x++)
        {
            Grid.SetCellType(new Vector2Int(x, bottomY), CellType.Wall);
        }

        // Pared superior (desde x+1 hasta x-2 para excluir esquinas)
        for (int x = room.BottomLeftAreaCorner.x + 1; x < room.TopRightAreaCorner.x - 1; x++)
        {
            Grid.SetCellType(new Vector2Int(x, topY), CellType.Wall);
        }

        // Pared izquierda (desde y+1 hasta y-2 para excluir esquinas)
        for (int y = room.BottomLeftAreaCorner.y + 1; y < room.TopRightAreaCorner.y - 1; y++)
        {
            Grid.SetCellType(new Vector2Int(leftX, y), CellType.Wall);
        }

        // Pared derecha (desde y+1 hasta y-2 para excluir esquinas)
        for (int y = room.BottomLeftAreaCorner.y + 1; y < room.TopRightAreaCorner.y - 1; y++)
        {
            Grid.SetCellType(new Vector2Int(rightX, y), CellType.Wall);
        }
    }

    private void UpdateGridWithCorridors(List<Node> corridors)
    {
        foreach (var corridor in corridors)
        {
            // Marcar el corredor
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

            // Marcar paredes del corredor
            MarkCorridorWalls(corridor);
        }
    }

    private void MarkCorridorWalls(Node corridor)
    {
        int leftX = corridor.BottomLeftAreaCorner.x - 1;
        int rightX = corridor.TopRightAreaCorner.x;
        int bottomY = corridor.BottomLeftAreaCorner.y - 1;
        int topY = corridor.TopRightAreaCorner.y;

        // Pared inferior (desde x+1 hasta x-2 para excluir esquinas)
        for (int x = corridor.BottomLeftAreaCorner.x + 1; x < corridor.TopRightAreaCorner.x - 1; x++)
        {
            Vector2Int pos = new Vector2Int(x, bottomY);
            GridCell cell = Grid.GetCell(pos);
            
            // Solo marcar si no es ya una habitación
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
            }
        }

        // Pared superior (desde x+1 hasta x-2 para excluir esquinas)
        for (int x = corridor.BottomLeftAreaCorner.x + 1; x < corridor.TopRightAreaCorner.x - 1; x++)
        {
            Vector2Int pos = new Vector2Int(x, topY);
            GridCell cell = Grid.GetCell(pos);
            
            // Solo marcar si no es ya una habitación
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
            }
        }

        // Pared izquierda (desde y+1 hasta y-2 para excluir esquinas)
        for (int y = corridor.BottomLeftAreaCorner.y + 1; y < corridor.TopRightAreaCorner.y - 1; y++)
        {
            Vector2Int pos = new Vector2Int(leftX, y);
            GridCell cell = Grid.GetCell(pos);
            
            // Solo marcar si no es ya una habitación
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
            }
        }

        // Pared derecha (desde y+1 hasta y-2 para excluir esquinas)
        for (int y = corridor.BottomLeftAreaCorner.y + 1; y < corridor.TopRightAreaCorner.y - 1; y++)
        {
            Vector2Int pos = new Vector2Int(rightX, y);
            GridCell cell = Grid.GetCell(pos);
            
            // Solo marcar si no es ya una habitación
            if (cell != null && cell.Type != CellType.Floor)
            {
                Grid.SetCellType(pos, CellType.Wall);
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