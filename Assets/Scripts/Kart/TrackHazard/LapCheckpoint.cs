using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LapCheckpoint : MonoBehaviour
{
    [SerializeField] private LapTracker lapTracker;
    [SerializeField] private int checkpointIndex = 0;
    [SerializeField] private bool enableDebugLogs = true;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (lapTracker == null)
            lapTracker = FindAnyObjectByType<LapTracker>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (lapTracker == null) return;

        BroomMove racer = other.GetComponentInParent<BroomMove>();
        if (racer == null) return;

        if (enableDebugLogs)
            Debug.Log($"[LAP] Checkpoint {checkpointIndex} touched by {racer.name}.", this);

        lapTracker.RegisterCheckpointPass(racer, checkpointIndex);

        RaceRespawnData respawn = racer.GetComponent<RaceRespawnData>();
        if (respawn != null)
            respawn.SaveCheckpoint(transform.position, transform.rotation, checkpointIndex);
    }
}