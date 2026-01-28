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
        Debug.Log($"PlayerJoined - Player: {player.PlayerId}, LocalPlayer: {Runner.LocalPlayer.PlayerId}, IsServer: {Runner.IsServer}, GameMode: {Runner.GameMode}");
      
        // Only the server/host should spawn objects
        if (!Runner.IsServer)
        {
            Debug.Log("Not server, skipping spawn");
            return;
        }
        
        // Spawn the DungeonNetworkRunner once (only on server)
        if (!dungeonRunnerSpawned)
        {
            // Check if one already exists
            var existing = FindObjectOfType<DungeonNetworkRunner>();
            if (existing == null)
            {
                Debug.Log("Server spawning DungeonNetworkRunner...");
                
                NetworkObject dungeonRunner = Runner.Spawn(
                    dungeonNetworkRunnerPrefab, 
                    Vector3.zero, 
                    Quaternion.identity
                );
                
                dungeonRunnerSpawned = true;
                Debug.Log($"DungeonNetworkRunner spawned");
            }
            else
            {
                Debug.Log("DungeonNetworkRunner already exists, skipping spawn");
                dungeonRunnerSpawned = true;
            }
        }
        
        // Spawn player for the joining player (server spawns for ALL players)
        Vector3 spawnPos = new Vector3(3f, 1f, 3f);
        
        NetworkObject playerObj = Runner.Spawn(
            PlayerPrefab, 
            spawnPos, 
            Quaternion.identity, 
            inputAuthority: player  // Give input authority to the player who joined
        );
        
        Debug.Log($"Server spawned player for {player.PlayerId} at {spawnPos}");
    }
    
    // Reset the flag when destroyed
    private void OnDestroy()
    {
        dungeonRunnerSpawned = false;
    }
}