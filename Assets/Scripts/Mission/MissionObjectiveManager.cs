using System.Collections.Generic;
using UnityEngine;

public class MissionObjectiveManager : MonoBehaviour
{
    public static MissionObjectiveManager Instance { get; private set; }

    public event System.Action<MissionObjectivePylon> PylonActivated;
    public event System.Action<Vector3, Quaternion> PortalSpawned;

    [Header("Portal")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;
    [SerializeField] private bool useRandomSpawnFromDungeon = true;
    [SerializeField] private float portalSpawnBlockCheckRadius = 1.25f;
    private float portalHeightOffset = 3f;

    [Header("Escape Timer")]
    [SerializeField] private bool enableEscapeTimeLimit = true;
    [SerializeField] private float escapeTimeLimitSeconds = 90f;

    private readonly HashSet<MissionObjectivePylon> pylons = new HashSet<MissionObjectivePylon>();
    private readonly HashSet<MissionObjectivePylon> activatedPylons = new HashSet<MissionObjectivePylon>();
    private readonly List<Vector3> portalCandidates = new List<Vector3>();
    private int activatedCount;
    private bool portalSpawned;
    private bool escapeTimerRunning;
    private float remainingEscapeTime;
    private bool escapeWindowClosed;
    private readonly Collider[] portalSpawnCheckHits = new Collider[48];

    public bool IsEscapeWindowOpen => portalSpawned && !escapeWindowClosed;
    public float RemainingEscapeTime => remainingEscapeTime;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (!escapeTimerRunning || !enableEscapeTimeLimit || escapeWindowClosed) return;

        remainingEscapeTime -= Time.deltaTime;
        if (remainingEscapeTime <= 0f)
        {
            remainingEscapeTime = 0f;
            escapeWindowClosed = true;
            escapeTimerRunning = false;
        }
    }

    public void Configure(
        GameObject portalPrefabOverride,
        float timeLimitSeconds,
        bool randomSpawn,
        List<Vector3> randomCandidates,
        float heightOffset)
    {
        if (portalPrefabOverride != null)
            portalPrefab = portalPrefabOverride;

        useRandomSpawnFromDungeon = randomSpawn;
        portalHeightOffset = heightOffset;
        escapeTimeLimitSeconds = Mathf.Max(5f, timeLimitSeconds);
        enableEscapeTimeLimit = true;

        portalCandidates.Clear();
        if (randomCandidates != null && randomCandidates.Count > 0)
        {
            portalCandidates.AddRange(randomCandidates);
        }

        ResetMissionState();
    }

    public void SetPortalCandidates(List<Vector3> randomCandidates)
    {
        portalCandidates.Clear();
        if (randomCandidates != null && randomCandidates.Count > 0)
        {
            portalCandidates.AddRange(randomCandidates);
        }
    }

    public void ResetMissionState()
    {
        pylons.Clear();
        activatedPylons.Clear();
        activatedCount = 0;
        portalSpawned = false;
        escapeWindowClosed = false;
        escapeTimerRunning = false;
        remainingEscapeTime = escapeTimeLimitSeconds;
    }

    public void RegisterPylon(MissionObjectivePylon pylon)
    {
        if (pylon == null || pylons.Contains(pylon)) return;

        pylons.Add(pylon);
        if (pylon.IsActivated)
        {
            activatedPylons.Add(pylon);
            activatedCount = activatedPylons.Count;
        }
        CheckCompletion();
    }

    public void UnregisterPylon(MissionObjectivePylon pylon)
    {
        if (pylon == null || !pylons.Remove(pylon)) return;

        if (activatedPylons.Remove(pylon))
        {
            activatedCount = activatedPylons.Count;
        }
    }

    public void NotifyPylonActivated(MissionObjectivePylon pylon)
    {
        if (pylon == null) return;

        if (!pylons.Contains(pylon))
        {
            pylons.Add(pylon);
        }

        if (activatedPylons.Add(pylon))
        {
            activatedCount = activatedPylons.Count;
            PylonActivated?.Invoke(pylon);
        }

        CheckCompletion();
    }

    private void CheckCompletion()
    {
        if (portalSpawned || pylons.Count == 0) return;
        if (activatedCount < pylons.Count) return;

        SpawnPortal();
    }

    private void SpawnPortal()
    {
        if (portalPrefab == null || portalSpawned) return;

        Vector3 position = portalSpawnPoint != null ? portalSpawnPoint.position : Vector3.zero;
        Quaternion rotation = portalSpawnPoint != null ? portalSpawnPoint.rotation : Quaternion.identity;

        if (useRandomSpawnFromDungeon && portalCandidates.Count > 0)
        {
            if (!TryPickRandomClearPortalCandidate(out position))
            {
                position = portalSpawnPoint != null ? portalSpawnPoint.position : Vector3.zero;
                Debug.LogWarning("[MissionObjectiveManager] No hay candidatos libres para portal (objetivos/items bloqueando). Se usa portalSpawnPoint fallback.");
            }
        }

        if (!Mathf.Approximately(portalHeightOffset, 0f))
        {
            position += Vector3.up * portalHeightOffset;
        }

        SpawnPortalInternal(position, rotation, emitEvent: true);
    }

    private bool TryPickRandomClearPortalCandidate(out Vector3 position)
    {
        position = Vector3.zero;
        if (portalCandidates == null || portalCandidates.Count == 0) return false;

        int start = Random.Range(0, portalCandidates.Count);

        for (int i = 0; i < portalCandidates.Count; i++)
        {
            int index = (start + i) % portalCandidates.Count;
            Vector3 candidate = portalCandidates[index];
            if (IsPortalPositionBlocked(candidate)) continue;

            position = candidate;
            return true;
        }

        return false;
    }

    private bool IsPortalPositionBlocked(Vector3 position)
    {
        float radius = Mathf.Max(0.1f, portalSpawnBlockCheckRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(position, radius, portalSpawnCheckHits, ~0, QueryTriggerInteraction.Collide);
        if (hitCount <= 0) return false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = portalSpawnCheckHits[i];
            if (col == null) continue;

            if (col.GetComponentInParent<MissionObjectivePylon>() != null) return true;
            if (col.GetComponentInParent<InteractableItem>() != null) return true;
        }

        return false;
    }

    public void SpawnPortalFromNetwork(Vector3 position, Quaternion rotation)
    {
        if (portalPrefab == null || portalSpawned) return;
        SpawnPortalInternal(position, rotation, emitEvent: false);
    }

    private void SpawnPortalInternal(Vector3 position, Quaternion rotation, bool emitEvent)
    {
        Instantiate(portalPrefab, position, rotation);
        portalSpawned = true;

        remainingEscapeTime = escapeTimeLimitSeconds;
        escapeTimerRunning = enableEscapeTimeLimit;
        escapeWindowClosed = false;

        if (emitEvent)
        {
            PortalSpawned?.Invoke(position, rotation);
        }
    }
}
