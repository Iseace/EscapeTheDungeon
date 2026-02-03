using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement; 

public class DungeonNetworkRunner : NetworkBehaviour
{
    [Networked] public int SharedSeed { get; set; }

    private DungeonCreator dungeonCreator;
    private bool hasGeneratedLocally = false;

    public override void Spawned()
    {
        Debug.Log($"=== DungeonNetworkRunner.Spawned() ===");
        
        dungeonCreator = FindAnyObjectByType<DungeonCreator>();

        if (dungeonCreator == null)
        {
            Debug.LogError("DungeonCreator NOT found in scene!");
        }

        bool shouldGenerateSeed = Runner.GameMode == GameMode.Shared 
            ? Runner.IsSharedModeMasterClient 
            : Object.HasStateAuthority;

        if (shouldGenerateSeed && SharedSeed == 0)
        {
            int newSeed = Random.Range(1, int.MaxValue);
            SharedSeed = newSeed;
            Debug.Log($"[MASTER CLIENT] Generated seed: {SharedSeed}");
        }
    }

    public override void Render()
    {
        if (hasGeneratedLocally) return;
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
    }
}