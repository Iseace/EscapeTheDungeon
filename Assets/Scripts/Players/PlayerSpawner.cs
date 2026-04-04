using UnityEngine;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using System.Linq;

public class PlayerSpawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    public NetworkObject PlayerPrefab;
    public NetworkObject broomPrefab;

    [Header("Character Prefabs")]
    public NetworkObject BossPrefab;      //prefab del Boss

    [Header("Dungeon Runner")]
    public NetworkObject dungeonNetworkRunnerPrefab;
    private bool dungeonRunnerSpawned = false;

    [Header("Game Spawn")]
    [SerializeField] private Vector3 fallbackGameCenterSpawn = new Vector3(0f, 1f, 0f);
    [SerializeField] private float gameSpawnRingRadius = 2.5f;
    [SerializeField] private float gameSpawnY = 1f;
    [SerializeField] private bool randomizeSafeSpawns = true;
    [SerializeField] private float minSpawnDistanceBetweenPlayers = 1.5f;
    [SerializeField] private float minDistanceFromBossCenter = 5f;
    [SerializeField] private float spawnCollisionRadius = 0.35f;
    [SerializeField] private float spawnCollisionHeight = 1.8f;
    [SerializeField] private float groundRayStartHeight = 10f;
    [SerializeField] private float groundSnapOffset = 0.05f;

    [Header("Race Spawn")]
    [SerializeField] private Vector3 raceSpawnOrigin = new Vector3(0f, 1f, 0f);
    [SerializeField] private float raceSpawnLaneWidth = 2f;
    [SerializeField] private float raceSpawnRowDepth = 3f;

    [Header("Boss System")]
    [SerializeField] private string menuSceneName = "LobbyList";
    [SerializeField] private int menuSceneIndex = 0;

    private bool bossSelected = false;
    private PlayerRef bossPlayer;
    private bool isSwappingBoss = false;
    private bool matchFlowStarted = false;
    private bool callbacksRegistered = false;
    private NetworkRunner registeredRunner;

    private void Start()
    {
        TryRegisterRunnerCallbacks();
    }

    private void Update()
    {
        if (!callbacksRegistered)
        {
            TryRegisterRunnerCallbacks();
        }
    }

    private void TryRegisterRunnerCallbacks()
    {
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner == null)
            return;

        if (callbacksRegistered && registeredRunner == runner)
            return;

        runner.AddCallbacks(this);
        registeredRunner = runner;
        callbacksRegistered = true;
        Debug.Log("[SPAWNER] Registered callbacks with NetworkRunner");
    }

    // Returns true when the current session is a Race session
    private bool IsRaceSession(NetworkRunner runner)
    {
        if (runner.SessionInfo == null || runner.SessionInfo.Properties == null)
            return false;

        if (runner.SessionInfo.Properties.TryGetValue(NetworkRunnerHandler.SESSION_TYPE_KEY, out SessionProperty prop))
            return (string)prop == NetworkRunnerHandler.SESSION_TYPE_RACE;

        return false;
    }

    // Returns PlayerPrefab for normal sessions, broomPrefab for race sessions
    private NetworkObject GetPrefabForSession(NetworkRunner runner)
    {
        return IsRaceSession(runner) ? broomPrefab : PlayerPrefab;
    }

    public void PlayerJoinedLogic(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer) return;

        Debug.Log($"[SPAWNER] Player {player.PlayerId} joining");

        // Prevent duplicate spawns - check if player already has an object
        if (runner.TryGetPlayerObject(player, out NetworkObject existingPlayerObj))
        {
            Debug.LogWarning($"[SPAWNER] Player {player.PlayerId} already has an object! Skipping spawn.");
            return;
        }

        // Handle Dungeon Runner logic only in the Game scene
        if (SceneManager.GetActiveScene().name == "Game")
        {
            if (!dungeonRunnerSpawned)
            {
                var existing = FindFirstObjectByType<DungeonNetworkRunner>();
                if (existing == null)
                {
                    runner.Spawn(dungeonNetworkRunnerPrefab, Vector3.zero, Quaternion.identity);
                    dungeonRunnerSpawned = true;
                }
            }
        }

        // Default spawn position — must be declared before any scene-specific block overrides it
        Vector3 spawnPos = new Vector3(3f, 1f, 3f);
        Quaternion spawnRot = Quaternion.identity;

        // LOBBY LINE-UP: Initial spawn position (will be repositioned by FixedUpdateNetwork)
        if (SceneManager.GetActiveScene().name == "LobbyRoom")
        {
            float spacing = 1.5f;
            int totalPlayers = runner.ActivePlayers.Count();
            float totalWidth = (totalPlayers - 1) * spacing;
            float startOffset = -totalWidth / 2f;

            spawnPos = new Vector3(startOffset + (totalPlayers - 1) * spacing, 0f, 0f);
            spawnRot = Quaternion.identity;

            // Spawn PlayerPrefab or broomPrefab depending on session type
            NetworkObject lobbyObj = runner.Spawn(GetPrefabForSession(runner), spawnPos, spawnRot, player);
            runner.SetPlayerObject(player, lobbyObj);
            Debug.Log($"[SPAWNER] Player {player.PlayerId} spawned in LobbyRoom as {GetPrefabForSession(runner).name}");
            return;
        }

        if (SceneManager.GetActiveScene().name == "Game")
        {
            spawnPos = GetSafeGameSpawnForPlayer(runner, player);
        }

        if (SceneManager.GetActiveScene().name == "Race")
        {
            var orderedPlayers = runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
            int idx = orderedPlayers.IndexOf(player);

            // 3-2 grid offsets: X = lateral, Z = depth (negative = further back from start)
            Vector3[] gridOffsets = new Vector3[]
            {
                new Vector3(-raceSpawnLaneWidth,  0f,  0f),              // P1 front-left
                new Vector3( 0f,                  0f,  0f),              // P2 front-center
                new Vector3( raceSpawnLaneWidth,  0f,  0f),              // P3 front-right
                new Vector3(-raceSpawnLaneWidth * 0.5f, 0f, -raceSpawnRowDepth), // P4 back-left
                new Vector3( raceSpawnLaneWidth * 0.5f, 0f, -raceSpawnRowDepth)  // P5 back-right
            };

            Vector3 offset = idx < gridOffsets.Length ? gridOffsets[idx] : gridOffsets[gridOffsets.Length - 1];
            spawnPos = raceSpawnOrigin + offset;
            spawnRot = Quaternion.identity; // faces forward (positive Z)
            Debug.Log($"[SPAWNER] Race grid slot {idx} for player {player.PlayerId} at {spawnPos}");
        }

        NetworkObject playerObj = runner.Spawn(broomPrefab, spawnPos, spawnRot, player);
        runner.SetPlayerObject(player, playerObj);

        Debug.Log($"[SPAWNER] Player {player.PlayerId} spawned");
    }

    public override void FixedUpdateNetwork()
    {
        // Continuously reposition lobby players to stay centered
        if (Runner.IsServer && SceneManager.GetActiveScene().name == "LobbyRoom" && !bossSelected)
        {
            float spacing = 1.5f;
            var activePlayers = Runner.ActivePlayers.ToList();
            int totalPlayers = activePlayers.Count;

            if (totalPlayers > 0)
            {
                float totalWidth = (totalPlayers - 1) * spacing;
                float startOffset = -totalWidth / 2f;

                int index = 0;
                foreach (var p in activePlayers)
                {
                    if (Runner.TryGetPlayerObject(p, out NetworkObject playerObj))
                    {
                        Vector3 targetPos = new Vector3(startOffset + index * spacing, 0f, 0f);
                        playerObj.transform.position = targetPos;
                        index++;
                    }
                }
            }
        }

        if (!Runner.IsServer || SceneManager.GetActiveScene().name != "Game") return;

        if (bossSelected && !matchFlowStarted)
        {
            TryStartMatchFlow();
            return;
        }

        if (bossSelected || isSwappingBoss) return;

        if (Runner.ActivePlayers.Count() == 0) return;
        foreach (var p in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(p, out _)) return;
        }

        isSwappingBoss = true;
        bossSelected = true;

        var players = Runner.ActivePlayers.ToList();
        bossPlayer = players[UnityEngine.Random.Range(0, players.Count)];

        Debug.Log($"[SPAWNER] Selected player {bossPlayer.PlayerId} as Boss");

        if (Runner.TryGetPlayerObject(bossPlayer, out NetworkObject oldObj))
        {
            Vector3 centerSpawn = GetSafeBossSpawnPosition();
            Quaternion rot = oldObj.transform.rotation;

            Debug.Log($"[SPAWNER] Despawning old player object at {centerSpawn}");

            Runner.SetPlayerObject(bossPlayer, null);
            Runner.Despawn(oldObj);

            if (BossPrefab != null)
            {
                Debug.Log($"[SPAWNER] Spawning Boss prefab at {centerSpawn}");
                NetworkObject newBoss = Runner.Spawn(BossPrefab, centerSpawn, rot, bossPlayer);
                Runner.SetPlayerObject(bossPlayer, newBoss);

                if (newBoss.TryGetComponent<PlayerRole>(out var role))
                {
                    role.SetBoss();
                    Debug.Log($"[SPAWNER] Boss role set successfully");
                }

                TryStartMatchFlow();
            }
            else
            {
                Debug.LogError("¡ERROR! Falta asignar el BossPrefab en el Spawner.");
            }
        }

        isSwappingBoss = false;
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer) return;

        string currentScene = SceneManager.GetActiveScene().name;

        // Re-spawning logic for transitions into Lobby, Game, or Race
        if (currentScene == "Game" || currentScene == "LobbyRoom" || currentScene == "Race")
        {
            Debug.Log($"[SPAWNER] Scene {currentScene} loaded, spawning players");
            dungeonRunnerSpawned = false;
            bossSelected = false; // Reset boss selection for new scene
            isSwappingBoss = false; // Reset swap flag
            matchFlowStarted = false;

            foreach (var player in runner.ActivePlayers)
            {
                // Only spawn if player doesn't already have an object
                if (!runner.TryGetPlayerObject(player, out NetworkObject existingObj))
                {
                    PlayerJoinedLogic(runner, player);
                }
                else
                {
                    Debug.Log($"[SPAWNER] Player {player.PlayerId} already has object, skipping spawn");
                }
            }
        }
    }

    private void TryStartMatchFlow()
    {
        if (matchFlowStarted) return;

        var dungeonRunner = DungeonNetworkRunner.Instance;
        if (dungeonRunner == null)
        {
            dungeonRunner = FindFirstObjectByType<DungeonNetworkRunner>();
        }

        if (dungeonRunner == null)
        {
            Debug.LogWarning("[SPAWNER] No se encontro DungeonNetworkRunner para iniciar match flow.");
            return;
        }

        dungeonRunner.StartMatchFlow();
        if (dungeonRunner.MatchInProgress)
        {
            matchFlowStarted = true;
        }
    }

    private Vector3 GetSafeGameSpawnForPlayer(NetworkRunner runner, PlayerRef player)
    {
        var dungeonCreator = FindFirstObjectByType<DungeonCreator>();
        if (dungeonCreator == null)
        {
            return GetFallbackRingSpawn(runner, player);
        }

        DungeonGrid grid = dungeonCreator.GetGrid();
        RoomNode centralRoom = dungeonCreator.GetCentralRoom();
        if (grid == null || centralRoom == null)
        {
            return GetFallbackRingSpawn(runner, player);
        }

        List<Vector2Int> candidateCells = grid.GetAvailableCellsInRoom(centralRoom);
        if (candidateCells == null || candidateCells.Count == 0)
        {
            return GetFallbackRingSpawn(runner, player);
        }

        if (randomizeSafeSpawns)
        {
            for (int i = 0; i < candidateCells.Count; i++)
            {
                int randomIndex = UnityEngine.Random.Range(i, candidateCells.Count);
                Vector2Int temp = candidateCells[i];
                candidateCells[i] = candidateCells[randomIndex];
                candidateCells[randomIndex] = temp;
            }
        }

        Vector3 bossCenter = GetDungeonCentralPosition();
        bossCenter.y = gameSpawnY;

        var existingPositions = new List<Vector3>();
        foreach (var p in runner.ActivePlayers)
        {
            if (p == player) continue;
            if (!runner.TryGetPlayerObject(p, out NetworkObject obj) || obj == null) continue;
            existingPositions.Add(obj.transform.position);
        }

        for (int i = 0; i < candidateCells.Count; i++)
        {
            Vector3 candidate = dungeonCreator.GridToWorld(candidateCells[i], gameSpawnY);
            if (Vector3.Distance(candidate, bossCenter) < Mathf.Max(0f, minDistanceFromBossCenter)) continue;
            if (!HasDistanceFromPlayers(candidate, existingPositions)) continue;

            candidate = SnapToGround(candidate);
            if (!HasCollisionClearance(candidate)) continue;

            return candidate;
        }

        return GetFallbackRingSpawn(runner, player);
    }

    private Vector3 GetSafeBossSpawnPosition()
    {
        Vector3 center = GetDungeonCentralPosition();
        center.y = gameSpawnY;

        var dungeonCreator = FindFirstObjectByType<DungeonCreator>();
        if (dungeonCreator == null)
        {
            return SnapToGround(center);
        }

        DungeonGrid grid = dungeonCreator.GetGrid();
        RoomNode centralRoom = dungeonCreator.GetCentralRoom();
        if (grid == null || centralRoom == null)
        {
            return SnapToGround(center);
        }

        List<Vector2Int> candidateCells = grid.GetAvailableCellsInRoom(centralRoom);
        if (candidateCells == null || candidateCells.Count == 0)
        {
            return SnapToGround(center);
        }

        candidateCells.Sort((a, b) =>
        {
            Vector3 wa = dungeonCreator.GridToWorld(a, gameSpawnY);
            Vector3 wb = dungeonCreator.GridToWorld(b, gameSpawnY);
            return (wa - center).sqrMagnitude.CompareTo((wb - center).sqrMagnitude);
        });

        for (int i = 0; i < candidateCells.Count; i++)
        {
            Vector3 candidate = dungeonCreator.GridToWorld(candidateCells[i], gameSpawnY);
            candidate = SnapToGround(candidate);
            if (!HasCollisionClearance(candidate)) continue;
            return candidate;
        }

        return SnapToGround(center);
    }

    private Vector3 GetDungeonCentralPosition()
    {
        var dungeonCreator = FindFirstObjectByType<DungeonCreator>();
        if (dungeonCreator != null)
        {
            return dungeonCreator.GetCentralRoomWorldPosition();
        }

        return fallbackGameCenterSpawn;
    }

    private Vector3 GetFallbackRingSpawn(NetworkRunner runner, PlayerRef player)
    {
        Vector3 center = GetDungeonCentralPosition();
        center.y = gameSpawnY;

        var orderedPlayers = runner.ActivePlayers.OrderBy(p => p.PlayerId).ToList();
        int totalPlayers = orderedPlayers.Count;
        int index = orderedPlayers.FindIndex(p => p == player);

        if (totalPlayers <= 1 || index < 0 || gameSpawnRingRadius <= 0.01f)
        {
            return SnapToGround(center);
        }

        float angle = (Mathf.PI * 2f * index) / totalPlayers;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * gameSpawnRingRadius;
        return SnapToGround(center + offset);
    }

    private bool HasDistanceFromPlayers(Vector3 candidate, List<Vector3> existingPositions)
    {
        float minDist = Mathf.Max(0.1f, minSpawnDistanceBetweenPlayers);
        float minDistSqr = minDist * minDist;

        for (int i = 0; i < existingPositions.Count; i++)
        {
            if ((existingPositions[i] - candidate).sqrMagnitude < minDistSqr)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 SnapToGround(Vector3 position)
    {
        float rayHeight = Mathf.Max(1f, groundRayStartHeight);
        Vector3 rayOrigin = new Vector3(position.x, position.y + rayHeight, position.z);

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + groundSnapOffset;
        }

        return position;
    }

    private bool HasCollisionClearance(Vector3 position)
    {
        float radius = Mathf.Max(0.1f, spawnCollisionRadius);
        float height = Mathf.Max(radius * 2f + 0.1f, spawnCollisionHeight);

        Vector3 bottom = position + Vector3.up * radius;
        Vector3 top = position + Vector3.up * (height - radius);

        return !Physics.CheckCapsule(bottom, top, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        PlayerJoinedLogic(runner, player);
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[SPAWNER] Player {player.PlayerId} disconnected");

        // Check if boss disconnected
        if (bossSelected && player == bossPlayer)
        {
            Debug.Log($"[BOSS DISCONNECT] BOSS Player {player.PlayerId} LEFT");
            Debug.Log($"[BOSS DISCONNECT] Returning all players to menu");

            ReturnToMenu(runner);
            return;
        }

        Debug.Log($"[SPAWNER] Regular player left, game continues");

        if (runner.IsServer && runner.TryGetPlayerObject(player, out NetworkObject playerObj))
        {
            runner.Despawn(playerObj);
        }
    }

    private async void ReturnToMenu(NetworkRunner runner)
    {
        if (runner == null) return;

        try
        {
            Debug.Log("[BOSS DISCONNECT] Loading menu scene...");

            int sceneIndex = SceneUtility.GetBuildIndexByScenePath("Scenes/" + menuSceneName);
            if (sceneIndex >= 0)
            {
                await runner.LoadScene(SceneRef.FromIndex(sceneIndex));
            }
            else
            {
                await runner.LoadScene(SceneRef.FromIndex(menuSceneIndex));
            }

            // Wait a moment for scene to load
            await System.Threading.Tasks.Task.Delay(500);

            Debug.Log("[BOSS DISCONNECT] Shutting down runner...");
            await runner.Shutdown();
        }
        catch (Exception e)
        {
            Debug.LogError($"[BOSS DISCONNECT] Failed during cleanup: {e.Message}");
            if (runner != null)
            {
                await runner.Shutdown();
            }
        }
    }

    private void OnDestroy()
    {
        dungeonRunnerSpawned = false;

        if (registeredRunner != null)
        {
            registeredRunner.RemoveCallbacks(this);
        }

        callbacksRegistered = false;
        registeredRunner = null;
    }

    // Boilerplate Fusion callbacks
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        Debug.Log($"[SPAWNER] Runner shutdown: {reason}");
    }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}