using UnityEngine;

public class MissionObjectivePylon : MonoBehaviour, IInteractable
{
    [Header("Activation")]
    [SerializeField] private bool autoActivateByZone = true;
    [SerializeField] private bool allowOfflineDebugActivation = true;
    [SerializeField] private float activationRadius = 2.5f;
    [SerializeField] private float activationDuration = 5f;
    [SerializeField] private float progressDecayPerSecond = 1f;
    [SerializeField] private int requiredPlayersInZone = 1;
    [SerializeField] private string interactText = "Activar pylon";
    [SerializeField] private string progressText = "Reparando pylon";
    [SerializeField] private string activatedText = "Pylon activado";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugDrawActivationRadius = true;
    [SerializeField] private int debugLogStepPercent = 10;

    public bool IsActivated { get; private set; }
    public float Progress01 => activationDuration <= 0f ? 1f : Mathf.Clamp01(currentProgress / activationDuration);

    private float currentProgress;
    private int lastLoggedStep = -1;
    private int lastPlayersInZone = -1;

    public string GetInteractText()
    {
        if (IsActivated) return activatedText;
        if (!autoActivateByZone) return interactText;

        int pct = Mathf.RoundToInt(Progress01 * 100f);
        return $"{progressText} {pct}%";
    }

    public void Interact(PlayerSetup player)
    {
        if (autoActivateByZone)
        {
            return;
        }

        if (IsActivated || player == null) return;

        float distance = Vector3.Distance(player.transform.position, transform.position);
        if (distance > activationRadius) return;

        Activate();
    }

    private void Update()
    {
        if (IsActivated || !autoActivateByZone) return;
        if (!ShouldSimulateProgressOnThisPeer()) return;

        int playersInZone = CountEligiblePlayersInZone();
        LogPlayersInZoneIfChanged(playersInZone);

        if (playersInZone >= Mathf.Max(1, requiredPlayersInZone))
        {
            currentProgress += Time.deltaTime;
            LogProgressStepIfNeeded();
            if (currentProgress >= Mathf.Max(0.01f, activationDuration))
            {
                Activate();
            }
        }
        else
        {
            currentProgress = Mathf.Max(0f, currentProgress - Mathf.Max(0f, progressDecayPerSecond) * Time.deltaTime);
        }
    }

    private int CountEligiblePlayersInZone()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, activationRadius);
        int count = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            var player = hits[i].GetComponentInParent<PlayerSetup>();
            if (player == null) continue;
            if (player.HasEscaped) continue;

            if (player.Object == null)
            {
                if (allowOfflineDebugActivation)
                {
                    count++;
                }
                continue;
            }

            if (!player.Object.HasStateAuthority) continue;
            count++;
        }

        return count;
    }

    private bool ShouldSimulateProgressOnThisPeer()
    {
        PlayerSetup[] players = FindObjectsOfType<PlayerSetup>();
        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null) continue;

            if (player.Object == null)
            {
                if (allowOfflineDebugActivation) return true;
                continue;
            }

            if (player.Object.HasStateAuthority) return true;
        }
        return false;
    }

    private void Activate()
    {
        if (IsActivated) return;

        IsActivated = true;
        currentProgress = Mathf.Max(currentProgress, activationDuration);
        MissionObjectiveManager.Instance?.NotifyPylonActivated(this);

        if (debugLogs)
        {
            Debug.Log($"[Pylon] ACTIVATED: {name} | Pos: {transform.position}");
        }
    }

    public void ForceActivateFromNetwork()
    {
        if (IsActivated) return;

        IsActivated = true;
        currentProgress = Mathf.Max(currentProgress, activationDuration);

        if (debugLogs)
        {
            Debug.Log($"[Pylon] ACTIVATED FROM NETWORK: {name} | Pos: {transform.position}");
        }
    }

    public void SetDebugLogs(bool enabled)
    {
        debugLogs = enabled;
    }

    private void LogPlayersInZoneIfChanged(int playersInZone)
    {
        if (!debugLogs) return;
        if (playersInZone == lastPlayersInZone) return;

        lastPlayersInZone = playersInZone;
        Debug.Log($"[Pylon] Zone players: {playersInZone}/{Mathf.Max(1, requiredPlayersInZone)} | {name}");
    }

    private void LogProgressStepIfNeeded()
    {
        if (!debugLogs) return;

        int stepPercent = Mathf.Clamp(debugLogStepPercent, 1, 100);
        int progressPercent = Mathf.RoundToInt(Progress01 * 100f);
        int currentStep = progressPercent / stepPercent;

        if (currentStep <= lastLoggedStep) return;

        lastLoggedStep = currentStep;
        Debug.Log($"[Pylon] Progress: {progressPercent}% | {name}");
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugDrawActivationRadius) return;

        Color color = IsActivated ? Color.green : new Color(1f, 0.6f, 0f, 1f);
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, activationRadius));
    }

    private void OnDestroy()
    {
        MissionObjectiveManager.Instance?.UnregisterPylon(this);
    }
}
