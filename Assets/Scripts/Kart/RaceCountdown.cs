using Fusion;
using TMPro;
using UnityEngine;

public class RaceCountdown : NetworkBehaviour
{
    [Header("Settings")]
    [SerializeField] private float readySeconds = 2f;
    [SerializeField] private float countdownSeconds = 3f;

    [Header("UI")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private GameObject readyTextCanvas;

    [Header("Text Style")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color goColor     = Color.green;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private float outlineWidth = 0.2f;

    // Networked timers
    [Networked] private TickTimer ReadyTimer { get; set; }
    [Networked] private TickTimer CountdownTimer { get; set; }
    [Networked, OnChangedRender(nameof(OnRaceStartedChanged))]
    public NetworkBool RaceStarted { get; private set; }

    public static bool IsRaceStarted { get; private set; }

    public override void Spawned()
    {
        IsRaceStarted = false;
        RaceStarted   = false;

        if (readyTextCanvas != null)
            readyTextCanvas.SetActive(true);

        if (countdownText != null)
        {
            countdownText.text = "Ready?";
            ApplyOutline();
            SetTextColor(normalColor);
        }

        if (HasStateAuthority)
        {
            ReadyTimer    = TickTimer.CreateFromSeconds(Runner, readySeconds);
            CountdownTimer = TickTimer.None;
            Debug.Log("[RACE COUNTDOWN] Ready phase started");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || RaceStarted)
            return;

        if (ReadyTimer.IsRunning && ReadyTimer.Expired(Runner))
        {
            CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownSeconds);
            ReadyTimer     = TickTimer.None;
            Debug.Log("[RACE COUNTDOWN] Countdown started");
        }

        if (CountdownTimer.IsRunning && CountdownTimer.Expired(Runner))
        {
            RaceStarted    = true;
            IsRaceStarted  = true;
            CountdownTimer = TickTimer.None; // cleanup
            Debug.Log("[RACE COUNTDOWN] GO! Race started.");
        }
    }

    public override void Render()
    {
        if (countdownText == null)
            return;

        if (ReadyTimer.IsRunning)
        {
            countdownText.text = "Ready?";
            SetTextColor(normalColor);
            return;
        }

        if (CountdownTimer.IsRunning)
        {
            float remaining = CountdownTimer.RemainingTime(Runner) ?? 0f;
            int displayed   = Mathf.CeilToInt(remaining);

            if (displayed > 0)
            {
                countdownText.text = displayed.ToString();
                SetTextColor(normalColor);
            }
            else
            {
                countdownText.text = "GO!";
                SetTextColor(goColor);
            }
            return;
        }

        if (RaceStarted)
        {
            countdownText.text = "GO!";
            SetTextColor(goColor);
        }
    }

    private void OnRaceStartedChanged()
    {
        IsRaceStarted = RaceStarted;

        if (!RaceStarted)
            return;

        if (countdownText != null)
        {
            countdownText.text = "GO!";
            SetTextColor(goColor);
        }

        Invoke(nameof(HideCountdownCanvas), 1f);
        Debug.Log("[RACE COUNTDOWN] Clients received RaceStarted = true");
    }

    private void HideCountdownCanvas()
    {
        if (readyTextCanvas != null)
            readyTextCanvas.SetActive(false);
    }

    private void OnDestroy()
    {
        IsRaceStarted = false;
    }

    private void ApplyOutline()
    {
        if (countdownText == null) return;

        countdownText.fontMaterial.EnableKeyword("OUTLINE_ON");
        countdownText.outlineColor = outlineColor;
        countdownText.outlineWidth = outlineWidth;
    }

    private void SetTextColor(Color color)
    {
        if (countdownText == null) return;
        countdownText.color = color;
    }
}