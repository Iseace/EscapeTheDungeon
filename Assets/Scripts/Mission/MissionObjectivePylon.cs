using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MissionObjectivePylon : MonoBehaviour, IInteractable
{
    [Header("Activation")]
    [SerializeField] private bool autoActivateByZone = true;
    [SerializeField] private bool allowOfflineDebugActivation = true;
    [SerializeField] private float activationRadius = 2.5f;
    [SerializeField] private float activationDuration = 5f;
    [SerializeField] private float progressDecayPerSecond = 1f;
    [SerializeField] private int requiredPlayersInZone = 1;
    [SerializeField] private bool allowBossToActivate = false;
    [SerializeField] private string interactText = "Activar pylon";
    [SerializeField] private string progressText = "Reparando pylon";
    [SerializeField] private string activatedText = "Pylon activado";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;
    [SerializeField] private bool debugDrawActivationRadius = true;
    [SerializeField] private int debugLogStepPercent = 10;

    [Header("Visual Feedback")]
    [SerializeField] private bool showWorldProgress = true;
    [SerializeField] private CanvasGroup progressCanvasGroup;
    [SerializeField] private Image progressFillImage;
    [SerializeField] private RectTransform progressFillRect;
    [SerializeField] private bool autoCreateFillSpriteIfMissing = true;
    [SerializeField] private TMP_Text progressPercentText;
    [SerializeField] private Animator activationAnimator;
    [SerializeField] private string activationAnimatorTrigger = "Activate";
    [SerializeField] private ParticleSystem activationParticles;
    [SerializeField] private AudioSource activationAudioSource;
    [SerializeField] private AudioClip activationSfx;
    [SerializeField] private GameObject[] objectsToEnableOnActivated;
    [SerializeField] private GameObject[] objectsToDisableOnActivated;

    public bool IsActivated { get; private set; }
    public float Progress01 => activationDuration <= 0f ? 1f : Mathf.Clamp01(currentProgress / activationDuration);

    private float currentProgress;
    private int lastLoggedStep = -1;
    private int lastPlayersInZone = -1;
    private float progressFillBaseWidth;
    private Vector3 progressFillBaseScale = Vector3.one;
    private static Sprite runtimeWhiteSprite;

    private void Start()
    {
        if (progressFillRect == null && progressFillImage != null)
        {
            progressFillRect = progressFillImage.rectTransform;
        }

        EnsureProgressFillRenderable();

        if (progressFillRect != null)
        {
            progressFillBaseWidth = progressFillRect.sizeDelta.x;
            progressFillBaseScale = progressFillRect.localScale;
        }

        RefreshProgressVisuals();
        ApplyActivatedVisuals(IsActivated);
    }

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

        bool canCommitActivation = ShouldSimulateProgressOnThisPeer();
        if (!canCommitActivation)
        {
            RefreshProgressVisuals();
            return;
        }

        int playersInZone = CountEligiblePlayersInZone(authoritativeOnly: canCommitActivation);
        LogPlayersInZoneIfChanged(playersInZone);

        float completionThreshold = Mathf.Max(0.01f, activationDuration);

        if (playersInZone >= Mathf.Max(1, requiredPlayersInZone))
        {
            currentProgress += Time.deltaTime;

            LogProgressStepIfNeeded();

            if (canCommitActivation && currentProgress >= completionThreshold)
            {
                Activate();
            }
        }
        else
        {
            currentProgress = Mathf.Max(0f, currentProgress - Mathf.Max(0f, progressDecayPerSecond) * Time.deltaTime);
        }

        RefreshProgressVisuals();
    }

    private int CountEligiblePlayersInZone(bool authoritativeOnly)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, activationRadius);
        int count = 0;

        for (int i = 0; i < hits.Length; i++)
        {
            var player = hits[i].GetComponentInParent<PlayerSetup>();
            if (player == null) continue;
            if (player.HasEscapedSafe) continue;

            if (!allowBossToActivate)
            {
                var role = player.GetComponent<PlayerRole>();
                if (role != null && role.IsBossSafe)
                {
                    continue;
                }
            }

            if (player.Object == null)
            {
                if (allowOfflineDebugActivation)
                {
                    count++;
                }
                continue;
            }

            if (authoritativeOnly)
            {
                if (!player.Object.HasStateAuthority) continue;
            }

            count++;
        }

        return count;
    }

    private bool ShouldSimulateProgressOnThisPeer()
    {
        var dungeonNetworkRunner = DungeonNetworkRunner.Instance;
        if (dungeonNetworkRunner != null)
        {
            return dungeonNetworkRunner.HasMissionStateAuthority;
        }

        return allowOfflineDebugActivation;
    }

    private void Activate()
    {
        if (IsActivated) return;

        IsActivated = true;
        currentProgress = Mathf.Max(currentProgress, activationDuration);
        MissionObjectiveManager.Instance?.NotifyPylonActivated(this);
        ApplyActivatedVisuals(true);
        RefreshProgressVisuals();

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
        ApplyActivatedVisuals(true);
        RefreshProgressVisuals();

        if (debugLogs)
        {
            Debug.Log($"[Pylon] ACTIVATED FROM NETWORK: {name} | Pos: {transform.position}");
        }
    }

    public void ForceProgressFromNetwork(float progress01)
    {
        if (IsActivated) return;

        float clampedProgress = Mathf.Clamp01(progress01);
        float targetDuration = Mathf.Max(0.01f, activationDuration);
        currentProgress = clampedProgress * targetDuration;
        RefreshProgressVisuals();
    }

    public void SetDebugLogs(bool enabled)
    {
        debugLogs = enabled;
    }

    private void RefreshProgressVisuals()
    {
        if (progressFillImage != null)
        {
            if (progressFillImage.type == Image.Type.Filled)
            {
                progressFillImage.fillAmount = Progress01;
            }
        }

        // Fallback: if Image is not Filled, drive bar width using RectTransform.
        if (progressFillRect != null && progressFillBaseWidth > 0.01f)
        {
            Vector2 size = progressFillRect.sizeDelta;
            size.x = progressFillBaseWidth * Progress01;
            progressFillRect.sizeDelta = size;
        }

        // Extra fallback: drive local scale X in case sizeDelta is controlled by layout.
        if (progressFillRect != null)
        {
            Vector3 scale = progressFillBaseScale;
            scale.x = Mathf.Max(0.001f, progressFillBaseScale.x * Progress01);
            progressFillRect.localScale = scale;
        }

        if (progressPercentText != null)
        {
            progressPercentText.text = $"{Mathf.RoundToInt(Progress01 * 100f)}%";
        }

        if (progressCanvasGroup != null)
        {
            bool show = showWorldProgress && !IsActivated;
            progressCanvasGroup.alpha = show ? 1f : 0f;
            progressCanvasGroup.interactable = false;
            progressCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ApplyActivatedVisuals(bool activated)
    {
        if (objectsToEnableOnActivated != null)
        {
            for (int i = 0; i < objectsToEnableOnActivated.Length; i++)
            {
                if (objectsToEnableOnActivated[i] != null)
                    objectsToEnableOnActivated[i].SetActive(activated);
            }
        }

        if (objectsToDisableOnActivated != null)
        {
            for (int i = 0; i < objectsToDisableOnActivated.Length; i++)
            {
                if (objectsToDisableOnActivated[i] != null)
                    objectsToDisableOnActivated[i].SetActive(!activated);
            }
        }

        if (!activated) return;

        if (activationAnimator != null && !string.IsNullOrWhiteSpace(activationAnimatorTrigger))
        {
            activationAnimator.SetTrigger(activationAnimatorTrigger);
        }

        if (activationParticles != null)
        {
            activationParticles.Play();
        }

        if (activationAudioSource != null && activationSfx != null)
        {
            activationAudioSource.PlayOneShot(activationSfx);
        }
    }

    private void EnsureProgressFillRenderable()
    {
        if (progressFillImage == null) return;
        if (!autoCreateFillSpriteIfMissing) return;
        if (progressFillImage.sprite != null) return;

        if (runtimeWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[]
            {
                Color.white, Color.white,
                Color.white, Color.white
            });
            tex.Apply();

            runtimeWhiteSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        progressFillImage.sprite = runtimeWhiteSprite;
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
