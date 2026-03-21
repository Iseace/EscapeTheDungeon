using Fusion;
using System;
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

  [Tooltip("Reference to SpectatorMobileInput — enabled on death so it owns the touch area and nav buttons. " +
           "Auto-found if not assigned.")]
  [SerializeField] private SpectatorMobileInput spectatorMobileInput;

  [Networked, OnChangedRender(nameof(HealthChanged))]
  public float CurrentHealth { get; set; }

  [Networked, OnChangedRender(nameof(OnIsDeadChanged))]
  public NetworkBool IsDead { get; set; }

  public bool HasCompletedSpawn { get; private set; }

  private Slider localHealthSlider;
  private string currentSceneName;
  private bool isHealthInitialized = false;
  private bool deathHandled = false;
  private bool spectatorModeActive = false;

  private bool _hasSpawned = false;

  // Local mirror of IsDead — always safe to read, never touches the networked backing
  // until Spawned() has been called and the object is valid.
  private bool _isDeadLocal = false;

  /// <summary>Safe to call at any time — returns false if not yet networked.</summary>
  public bool IsDeadSafe => _isDeadLocal;

  void OnIsDeadChanged()
  {
    if (!_hasSpawned) return;
    _isDeadLocal = (bool)IsDead;
    HandleDeath();
  }

  public override void Spawned()
  {
    _hasSpawned = true;
    _isDeadLocal = (bool)IsDead;
    HasCompletedSpawn = true;
    deathHandled = false;
    spectatorModeActive = false;

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

      // Auto-find SpectatorMobileInput if not assigned in the Inspector
      if (spectatorMobileInput == null)
        spectatorMobileInput = FindFirstObjectByType<SpectatorMobileInput>();

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

  public override void Despawned(NetworkRunner runner, bool hasState)
  {
    HasCompletedSpawn = false;
  }

  public bool TryGetIsDeadSafe(out bool isDead)
  {
    isDead = false;

    if (!HasCompletedSpawn || Object == null)
      return false;

    try
    {
      isDead = IsDead;
      return true;
    }
    catch (InvalidOperationException)
    {
      return false;
    }
  }

  private void OnDestroy()
  {
    HasCompletedSpawn = false;
    SceneManager.activeSceneChanged -= OnSceneChanged;
  }

  // ── UI State Helpers ────────────────────────────────────────────────────────

  /// <summary>
  /// Shows normal gameplay HUD.
  /// ControlsUI stays ACTIVE at all times so MobileControlsBridge (and its transparent
  /// drag-receiver Image) is never destroyed and camera look always works on mobile.
  /// SpectatorMobileInput is DISABLED so it no longer intercepts any touches.
  /// </summary>
  private void SetAliveUI()
  {
    if (controlsUI == null)
      controlsUI = GameObject.Find("ControlsUI");

    if (spectatorUI == null)
      spectatorUI = GameObject.Find("Spectator Controller");

    // Keep ControlsUI active — never SetActive(false) it
    if (controlsUI != null) controlsUI.SetActive(true);

    // Tell MobileControlsBridge to show all gameplay widgets and reclaim its touch area
    if (mobileControls != null) mobileControls.SetSpectatorMode(false);

    // Show the entire health bar canvas (includes health + keybinds) ONLY if not in lobby
    if (healthBarObject != null && currentSceneName != "LobbyRoom")
      healthBarObject.SetActive(true);

    // Disable spectator input first so its OnDisable hides nav buttons before
    // MobileControlsBridge re-enables its own Image raycast
    if (spectatorMobileInput != null) spectatorMobileInput.enabled = false;

    // Hide spectator overlay (also hides LastPlayer / NextPlayer as children)
    if (spectatorUI != null) spectatorUI.SetActive(false);

    PCKeybindUI pcKeybindUI = FindFirstObjectByType<PCKeybindUI>();
    if (pcKeybindUI != null) pcKeybindUI.SetSpectatorMode(true);

  }

  /// <summary>
  /// Switches to spectator state.
  /// ControlsUI stays ACTIVE so MobileControlsBridge keeps receiving drag events
  /// for camera look and pinch-to-zoom. Only the gameplay widgets inside it are
  /// hidden via SetSpectatorMode (joystick, attack, jump, pickup buttons).
  /// MobileControlsBridge also disables its Image raycastTarget so it no longer
  /// blocks the nav buttons.
  /// SpectatorMobileInput is ENABLED: it owns the full-screen touch area for
  /// camera drag and explicitly shows LastPlayer / NextPlayer.
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

    // Hide all gameplay widgets and release the touch area
    if (mobileControls != null) mobileControls.SetSpectatorMode(true);

    // Hide the entire health bar canvas (includes health + keybinds)
    if (healthBarObject != null) healthBarObject.SetActive(false);

    // Show the "Spectator Controller" canvas parent so the nav buttons exist in the hierarchy
    if (spectatorUI != null) spectatorUI.SetActive(true);

    // Enable SpectatorMobileInput: it owns camera drag and shows LastPlayer/NextPlayer
    if (spectatorMobileInput != null) spectatorMobileInput.enabled = true;
  }

  public void EnterSpectatorModeFromEscape()
  {
    if (!HasInputAuthority) return;
    if (spectatorModeActive) return;

    spectatorModeActive = true;

    if (TryGetComponent<PlayerInteraction>(out var pi)) pi.enabled = false;
    if (TryGetComponent<AnimatorBasic>(out var ab)) ab.enabled = false;
    if (TryGetComponent<PlayerMovement>(out var pm)) pm.enabled = false;

    if (TryGetComponent<CharacterController>(out var cc))
      cc.enabled = false;

    SetDeadUI();

    if (GetComponent<SpectatorSystem>() == null)
      gameObject.AddComponent<SpectatorSystem>();
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
    spectatorModeActive = true;

    Debug.Log($"[PlayerHealth] HandleDeath called for player {Object.InputAuthority.PlayerId}");

    // 1. Play death animation
    Animator anim = GetComponentInChildren<Animator>();
    if (anim != null)
      anim.SetBool("IsDead", true);

    // 2. Disable physics / movement so the corpse freezes on all clients
    if (TryGetComponent<CharacterController>(out var cc))
      cc.enabled = false;

    if (TryGetComponent<PlayerMovement>(out var pm))
      pm.enabled = false;

    // 3. Local-only: switch UI and enable spectator camera
    if (HasInputAuthority)
    {
      if (TryGetComponent<PlayerInteraction>(out var pi)) pi.enabled = false;
      if (TryGetComponent<AnimatorBasic>(out var ab)) ab.enabled = false;

      // Switch canvas panels: hide controls, show spectator overlay
      SetDeadUI();

      // Only add SpectatorSystem once
      if (GetComponent<SpectatorSystem>() == null)
        gameObject.AddComponent<SpectatorSystem>();
    }
  }
}