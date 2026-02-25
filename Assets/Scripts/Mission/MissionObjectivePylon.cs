using UnityEngine;

public class MissionObjectivePylon : MonoBehaviour, IInteractable
{
    [Header("Activation")]
    [SerializeField] private bool autoActivateByZone = true;
    [SerializeField] private float activationRadius = 2.5f;
    [SerializeField] private float activationDuration = 5f;
    [SerializeField] private float progressDecayPerSecond = 1f;
    [SerializeField] private int requiredPlayersInZone = 1;
    [SerializeField] private string interactText = "Activar pylon";
    [SerializeField] private string progressText = "Reparando pylon";
    [SerializeField] private string activatedText = "Pylon activado";

    public bool IsActivated { get; private set; }
    public float Progress01 => activationDuration <= 0f ? 1f : Mathf.Clamp01(currentProgress / activationDuration);

    private float currentProgress;

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
        if (playersInZone >= Mathf.Max(1, requiredPlayersInZone))
        {
            currentProgress += Time.deltaTime;
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
            if (player == null || player.Object == null) continue;
            if (player.HasEscaped) continue;
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
            if (player == null || player.Object == null) continue;
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
    }

    private void OnDestroy()
    {
        MissionObjectiveManager.Instance?.UnregisterPylon(this);
    }
}
