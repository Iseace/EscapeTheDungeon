using UnityEngine;
using Fusion;
using UnityEngine.SceneManagement; 

public class DungeonNetworkRunner : NetworkBehaviour
{
    public static DungeonNetworkRunner Instance { get; private set; }

    public bool HasMissionStateAuthority => Object != null && Object.HasStateAuthority;

    [Networked] public int SharedSeed { get; set; }
    [SerializeField] private float pylonProgressSyncInterval = 0.1f;
    [SerializeField] private float pylonSyncMaxDistance = 0.5f;

    [Header("Match Flow")]
    [Tooltip("Fallback: usado si no se encuentra DungeonCreator o no tiene config")]
    [SerializeField] private float bossFreezeDurationSeconds = 10f;
    [Tooltip("Fallback: usado si no se encuentra DungeonCreator o no tiene config")]
    [SerializeField] private bool enableMatchTimeLimit = true;
    [Tooltip("Fallback: usado si no se encuentra DungeonCreator o no tiene config")]
    [SerializeField] private float matchDurationSeconds = 600f;

    [Networked] public NetworkBool MatchInProgress { get; set; }
    [Networked] public NetworkBool MatchEnded { get; set; }
    [Networked] private TickTimer BossFreezeTimer { get; set; }
    [Networked] private TickTimer MatchTimer { get; set; }

    private DungeonCreator dungeonCreator;
    private MissionObjectiveManager missionObjectiveManager;
    private bool hasGeneratedLocally = false;
    private bool missionSyncHooked = false;
    private float nextPylonProgressSyncTime;
    private bool localBossReleasedLogged;
    private bool localMatchEndedLogged;
    private bool isEndingMatch;

    public float RemainingMatchTimeSeconds
    {
        get
        {
            if (!MatchInProgress || !enableMatchTimeLimit) return 0f;
            float? remaining = MatchTimer.RemainingTime(Runner);
            return Mathf.Max(0f, remaining ?? 0f);
        }
    }

    public bool HasMatchTimeLimit => enableMatchTimeLimit;

    public float RemainingBossFreezeTimeSeconds
    {
        get
        {
            if (!MatchInProgress || !IsBossFrozen) return 0f;
            float? remaining = BossFreezeTimer.RemainingTime(Runner);
            return Mathf.Max(0f, remaining ?? 0f);
        }
    }

    public bool IsBossFrozen
    {
        get
        {
            if (!MatchInProgress) return false;
            return !BossFreezeTimer.ExpiredOrNotRunning(Runner);
        }
    }

