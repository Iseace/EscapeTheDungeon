using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement; 

public class DungeonNetworkRunner : NetworkBehaviour
{
    [Networked] public int SharedSeed { get; set; }

    private DungeonCreator dungeonCreator;
    private MissionObjectiveManager missionObjectiveManager;
    private bool hasGeneratedLocally = false;
    private bool missionSyncHooked = false;

    public override void Spawned()
    {
        Debug.Log($"=== DungeonNetworkRunner.Spawned() ===");
        Debug.Log($"HasStateAuthority: {Object.HasStateAuthority}");
        Debug.Log($"HasInputAuthority: {Object.HasInputAuthority}");
        Debug.Log($"Runner.IsSharedModeMasterClient: {Runner.IsSharedModeMasterClient}");
        Debug.Log($"Runner.LocalPlayer: {Runner.LocalPlayer}");
        Debug.Log($"CurrentSeed: {SharedSeed}");

        // Find the DungeonCreator in the scene
        dungeonCreator = FindFirstObjectByType<DungeonCreator>();

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
        TryHookMissionSync();
    }

    private void TryHookMissionSync()
    {
        if (missionSyncHooked) return;

        missionObjectiveManager = MissionObjectiveManager.Instance;
        if (missionObjectiveManager == null)
        {
            missionObjectiveManager = FindAnyObjectByType<MissionObjectiveManager>();
        }

        if (missionObjectiveManager == null) return;

        missionObjectiveManager.PylonActivated += OnLocalPylonActivated;
        missionObjectiveManager.PortalSpawned += OnLocalPortalSpawned;
        missionSyncHooked = true;
    }

    private void OnDestroy()
    {
        if (!missionSyncHooked || missionObjectiveManager == null) return;

        missionObjectiveManager.PylonActivated -= OnLocalPylonActivated;
        missionObjectiveManager.PortalSpawned -= OnLocalPortalSpawned;
        missionSyncHooked = false;
    }

    private void OnLocalPylonActivated(MissionObjectivePylon pylon)
    {
        if (!Object.HasStateAuthority || pylon == null) return;
        Rpc_SyncPylonActivated(pylon.transform.position);
    }

    private void OnLocalPortalSpawned(Vector3 position, Quaternion rotation)
    {
        if (!Object.HasStateAuthority) return;
        Rpc_SyncPortalSpawn(position, rotation);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncPylonActivated(Vector3 pylonWorldPosition)
    {
        if (Object.HasStateAuthority) return;

        MissionObjectivePylon[] pylons = FindObjectsOfType<MissionObjectivePylon>();
        if (pylons == null || pylons.Length == 0) return;

        MissionObjectivePylon closest = null;
        float bestDistSqr = float.MaxValue;

        for (int i = 0; i < pylons.Length; i++)
        {
            var p = pylons[i];
            if (p == null) continue;
            float d = (p.transform.position - pylonWorldPosition).sqrMagnitude;
            if (d < bestDistSqr)
            {
                bestDistSqr = d;
                closest = p;
            }
        }

        if (closest != null && bestDistSqr <= 4f)
        {
            closest.ForceActivateFromNetwork();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncPortalSpawn(Vector3 position, Quaternion rotation)
    {
        if (Object.HasStateAuthority) return;

        if (missionObjectiveManager == null)
        {
            missionObjectiveManager = MissionObjectiveManager.Instance;
            if (missionObjectiveManager == null)
            {
                missionObjectiveManager = FindAnyObjectByType<MissionObjectiveManager>();
            }
        }

        missionObjectiveManager?.SpawnPortalFromNetwork(position, rotation);
    }
}