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
    
    [Header("Dungeon Runner")]
    public NetworkObject dungeonNetworkRunnerPrefab;
    private bool dungeonRunnerSpawned = false;

    [Header("Boss System")]
    [SerializeField] private string menuSceneName = "LobbyMenu";
    [SerializeField] private int menuSceneIndex = 0;
    
    private bool bossSelected = false;
    private PlayerRef bossPlayer;

private void Start()
    {
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null) runner.AddCallbacks(this);
    }

    public void PlayerJoinedLogic(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer) return;
        
        Debug.Log($"[SPAWNER] Player {player.PlayerId} joining");
        
        // Handle Dungeon Runner logic only in the Game scene
        if (SceneManager.GetActiveScene().name == "Game")
        {
            if (!dungeonRunnerSpawned)
            {
                var existing = FindFirstObjectByType<DungeonNetworkRunner>();
                if (existing == null){
                    runner.Spawn(dungeonNetworkRunnerPrefab, Vector3.zero, Quaternion.identity);
                    dungeonRunnerSpawned = true;
                }
            }
        }
        
        // Default spawn position
        Vector3 spawnPos = new Vector3(3f, 1f, 3f);
        Quaternion spawnRot = Quaternion.identity;

        // LOBBY LINE-UP: Positioning players side-by-side
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            float spacing = 1.5f; 
            spawnPos = new Vector3(player.PlayerId * spacing, 0f, 0f);
            spawnRot = Quaternion.identity;
        }

        NetworkObject playerObj = runner.Spawn(PlayerPrefab, spawnPos, spawnRot, player);
        runner.SetPlayerObject(player, playerObj);

        Debug.Log($"[SPAWNER] Player {player.PlayerId} spawned");
    }

    public override void FixedUpdateNetwork()
    {
        // Only server selects boss and only in Game scene
        if (!Runner.IsServer) return;
        if (SceneManager.GetActiveScene().name != "Game") return;
        if (bossSelected) return;

        // Wait for players to spawn
        if (Runner.ActivePlayers.Count() == 0) return;

        // Check if all players have their objects spawned
        bool allSpawned = true;
        foreach (var player in Runner.ActivePlayers)
        {
            if (!Runner.TryGetPlayerObject(player, out NetworkObject _))
            {
                allSpawned = false;
                break;
            }
        }

        if (!allSpawned) return;

        // RANDOM BOSS SELECTION
        List<PlayerRef> players = Runner.ActivePlayers.ToList();
        
        Debug.Log($"[BOSS SELECTION] Selecting boss from {players.Count} players");
        foreach (var p in players)
        {
            Debug.Log($"[BOSS SELECTION] - Player {p.PlayerId}");
        }

        int randomIndex = UnityEngine.Random.Range(0, players.Count);
        bossPlayer = players[randomIndex];
        
        Debug.Log($"[BOSS SELECTION] Random Index: {randomIndex}");
        Debug.Log($"[BOSS SELECTION] BOSS IS: Player {bossPlayer.PlayerId}");

        // Assign boss role
        if (Runner.TryGetPlayerObject(bossPlayer, out NetworkObject bossObj))
        {
            PlayerRole role = bossObj.GetComponent<PlayerRole>();
            if (role != null)
            {
                role.SetBoss();
            }
        }

        bossSelected = true;
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer) return;

        string currentScene = SceneManager.GetActiveScene().name;
        
        // Re-spawning logic for transitions into Lobby or Game
        if (currentScene == "Game" || currentScene == "Lobby")
        {
            Debug.Log($"[SPAWNER] Scene {currentScene} loaded, spawning players");
            dungeonRunnerSpawned = false;
            bossSelected = false; // Reset boss selection for new scene

            foreach (var player in runner.ActivePlayers)
            {
                PlayerJoinedLogic(runner, player);
            }
        }
    }

    // --- INTERFACE IMPLEMENTATIONS ---
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

    private void ReturnToMenu(NetworkRunner runner)
    {
        try
        {
            int sceneIndex = SceneUtility.GetBuildIndexByScenePath("Scenes/" + menuSceneName);
            if (sceneIndex >= 0)
            {
                Debug.Log($"[BOSS DISCONNECT] Loading menu: {menuSceneName} at index {sceneIndex}");
                runner.LoadScene(SceneRef.FromIndex(sceneIndex));
            }
            else
            {
                Debug.Log($"[BOSS DISCONNECT] Loading menu by index: {menuSceneIndex}");
                runner.LoadScene(SceneRef.FromIndex(menuSceneIndex));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BOSS DISCONNECT] Failed to load menu: {e.Message}");
            runner.Shutdown();
        }
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