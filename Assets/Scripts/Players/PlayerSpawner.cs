using UnityEngine;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;
using System.Linq;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    public NetworkObject PlayerPrefab;

    [Header("Character Prefabs")]
    public NetworkObject BossPrefab;      //prefab del Boss
    public NetworkObject SurvivorPrefab;  // prefab del Superviviente

    [Header("Dungeon Runner")]
    public NetworkObject dungeonNetworkRunnerPrefab;
    private bool dungeonRunnerSpawned = false;

    [Header("Boss System")]
    [SerializeField] private string menuSceneName = "LobbyList";
    [SerializeField] private int menuSceneIndex = 0;


    private bool bossSelected = false;
    private PlayerRef bossPlayer;
    private bool isSwappingBoss = false;
    private bool isSwappingBoss = false;
    private bool isSwappingBoss = false;

    private void Start()
    {
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null) runner.AddCallbacks(this);
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

        // Default spawn position
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
        }

        NetworkObject playerObj = runner.Spawn(PlayerPrefab, spawnPos, spawnRot, player);
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
        
        if (!Runner.IsServer || SceneManager.GetActiveScene().name != "Game" || bossSelected || isSwappingBoss) return;

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
            Vector3 pos = oldObj.transform.position;
            Quaternion rot = oldObj.transform.rotation;

            Debug.Log($"[SPAWNER] Despawning old player object at {pos}");

            Runner.SetPlayerObject(bossPlayer, null);
            Runner.Despawn(oldObj);

            if (BossPrefab != null)
            {
                Debug.Log($"[SPAWNER] Spawning Boss prefab at {pos}");
                NetworkObject newBoss = Runner.Spawn(BossPrefab, pos, rot, bossPlayer);
                Runner.SetPlayerObject(bossPlayer, newBoss);

                if (newBoss.TryGetComponent<PlayerRole>(out var role))
                {
                    role.SetBoss();
                    Debug.Log($"[SPAWNER] Boss role set successfully");
                }
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

        // Re-spawning logic for transitions into Lobby or Game
        if (currentScene == "Game" || currentScene == "LobbyRoom")
        {
            Debug.Log($"[SPAWNER] Scene {currentScene} loaded, spawning players");
            dungeonRunnerSpawned = false;
            bossSelected = false; // Reset boss selection for new scene
            isSwappingBoss = false; // Reset swap flag

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
    }

    // Boilerplate Fusion callbacks
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
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