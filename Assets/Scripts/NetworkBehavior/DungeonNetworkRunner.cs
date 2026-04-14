using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

public class DungeonNetworkRunner : NetworkBehaviour
{
    public static DungeonNetworkRunner Instance { get; private set; }

    public bool HasMissionStateAuthority => Object != null && Object.HasStateAuthority;

    [Networked] public int SharedSeed { get; set; }
    [SerializeField] private float pylonProgressSyncInterval = 0.1f;
    [SerializeField] private float pylonSyncMaxDistance = 0.5f;
    [SerializeField] private bool debugPickupSpawnVerbose = true;

    [Header("Match Flow")]
    [Tooltip("Fallback: usado si no se encuentra DungeonCreator o no tiene config")]
    [SerializeField] private float bossFreezeDurationSeconds = 10f;
    [Tooltip("Fallback: usado si no se encuentra DungeonCreator o no tiene config")]
    [SerializeField] private bool enableMatchTimeLimit = true;
    [Tooltip("Fallback: usado si no se encuentra DungeonCreator o no tiene config")]
    [SerializeField] private float matchDurationSeconds = 600f;
    [Tooltip("Escena unica de finales (debe existir en Build Settings)")]
    [SerializeField] private string endMatchSceneName = "EndMatch";
    [Tooltip("Fallback por indice si no se resuelve endMatchSceneName")]
    [SerializeField] private int endMatchSceneIndex = -1;
    [Tooltip("Tiempo para que el snapshot se replique antes de cargar la escena final")]
    [SerializeField] private float endMatchLoadDelaySeconds = 0.5f;

    [Networked] public NetworkBool MatchInProgress { get; set; }
    [Networked] public NetworkBool MatchEnded { get; set; }
    [Networked] private int EndMatchReasonCode { get; set; }
    [Networked] private TickTimer BossFreezeTimer { get; set; }
    [Networked] private TickTimer MatchTimer { get; set; }

    private DungeonCreator dungeonCreator;
    private MissionObjectiveManager missionObjectiveManager;
    private bool hasGeneratedLocally = false;
    private bool missionSyncHooked = false;
    private float nextPylonProgressSyncTime;
    private bool localBossReleasedLogged;
    private bool localMatchEndedLogged;
    private bool isEndingMatch;
    private bool pickupItemsSpawned;
    private int lastPickupGeneration = -1;
    private bool hasSpawned;

    public bool IsNetworkStateReady => hasSpawned && Object != null && Runner != null;
    public bool IsMatchInProgressSafe => IsNetworkStateReady && MatchInProgress;
    public bool IsMatchEndedSafe => IsNetworkStateReady && MatchEnded;

    public float RemainingMatchTimeSeconds
    {
        get
        {
            if (!IsNetworkStateReady || !IsMatchInProgressSafe || !enableMatchTimeLimit) return 0f;
            float? remaining = MatchTimer.RemainingTime(Runner);
            return Mathf.Max(0f, remaining ?? 0f);
        }
    }

    public bool HasMatchTimeLimit => enableMatchTimeLimit;

    public float RemainingBossFreezeTimeSeconds
    {
        get
        {
            if (!IsNetworkStateReady || !IsMatchInProgressSafe || !IsBossFrozen) return 0f;
            float? remaining = BossFreezeTimer.RemainingTime(Runner);
            return Mathf.Max(0f, remaining ?? 0f);
        }
    }

    public bool IsBossFrozen
    {
        get
        {
            if (!IsNetworkStateReady || !IsMatchInProgressSafe) return false;
            return !BossFreezeTimer.ExpiredOrNotRunning(Runner);
        }
    }

