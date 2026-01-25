using UnityEngine;
using Fusion;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined
{
    public NetworkObject PlayerPrefab;
    
    [Header("Dungeon Runner")]
    public NetworkObject dungeonNetworkRunnerPrefab;

    // Track if we've spawned the dungeon runner
    private bool dungeonRunnerSpawned = false;

    public void PlayerJoined(PlayerRef player)
    {
        Debug.Log($"PlayerJoined - Player: {player.PlayerId}, LocalPlayer: {Runner.LocalPlayer.PlayerId}, IsServer: {Runner.IsServer}, IsSharedModeMasterClient: {Runner.IsSharedModeMasterClient}");

        // In Shared mode, the FIRST player (master client) spawns the DungeonNetworkRunner
        // Use IsSharedModeMasterClient instead of IsServer for Shared mode
        bool shouldSpawnDungeon = Runner.GameMode == GameMode.Shared 
            ? Runner.IsSharedModeMasterClient 
            : Runner.IsServer;

        if (shouldSpawnDungeon && !dungeonRunnerSpawned)
        {
            // Check if one already exists (in case of reconnection (not tested yet))
            var existing = FindObjectOfType<DungeonNetworkRunner>();
            if (existing == null)
            {
                Debug.Log("Master client spawning DungeonNetworkRunner...");
                
                // In Shared mode, give state authority to the master client
                NetworkObject dungeonRunner = Runner.Spawn(
                    dungeonNetworkRunnerPrefab, 
                    Vector3.zero, 
                    Quaternion.identity,
                    inputAuthority: Runner.LocalPlayer  // Give authority to master client
                );
                
                dungeonRunnerSpawned = true;
                Debug.Log($"DungeonNetworkRunner spawned with authority: {dungeonRunner.HasStateAuthority}");
            }
            else
            {
                Debug.Log("DungeonNetworkRunner already exists, skipping spawn");
                dungeonRunnerSpawned = true;
            }
        }

        // Spawn the local player for this client
        if (player == Runner.LocalPlayer)
        {
            Vector3 spawnPos = new Vector3(3, 3, 3);
            NetworkObject playerObj = Runner.Spawn(
                PlayerPrefab, 
                spawnPos, 
                Quaternion.identity, 
                inputAuthority: player
            );
            Debug.Log($"Local player spawned at {spawnPos} with input authority");
        }
    }

    // Reset the flag when destroyed
    private void OnDestroy()
    {
        dungeonRunnerSpawned = false;
    }
}