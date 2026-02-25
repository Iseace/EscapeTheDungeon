using System.Collections.Generic;
using UnityEngine;

public class MissionObjectiveManager : MonoBehaviour
{
    public static MissionObjectiveManager Instance { get; private set; }

    [Header("Portal")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;

    private readonly HashSet<MissionObjectivePylon> pylons = new HashSet<MissionObjectivePylon>();
    private readonly HashSet<MissionObjectivePylon> activatedPylons = new HashSet<MissionObjectivePylon>();
    private int activatedCount;
    private bool portalSpawned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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
        Instantiate(portalPrefab, position, rotation);
        portalSpawned = true;
    }
}
