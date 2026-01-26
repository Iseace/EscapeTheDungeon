using UnityEngine;
using Fusion;

public class DungeonNetworkRunner : NetworkBehaviour
{
    [Networked] public int SharedSeed { get; set; }

    private DungeonCreator dungeonCreator;
    private bool hasGeneratedLocally = false;

    public override void Spawned()
    {
        Debug.Log($"=== DungeonNetworkRunner.Spawned() ===");
        Debug.Log($"HasStateAuthority: {Object.HasStateAuthority}");
        Debug.Log($"HasInputAuthority: {Object.HasInputAuthority}");
        Debug.Log($"Runner.IsSharedModeMasterClient: {Runner.IsSharedModeMasterClient}");
        Debug.Log($"Runner.LocalPlayer: {Runner.LocalPlayer}");
        Debug.Log($"CurrentSeed: {SharedSeed}");

        // Find the DungeonCreator in the scene
        dungeonCreator = FindObjectOfType<DungeonCreator>();

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
            // Generate a random seed
            int newSeed = Random.Range(1, int.MaxValue);
            SharedSeed = newSeed;
            
            Debug.Log($"[MASTER CLIENT] Generated seed: {SharedSeed}");
        }
        else if (SharedSeed != 0)
        {
            Debug.Log($"[CLIENT] Seed already available: {SharedSeed}, will generate on next frame");
        }
        else
        {
            Debug.Log($"[CLIENT] Waiting for seed from master client...");
        }
    }

    // Change Render() instead of FixedUpdateNetwork() for Shared mode
    // Render() is called every frame on ALL clients, while FixedUpdateNetwork() 
    public override void Render()
    {
        // Only try to generate once
        if (hasGeneratedLocally)
            return;

        // Wait for a valid seed
        if (SharedSeed == 0)
        {
            return; // Still waiting for seed
        }

        if (dungeonCreator == null)
        {
            Debug.LogError("DungeonCreator is NULL in Render!");
            return;
        }

        // Generate the dungeon!
        Debug.Log($"[Player {Runner.LocalPlayer}] Generating dungeon with seed: {SharedSeed}");

        dungeonCreator.CreateDungeonWithSeed(SharedSeed);
        hasGeneratedLocally = true;

        Debug.Log($"[Player {Runner.LocalPlayer}] Dungeon generation complete!");
    }
}