    public override void Spawned()
    {
        Instance = this;

        Debug.Log($"=== DungeonNetworkRunner.Spawned() ===");
        Debug.Log($"HasStateAuthority: {Object.HasStateAuthority}");
        Debug.Log($"HasInputAuthority: {Object.HasInputAuthority}");
        Debug.Log($"Runner.IsSharedModeMasterClient: {Runner.IsSharedModeMasterClient}");
        Debug.Log($"Runner.LocalPlayer: {Runner.LocalPlayer}");
        Debug.Log($"CurrentSeed: {SharedSeed}");

        if (Object.HasStateAuthority)
        {
            MatchInProgress = false;
            MatchEnded = false;
            BossFreezeTimer = TickTimer.None;
            MatchTimer = TickTimer.None;
        }

        localBossReleasedLogged = false;
        localMatchEndedLogged = false;
        isEndingMatch = false;

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
        if (!hasGeneratedLocally)
        {
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

        if (MatchInProgress && !IsBossFrozen && !localBossReleasedLogged)
        {
            localBossReleasedLogged = true;
            Debug.Log("[MATCH] Boss liberado. Los supervivientes ya tuvieron tiempo para alejarse.");
        }

        if (MatchEnded && !localMatchEndedLogged)
        {
            localMatchEndedLogged = true;
            Debug.Log("[MATCH] Tiempo de partida finalizado.");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (!MatchInProgress || MatchEnded) return;

        if (!enableMatchTimeLimit) return;

        if (IsBossFrozen) return;

        if (!MatchTimer.IsRunning)
        {
            MatchTimer = TickTimer.CreateFromSeconds(Runner, matchDurationSeconds);
            Debug.Log($"[MATCH] Inicia el timer global de partida: {matchDurationSeconds:0.##}s");
            return;
        }

        if (MatchTimer.Expired(Runner))
        {
            EndMatchByTimeLimit();
        }
    }

    public void StartMatchFlow()
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (MatchInProgress || MatchEnded) return;

        ApplyMatchSettingsFromDungeonCreator();

        bossFreezeDurationSeconds = Mathf.Max(0f, bossFreezeDurationSeconds);
        matchDurationSeconds = Mathf.Max(5f, matchDurationSeconds);

        MatchInProgress = true;
        MatchEnded = false;
        BossFreezeTimer = bossFreezeDurationSeconds > 0f
            ? TickTimer.CreateFromSeconds(Runner, bossFreezeDurationSeconds)
            : TickTimer.None;
        MatchTimer = TickTimer.None;

        localBossReleasedLogged = false;
        localMatchEndedLogged = false;

        Debug.Log($"[MATCH] Inicio de partida. Boss inmovil por {bossFreezeDurationSeconds:0.##}s. " +
                  (enableMatchTimeLimit
                      ? $"Duracion total: {matchDurationSeconds:0.##}s (comienza tras liberar al boss)"
                      : "Sin limite de tiempo global"));
    }

    private void ApplyMatchSettingsFromDungeonCreator()
    {
        if (dungeonCreator == null)
        {
            dungeonCreator = FindAnyObjectByType<DungeonCreator>();
        }

        if (dungeonCreator == null) return;

        bossFreezeDurationSeconds = dungeonCreator.GetBossFreezeDurationSeconds();
        enableMatchTimeLimit = dungeonCreator.GetEnableMatchTimeLimit();
        matchDurationSeconds = dungeonCreator.GetMatchDurationSeconds();
    }

    private void LateUpdate()
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (SceneManager.GetActiveScene().name != "Game") return;
        if (Time.time < nextPylonProgressSyncTime) return;

        nextPylonProgressSyncTime = Time.time + Mathf.Max(0.02f, pylonProgressSyncInterval);
        SyncPylonProgressToPeers();
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
        if (Instance == this)
        {
            Instance = null;
        }

        if (!missionSyncHooked || missionObjectiveManager == null) return;

        missionObjectiveManager.PylonActivated -= OnLocalPylonActivated;
        missionObjectiveManager.PortalSpawned -= OnLocalPortalSpawned;
        missionSyncHooked = false;
    }

    private async void EndMatchByTimeLimit()
    {
        if (isEndingMatch) return;
        isEndingMatch = true;

        MatchEnded = true;
        MatchInProgress = false;
        BossFreezeTimer = TickTimer.None;

        Debug.Log("[MATCH] El tiempo global se agoto. Cerrando sesion del host para terminar la partida.");
        try
        {
            await Runner.Shutdown();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MATCH] Error al cerrar la sesion: {e.Message}");
        }
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

    private void SyncPylonProgressToPeers()
    {
        MissionObjectivePylon[] pylons = FindObjectsOfType<MissionObjectivePylon>();
        if (pylons == null || pylons.Length == 0) return;

        for (int i = 0; i < pylons.Length; i++)
        {
            var pylon = pylons[i];
            if (pylon == null) continue;
            Rpc_SyncPylonProgress(pylon.transform.position, pylon.Progress01, pylon.IsActivated);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncPylonActivated(Vector3 pylonWorldPosition)
    {
        if (Object.HasStateAuthority) return;

        float maxDistSqr = Mathf.Max(0.01f, pylonSyncMaxDistance);
        maxDistSqr *= maxDistSqr;

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

        if (closest != null && bestDistSqr <= maxDistSqr)
        {
            closest.ForceActivateFromNetwork();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_SyncPylonProgress(Vector3 pylonWorldPosition, float progress01, bool isActivated)
    {
        if (Object.HasStateAuthority) return;

        float maxDistSqr = Mathf.Max(0.01f, pylonSyncMaxDistance);
        maxDistSqr *= maxDistSqr;

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

        if (closest == null || bestDistSqr > maxDistSqr) return;

        if (isActivated)
        {
            closest.ForceActivateFromNetwork();
            return;
        }

        closest.ForceProgressFromNetwork(progress01);
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