    public override void Spawned()
    {
        Instance = this;
        hasSpawned = true;

        Debug.Log($"=== DungeonNetworkRunner.Spawned() ===");
        Debug.Log($"HasStateAuthority: {Object.HasStateAuthority}");
        Debug.Log($"HasInputAuthority: {Object.HasInputAuthority}");
        Debug.Log($"Runner.IsSharedModeMasterClient: {Runner.IsSharedModeMasterClient}");
        Debug.Log($"Runner.LocalPlayer: {Runner.LocalPlayer}");
        Debug.Log($"CurrentSeed: {SharedSeed}");

        if (Object.HasStateAuthority)
        {
            MatchInProgress = false;
            MatchEnded = false;
            EndMatchReasonCode = -1;
            BossFreezeTimer = TickTimer.None;
            MatchTimer = TickTimer.None;
        }

        localBossReleasedLogged = false;
        localMatchEndedLogged = false;
        isEndingMatch = false;
        pickupItemsSpawned = false;
        lastPickupGeneration = -1;

        // Find the DungeonCreator in the scene
        dungeonCreator = FindFirstObjectByType<DungeonCreator>();

        if (dungeonCreator == null)
        {
            Debug.LogError("DungeonCreator NOT found in scene!");
            return;
        }

        Debug.Log("DungeonCreator found!");

        // Master client generates the seed ONLY if it's not set yet
        bool shouldGenerateSeed = Runner.GameMode == GameMode.Shared
            ? Runner.IsSharedModeMasterClient
            : Object.HasStateAuthority;

        if (shouldGenerateSeed && SharedSeed == 0)
        {
            int newSeed = GenerateSessionSeed();
            SharedSeed = newSeed;

            Debug.Log($"[MASTER CLIENT] Generated seed: {SharedSeed}");
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawned = false;
    }

    private static int GenerateSessionSeed()
    {
        unchecked
        {
            int guidPart = Guid.NewGuid().GetHashCode();
            int ticksPart = (int)DateTime.UtcNow.Ticks;
            int envPart = Environment.TickCount;

            int seed = guidPart ^ ticksPart ^ envPart;
            if (seed == 0) seed = 1;
            if (seed < 0) seed = -seed;
            return seed;
        }
    }

    public override void Render()
    {
        if (!hasGeneratedLocally)
        {
            if (SceneManager.GetActiveScene().name != "Game") return;
            if (SharedSeed == 0) return;

            if (dungeonCreator == null)
            {
                dungeonCreator = FindAnyObjectByType<DungeonCreator>();
                if (dungeonCreator == null) return;
            }

            Debug.Log($"[Player {Runner.LocalPlayer}] Generating dungeon in Game Scene with seed: {SharedSeed}");
            dungeonCreator.CreateDungeonWithSeed(SharedSeed);
            hasGeneratedLocally = true;
            if (Object != null && Object.HasStateAuthority)
            {
                TrySpawnNetworkPickupItems();
                dungeonCreator.SpawnDeferredGenericObjectsLocal();
            }
            TryHookMissionSync();
        }

        if (MatchInProgress && !IsBossFrozen && !localBossReleasedLogged)
        {
            localBossReleasedLogged = true;
            Debug.Log("[MATCH] Boss liberado. Los supervivientes ya tuvieron tiempo para alejarse.");
        }

        if (MatchEnded && !localMatchEndedLogged)
        {
            localMatchEndedLogged = true;
            Debug.Log("[MATCH] Tiempo de partida finalizado.");
        }

        TryEnsureLocalEndMatchSnapshot();
    }

    public override void FixedUpdateNetwork()
    {
        if (Object && Object.HasStateAuthority && hasGeneratedLocally)
        {
            TrySpawnNetworkPickupItems();
            dungeonCreator?.SpawnDeferredGenericObjectsLocal();
        }

        if (!Object || !Object.HasStateAuthority) return;
        if (!MatchInProgress || MatchEnded) return;

        if (TryEndMatchBySurvivorOutcome()) return;

        if (!enableMatchTimeLimit) return;

        if (IsBossFrozen) return;

        if (!MatchTimer.IsRunning)
        {
            MatchTimer = TickTimer.CreateFromSeconds(Runner, matchDurationSeconds);
            Debug.Log($"[MATCH] Inicia el timer global de partida: {matchDurationSeconds:0.##}s");
            return;
        }

        if (MatchTimer.Expired(Runner))
        {
            EndMatchByTimeLimit();
        }
    }

    private bool TryEndMatchBySurvivorOutcome()
    {
        int survivorsTotal = 0;
        int survivorsEscaped = 0;
        int survivorsDefeated = 0;

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
                continue;

            PlayerSetup setup = playerObject.GetComponent<PlayerSetup>();
            if (setup == null) continue;
            if (setup.IsBossPlayer()) continue;

            survivorsTotal++;

            if (setup.HasEscapedSafe)
            {
                survivorsEscaped++;
                continue;
            }

            if (playerObject.TryGetComponent<PlayerHealth>(out PlayerHealth health) && health.IsDeadSafe)
            {
                survivorsDefeated++;
            }
        }

        if (survivorsTotal <= 0) return false;

        if (survivorsEscaped >= survivorsTotal)
        {
            BeginEndMatch(MatchEndReason.AllSurvivorsEscaped);
            return true;
        }

        if (survivorsDefeated >= survivorsTotal)
        {
            BeginEndMatch(MatchEndReason.AllSurvivorsDefeated);
            return true;
        }

        // Mixed resolution case: e.g. one escaped and one died.
        // If nobody remains actively playing as survivor, the match should end.
        if (survivorsEscaped + survivorsDefeated >= survivorsTotal)
        {
            BeginEndMatch(MatchEndReason.NoActiveSurvivors);
            return true;
        }

        return false;
    }

