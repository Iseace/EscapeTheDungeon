using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class DungeonCreator : MonoBehaviour
{
    [HideInInspector]
    public bool useRandomSeed = true;

    [Header("Seed Settings")]
    public int seed = 0;
    [SerializeField] private int lastUsedSeed;

    [Header("Dungeon Settings")]
    public int dungeonWidth = 100;
    public int dungeonLength = 100;
    public int roomWidthMin = 10;
    public int roomLengthMin = 10;
    public int wallHeight = 3;
    public int corridorWidth = 5;
    public int maxIterations = 10;

    [Range(0.0f, 0.3f)]
    public float roomBottomCornerModifier = 0.1f;
    [Range(0.7f, 1.0f)]
    public float roomTopCornerMidifier = 0.9f;
    [Range(0, 2)]
    public int roomOffset = 1;

    [Header("Floor Textures")]
    public Material material;
    
    [Header("Wall Generation")]
    public GameObject wallPrefab;
    [HideInInspector]
    public bool useProceduralWalls = true;
    public Material wallMaterial;
    
    [HideInInspector]
    public bool useCornerPillars = true;
    
    [Header("Corner Pillars")]
    public Material pillarMaterial;

    [Tooltip("Recomended values between 0.3 and 1.0")]
    [Range(0.3f, 2.0f)]
    public float cornerPillarSize = 0.6f;

    [Header("Room Types")]
    public RoomTypeConfiguration roomTypeConfiguration;
    public bool enableRoomTypes = true;

    [Header("Room Shapes")]
    public bool enableVariedShapes = false; //Change to true in release
    
    public RoomShapeConfig roomShapeConfig = new RoomShapeConfig();

    [Header("Prefab Rooms")]
    public bool enablePrefabRooms = false;
    public PrefabRoomConfiguration prefabRoomConfig;

    [Header("Procedural Objects")]
    public bool spawnObjects = true;
    public List<SpawnableObject> genericObjects = new List<SpawnableObject>();

    [Header("Debug")]
    public bool showRoomTypes = true;
    public bool showGrid = false;

    private DugeonGenerator generator;
    private ProceduralObjectSpawner objectSpawner;
    private List<Vector3Int> possibleDoorVerticalPosition;
    private List<Vector3Int> possibleDoorHorizontalPosition;
    private List<Vector3Int> possibleWallHorizontalPosition;
    private List<Vector3Int> possibleWallVerticalPosition;

    void Start()
    {  // DungeonNetworkRunner will handle dungeon creation
       // CreateDungeon();
    }

    public void CreateDungeon()
    {
        if (useRandomSeed)
        {
            seed = Random.Range(0, int.MaxValue);
        }

        lastUsedSeed = seed;
        Random.InitState(seed);

        DestroyAllChildren();

        generator = new DugeonGenerator(dungeonWidth, dungeonLength);

        var listOfRooms = generator.CalculateDungeon(
            maxIterations,
            roomWidthMin,
            roomLengthMin,
            roomBottomCornerModifier,
            roomTopCornerMidifier,
            roomOffset,
            corridorWidth,
            enableRoomTypes ? roomTypeConfiguration : null,
            enableVariedShapes ? roomShapeConfig : null,
            enablePrefabRooms ? prefabRoomConfig : null
        );

        GameObject wallParent = new GameObject("WallParent");
        wallParent.transform.parent = transform;

        GameObject objectParent = new GameObject("ObjectParent");
        objectParent.transform.parent = transform;

        possibleDoorVerticalPosition = new List<Vector3Int>();
        possibleDoorHorizontalPosition = new List<Vector3Int>();
        possibleWallHorizontalPosition = new List<Vector3Int>();
        possibleWallVerticalPosition = new List<Vector3Int>();

        for (int i = 0; i < listOfRooms.Count; i++)
        {
            if (listOfRooms[i] is RoomNode roomNode)
            {
                CreateFloorMeshFromGrid(roomNode);

                if (roomNode.AssignedPrefab != null)
                {
                    PrefabRoomApplicator.InstantiatePrefabRoom(roomNode, objectParent.transform);
                }

                else if (roomNode.RoomTypeData != null)
                {
                    ApplyRoomPrefab(roomNode, objectParent.transform);
                }
            }
            else
            {
                CreateMesh(listOfRooms[i].BottomLeftAreaCorner, listOfRooms[i].TopRightAreaCorner);
            }
        }

        CreateWalls(wallParent);

        if (spawnObjects && generator.RoomList != null)
        {
            objectSpawner = new ProceduralObjectSpawner(generator.Grid, objectParent.transform);
            SpawnAllObjects();
        }

        if (showRoomTypes)
        {
            VisualizeRoomTypes();
        }
    }

    private void ApplyRoomPrefab(RoomNode room, Transform parent)
    {
        if (room.RoomTypeData.roomPrefab != null)
        {
            Vector3 roomCenter = new Vector3(
                (room.BottomLeftAreaCorner.x + room.TopRightAreaCorner.x) / 2f,
                0,
                (room.BottomLeftAreaCorner.y + room.TopRightAreaCorner.y) / 2f
            );

            GameObject prefabInstance = Instantiate(
                room.RoomTypeData.roomPrefab,
                roomCenter,
                Quaternion.identity,
                parent
            );

            prefabInstance.name = $"{room.RoomType}_Prefab_{room.RoomID}";
        }
    }

    private void SpawnAllObjects()
    {
        foreach (var room in generator.RoomList)
        {
            objectSpawner.SpawnObjectsInRoom(room);

            if (genericObjects.Count > 0)
            {
                objectSpawner.SpawnObjects(room, genericObjects);
            }

            SpawnSpecialObjectsForRoomType(room);
        }
    }

    private void SpawnSpecialObjectsForRoomType(RoomNode room)
    {
        switch (room.RoomType)
        {
            case RoomType.Boss:
                Debug.Log($"Boss room at {room.RoomID}");
                break;

            case RoomType.Normal:
                Debug.Log($"Normal room at {room.RoomID}");
                break;

            case RoomType.Start:
                Debug.Log($"Start room at {room.RoomID}");
                break;
        }
    }

    private void VisualizeRoomTypes()
    {
        if (generator.RoomList == null) return;

        foreach (var room in generator.RoomList)
        {
            Color roomColor = room.RoomTypeData?.debugColor ?? Color.white;
            Vector3 center = new Vector3(
                (room.BottomLeftAreaCorner.x + room.TopRightAreaCorner.x) / 2f,
                0.1f,
                (room.BottomLeftAreaCorner.y + room.TopRightAreaCorner.y) / 2f
            );

            Debug.DrawLine(
                new Vector3(room.BottomLeftAreaCorner.x, 0.1f, room.BottomLeftAreaCorner.y),
                new Vector3(room.TopRightAreaCorner.x, 0.1f, room.BottomLeftAreaCorner.y),
                roomColor, 100f
            );
        }
    }

    public void CreateDungeonRandom()
    {
        useRandomSeed = true;
        CreateDungeon();
    }

   public void CreateDungeonWithSeed(int specificSeed)
{
    Debug.Log($"CreateDungeonWithSeed called with seed: {specificSeed}");
    useRandomSeed = false;
    seed = specificSeed;
    CreateDungeon();
    
    Debug.Log($"CreateDungeon() completed. Final seed used: {seed}");
}

    private void CreateWalls(GameObject wallParent)
    {
        if (!useProceduralWalls || generator?.Grid == null)
            return;

        DungeonGrid grid = generator.Grid;
        var cells = grid.GetAllCells();

        List<Vector3Int> horizontalWalls = new();
        List<Vector3Int> verticalWalls = new();
        HashSet<Vector3Int> doors = new();

        foreach (var kvp in cells)
        {
            Vector2Int p = kvp.Key;
            GridCell cell = kvp.Value;

            if (!WallCellAnalyzer.IsWalkable(cell))
                continue;

            if (!WallCellAnalyzer.IsWalkable(grid.GetCell(p + Vector2Int.up)))
                horizontalWalls.Add(new Vector3Int(p.x, 0, p.y + 1));

            if (!WallCellAnalyzer.IsWalkable(grid.GetCell(p + Vector2Int.down)))
                horizontalWalls.Add(new Vector3Int(p.x, 0, p.y));

            if (!WallCellAnalyzer.IsWalkable(grid.GetCell(p + Vector2Int.right)))
                verticalWalls.Add(new Vector3Int(p.x + 1, 0, p.y));

            if (!WallCellAnalyzer.IsWalkable(grid.GetCell(p + Vector2Int.left)))
                verticalWalls.Add(new Vector3Int(p.x, 0, p.y));
        }

        ProceduralWallGenerator wallGenerator =
            new ProceduralWallGenerator(wallHeight, wallMaterial);

        wallGenerator.GenerateWalls(
            horizontalWalls,
            verticalWalls,
            doors,
            wallParent.transform
        );

        if (useCornerPillars)
        {
          HashSet<Vector3Int> allCorners =
              WallCellAnalyzer.DetectCorners(grid, cells);

          CornerPillarGenerator pillarGenerator =
              new CornerPillarGenerator(
                  wallHeight,
                  cornerPillarSize,
                  pillarMaterial  
              );

          pillarGenerator.GeneratePillars(
              allCorners,
              wallParent.transform
          );
        }
    }

    private void CreateWall(GameObject wallParent, Vector3Int wallPosition, float yRotation)
    {
        Quaternion rotation = Quaternion.Euler(0, yRotation, 0);
        GameObject wall = Instantiate(wallPrefab, wallPosition, rotation, wallParent.transform);

        Vector3 scale = wall.transform.localScale;
        scale.y = wallHeight;
        wall.transform.localScale = scale;
    }

    private void CreateFloorMeshFromGrid(RoomNode room)
    {
        if (generator?.Grid == null)
        {
            CreateMesh(room.BottomLeftAreaCorner, room.TopRightAreaCorner);
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                GridCell cell = generator.Grid.GetCell(pos);

                if (cell != null && cell.Type == CellType.Floor)
                {
                    int vertexIndex = vertices.Count;

                    vertices.Add(new Vector3(x, 0, y));
                    vertices.Add(new Vector3(x + 1, 0, y));
                    vertices.Add(new Vector3(x, 0, y + 1));  
                    vertices.Add(new Vector3(x + 1, 0, y + 1)); 

                    uvs.Add(new Vector2(x, y));
                    uvs.Add(new Vector2(x + 1, y));
                    uvs.Add(new Vector2(x, y + 1));
                    uvs.Add(new Vector2(x + 1, y + 1));

                    triangles.Add(vertexIndex + 2); // Top-left
                    triangles.Add(vertexIndex + 3); // Top-right
                    triangles.Add(vertexIndex + 0); // Bottom-left

                    triangles.Add(vertexIndex + 0); // Bottom-left
                    triangles.Add(vertexIndex + 3); // Top-right
                    triangles.Add(vertexIndex + 1); // Bottom-right
                }
            }
        }

        if (vertices.Count == 0)
            return;

        Mesh mesh = new Mesh();
        mesh.name = $"RoomFloor_{room.RoomID}";
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        GameObject floorObject = new GameObject(
            $"Floor_{room.RoomType}_{room.RoomID}",
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(MeshCollider)
        );

        floorObject.transform.position = Vector3.zero;
        floorObject.transform.localScale = Vector3.one;
        floorObject.GetComponent<MeshFilter>().mesh = mesh;
        floorObject.GetComponent<MeshRenderer>().material = material;
        floorObject.GetComponent<MeshCollider>().sharedMesh = mesh;
        floorObject.transform.parent = transform;
    }

    private void CreateMesh(Vector2 bottomLeftCorner, Vector2 topRightCorner)
    {
        Vector3 bottomLeftV = new Vector3(bottomLeftCorner.x, 0, bottomLeftCorner.y);
        Vector3 bottomRightV = new Vector3(topRightCorner.x, 0, bottomLeftCorner.y);
        Vector3 topLeftV = new Vector3(bottomLeftCorner.x, 0, topRightCorner.y);
        Vector3 topRightV = new Vector3(topRightCorner.x, 0, topRightCorner.y);

        Vector3[] vertices = new Vector3[]
        {
            topLeftV, topRightV, bottomLeftV, bottomRightV
        };

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        int[] triangles = new int[] { 0, 1, 2, 2, 1, 3 };

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        GameObject dungeonFloor = new GameObject(
            "Mesh" + bottomLeftCorner,
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(MeshCollider)
        );

        dungeonFloor.transform.position = Vector3.zero;
        dungeonFloor.transform.localScale = Vector3.one;
        dungeonFloor.GetComponent<MeshFilter>().mesh = mesh;
        dungeonFloor.GetComponent<MeshRenderer>().material = material;
        dungeonFloor.GetComponent<MeshCollider>().sharedMesh = mesh;
        dungeonFloor.transform.parent = transform;

        for (int row = (int)bottomLeftV.x; row <= (int)bottomRightV.x; row++)
        {
            var wallPosition = new Vector3(row, 0, bottomLeftV.z);
            AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int row = (int)topLeftV.x; row <= (int)topRightCorner.x; row++)
        {
            var wallPosition = new Vector3(row, 0, topRightV.z);
            AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int col = (int)bottomLeftV.z; col <= (int)topLeftV.z; col++)
        {
            var wallPosition = new Vector3(bottomLeftV.x, 0, col);
            AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
        for (int col = (int)bottomRightV.z; col <= (int)topRightV.z; col++)
        {
            var wallPosition = new Vector3(bottomRightV.x, 0, col);
            AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
    }

    private void AddWallPositionToList(Vector3 wallPosition, List<Vector3Int> wallList, List<Vector3Int> doorList)
    {
        Vector3Int point = Vector3Int.CeilToInt(wallPosition);
        if (wallList.Contains(point))
        {
            doorList.Add(point);
            wallList.Remove(point);
        }
        else
        {
            wallList.Add(point);
        }
    }

    public void DestroyAllChildren()
    {
        while (transform.childCount != 0)
        {
            foreach (Transform item in transform)
            {
                DestroyImmediate(item.gameObject);
            }
        }
    }

    public RoomNode GetStartRoom()
    {
        return generator?.GetRoomByType(RoomType.Start);
    }

    public RoomNode GetBossRoom()
    {
        return generator?.GetRoomByType(RoomType.Boss);
    }

    public DungeonGrid GetGrid()
    {
        return generator?.Grid;
    }

    private void OnDrawGizmos()
    {
        if (!showGrid || generator?.Grid == null) return;

        var allCells = generator.Grid.GetAllCells();
        foreach (var kvp in allCells)
        {
            Vector3 pos = new Vector3(kvp.Key.x + 0.5f, 0.1f, kvp.Key.y + 0.5f);

            switch (kvp.Value.Type)
            {
                case CellType.Floor:
                    Gizmos.color = new Color(0, 1, 0, 0.2f);
                    break;
                case CellType.Wall:
                    Gizmos.color = new Color(1, 0, 0, 0.2f);
                    break;
                case CellType.Corridor:
                    Gizmos.color = new Color(0, 0, 1, 0.2f);
                    break;
                default:
                    continue;
            }

            if (kvp.Value.IsOccupied)
            {
                Gizmos.color = new Color(1, 1, 0, 0.5f);
            }

            Gizmos.DrawCube(pos, Vector3.one * 0.8f);
        }
    }
}