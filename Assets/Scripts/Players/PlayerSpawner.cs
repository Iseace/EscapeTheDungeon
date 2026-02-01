using UnityEngine;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System;

public class PlayerSpawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    public NetworkObject PlayerPrefab;
    
    [Header("Dungeon Runner")]
    public NetworkObject dungeonNetworkRunnerPrefab;
    private bool dungeonRunnerSpawned = false;

    private void Start()
    {
        var runner = FindObjectOfType<NetworkRunner>();
        if (runner != null) runner.AddCallbacks(this);
    }

    public void PlayerJoinedLogic(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null || !runner.IsServer) return;
        
        // Handle Dungeon Runner logic only in the Game scene
        if (SceneManager.GetActiveScene().name == "Game")
        {
            if (!dungeonRunnerSpawned){
                var existing = FindObjectOfType<DungeonNetworkRunner>();
                if (existing == null){
                    runner.Spawn(dungeonNetworkRunnerPrefab, Vector3.zero, Quaternion.identity);
                    dungeonRunnerSpawned = true;
                }
            }
        }
        
        // Default spawn for the Game scene
        Vector3 spawnPos = new Vector3(3f, 1f, 3f);
        Quaternion spawnRot = Quaternion.identity;

        // LOBBY LINE-UP: Positioning players side-by-side
        if (SceneManager.GetActiveScene().name == "Lobby")
        {
            // 'spacing' is the distance between each character
            float spacing = 1.5f; 
            
            // This spreads players out on the X-axis: Player 0 is at 0, Player 1 at 1.5, etc.
            spawnPos = new Vector3(player.PlayerId * spacing, 0f, 0f);
            
            // Using identity rotation because you already adjusted your camera to face this spot
            spawnRot = Quaternion.identity;
        }

        NetworkObject playerObj = runner.Spawn(PlayerPrefab, spawnPos, spawnRot, player);
        runner.SetPlayerObject(player, playerObj);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        if (!runner.IsServer) return;

        string currentScene = SceneManager.GetActiveScene().name;
        // Re-spawning logic for transitions into Lobby or Game
        if (currentScene == "Game" || currentScene == "Lobby")
        {
            Debug.Log($"Scene {currentScene} loaded. Spawning players in lineup.");
            dungeonRunnerSpawned = false; 

            foreach (var player in runner.ActivePlayers)
            {
                PlayerJoinedLogic(runner, player);
            }
        }
    }

    // --- INTERFACE IMPLEMENTATIONS ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { PlayerJoinedLogic(runner, player); }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) 
    { 
        if (runner.IsServer && runner.TryGetPlayerObject(player, out NetworkObject playerObj))
            runner.Despawn(playerObj);
    }

    // Boilerplate Fusion callbacks
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
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