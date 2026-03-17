using TMPro;
using UnityEngine;

/// <summary>
/// Minimal HUD text that shows remaining match time from DungeonNetworkRunner.
/// Intended to live under a Screen Space canvas panel such as ControlsUI.
/// </summary>
public class MatchTimerHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private string labelPrefix = "Tiempo";
    [SerializeField] private string bossFreezeLabelPrefix = "Boss libre en";
    [SerializeField] private bool hideWhenNoTimeLimit = true;
    [SerializeField] private bool verboseBuildLogs = true;

    private DungeonNetworkRunner dungeonRunner;
    private float nextRunnerResolveTime;

    private void Awake()
    {
        ResolveTextReference();
        SetVisible(false);
    }

    private void Update()
    {
        if (timerText == null)
        {
            ResolveTextReference();
            if (timerText == null) return;
        }

        if (dungeonRunner == null)
        {
            if (Time.unscaledTime >= nextRunnerResolveTime)
            {
                dungeonRunner = DungeonNetworkRunner.Instance;
                if (dungeonRunner == null)
                {
                    dungeonRunner = FindAnyObjectByType<DungeonNetworkRunner>();
                }

                nextRunnerResolveTime = Time.unscaledTime + 0.5f;
            }

            if (dungeonRunner == null)
            {
                if (verboseBuildLogs)
                {
                    timerText.text = labelPrefix + " --:--";
                    SetVisible(true);
                    return;
                }
                SetVisible(false);
                return;
            }

            if (verboseBuildLogs)
            {
                Debug.Log("[MatchTimerHUD] DungeonNetworkRunner resuelto correctamente.", this);
            }
        }

        if (!dungeonRunner.MatchInProgress || dungeonRunner.MatchEnded)
        {
            if (verboseBuildLogs)
            {
                timerText.text = labelPrefix + " --:--";
                SetVisible(true);
                return;
            }
            SetVisible(false);
            return;
        }

        if (dungeonRunner.IsBossFrozen)
        {
            int freezeSeconds = Mathf.CeilToInt(Mathf.Max(0f, dungeonRunner.RemainingBossFreezeTimeSeconds));
            int freezeMinutesPart = freezeSeconds / 60;
            int freezeSecondsPart = freezeSeconds % 60;

            timerText.text = string.Format("{0} {1:00}:{2:00}", bossFreezeLabelPrefix, freezeMinutesPart, freezeSecondsPart);
            SetVisible(true);
            return;
        }

        if (!dungeonRunner.HasMatchTimeLimit)
        {
            if (hideWhenNoTimeLimit)
            {
                SetVisible(false);
                return;
            }

            timerText.text = labelPrefix + " --:--";
            SetVisible(true);
            return;
        }

        int seconds = Mathf.CeilToInt(Mathf.Max(0f, dungeonRunner.RemainingMatchTimeSeconds));
        int minutesPart = seconds / 60;
        int secondsPart = seconds % 60;

        timerText.text = string.Format("{0} {1:00}:{2:00}", labelPrefix, minutesPart, secondsPart);
        SetVisible(true);
    }

    private void ResolveTextReference()
    {
        if (timerText != null) return;

        timerText = GetComponent<TMP_Text>();
        if (timerText != null) return;

        timerText = GetComponentInChildren<TMP_Text>(true);
        if (timerText == null)
        {
            Debug.LogWarning("[MatchTimerHUD] No se encontro TMP_Text. Asigna el texto del Canvas en el Inspector.", this);
        }
    }

    private void SetVisible(bool visible)
    {
        if (timerText != null)
        {
            if (timerText.enabled != visible)
            {
                timerText.enabled = visible;
            }
        }
    }
}
