using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RacerLapData : NetworkBehaviour
{
    [Tooltip("Prefix shown before the lap numbers, e.g. 'Lap'.")]
    [SerializeField] private string lapPrefix = "Lap";

    [Tooltip("Text shown when this racer finishes the race.")]
    [SerializeField] private string finishedText = "FINISHED";

    [Networked] public int CurrentLap { get; set; }
    [Networked] public bool Finished { get; set; }
    [Networked] public int TotalLaps { get; set; }

    private int lastRenderedLap = -1;
    private bool lastRenderedFinished = false;
    private TMP_Text lapText;

    [SerializeField] private bool enableDebugLogs = true;

    public override void Spawned()
    {
        // Check if the current scene is the Race scene
        if (SceneManager.GetActiveScene().name != "Race")
        {
            if (enableDebugLogs)
                Debug.Log($"[LAP] RacerLapData on {name}: Not in Race scene, disabling LapCounter.", this);

            lapText = FindLapCounterText(transform);
            if (lapText != null)
                lapText.gameObject.SetActive(false);

            return;
        }

        lapText = FindLapCounterText(transform);

        if (lapText == null && enableDebugLogs)
            Debug.LogWarning($"[LAP] RacerLapData on {name}: could not find a child named 'LapCounter' with a TMP_Text component.", this);

        // Hide the world-space UI entirely for non-local racers
        if (!Object.HasInputAuthority)
        {
            if (lapText != null)
                lapText.gameObject.SetActive(false);
            return;
        }

        RefreshText();
    }

    public override void Render()
    {
        if (!Object.HasInputAuthority) return;

        if (CurrentLap == lastRenderedLap && Finished == lastRenderedFinished) return;

        lastRenderedLap = CurrentLap;
        lastRenderedFinished = Finished;

        RefreshText();
    }

    public void SetLap(int lap, int total, bool finished)
    {
        CurrentLap = lap;
        TotalLaps = total;
        Finished = finished;
    }

    private void RefreshText()
    {
        if (lapText == null) return;

        if (Finished)
        {
            lapText.text = finishedText;
            return;
        }

        int total = Mathf.Max(1, TotalLaps);
        int displayLap = Mathf.Clamp(CurrentLap + 1, 1, total);
        lapText.text = string.Format("{0} {1}/{2}", lapPrefix, displayLap, total);
    }

    private static TMP_Text FindLapCounterText(Transform root)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "LapCounter")
            {
                TMP_Text txt = child.GetComponent<TMP_Text>();
                if (txt != null) return txt;
            }
        }
        return null;
    }
}