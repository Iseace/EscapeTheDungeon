using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LapCheckpoint : MonoBehaviour
{
    [SerializeField] private LapTracker lapTracker;
    [SerializeField] private int checkpointIndex = 0;
    [SerializeField] private bool enableDebugLogs = true;

    private void Awake()
    {
        Collider checkpointCollider = GetComponent<Collider>();
        checkpointCollider.isTrigger = true;

        if (lapTracker == null)
        {
            lapTracker = FindAnyObjectByType<LapTracker>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (lapTracker == null)
        {
            return;
        }

        BroomMove racer = other.GetComponentInParent<BroomMove>();
        if (racer == null)
        {
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[LAP] Checkpoint trigger {checkpointIndex} touched by {racer.name}.", this);
        }

        lapTracker.RegisterCheckpointPass(racer, checkpointIndex);
    }
}