    public void StartMatchFlow()
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (MatchInProgress || MatchEnded) return;

        ApplyMatchSettingsFromDungeonCreator();

        bossFreezeDurationSeconds = Mathf.Max(0f, bossFreezeDurationSeconds);
        matchDurationSeconds = Mathf.Max(5f, matchDurationSeconds);

        MatchInProgress = true;
        MatchEnded = false;
        BossFreezeTimer = bossFreezeDurationSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, bossFreezeDurationSeconds)
            : TickTimer.None;
        MatchTimer = TickTimer.None;

        localBossReleasedLogged = false;
        localMatchEndedLogged = false;

        Debug.Log($"[MATCH] Inicio de partida. Boss inmovil por {bossFreezeDurationSeconds:0.##}s. " +
                  (enableMatchTimeLimit
                      ? $"Duracion total: {matchDurationSeconds:0.##}s (comienza tras liberar al boss)"
                      : "Sin limite de tiempo global"));
    }

    private void ApplyMatchSettingsFromDungeonCreator()
    {
        if (dungeonCreator == null)
        {
            dungeonCreator = FindAnyObjectByType<DungeonCreator>();
        }

        if (dungeonCreator == null) return;

        bossFreezeDurationSeconds = dungeonCreator.GetBossFreezeDurationSeconds();
        enableMatchTimeLimit = dungeonCreator.GetEnableMatchTimeLimit();
        matchDurationSeconds = dungeonCreator.GetMatchDurationSeconds();
    }

    private void LateUpdate()
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (SceneManager.GetActiveScene().name != "Game") return;
        if (Time.time < nextPylonProgressSyncTime) return;

        nextPylonProgressSyncTime = Time.time + Mathf.Max(0.02f, pylonProgressSyncInterval);
        SyncPylonProgressToPeers();
    }

    private void TryHookMissionSync()
    {
        if (missionSyncHooked) return;

        missionObjectiveManager = MissionObjectiveManager.Instance;
        if (missionObjectiveManager == null)
        {
            missionObjectiveManager = FindAnyObjectByType<MissionObjectiveManager>();
        }

        if (missionObjectiveManager == null) return;

        missionObjectiveManager.PylonActivated += OnLocalPylonActivated;
        missionObjectiveManager.PortalSpawned += OnLocalPortalSpawned;
        missionSyncHooked = true;
    }

    private void TrySpawnNetworkPickupItems()
    {
        if (!Object || !Object.HasStateAuthority) return;

        if (dungeonCreator == null)
        {
            dungeonCreator = FindAnyObjectByType<DungeonCreator>();
            if (dungeonCreator == null) return;
        }

        int generation = dungeonCreator.GetGenerationCounter();
        if (generation != lastPickupGeneration)
        {
            pickupItemsSpawned = false;
            lastPickupGeneration = generation;
        }

        if (pickupItemsSpawned) return;

        if (!dungeonCreator.ShouldSpawnPickupItems())
        {
            pickupItemsSpawned = true;
            return;
        }

        DungeonGrid grid = dungeonCreator.GetGrid();
        List<RoomNode> rooms = dungeonCreator.GetAllRooms();
        List<SpawnablePickupItem> pickupConfigs = dungeonCreator.GetPickupItems();

        if (grid == null || rooms == null || pickupConfigs == null || pickupConfigs.Count == 0)
        {
            Debug.LogWarning("[DungeonNetworkRunner] Pickup items enabled, pero no hay grid/rooms/config validos.");
            return;
        }

        int minPerRoom = dungeonCreator.GetMinPickupItemsPerRoom();
        int maxPerRoom = dungeonCreator.GetMaxPickupItemsPerRoom();
        Vector3 centerOffset = dungeonCreator.GetCenterOffset();
        int validConfigs = CountValidPickupConfigs(pickupConfigs);

        Debug.Log($"[DungeonNetworkRunner] Pickup config: rooms={rooms.Count}, min={minPerRoom}, max={maxPerRoom}, configs={pickupConfigs.Count}, validConfigs={validConfigs}");

        if (validConfigs == 0)
        {
            pickupItemsSpawned = true;
            Debug.LogWarning("[DungeonNetworkRunner] Todos los pickupItems tienen prefab vacio/invalido. No se puede spawnear nada.");
            return;
        }

        int totalSpawned = 0;
        foreach (RoomNode room in rooms)
        {
            totalSpawned += SpawnPickupItemsInRoom(grid, room, pickupConfigs, minPerRoom, maxPerRoom, centerOffset);
        }

        pickupItemsSpawned = true;

        Debug.Log($"[DungeonNetworkRunner] Pickup items spawneados por red: {totalSpawned}");
        if (totalSpawned == 0)
        {
            Debug.LogWarning("[DungeonNetworkRunner] No se spawneo ningun pickup. Revisa: NetworkPrefabRef en pickupItems, prefabs registrados en Fusion y restricciones de clearance/chance.");
        }
    }

    private int SpawnPickupItemsInRoom(
        DungeonGrid grid,
        RoomNode room,
        List<SpawnablePickupItem> pickupConfigs,
        int minPerRoom,
        int maxPerRoom,
        Vector3 centerOffset)
    {
        if (room == null || pickupConfigs == null || pickupConfigs.Count == 0)
            return 0;

        List<Vector2Int> availableCells = grid.GetAvailableCellsInRoom(room);
        if (availableCells == null || availableCells.Count == 0)
        {
            Debug.LogWarning($"[DungeonNetworkRunner] Sala {room.RoomID}: 0 celdas disponibles para pickups.");
            return 0;
        }

        int targetCount = UnityEngine.Random.Range(minPerRoom, maxPerRoom + 1);
        int spawned = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(8, targetCount * 6);

        while (spawned < targetCount && attempts < maxAttempts)
        {
            attempts++;
            if (availableCells.Count == 0) break;

            SpawnablePickupItem config = pickupConfigs[UnityEngine.Random.Range(0, pickupConfigs.Count)];
            if (config == null) continue;

            if (config.prefab.Equals(default(NetworkPrefabRef)))
                continue;

            // El minimo por sala ignora chance para que sea realmente minimo garantizado si hay posiciones validas.
            bool canApplyChance = spawned >= minPerRoom;
            if (canApplyChance && UnityEngine.Random.Range(0f, 100f) > config.spawnChance)
                continue;

            if (!TryFindPickupPosition(grid, room, availableCells, config, out Vector2Int spawnPos))
                continue;

            Quaternion spawnRot = BuildPickupRotation(config);
            Vector3 worldPos = new Vector3(spawnPos.x + 0.5f, 0f, spawnPos.y + 0.5f) + centerOffset;

            NetworkObject spawnedObject = Runner.Spawn(config.prefab, worldPos, spawnRot);
            if (spawnedObject == null)
            {
                if (debugPickupSpawnVerbose)
                {
                    Debug.LogWarning($"[DungeonNetworkRunner] Runner.Spawn devolvio null para pickup '{config.itemName}' en {worldPos}.");
                }
                continue;
            }

            if (debugPickupSpawnVerbose)
            {
                Debug.Log($"[DungeonNetworkRunner] Pickup spawned: name={spawnedObject.name}, room={room.RoomID}, pos={spawnedObject.transform.position}");
            }

            StabilizeSpawnedPickup(spawnedObject, worldPos, spawnRot);

            grid.OccupyCell(spawnPos, spawnedObject.gameObject);
            availableCells.Remove(spawnPos);
            spawned++;
        }

        if (spawned < minPerRoom)
        {
            Debug.LogWarning($"[DungeonNetworkRunner] Sala {room.RoomID}: minimo pickups no alcanzado ({spawned}/{minPerRoom}). Posibles causas: pocos floors libres, clearance alto o prefabs no validos.");
        }

        return spawned;
    }

    private bool TryFindPickupPosition(
        DungeonGrid grid,
        RoomNode room,
        List<Vector2Int> availableCells,
        SpawnablePickupItem config,
        out Vector2Int position)
    {
        position = default;

        List<Vector2Int> shuffled = new List<Vector2Int>(availableCells);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, shuffled.Count);
            Vector2Int temp = shuffled[i];
            shuffled[i] = shuffled[randomIndex];
            shuffled[randomIndex] = temp;
        }

        foreach (Vector2Int candidate in shuffled)
        {
            if (config.needsClearSpace && !HasPickupClearSpace(grid, candidate, config.clearanceRadius))
                continue;

            if (config.avoidWalls && !HasPickupWallClearance(grid, room, candidate, config.wallClearanceRadius))
                continue;

            position = candidate;
            return true;
        }

        return false;
    }

    private bool HasPickupClearSpace(DungeonGrid grid, Vector2Int center, int radius)
    {
        int clampedRadius = Mathf.Max(0, radius);

        for (int x = -clampedRadius; x <= clampedRadius; x++)
        {
            for (int y = -clampedRadius; y <= clampedRadius; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(checkPos);

                if (cell == null || cell.IsOccupied || cell.Type != CellType.Floor)
                    return false;
            }
        }

        return true;
    }

    private bool HasPickupWallClearance(DungeonGrid grid, RoomNode room, Vector2Int center, int radius)
    {
        int clampedRadius = Mathf.Max(0, radius);

        for (int x = -clampedRadius; x <= clampedRadius; x++)
        {
            for (int y = -clampedRadius; y <= clampedRadius; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(checkPos);

                if (cell == null || cell.Type != CellType.Floor)
                    return false;

                if (!ReferenceEquals(cell.ParentRoom, room))
                    return false;
            }
        }

        return true;
    }

    private Quaternion BuildPickupRotation(SpawnablePickupItem config)
    {
        float y = 0f;
        if (config.randomizeRotationY)
        {
            float minY = Mathf.Min(config.rotationRangeY.x, config.rotationRangeY.y);
            float maxY = Mathf.Max(config.rotationRangeY.x, config.rotationRangeY.y);
            y = UnityEngine.Random.Range(minY, maxY);
        }

        return Quaternion.Euler(0f, y, 0f);
    }

    private int CountValidPickupConfigs(List<SpawnablePickupItem> pickupConfigs)
    {
        int count = 0;
        if (pickupConfigs == null) return count;

        for (int i = 0; i < pickupConfigs.Count; i++)
        {
            SpawnablePickupItem config = pickupConfigs[i];
            if (config == null) continue;
            if (config.prefab.Equals(default(NetworkPrefabRef))) continue;
            count++;
        }

        return count;
    }

    private void StabilizeSpawnedPickup(NetworkObject spawnedObject, Vector3 worldPos, Quaternion worldRot)
    {
        if (spawnedObject == null) return;

        // Keep pickups static on the ground to avoid physics drift/tunneling on FBX variants.
        spawnedObject.transform.SetPositionAndRotation(worldPos, worldRot);

        float groundY = worldPos.y;
        float raise = 0f;

        Collider col = spawnedObject.GetComponentInChildren<Collider>();
        if (col != null)
        {
            float minY = col.bounds.min.y;
            if (minY < groundY)
            {
                raise = (groundY - minY) + 0.005f;
            }
        }
        else
        {
            Renderer r = spawnedObject.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                float minY = r.bounds.min.y;
                if (minY < groundY)
                {
                    raise = (groundY - minY) + 0.005f;
                }
            }
        }

        if (raise > 0f)
        {
            spawnedObject.transform.position += Vector3.up * raise;
        }

        Rigidbody rb = spawnedObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private void OnDestroy()
    {
        hasSpawned = false;

        if (Instance == this)
        {
            Instance = null;
        }

        if (!missionSyncHooked || missionObjectiveManager == null) return;

        missionObjectiveManager.PylonActivated -= OnLocalPylonActivated;
        missionObjectiveManager.PortalSpawned -= OnLocalPortalSpawned;
        missionSyncHooked = false;
    }

    private void EndMatchByTimeLimit()
    {
        BeginEndMatch(MatchEndReason.TimeLimitExpired);
    }

    public void EndMatchManualDebug()
    {
        if (!Object || !Object.HasStateAuthority) return;
        BeginEndMatch(MatchEndReason.Manual);
    }

    private async void BeginEndMatch(MatchEndReason reason)
    {
        if (isEndingMatch) return;
        isEndingMatch = true;

        EndMatchReasonCode = (int)reason;
        MatchEnded = true;
        MatchInProgress = false;
        BossFreezeTimer = TickTimer.None;
        MatchTimer = TickTimer.None;

        MatchEndSnapshot localSnapshot = MatchEndSnapshotBuilder.CaptureFromRunner(Runner, reason);
        MatchEndRuntimeContext.SetSnapshot(localSnapshot);
        Debug.Log($"[MATCH] Snapshot local capturado: players={localSnapshot.Players.Count}, reason={reason}");

        Rpc_PrepareLocalEndMatchSnapshot(reason);

        int sceneIndex = ResolveEndMatchSceneIndex();
        if (sceneIndex < 0)
        {
            Debug.LogError($"[MATCH] No se encontro escena final. Configura endMatchSceneName='{endMatchSceneName}' o endMatchSceneIndex.");
            isEndingMatch = false;
            return;
        }

        int delayMs = Mathf.CeilToInt(Mathf.Max(0.1f, endMatchLoadDelaySeconds) * 1000f);
        await Task.Delay(delayMs);

        if (!Object || !Object.HasStateAuthority || Runner == null || !Runner.IsRunning)
            return;

        try
        {
            Debug.Log($"[MATCH] Fin de partida ({reason}). Cargando escena final index={sceneIndex}.");
            await Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
        }
        catch (Exception e)
        {
            Debug.LogError($"[MATCH] Error al cargar escena final: {e.Message}");
            isEndingMatch = false;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PrepareLocalEndMatchSnapshot(MatchEndReason reason)
    {
        if (Object != null && Object.HasStateAuthority) return;
        if (Runner == null) return;

        MatchEndSnapshot snapshot = MatchEndSnapshotBuilder.CaptureFromRunner(Runner, reason);
        MatchEndRuntimeContext.SetSnapshot(snapshot);
        Debug.Log($"[MATCH] Snapshot por RPC capturado: players={snapshot.Players.Count}, reason={reason}");
    }

    private void TryEnsureLocalEndMatchSnapshot()
    {
        if (!MatchEnded) return;
        if (MatchEndRuntimeContext.LatestSnapshot != null) return;
        if (Runner == null || !Runner.IsRunning) return;
        if (SceneManager.GetActiveScene().name != "Game") return;

        MatchEndReason reason = DecodeNetworkedEndReason();
        MatchEndSnapshot snapshot = MatchEndSnapshotBuilder.CaptureFromRunner(Runner, reason);
        if (snapshot == null || snapshot.Players.Count == 0) return;

        MatchEndRuntimeContext.SetSnapshot(snapshot);
        Debug.Log($"[MATCH] Snapshot fallback capturado: players={snapshot.Players.Count}, reason={reason}");
    }

    private MatchEndReason DecodeNetworkedEndReason()
    {
        if (Enum.IsDefined(typeof(MatchEndReason), EndMatchReasonCode))
            return (MatchEndReason)EndMatchReasonCode;

        return MatchEndReason.Manual;
    }

    private int ResolveEndMatchSceneIndex()
    {
        if (!string.IsNullOrWhiteSpace(endMatchSceneName))
        {
            int resolved = SceneUtility.GetBuildIndexByScenePath("Scenes/" + endMatchSceneName);
            if (resolved >= 0) return resolved;

            resolved = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + endMatchSceneName + ".unity");
            if (resolved >= 0) return resolved;

            resolved = SceneUtility.GetBuildIndexByScenePath(endMatchSceneName);
            if (resolved >= 0) return resolved;

            int sceneCount = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < sceneCount; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrWhiteSpace(path)) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(fileName, endMatchSceneName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        if (endMatchSceneIndex >= 0) return endMatchSceneIndex;
        return -1;
    }

    private void OnLocalPylonActivated(MissionObjectivePylon pylon)
    {
        if (!Object.HasStateAuthority || pylon == null) return;
        Rpc_SyncPylonActivated(pylon.transform.position);
    }

    private void OnLocalPortalSpawned(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;
        Rpc_SyncPortalSpawn(position, rotation);
    }

    private void SyncPylonProgressToPeers()
    {
        MissionObjectivePylon[] pylons = FindObjectsOfType<MissionObjectivePylon>();
        if (pylons == null || pylons.Length == 0) return;

        for (int i = 0; i < pylons.Length; i++)
        {
            var pylon = pylons[i];
            if (pylon == null) continue;
            Rpc_SyncPylonProgress(pylon.transform.position, pylon.Progress01, pylon.IsActivated);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncPylonActivated(Vector3 pylonWorldPosition)
    {
        if (Object.HasStateAuthority) return;

        float maxDistSqr = Mathf.Max(0.01f, pylonSyncMaxDistance);
        maxDistSqr *= maxDistSqr;

        MissionObjectivePylon[] pylons = FindObjectsOfType<MissionObjectivePylon>();
        if (pylons == null || pylons.Length == 0) return;

        MissionObjectivePylon closest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < pylons.Length; i++)
        {
            var p = pylons[i];
            if (p == null) continue;
            float d = (p.transform.position - pylonWorldPosition).sqrMagnitude;
            if (d < bestDistSqr)
            {
                bestDistSqr = d;
                closest = p;
            }
        }

        if (closest != null && bestDistSqr <= maxDistSqr)
        {
            closest.ForceActivateFromNetwork();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncPylonProgress(Vector3 pylonWorldPosition, float progress01, bool isActivated)
    {
        if (Object.HasStateAuthority) return;

        float maxDistSqr = Mathf.Max(0.01f, pylonSyncMaxDistance);
        maxDistSqr *= maxDistSqr;

        MissionObjectivePylon[] pylons = FindObjectsOfType<MissionObjectivePylon>();
        if (pylons == null || pylons.Length == 0) return;

        MissionObjectivePylon closest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < pylons.Length; i++)
        {
            var p = pylons[i];
            if (p == null) continue;
            float d = (p.transform.position - pylonWorldPosition).sqrMagnitude;
            if (d < bestDistSqr)
            {
                bestDistSqr = d;
                closest = p;
            }
        }

        if (closest == null || bestDistSqr > maxDistSqr) return;

        if (isActivated)
        {
            closest.ForceActivateFromNetwork();
            return;
        }

        closest.ForceProgressFromNetwork(progress01);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncPortalSpawn(Vector3 position, Quaternion rotation)
    {
        if (Object.HasStateAuthority) return;

        if (missionObjectiveManager == null)
        {
            missionObjectiveManager = MissionObjectiveManager.Instance;
            if (missionObjectiveManager == null)
            {
                missionObjectiveManager = FindAnyObjectByType<MissionObjectiveManager>();
            }
        }

        missionObjectiveManager?.SpawnPortalFromNetwork(position, rotation);
    }
}