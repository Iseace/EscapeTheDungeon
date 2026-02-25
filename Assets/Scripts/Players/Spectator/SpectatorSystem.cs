using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using System.Collections.Generic;

/// <summary>
/// Added to the local player's GameObject when they die.
/// Switches the main camera to follow living players.
/// Navigation:  Right Arrow / D / Mouse Left  → next player
///              Left  Arrow / A / Mouse Right → previous player
/// Zoom:        Mouse scroll wheel (PC) · Pinch gesture (mobile)
///              Distance 0 = first-person on pivot, max = pulled back (third-person)
/// </summary>
public class SpectatorSystem : MonoBehaviour
{
    [Header("UI (optional)")]
    [Tooltip("Assign a canvas/panel to show 'SPECTATING' overlay")]
    [SerializeField] private GameObject spectatorHUDPrefab;

    [Header("Zoom Settings")]
    [SerializeField] private float minZoomDistance  = 0f;   // fully first-person
    [SerializeField] private float maxZoomDistance  = 6f;  // fully third-person
    [SerializeField] private float scrollSpeed      = 10f;   // PC scroll sensitivity
    [SerializeField] private float pinchSpeed       = 1f;// Mobile pinch sensitivity
    [SerializeField] private float zoomSmoothSpeed  = 8f;   // smoothing towards target

    // ── Internal state ─────────────────────────────────────────────────────────
    private List<PlayerSetup> livingPlayers = new List<PlayerSetup>();
    private int currentIndex = 0;

    private FirstPersonCamera fpCamera;
    private GameObject spectatorHUDInstance;

    // Input debounce
    private float previousHorizontalInput = 0f;

    // Zoom state
    private float targetZoomDistance  = 0f; // what we're zooming towards
    private float currentZoomDistance = 0f; // smoothed value sent to fpCamera

    // Pinch state
    private float previousPinchDistance = 0f;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        fpCamera = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;

        if (fpCamera == null)
        {
            Debug.LogError("[SpectatorSystem] FirstPersonCamera not found on Main Camera!");
            return;
        }

        // Start fully first-person
        targetZoomDistance  = 0f;
        currentZoomDistance = 0f;
        fpCamera.ZoomDistance = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (spectatorHUDPrefab != null)
            spectatorHUDInstance = Instantiate(spectatorHUDPrefab);

        RefreshPlayerList();

        if (livingPlayers.Count > 0)
            FocusCurrentTarget();

