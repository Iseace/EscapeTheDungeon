using System;
using System.Collections.Generic;
using TMPro;
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

    [Header("UI (Local Player)")]
    [SerializeField] private TMP_Text localLapText;
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
    private BroomMove localRacer;
    private float nextLocalRacerResolveTime;

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

    private void Update()
    {
        RefreshLocalHud();
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

        string racerLabel = GetRacerLabel(racer);
        if (enableDebugLogs)
        {
            Debug.Log($"[LAP] Finish line crossed by {racerLabel}.", this);
        }

        int racerId = GetRacerId(racer);
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

        if (racer == localRacer)
        {
            UpdateLocalLapText(progress);
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

    private void RefreshLocalHud()
    {
        // 1. Try to find the local racer if we don't have one yet
        if (localRacer == null && Time.time >= nextLocalRacerResolveTime)
        {
            BroomMove[] allRacers = FindObjectsByType<BroomMove>(FindObjectsSortMode.None);
            foreach (var racer in allRacers)
            {
                if (racer != null && racer.HasInputAuthority)
                {
                    localRacer = racer;

                    // 2. NEW: Automatically find the TMP_Text on the spawned prefab
                    // This searches the racer and all its children for a TMP_Text component
                    localLapText = racer.GetComponentInChildren<TMP_Text>();

                    if (localLapText == null && enableDebugLogs)
                    {
                        Debug.LogWarning($"[LAP] Found local racer {racer.name}, but no TMP_Text found on it!");
                    }
                    break;
                }
            }
            nextLocalRacerResolveTime = Time.time + 0.5f;
        }

        // 3. Safety check: If we still don't have a text component, we can't update anything
        if (localLapText == null) return;

        // 4. Update the text based on progress
        if (localRacer == null)
        {
            localLapText.text = $"{lapPrefix} 1/{totalLaps}";
            return;
        }

        UpdateLocalLapText(GetOrCreateProgress(GetRacerId(localRacer)));
    }

    private void UpdateLocalLapText(RacerProgress progress)
    {
        if (localLapText == null || progress == null)
        {
            return;
        }

        if (progress.finished)
        {
            localLapText.text = finishedText;
            return;
        }

        int displayLap = Mathf.Clamp(progress.currentLap + 1, 1, totalLaps);
        localLapText.text = string.Format("{0} {1}/{2}", lapPrefix, displayLap, totalLaps);
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

        return racer.Object.InputAuthority.PlayerId;
    }
}
