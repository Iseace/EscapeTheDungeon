using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LapTracker : MonoBehaviour
{
    [Header("Race")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private float minSecondsBetweenFinishPasses = 1f;

    [Header("Checkpoint Validation")]
    [SerializeField] private bool requireAllCheckpointsForLap = false;
    [SerializeField] private int checkpointCount = 0;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool logAllTriggerContacts = false;

    [Header("UI (Local Player) — legacy, now handled by RacerLapData on the prefab")]
    [SerializeField] private string lapPrefix = "Lap";
    [SerializeField] private string finishedText = "FINISHED";

    public event Action<BroomMove, int> OnLapCompleted;
    public event Action<BroomMove> OnRaceFinished;

    private sealed class RacerProgress
    {
        public int currentLap = 0;
        public bool finished;
        public float lastFinishPassTime = -999f;
        public HashSet<int> visitedCheckpoints = new HashSet<int>();
    }

    private readonly Dictionary<int, RacerProgress> progressByRacerId = new Dictionary<int, RacerProgress>();

    private void Awake()
    {
        Collider finishCollider = GetComponent<Collider>();
        finishCollider.isTrigger = true;

        totalLaps = Mathf.Max(1, totalLaps);
        checkpointCount = Mathf.Max(0, checkpointCount);

        if (enableDebugLogs)
        {
            Debug.Log($"[LAP] LapTracker active on {name}. ActiveInHierarchy={gameObject.activeInHierarchy}, ScriptEnabled={enabled}, ColliderEnabled={finishCollider.enabled}, IsTrigger={finishCollider.isTrigger}, Layer={gameObject.layer}", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs && logAllTriggerContacts)
        {
            Rigidbody otherRb = other.attachedRigidbody;
            string rbName = otherRb != null ? otherRb.name : "None";
            Debug.Log($"[LAP] Trigger contact from collider={other.name}, root={other.transform.root.name}, attachedRigidbody={rbName}", this);
        }

        BroomMove racer = other.GetComponentInParent<BroomMove>();
        if (racer == null)
        {
            return;
        }

        // Guard: NetworkObject must be valid before we touch any Fusion state
        if (racer.Object == null || !racer.Object.IsValid)
        {
            return;
        }

        if (!racer.Object.HasStateAuthority)
        {
            return;
        }

        int racerId = GetRacerId(racer);
        if (racerId == -1)
        {
            return;
        }

        string racerLabel = GetRacerLabel(racer);
        if (enableDebugLogs)
        {
            Debug.Log($"[LAP] Finish line crossed by {racerLabel}.", this);
        }

        RacerProgress progress = GetOrCreateProgress(racerId);
        if (progress.finished)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LAP] Ignored finish line crossing for {racerLabel} because race is already finished.", this);
            }
            return;
        }

        if (Time.time - progress.lastFinishPassTime < minSecondsBetweenFinishPasses)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LAP] Ignored finish line crossing for {racerLabel} due to cooldown ({minSecondsBetweenFinishPasses:0.00}s).", this);
            }
            return;
        }

        progress.lastFinishPassTime = Time.time;

        if (requireAllCheckpointsForLap && checkpointCount > 0 && progress.visitedCheckpoints.Count < checkpointCount)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LAP] Ignored lap for {racerLabel}: checkpoints {progress.visitedCheckpoints.Count}/{checkpointCount}.", this);
            }
            return;
        }

        progress.visitedCheckpoints.Clear();
        progress.currentLap++;
        OnLapCompleted?.Invoke(racer, progress.currentLap);

        if (enableDebugLogs)
        {
            Debug.Log($"[LAP] Lap counted for {racerLabel}: {progress.currentLap}/{totalLaps}.", this);
        }

        if (progress.currentLap >= totalLaps)
        {
            progress.finished = true;
            OnRaceFinished?.Invoke(racer);

            if (enableDebugLogs)
            {
                Debug.Log($"[LAP] Race finished by {racerLabel}.", this);
            }
        }

        RacerLapData lapData = racer.GetComponent<RacerLapData>();
        if (lapData != null)
        {
            lapData.SetLap(progress.currentLap, totalLaps, progress.finished);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"[LAP] RacerLapData not found on {racer.name}. Add it to the racer prefab!", this);
        }
    }

    public void RegisterCheckpointPass(BroomMove racer, int checkpointIndex)
    {
        if (racer == null)
        {
            return;
        }

        if (!requireAllCheckpointsForLap || checkpointCount <= 0)
        {
            return;
        }

        if (checkpointIndex < 0 || checkpointIndex >= checkpointCount)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[LAP] Ignored checkpoint index {checkpointIndex} for {GetRacerLabel(racer)}. Expected range: 0 to {checkpointCount - 1}.", this);
            }
            return;
        }

        int racerId = GetRacerId(racer);
        RacerProgress progress = GetOrCreateProgress(racerId);
        if (progress.finished)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[LAP] Ignored checkpoint {checkpointIndex} for {GetRacerLabel(racer)} because race is already finished.", this);
            }
            return;
        }

        progress.visitedCheckpoints.Add(checkpointIndex);

        if (enableDebugLogs)
        {
            Debug.Log($"[LAP] Checkpoint {checkpointIndex} passed by {GetRacerLabel(racer)} ({progress.visitedCheckpoints.Count}/{checkpointCount}).", this);
        }
    }

    public int GetCurrentLap(BroomMove racer)
    {
        if (racer == null)
        {
            return 0;
        }

        RacerProgress progress;
        int racerId = GetRacerId(racer);
        return progressByRacerId.TryGetValue(racerId, out progress) ? progress.currentLap : 0;
    }

    public bool HasFinished(BroomMove racer)
    {
        if (racer == null)
        {
            return false;
        }

        RacerProgress progress;
        int racerId = GetRacerId(racer);
        return progressByRacerId.TryGetValue(racerId, out progress) && progress.finished;
    }

    private RacerProgress GetOrCreateProgress(int racerId)
    {
        RacerProgress progress;
        if (!progressByRacerId.TryGetValue(racerId, out progress))
        {
            progress = new RacerProgress();
            progressByRacerId[racerId] = progress;
        }

        return progress;
    }

    private static string GetRacerLabel(BroomMove racer)
    {
        return racer == null ? "Unknown" : $"{racer.name}#{GetRacerId(racer)}";
    }

    private static int GetRacerId(BroomMove racer)
    {
        if (racer == null || racer.Object == null || !racer.Object.IsValid)
        {
            return -1;
        }

        return (int)racer.Object.Id.Raw;
    }
}