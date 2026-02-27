using Fusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerHealth : NetworkBehaviour
{
  [Header("Stats")]
  [SerializeField] private float maxHealth = 100f;

  [Header("Health Bar Reference")]
  [SerializeField] private GameObject healthBarObject; // Assign HUD_Canvas or HealthBar_HUD from the prefab

  [Header("UI Panels")]
  [Tooltip("The normal player HUD panel (named 'ControlsUI' on your Canvas)")]
  [SerializeField] private GameObject controlsUI;

  [Tooltip("The spectator overlay panel (named 'Spectator Controller' on your Canvas)")]
  [SerializeField] private GameObject spectatorUI;

  [Tooltip("Reference to MobileControlsBridge — hides gameplay widgets but keeps camera drag alive on mobile")]
  [SerializeField] private MobileControlsBridge mobileControls;

  [Networked, OnChangedRender(nameof(HealthChanged))]
  public float CurrentHealth { get; set; }

  [Networked, OnChangedRender(nameof(OnIsDeadChanged))]
  public NetworkBool IsDead { get; set; }

  private Slider localHealthSlider;
  private string currentSceneName;
  private bool isHealthInitialized = false;
  private bool deathHandled = false;

  void OnIsDeadChanged()
  {
    HandleDeath();
  }

  public override void Spawned()
  {
    deathHandled = false;

    // Only the Server/Host sets the initial value once
    if (Object.HasStateAuthority && !isHealthInitialized)
    {
      CurrentHealth = maxHealth;
      IsDead = false;
      isHealthInitialized = true;
    }

    currentSceneName = SceneManager.GetActiveScene().name;

    // Subscribe to scene changes
    SceneManager.activeSceneChanged += OnSceneChanged;

    // Only configure for the local player (the one we control)
    if (HasInputAuthority)
    {
      // Auto-find MobileControlsBridge if not assigned in the Inspector
      if (mobileControls == null)
        mobileControls = FindFirstObjectByType<MobileControlsBridge>();

      SetupHealthBar();
      ConfigureHUDForScene();

      // Start alive: show player controls, hide spectator overlay
      SetAliveUI();
    }
    else
    {
      // For other players, make sure their health bar is always hidden
      if (healthBarObject != null)
        healthBarObject.SetActive(false);
    }
  }

  private void OnDestroy()
  {
    SceneManager.activeSceneChanged -= OnSceneChanged;
  }

  // ── UI State Helpers ────────────────────────────────────────────────────────

  /// <summary>
  /// Shows normal gameplay HUD.
  /// ControlsUI stays ACTIVE at all times so MobileControlsBridge (and its transparent
  /// drag-receiver Image) is never destroyed and camera look always works on mobile.
  /// </summary>
  private void SetAliveUI()
  {
    if (controlsUI == null)
      controlsUI = GameObject.Find("ControlsUI");

    if (spectatorUI == null)
      spectatorUI = GameObject.Find("Spectator Controller");

    // Keep ControlsUI active — never SetActive(false) it
    if (controlsUI != null) controlsUI.SetActive(true);

    // Tell MobileControlsBridge to show all gameplay widgets
    if (mobileControls != null) mobileControls.SetSpectatorMode(false);

    // Hide spectator overlay
    if (spectatorUI != null) spectatorUI.SetActive(false);
  }

  /// <summary>
  /// Switches to spectator state.
  /// ControlsUI stays ACTIVE so MobileControlsBridge keeps receiving drag events
  /// for camera look and pinch-to-zoom. Only the gameplay widgets inside it are
  /// hidden via SetSpectatorMode (joystick, attack, jump, pickup buttons).
  /// "Spectator Controller" canvas is shown so the player can navigate targets.
  /// </summary>
  private void SetDeadUI()
  {
    if (controlsUI == null)
      controlsUI = GameObject.Find("ControlsUI");

    if (spectatorUI == null)
      spectatorUI = GameObject.Find("Spectator Controller");

    // DO NOT call controlsUI.SetActive(false) — that kills MobileControlsBridge
    // and breaks camera drag / pinch zoom in spectator mode on mobile.
    if (controlsUI != null) controlsUI.SetActive(true);

    // Hide all gameplay widgets (joystick, attack, jump, pickup) via the bridge
    if (mobileControls != null) mobileControls.SetSpectatorMode(true);

    // Show the "Spectator Controller" canvas
    if (spectatorUI != null) spectatorUI.SetActive(true);
  }

  // ── Health Bar ──────────────────────────────────────────────────────────────

  private void SetupHealthBar()
  {
    if (healthBarObject == null)
    {
      Debug.LogError("Health Bar Object not assigned in PlayerHealth!");
      return;
    }

    localHealthSlider = healthBarObject.GetComponentInChildren<Slider>();

    if (localHealthSlider != null)
    {
      localHealthSlider.maxValue = maxHealth;
      localHealthSlider.minValue = 0;
      localHealthSlider.value = CurrentHealth;
    }
    else
    {
      Debug.LogError("Slider component not found in health bar!");
    }
  }

  private void OnSceneChanged(Scene oldScene, Scene newScene)
  {
    currentSceneName = newScene.name;

    if (HasInputAuthority)
      ConfigureHUDForScene();
  }

  private void ConfigureHUDForScene()
  {
    if (healthBarObject == null) return;

    // Show health bar only in game scenes, hide in lobby
    if (currentSceneName == "LobbyRoom")
    {
      healthBarObject.SetActive(false);
    }
    else
    {
      healthBarObject.SetActive(true);
      UpdateHUD();
    }
  }

  void HealthChanged()
  {
    UpdateHUD();
  }

  private void UpdateHUD()
  {
    if (HasInputAuthority && localHealthSlider != null && healthBarObject != null && healthBarObject.activeSelf)
      localHealthSlider.value = CurrentHealth;
  }

  // ── Damage / Death ──────────────────────────────────────────────────────────

  public void DealDamage(float damage)
  {
    if (!Object.HasStateAuthority || IsDead) return;
    CurrentHealth = Mathf.Max(0, CurrentHealth - damage);

    if (CurrentHealth <= 0)
      IsDead = true; // Triggers OnIsDeadChanged on all clients
  }

  // ── Animation helpers ───────────────────────────────────────────────────────

  /// <summary>
  /// Safely fires an Animator trigger only if the parameter actually exists.
  /// If the trigger is missing, logs a warning with the list of all available
  /// trigger parameters so you can find the correct name in your Animator.
  /// </summary>
  private void TrySetTrigger(Animator anim, string triggerName)
  {
    foreach (AnimatorControllerParameter p in anim.parameters)
    {
      if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
      {
        anim.SetTrigger(triggerName);
        return;
      }
    }

    // Parameter not found — collect all trigger names to help identify the right one
    var available = new System.Text.StringBuilder();
    foreach (AnimatorControllerParameter p in anim.parameters)
    {
      if (p.type == AnimatorControllerParameterType.Trigger)
        available.Append($"'{p.name}' ");
    }

    Debug.LogWarning($"[PlayerHealth] Animator trigger '{triggerName}' not found on '{anim.gameObject.name}'. " +
                     $"Available triggers: {(available.Length > 0 ? available.ToString() : "(none)")} " +
                     $"— Update the trigger name in HandleDeath() to match your Animator Controller.");
  }

  public void HandleDeath()
  {
    if (!IsDead || deathHandled) return;
    deathHandled = true;

    Debug.Log($"[PlayerHealth] HandleDeath called for player {Object.InputAuthority.PlayerId}");

    // 1. Play death animation — use safe setter so a missing trigger name
    //    doesn't throw an exception and halt the rest of HandleDeath.
    Animator anim = GetComponentInChildren<Animator>();
    if (anim != null)
      TrySetTrigger(anim, "Die");

    // 2. Disable physics / movement so the corpse freezes on all clients
    if (TryGetComponent<CharacterController>(out var cc))
      cc.enabled = false;

    if (TryGetComponent<PlayerMovement>(out var pm))
      pm.enabled = false;

    // 3. Local-only: switch UI and enable spectator camera
    if (HasInputAuthority)
    {
      if (TryGetComponent<PlayerInteraction>(out var pi)) pi.enabled = false;
      if (TryGetComponent<AnimatorBasic>(out var ab))     ab.enabled = false;

      // Switch canvas panels: hide controls, show spectator overlay
      SetDeadUI();

      // Only add SpectatorSystem once
      if (GetComponent<SpectatorSystem>() == null)
        gameObject.AddComponent<SpectatorSystem>();
    }
  }
}