        Debug.Log("[SpectatorSystem] Spectator mode active.");
    }

    void Update()
    {
        if (fpCamera == null) return;

        HandleNavigationInput();
        HandleZoomInput();
        ApplySmoothedZoom();

        // ── Keep retrying until we find a living player ──────────────────────
        if (livingPlayers.Count == 0)
        {
            RefreshPlayerList();
            if (livingPlayers.Count > 0)
                FocusCurrentTarget();
            return;
        }

        // ── Detect if any spectated player has since died ────────────────────
        bool needsRefresh = false;
        foreach (var p in livingPlayers)
        {
            if (p == null) { needsRefresh = true; break; }
            PlayerHealth health = p.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead) { needsRefresh = true; break; }
        }

        if (needsRefresh)
        {
            RefreshPlayerList();
            FocusCurrentTarget();
        }
    }

    void OnDestroy()
    {
        // Reset zoom so normal gameplay is unaffected after spectator ends
        if (fpCamera != null)
            fpCamera.ZoomDistance = 0f;

        if (spectatorHUDInstance != null)
            Destroy(spectatorHUDInstance);
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    private void HandleNavigationInput()
    {
        var kb    = Keyboard.current;
        var mouse = Mouse.current;

        float horizontalInput = 0f;
        if (kb != null)
        {
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed)
                horizontalInput = 1f;
            else if (kb.leftArrowKey.isPressed || kb.aKey.isPressed)
                horizontalInput = -1f;
        }

        if (previousHorizontalInput == 0f)
        {
            if (horizontalInput > 0.5f)       Navigate(1);
            else if (horizontalInput < -0.5f) Navigate(-1);
        }
        previousHorizontalInput = horizontalInput;

        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)  Navigate(1);
            if (mouse.rightButton.wasPressedThisFrame) Navigate(-1);
        }
    }

    private void HandleZoomInput()
    {
        if (Application.isMobilePlatform)
            HandlePinchZoom();
        else
            HandleScrollZoom();
    }

    /// <summary>
    /// PC: scroll wheel moves the camera closer/further from the pivot.
    /// Scroll up → zoom in (towards first-person).
    /// Scroll down → zoom out (towards third-person).
    /// </summary>
    private void HandleScrollZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        // scroll > 0  → wheel up  → pull camera IN  → decrease distance
        targetZoomDistance = Mathf.Clamp(
            targetZoomDistance - scroll * scrollSpeed * Time.unscaledDeltaTime,
            minZoomDistance, maxZoomDistance);
    }

    /// <summary>
    /// Mobile: two-finger pinch moves the camera closer/further from the pivot.
    /// Pinch apart → zoom in (closer). Pinch together → zoom out (further).
    /// </summary>
    private void HandlePinchZoom()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null) return;

        var t0 = touchscreen.touches[0];
        var t1 = touchscreen.touches[1];

        if (!t0.press.isPressed || !t1.press.isPressed)
        {
            previousPinchDistance = 0f;
            return;
        }

        float currentDistance = Vector2.Distance(
            t0.position.ReadValue(),
            t1.position.ReadValue());

        if (previousPinchDistance <= 0f)
        {
            previousPinchDistance = currentDistance;
            return;
        }

        float delta = currentDistance - previousPinchDistance;
        previousPinchDistance = currentDistance;

        // Pinch apart (positive delta) → fingers spreading → zoom IN → decrease distance
        targetZoomDistance = Mathf.Clamp(
            targetZoomDistance - delta * pinchSpeed,
            minZoomDistance, maxZoomDistance);
    }

    /// <summary>Smoothly lerps the actual camera distance towards the target.</summary>
    private void ApplySmoothedZoom()
    {
        currentZoomDistance = Mathf.Lerp(
            currentZoomDistance,
            targetZoomDistance,
            Time.unscaledDeltaTime * zoomSmoothSpeed);

        fpCamera.ZoomDistance = currentZoomDistance;
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void Navigate(int direction)
    {
        RefreshPlayerList();
        if (livingPlayers.Count == 0) return;

        currentIndex += direction;
        FocusCurrentTarget();
    }

    private void FocusCurrentTarget()
    {
        if (livingPlayers.Count == 0)
        {
            Debug.Log("[SpectatorSystem] No living players to spectate.");
            return;
        }

        int normalised = GetNormalisedIndex();
        PlayerSetup target = livingPlayers[normalised];

        Transform pivot = target.GetCameraPivot();

        if (pivot == null)
        {
            Debug.LogWarning($"[SpectatorSystem] GetCameraPivot() null for '{target.gameObject.name}', falling back to root.");
            pivot = target.transform;
        }

        // Pass null for graphics so the target player's mesh is never hidden
        fpCamera.SetTarget(pivot, null);

        Debug.Log($"[SpectatorSystem] Now spectating '{target.gameObject.name}' pivot Y={pivot.position.y:F2}");
    }

    private void RefreshPlayerList()
    {
        livingPlayers.Clear();

        PlayerSetup[] allPlayers = FindObjectsByType<PlayerSetup>(FindObjectsSortMode.None);

        foreach (var p in allPlayers)
        {
            if (p.gameObject == this.gameObject) continue;

            PlayerHealth health = p.GetComponent<PlayerHealth>();
            if (health == null || !health.IsDead)
                livingPlayers.Add(p);
        }
    }

    private int GetNormalisedIndex()
    {
        if (livingPlayers.Count == 0) return 0;

        int r = currentIndex % livingPlayers.Count;
        return (r < 0) ? r + livingPlayers.Count : r;
    }
}