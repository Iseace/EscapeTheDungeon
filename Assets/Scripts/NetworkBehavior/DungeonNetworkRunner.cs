using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement; // Added to check for the Game scene

public class DungeonNetworkRunner : NetworkBehaviour
{
    [Networked] public int SharedSeed { get; set; }

    private DungeonCreator dungeonCreator;
    private bool hasGeneratedLocally = false;

    public override void Spawned()
    {
        Debug.Log($"=== DungeonNetworkRunner.Spawned() ===");
        
        // We still try to find it, but we won't throw an error here 
        // because we might spawn this in the Lobby first.
        dungeonCreator = FindObjectOfType<DungeonCreator>();

        // Master client generates the seed ONLY if it's not set yet
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
        // 1. Only try to generate once
        if (hasGeneratedLocally)
            return;

        // 2. IMPORTANT: Only generate if we are in the "Game" scene
        // This prevents the dungeon from trying to spawn in your Lobby
        if (SceneManager.GetActiveScene().name != "Game")
            return;

        // 3. Wait for a valid seed
        if (SharedSeed == 0)
            return;

        // 4. Re-find the DungeonCreator if it was lost during scene transition
        if (dungeonCreator == null)
        {
            dungeonCreator = FindObjectOfType<DungeonCreator>();
            if (dungeonCreator == null) return; // Wait until it's loaded
        }

        // Generate the dungeon!
        Debug.Log($"[Player {Runner.LocalPlayer}] Generating dungeon in Game Scene with seed: {SharedSeed}");

        dungeonCreator.CreateDungeonWithSeed(SharedSeed);
        hasGeneratedLocally = true;

        Debug.Log($"[Player {Runner.LocalPlayer}] Dungeon generation complete!");
    }
}