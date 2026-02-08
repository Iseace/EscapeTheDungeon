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

    [Header("Anchor Generator Settings")]
    [Tooltip("Numero maximo de salas (incluye el ancla)")] public int anchorMaxRooms = 18;
    [Tooltip("Intentos por sala antes de descartar")] public int anchorAttemptsPerRoom = 40;
    [Tooltip("Separacion minima (padding) entre salas")] public int anchorRoomPadding = 1;
    [Tooltip("Rango de distancia radial desde el centro")] public Vector2Int anchorDistanceRange = new Vector2Int(8, 30);
    [Tooltip("Conexiones extra sobre el MST base")] public int anchorExtraConnections = 1;
    [Tooltip("Tamaño minimo salas secundarias")] public Vector2Int anchorRoomSizeMin = new Vector2Int(8, 8);
    [Tooltip("Tamaño maximo salas secundarias")] public Vector2Int anchorRoomSizeMax = new Vector2Int(16, 16);

    [Header("Prefabs (Placement)")]
    [Tooltip("Prefab principal de habitación (se escala al tamaño de la sala)")] public GameObject roomPrefab;
    [Tooltip("Prefab de corredor (tramos rectos escalados); si es null usa tiles")]
    public GameObject corridorPrefab;
    [Tooltip("Tile de piso para salas (fallback 1x1)")] public GameObject roomFloorTilePrefab;
    [Tooltip("Tile de piso para corredor (fallback 1x1)")] public GameObject corridorFloorTilePrefab;
    [Tooltip("Segmento de pared escalable (pivot centrado)")] public GameObject wallPiecePrefab;
    [Tooltip("Pilar para esquinas de pared")]
    public GameObject pillarPrefab;

    [Header("Procedural Objects")]
    public bool spawnObjects = true;
    public List<SpawnableObject> genericObjects = new List<SpawnableObject>();
    
    [Header("Object Spawn Settings")]
    [Range(0, 10)]
    public int minObjectsPerRoom = 0;
    [Range(0, 20)]
    public int maxObjectsPerRoom = 5;

    [Header("Wall Decorations")]
    public bool spawnWallDecorations = true;
    public List<WallDecoration> wallDecorations = new List<WallDecoration>();
    [Range(1, 16)] public int wallDecorSpacing = 3;
    [Range(0.1f, 5f)] public float wallDecorHeight = 1.6f;
    [Range(0f, 0.5f)] public float wallDecorInwardOffset = 0.05f;

    [Header("Debug")]
    public bool showGrid = false;

    private AnchorDungeonGenerator anchorGenerator;
    private ProceduralObjectSpawner objectSpawner;
    private WallDecorationSpawner wallDecorationSpawner;
    private DungeonGrid currentGrid;
    private List<RoomNode> currentRooms;
    private Vector3 currentCenterOffset;
    private DungeonPostProcessResult postProcessResult;
    private DungeonShapePostProcessor shapePostProcessor;

    void Start()
    {
        // DungeonNetworkRunner will handle dungeon creation
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

        anchorGenerator = null;
        currentGrid = null;
        currentRooms = null;
        currentCenterOffset = Vector3.zero;
        postProcessResult = null;
        shapePostProcessor = new DungeonShapePostProcessor();
        wallDecorationSpawner = new WallDecorationSpawner();

        // Siempre usamos el generador ancla + placer de prefabs
        anchorGenerator = new AnchorDungeonGenerator(dungeonWidth, dungeonLength);
        AnchorGenerationConfig cfg = new AnchorGenerationConfig
        {
            AnchorSize = new Vector2Int(Mathf.Max(2, roomWidthMin), Mathf.Max(2, roomLengthMin)),
            MaxRooms = Mathf.Max(1, anchorMaxRooms),
            MaxAttemptsPerRoom = Mathf.Max(1, anchorAttemptsPerRoom),
            Padding = Mathf.Max(0, anchorRoomPadding),
            CorridorWidth = corridorWidth,
            MinDistanceFromCenter = Mathf.Max(0, anchorDistanceRange.x),
            MaxDistanceFromCenter = Mathf.Max(anchorDistanceRange.x, anchorDistanceRange.y),
            ExtraConnections = Mathf.Max(0, anchorExtraConnections),
            RoomSizeMin = new Vector2Int(
                Mathf.Max(2, Mathf.Min(anchorRoomSizeMin.x, anchorRoomSizeMax.x)),
                Mathf.Max(2, Mathf.Min(anchorRoomSizeMin.y, anchorRoomSizeMax.y))
            ),
            RoomSizeMax = new Vector2Int(
                Mathf.Max(anchorRoomSizeMin.x, anchorRoomSizeMax.x),
                Mathf.Max(anchorRoomSizeMin.y, anchorRoomSizeMax.y)
            )
        };

        currentRooms = anchorGenerator.Generate(cfg);
        currentGrid = anchorGenerator.Grid;
        currentCenterOffset = anchorGenerator.GetCenterOffset();

        shapePostProcessor.Process(currentGrid, currentRooms, corridorWidth);

        var postProcessor = new DungeonPostProcessor();
        postProcessResult = postProcessor.Process(currentGrid, currentRooms);

        if (currentGrid != null && currentRooms != null)
        {
            GridPrefabPlacer.Place(
                currentGrid,
                currentRooms,
                transform,
                currentCenterOffset,
                roomPrefab,
                corridorPrefab,
                roomFloorTilePrefab,
                corridorFloorTilePrefab,
                wallPiecePrefab,
                pillarPrefab,
                wallHeight
            );

            if (spawnWallDecorations && wallDecorations != null && wallDecorations.Count > 0)
            {
                var doorSet = new HashSet<Vector2Int>(postProcessResult?.DoorCells ?? new List<Vector2Int>());
                wallDecorationSpawner.Spawn(
                    currentGrid,
                    transform,
                    currentCenterOffset,
                    wallDecorations,
                    Mathf.Max(1, wallDecorSpacing),
                    wallDecorHeight,
                    wallDecorInwardOffset,
                    doorSet
                );
            }
        }

        if (spawnObjects && currentGrid != null && currentRooms != null)
        {
            GameObject objectParent = new GameObject("ObjectParent");
            objectParent.transform.parent = transform;
            objectSpawner = new ProceduralObjectSpawner(currentGrid, objectParent.transform, currentCenterOffset);
            SpawnAllObjects();
        }
    }

    private void SpawnAllObjects()
    {
        foreach (var room in currentRooms)
        {
            if (genericObjects.Count > 0)
            {
                objectSpawner.SpawnObjects(room, genericObjects, minObjectsPerRoom, maxObjectsPerRoom);
            }
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

    public int GetLastUsedSeed()
    {
        return lastUsedSeed;
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

    public DungeonGrid GetGrid()
    {
        return currentGrid;
    }

    public List<RoomNode> GetAllRooms()
    {
        return currentRooms;
    }

    public DungeonPostProcessResult GetPostProcessResult()
    {
        return postProcessResult;
    }

    private void OnDrawGizmos()
    {
        if (!showGrid || currentGrid == null) return;

        Vector3 centerOffset = currentCenterOffset;
        var allCells = currentGrid.GetAllCells();
        foreach (var kvp in allCells)
        {
            Vector3 pos = new Vector3(kvp.Key.x + 0.5f, 0.1f, kvp.Key.y + 0.5f) + centerOffset;

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