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
    public int dungeonWidth = 300;
    public int dungeonLength = 300;
    public int roomWidthMin = 25;
    public int roomLengthMin = 25;
    public int wallHeight = 5;
    public int corridorWidth = 5;

    [Header("Wall Settings")]
    [Tooltip("Tiling por unidad (X = largo, Y = alto)")]
    public Vector2 wallTextureTilingPerUnit = Vector2.one;

    [Header("Ceiling Settings")]
    [Tooltip("Genera un techo usando la misma logica de celdas del piso")]
    public bool spawnCeiling = true;
    [Tooltip("Offset extra sobre la altura de pared para posicionar el techo")]
    public float ceilingHeightOffset = 0f;
    [Tooltip("Prefab de referencia para material del techo (opcional)")]
    public GameObject ceilingTilePrefab;

    [Header("Anchor Generator Settings")]
    [Tooltip("Numero maximo de salas (incluye el ancla)")] public int anchorMaxRooms = 18;
    [Tooltip("Intentos por sala antes de descartar")] public int anchorAttemptsPerRoom = 100;
    [Tooltip("Separacion minima (padding) entre salas")] public int anchorRoomPadding = 3;
    [Tooltip("Rango de distancia radial desde el centro")] public Vector2Int anchorDistanceRange = new Vector2Int(45, 45);
    [Tooltip("Conexiones extra sobre el MST base")] public int anchorExtraConnections = 13;
    [Tooltip("Tamaño minimo salas secundarias")] public Vector2Int anchorRoomSizeMin = new Vector2Int(25, 20);
    [Tooltip("Tamaño maximo salas secundarias")] public Vector2Int anchorRoomSizeMax = new Vector2Int(30, 25);

    [Header("Outer Rooms Settings")]
    [Tooltip("Salas adicionales para poblar más lejos del centro")] public int anchorAdditionalOuterRooms = 8;
    [Range(0.4f, 0.95f)]
    [Tooltip("Desde qué porcentaje del radio empieza la banda externa")] public float anchorOuterRoomStartNormalized = 0.45f;
    [Range(0.2f, 2f)]
    [Tooltip("Sesgo de distancia externa (<1 más lejos, >1 menos lejos)")] public float anchorOuterRoomBias = 0.55f;
    [Range(1f, 3f)]
    [Tooltip("Multiplicador del radio maximo para salas externas")] public float anchorOuterDistanceMultiplier = 1.6f;

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
    public int maxObjectsPerRoom = 17;

    [Header("Mission Objectives")]
    public bool spawnMissionObjectives = true;
    public List<MissionObjectiveConfig> missionObjectives = new List<MissionObjectiveConfig>();
    [SerializeField] private bool debugForcePylonInCentralRoom = false;
    [SerializeField] private GameObject debugCentralPylonPrefab;

    [Header("Mission Escape")]
    [Tooltip("Prefab del portal que aparece cuando se activan todos los pylons")]
    public GameObject missionPortalPrefab;
    [Tooltip("Tiempo limite para escapar una vez aparece el portal")]
    public float escapeTimeLimitSeconds = 90f;
    [Tooltip("Si esta activo, el portal aparece en una posicion aleatoria valida de la dungeon")]
    public bool randomPortalSpawnInDungeon = true;
    [Tooltip("Offset vertical del portal para evitar que se incruste en el piso")]
    public float missionPortalHeightOffset = 1f;
    [Tooltip("Area minima de sala para considerar spawn random del portal")]
    public int missionPortalMinRoomArea = 120;
    [Tooltip("Ancho/alto minimo de sala para considerar spawn random del portal")]
    public int missionPortalMinRoomSpan = 8;
    [Tooltip("Separacion minima del portal respecto a paredes (en tiles)")]
    public int missionPortalClearanceFromWall = 1;

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
    private MissionObjectiveSpawner missionObjectiveSpawner;
    private DungeonGrid currentGrid;
    private List<RoomNode> currentRooms;
    private Vector3 currentCenterOffset;
    private DungeonPostProcessResult postProcessResult;
    private DungeonShapePostProcessor shapePostProcessor;
    private MissionObjectiveManager missionObjectiveManager;

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
        missionObjectiveSpawner = new MissionObjectiveSpawner();
        missionObjectiveManager = GetComponent<MissionObjectiveManager>();

        if (missionObjectiveManager == null)
        {
            missionObjectiveManager = gameObject.AddComponent<MissionObjectiveManager>();
        }

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
            ),
            AdditionalOuterRooms = Mathf.Max(0, anchorAdditionalOuterRooms),
            OuterRoomStartNormalized = Mathf.Clamp01(anchorOuterRoomStartNormalized),
            OuterRoomBias = Mathf.Max(0.2f, anchorOuterRoomBias),
            OuterDistanceMultiplier = Mathf.Max(1f, anchorOuterDistanceMultiplier)
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
                ceilingTilePrefab,
                spawnCeiling,
                Mathf.Max(0.1f, wallHeight + ceilingHeightOffset),
                wallPiecePrefab,
                pillarPrefab,
                wallHeight,
                wallTextureTilingPerUnit
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

            if (spawnMissionObjectives && ((missionObjectives != null && missionObjectives.Count > 0) || debugForcePylonInCentralRoom))
            {
                GameObject missionParent = new GameObject("MissionObjectives");
                missionParent.transform.SetParent(transform, false);
                missionParent.transform.localPosition = Vector3.zero;

                List<Vector3> portalCandidates = BuildPortalCandidates(currentGrid, currentRooms, currentCenterOffset, anchorGenerator != null ? anchorGenerator.CentralRoom : null);
                missionObjectiveManager.Configure(
                    missionPortalPrefab,
                    escapeTimeLimitSeconds,
                    randomPortalSpawnInDungeon,
                    portalCandidates,
                    missionPortalHeightOffset
                );

                if (missionObjectives != null && missionObjectives.Count > 0)
                {
                    missionObjectiveSpawner.SpawnObjectives(
                        currentGrid,
                        currentRooms,
                        missionParent.transform,
                        currentCenterOffset,
                        missionObjectives,
                        anchorGenerator != null ? anchorGenerator.CentralRoom : null
                    );
                }

                if (debugForcePylonInCentralRoom && anchorGenerator != null && anchorGenerator.CentralRoom != null)
                {
                    GameObject debugPylonPrefab = GetFirstPylonObjectivePrefab();
                    if (debugPylonPrefab != null)
                    {
                        bool spawned = missionObjectiveSpawner.SpawnObjectiveInRoom(
                            currentGrid,
                            anchorGenerator.CentralRoom,
                            missionParent.transform,
                            currentCenterOffset,
                            debugPylonPrefab,
                            ensurePylonComponent: true,
                            enablePylonDebugLogs: true
                        );

                        if (!spawned)
                        {
                            Debug.LogWarning("[DungeonCreator] Debug pylon enabled, but no se pudo spawnear en la sala central (sin celdas disponibles).");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[DungeonCreator] Debug pylon enabled, pero no se encontró prefab para debug (ni debugCentralPylonPrefab ni entries en missionObjectives).");
                    }
                }
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

    private List<Vector3> BuildPortalCandidates(DungeonGrid grid, List<RoomNode> rooms, Vector3 offset, RoomNode excludedRoom)
    {
        List<Vector3> candidates = new List<Vector3>();
        if (grid == null || rooms == null) return candidates;

        int minArea = Mathf.Max(1, missionPortalMinRoomArea);
        int minSpan = Mathf.Max(1, missionPortalMinRoomSpan);
        int clearance = Mathf.Max(0, missionPortalClearanceFromWall);

        // First pass: strict filtering (room size + clearance).
        foreach (var room in rooms)
        {
            if (!IsPortalRoomEligible(room, excludedRoom, minArea, minSpan)) continue;

            var cells = grid.GetAvailableCellsInRoom(room);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int c = cells[i];
                if (!HasPortalClearance(grid, c, room, clearance)) continue;

                Vector3 world = new Vector3(c.x + 0.5f, 0f, c.y + 0.5f) + offset;
                candidates.Add(world);
            }
        }

        if (candidates.Count > 0) return candidates;

        // Second pass fallback: keep room constraints but relax clearance.
        foreach (var room in rooms)
        {
            if (!IsPortalRoomEligible(room, excludedRoom, minArea, minSpan)) continue;

            var cells = grid.GetAvailableCellsInRoom(room);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int c = cells[i];
                Vector3 world = new Vector3(c.x + 0.5f, 0f, c.y + 0.5f) + offset;
                candidates.Add(world);
            }
        }

        if (candidates.Count > 0) return candidates;

        // Last fallback: old behavior, any room/cell except central room.
        foreach (var room in rooms)
        {
            if (excludedRoom != null && ReferenceEquals(room, excludedRoom)) continue;

            var cells = grid.GetAvailableCellsInRoom(room);
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int c = cells[i];
                Vector3 world = new Vector3(c.x + 0.5f, 0f, c.y + 0.5f) + offset;
                candidates.Add(world);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[DungeonCreator] No se encontraron candidatos para portal con filtros actuales.");
        }

        return candidates;
    }

    private bool IsPortalRoomEligible(RoomNode room, RoomNode excludedRoom, int minArea, int minSpan)
    {
        if (room == null) return false;
        if (excludedRoom != null && ReferenceEquals(room, excludedRoom)) return false;

        int area = room.Width * room.Length;
        if (area < minArea) return false;
        if (room.Width < minSpan || room.Length < minSpan) return false;
        return true;
    }

    private bool HasPortalClearance(DungeonGrid grid, Vector2Int pos, RoomNode room, int clearance)
    {
        if (clearance <= 0) return true;

        for (int dx = -clearance; dx <= clearance; dx++)
        {
            for (int dz = -clearance; dz <= clearance; dz++)
            {
                Vector2Int p = new Vector2Int(pos.x + dx, pos.y + dz);
                var cell = grid.GetCell(p);
                if (cell == null) return false;
                if (cell.Type != CellType.Floor) return false;
                if (!ReferenceEquals(cell.ParentRoom, room)) return false;
            }
        }

        return true;
    }

    private GameObject GetFirstPylonObjectivePrefab()
    {
        if (debugCentralPylonPrefab != null) return debugCentralPylonPrefab;

        if (missionObjectives == null || missionObjectives.Count == 0) return null;

        GameObject firstAnyPrefab = null;

        for (int i = 0; i < missionObjectives.Count; i++)
        {
            GameObject prefab = missionObjectives[i]?.prefab;
            if (prefab == null) continue;
            if (firstAnyPrefab == null) firstAnyPrefab = prefab;
            if (prefab.GetComponentInChildren<MissionObjectivePylon>(true) == null) continue;
            return prefab;
        }

        if (firstAnyPrefab != null)
        {
            Debug.LogWarning("[DungeonCreator] Debug pylon: no se encontró MissionObjectivePylon en prefabs de missionObjectives. Se usará el primer prefab y se agregará MissionObjectivePylon en runtime para pruebas.");
        }

        return firstAnyPrefab;
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