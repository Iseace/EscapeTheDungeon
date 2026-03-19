using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

[System.Serializable]
public class SpawnablePickupItem
{
    public NetworkPrefabRef prefab;
    public string itemName = "Pickup Item";
    [Range(0f, 100f)]
    public float spawnChance = 50f;

    public bool needsClearSpace = true;
    [Range(0, 5)]
    public int clearanceRadius = 1;

    public bool avoidWalls = true;
    [Range(0, 6)]
    public int wallClearanceRadius = 1;

    [Header("Random Rotation")]
    public bool randomizeRotationY = true;
    public Vector2 rotationRangeY = new Vector2(0f, 360f);
}

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
    [Tooltip("Si esta activo y hay pickups de red, los objetos decorativos se spawnean despues para no bloquear celdas de pickups")]
    public bool deferGenericObjectSpawnUntilPickupItems = true;
    [Tooltip("Fallback: si el spawn diferido no es disparado por red, se ejecuta localmente tras este delay")]
    [Range(0.05f, 5f)]
    public float deferredObjectSpawnFallbackDelay = 0.5f;

    [Header("Pickup Items (Network)")]
    [Tooltip("Spawnea items agarrables sincronizados por red")]
    public bool spawnPickupItems = true;
    public List<SpawnablePickupItem> pickupItems = new List<SpawnablePickupItem>();

    [Header("Pickup Spawn Settings")]
    [Range(0, 10)]
    public int minPickupItemsPerRoom = 0;
    [Range(0, 20)]
    public int maxPickupItemsPerRoom = 3;

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
    [Tooltip("Offset vertical del portal para evitar que se incruste en el piso (fuente unica para MissionObjectiveManager)")]
    public float missionPortalHeightOffset = 2f;

    [Header("Match Flow")]
    [Tooltip("Tiempo en segundos que el boss queda inmovil al iniciar la partida")]
    [SerializeField] private float bossFreezeDurationSeconds = 10f;
    [Tooltip("Activa tiempo limite global para terminar la partida")]
    [SerializeField] private bool enableMatchTimeLimit = true;
    [Tooltip("Duracion total de la partida en segundos")]
    [SerializeField] private float matchDurationSeconds = 600f;

    [Header("Wall Decorations")]
    public bool spawnWallDecorations = true;
    public List<WallDecoration> wallDecorations = new List<WallDecoration>();
    [Range(1, 16)] public int wallDecorSpacing = 3;
    [Range(0f, 0.5f)] public float wallDecorInwardOffset = 0.05f;

    [Header("Debug")]
    public bool showGrid = false;

    [Header("Runtime Generation")]
    [Tooltip("Nombre del contenedor donde se crea toda la dungeon runtime")]
    [SerializeField] private string runtimeRootName = "_RuntimeDungeon";

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
    private Transform runtimeRoot;
    private bool hasPendingGenericObjectSpawn;
    private int generationCounter;

    void Start()
    {
        // DungeonNetworkRunner will handle dungeon creation
        // CreateDungeon();
    }

    public void CreateDungeon()
    {
        CancelInvoke(nameof(ForceSpawnDeferredObjectsFallback));
        generationCounter++;

        if (useRandomSeed)
        {
            seed = Random.Range(0, int.MaxValue);
        }

        lastUsedSeed = seed;
        Random.InitState(seed);

        EnsureRuntimeRoot();
        WarnIfLegacyDirectChildrenExist();
        DestroyAllChildren();

        anchorGenerator = null;
        objectSpawner = null;
        currentGrid = null;
        currentRooms = null;
        currentCenterOffset = Vector3.zero;
        postProcessResult = null;
        shapePostProcessor = new DungeonShapePostProcessor();
        wallDecorationSpawner = new WallDecorationSpawner();
        missionObjectiveSpawner = new MissionObjectiveSpawner();
        missionObjectiveManager = GetComponent<MissionObjectiveManager>();
        hasPendingGenericObjectSpawn = false;

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
                runtimeRoot,
                currentCenterOffset,
                null,
                null,
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
                    runtimeRoot,
                    currentCenterOffset,
                    wallDecorations,
                    Mathf.Max(1, wallDecorSpacing),
                    wallDecorInwardOffset,
                    doorSet
                );
            }

            if (spawnMissionObjectives && ((missionObjectives != null && missionObjectives.Count > 0) || debugForcePylonInCentralRoom))
            {
                GameObject missionParent = new GameObject("MissionObjectives");
                missionParent.transform.SetParent(runtimeRoot, false);
                missionParent.transform.localPosition = Vector3.zero;

                missionObjectiveManager.Configure(
                    missionPortalPrefab,
                    escapeTimeLimitSeconds,
                    randomPortalSpawnInDungeon,
                    null,
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

                List<Vector3> portalCandidates = BuildPortalCandidates(
                    currentGrid,
                    currentRooms,
                    currentCenterOffset,
                    anchorGenerator != null ? anchorGenerator.CentralRoom : null
                );
                missionObjectiveManager.SetPortalCandidates(portalCandidates);
            }
        }

        if (spawnObjects && currentGrid != null && currentRooms != null)
        {
            EnsureObjectSpawner();

            bool canUseDeferredFlow = Application.isPlaying;
            bool networkSessionActive = canUseDeferredFlow && FindAnyObjectByType<DungeonNetworkRunner>() != null;
            bool shouldDeferGenericObjects =
                canUseDeferredFlow &&
                !networkSessionActive &&
                deferGenericObjectSpawnUntilPickupItems &&
                ShouldSpawnPickupItems();
            if (shouldDeferGenericObjects)
            {
                hasPendingGenericObjectSpawn = true;
                Invoke(nameof(ForceSpawnDeferredObjectsFallback), Mathf.Max(0.05f, deferredObjectSpawnFallbackDelay));
            }
            else
            {
                SpawnAllObjects();
            }
        }

        if (!Application.isPlaying && ShouldSpawnPickupItems())
        {
            Debug.LogWarning("[DungeonCreator] Pickup Items (Network) solo se spawnean durante Play con NetworkRunner/StateAuthority.");
        }
    }

    public void SpawnDeferredGenericObjectsLocal()
    {
        if (!hasPendingGenericObjectSpawn) return;
        if (objectSpawner == null || currentRooms == null || currentGrid == null) return;

        CancelInvoke(nameof(ForceSpawnDeferredObjectsFallback));
        SpawnAllObjects();
    }

    private void ForceSpawnDeferredObjectsFallback()
    {
        if (!hasPendingGenericObjectSpawn) return;

        Debug.LogWarning("[DungeonCreator] Fallback de spawn diferido activado. Spawneando objetos genericos localmente.");
        SpawnDeferredGenericObjectsLocal();
    }

    private void EnsureObjectSpawner()
    {
        if (objectSpawner != null) return;

        GameObject objectParent = new GameObject("ObjectParent");
        objectParent.transform.SetParent(runtimeRoot, false);
        objectSpawner = new ProceduralObjectSpawner(currentGrid, objectParent.transform, currentCenterOffset);
    }

    private void SpawnAllObjects()
    {
        if (objectSpawner == null || currentRooms == null) return;

        Random.State previousRandomState = Random.state;
        int objectSeed = unchecked(seed * 486187739 + 137);
        Random.InitState(objectSeed);

        foreach (var room in currentRooms)
        {
            if (genericObjects.Count > 0)
            {
                objectSpawner.SpawnObjects(room, genericObjects, minObjectsPerRoom, maxObjectsPerRoom);
            }
        }

        Random.state = previousRandomState;

        hasPendingGenericObjectSpawn = false;
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

    public int GetGenerationCounter()
    {
        return generationCounter;
    }

    public Vector3 GetCentralRoomWorldPosition()
    {
        if (anchorGenerator != null && anchorGenerator.CentralRoom != null)
        {
            Vector2Int centerGrid = anchorGenerator.CentralRoom.GetCenterPosition();
            return new Vector3(centerGrid.x + 0.5f, 0f, centerGrid.y + 0.5f) + currentCenterOffset;
        }

        return Vector3.zero;
    }

    public float GetBossFreezeDurationSeconds()
    {
        return Mathf.Max(0f, bossFreezeDurationSeconds);
    }

    public bool GetEnableMatchTimeLimit()
    {
        return enableMatchTimeLimit;
    }

    public float GetMatchDurationSeconds()
    {
        return Mathf.Max(5f, matchDurationSeconds);
    }

    public void DestroyAllChildren()
    {
        EnsureRuntimeRoot();

        while (runtimeRoot.childCount != 0)
        {
            foreach (Transform item in runtimeRoot)
            {
                DestroyImmediate(item.gameObject);
            }
        }
    }

    [ContextMenu("Sanitize Legacy Generated Children")]
    public void SanitizeLegacyGeneratedChildren()
    {
        EnsureRuntimeRoot();

        List<Transform> legacy = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child == runtimeRoot) continue;
            legacy.Add(child);
        }

        for (int i = 0; i < legacy.Count; i++)
        {
            DestroyImmediate(legacy[i].gameObject);
        }

        Debug.Log($"[DungeonCreator] Legacy cleanup completo. Objetos eliminados: {legacy.Count}");
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null) return;

        Transform existing = transform.Find(runtimeRootName);
        if (existing != null)
        {
            runtimeRoot = existing;
            return;
        }

        GameObject root = new GameObject(runtimeRootName);
        root.transform.SetParent(transform, false);
        runtimeRoot = root.transform;
    }

    private void WarnIfLegacyDirectChildrenExist()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == runtimeRoot) continue;

            Debug.LogWarning("[DungeonCreator] Se detectaron hijos legacy fuera de _RuntimeDungeon. " +
                             "Para limpiar la escena usa el ContextMenu: 'Sanitize Legacy Generated Children'.", this);
            return;
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

    public RoomNode GetCentralRoom()
    {
        return anchorGenerator != null ? anchorGenerator.CentralRoom : null;
    }

    public Vector3 GetCenterOffset()
    {
        return currentCenterOffset;
    }

    public Vector3 GridToWorld(Vector2Int gridPos, float y = 0f)
    {
        return new Vector3(gridPos.x + 0.5f, y, gridPos.y + 0.5f) + currentCenterOffset;
    }

    public DungeonPostProcessResult GetPostProcessResult()
    {
        return postProcessResult;
    }

    public bool ShouldSpawnPickupItems()
    {
        return spawnPickupItems
            && pickupItems != null
            && pickupItems.Count > 0
            && Mathf.Max(0, maxPickupItemsPerRoom) > 0;
    }

    public List<SpawnablePickupItem> GetPickupItems()
    {
        return pickupItems;
    }

    public int GetMinPickupItemsPerRoom()
    {
        return Mathf.Max(0, minPickupItemsPerRoom);
    }

    public int GetMaxPickupItemsPerRoom()
    {
        return Mathf.Max(GetMinPickupItemsPerRoom(), maxPickupItemsPerRoom);
    }

    private List<Vector3> BuildPortalCandidates(DungeonGrid grid, List<RoomNode> rooms, Vector3 offset, RoomNode excludedRoom)
    {
        List<Vector3> candidates = new List<Vector3>();
        if (grid == null || rooms == null) return candidates;

        // The portal only picks rooms that are fully free of occupied cells.
        foreach (var room in rooms)
        {
            if (!IsPortalRoomEligible(room, excludedRoom)) continue;
            if (!IsRoomFreeForPortal(grid, room)) continue;

            if (TryGetRoomCenterCandidate(grid, room, offset, out Vector3 world))
            {
                candidates.Add(world);
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[DungeonCreator] No se encontraron salas libres para el portal (posibles objetivos/objetos ocupando rooms).");
        }

        return candidates;
    }

    private bool TryGetRoomCenterCandidate(DungeonGrid grid, RoomNode room, Vector3 offset, out Vector3 world)
    {
        world = Vector3.zero;
        if (grid == null || room == null) return false;

        List<Vector2Int> availableCells = grid.GetAvailableCellsInRoom(room);
        if (availableCells == null || availableCells.Count == 0) return false;

        Vector2Int roomCenter = room.GetCenterPosition();
        Vector2Int bestCell = default;
        int bestDistance = int.MaxValue;
        bool found = false;

        for (int i = 0; i < availableCells.Count; i++)
        {
            Vector2Int cell = availableCells[i];
            int distance = (cell - roomCenter).sqrMagnitude;
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            bestCell = cell;
            found = true;
        }

        if (!found) return false;

        world = new Vector3(bestCell.x + 0.5f, 0f, bestCell.y + 0.5f) + offset;
        return true;
    }

    private bool IsPortalRoomEligible(RoomNode room, RoomNode excludedRoom)
    {
        if (room == null) return false;
        if (excludedRoom != null && ReferenceEquals(room, excludedRoom)) return false;
        return true;
    }

    private bool IsRoomFreeForPortal(DungeonGrid grid, RoomNode room)
    {
        if (grid == null || room == null) return false;

        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int z = room.BottomLeftAreaCorner.y; z < room.TopRightAreaCorner.y; z++)
            {
                Vector2Int p = new Vector2Int(x, z);
                var cell = grid.GetCell(p);
                if (cell == null) return false;
                if (cell.Type != CellType.Floor) continue;
                if (!ReferenceEquals(cell.ParentRoom, room)) continue;
                if (cell.IsOccupied) return false;
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