using UnityEngine;
using Fusion;

public class PlayerSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    public NetworkObject PlayerPrefab;
    
    [Header("Dungeon Runner")]
    public NetworkObject dungeonNetworkRunnerPrefab;
    private bool dungeonRunnerSpawned = false;
    public void PlayerJoined(PlayerRef player)
    {
        if (!Runner.IsServer) return;
        
        if (!dungeonRunnerSpawned){
            var existing = FindObjectOfType<DungeonNetworkRunner>();
            if (existing == null){
                Runner.Spawn(dungeonNetworkRunnerPrefab, Vector3.zero, Quaternion.identity);
                dungeonRunnerSpawned = true;
            }
        }
        
        Vector3 spawnPos = new Vector3(3f, 1f, 3f);
        NetworkObject playerObj = Runner.Spawn(
            PlayerPrefab, 
            spawnPos, 
            Quaternion.identity, 
            inputAuthority: player 
        );
        
        Runner.SetPlayerObject(player, playerObj);
        Debug.Log($"Server spawned and assigned player for {player.PlayerId}");
    }

    public void PlayerLeft(PlayerRef player){
        if (!Runner.IsServer) return;

        if (Runner.TryGetPlayerObject(player, out NetworkObject playerObj)){
            Debug.Log($"Player {player.PlayerId} left. Despawning their character.");
            Runner.Despawn(playerObj);
        }
    }
    
    private void OnDestroy(){
        dungeonRunnerSpawned = false;
    }
}