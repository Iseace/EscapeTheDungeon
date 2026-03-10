using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Fusion;
using System.Collections.Generic;

public class SpectatorSystem : MonoBehaviour
{
    [Header("UI (optional)")]
    [Tooltip("Assign a canvas/panel to show 'SPECTATING' overlay")]
    [SerializeField] private GameObject spectatorHUDPrefab;

    [Header("Navigation Buttons (optional)")]
    [Tooltip("Assign directly, OR the buttons will be found automatically by name inside the HUD prefab " +
             "or the scene's 'Spectator Controller' canvas.\n" +
             "Supported names (case-insensitive): prev, last, left  →  Navigate(-1)\n" +
             "                                    next, right       →  Navigate( 1)")]
    [SerializeField] private Button prevPlayerButton;   // ◄  Navigate(-1)  — matches: LastPlayer, PrevButton, LeftArrow …
    [SerializeField] private Button nextPlayerButton;   // ►  Navigate( 1)  — matches: NextPlayer, NextButton, RightArrow …

    [Header("Zoom Settings")]
    [SerializeField] private float minZoomDistance = 0f;   // fully first-person
    [SerializeField] private float maxZoomDistance = 3.5f; // fully third-person
    [SerializeField] private float scrollSpeed = 30f;  // PC scroll sensitivity
    [SerializeField] private float pinchSpeed = 1f;   // Mobile pinch sensitivity
    [SerializeField] private float zoomSmoothSpeed = 10f;  // smoothing towards target

    // ── Internal state ─────────────────────────────────────────────────────────
    private List<PlayerSetup> livingPlayers = new List<PlayerSetup>();
    private int currentIndex = 0;

    private FirstPersonCamera fpCamera;
    private MobileControlsBridge mobileBridge;   // suppresses single-finger drag during pinch
    private GameObject spectatorHUDInstance;

    // Reusable list — avoids per-frame allocation when collecting active touches
    private readonly List<Vector2> _activeTouchPositions = new List<Vector2>(10);

    // Input debounce
    private float previousHorizontalInput = 0f;

    // Zoom state
    private float targetZoomDistance = 0f; // what we're zooming towards
    private float currentZoomDistance = 0f; // smoothed value sent to fpCamera

    // Pinch state
    private float previousPinchDistance = 0f;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        fpCamera = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;

        // Auto-find MobileControlsBridge so we can lock it during pinch zoom
        mobileBridge = FindFirstObjectByType<MobileControlsBridge>();

        // Start fully first-person
        targetZoomDistance = 0f;
        currentZoomDistance = 0f;
        fpCamera.ZoomDistance = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ── Step 1: search inside the optional HUD prefab ─────────────────────
        if (spectatorHUDPrefab != null)
        {
            spectatorHUDInstance = Instantiate(spectatorHUDPrefab);
            TryAutoFindNavButtons(spectatorHUDInstance);
        }

        // ── Step 2: search the scene's "Spectator Controller" canvas.
        // Always search regardless of platform — the previous isMobilePlatform guard
        // was false in the Unity Simulator and caused buttons to never be found.
        if (prevPlayerButton == null || nextPlayerButton == null)
        {
            // GameObject.Find only finds ACTIVE objects.
            // PlayerHealth.SetDeadUI() activates "Spectator Controller" before
            // SpectatorSystem.Start() runs, so it should be findable here.
            GameObject sceneCanvas = GameObject.Find("Spectator Controller");

            if (sceneCanvas != null)
            {
                TryAutoFindNavButtons(sceneCanvas);
            }
        }

        // Wire up whichever buttons were found (Inspector, HUD prefab, or scene canvas)
        if (prevPlayerButton != null)
            prevPlayerButton.onClick.AddListener(() => Navigate(-1));

        if (nextPlayerButton != null)
            nextPlayerButton.onClick.AddListener(() => Navigate(1));

        RefreshPlayerList();

        if (livingPlayers.Count > 0)
            FocusCurrentTarget();
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
            if (health != null && health.IsDeadSafe) { needsRefresh = true; break; }
        }

        if (needsRefresh)
        {
            RefreshPlayerList();
            FocusCurrentTarget();
        }
    }

    void OnDestroy()
    {
        if (prevPlayerButton != null)
            prevPlayerButton.onClick.RemoveAllListeners();

        if (nextPlayerButton != null)
            nextPlayerButton.onClick.RemoveAllListeners();

        // Release pinch lock so MobileControlsBridge works normally if spectator ends
        if (mobileBridge != null)
            mobileBridge.SetPinching(false);

        // Reset zoom so normal gameplay is unaffected after spectator ends
        if (fpCamera != null)
            fpCamera.ZoomDistance = 0f;

        if (spectatorHUDInstance != null)
            Destroy(spectatorHUDInstance);
    }

    // ── Auto-discovery ─────────────────────────────────────────────────────────

    private void TryAutoFindNavButtons(GameObject root)
    {
        if (root == null) return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);

        foreach (Button btn in buttons)
        {
            string lower = btn.gameObject.name.ToLower();

            if (prevPlayerButton == null &&
                (lower.Contains("prev") || lower.Contains("last") || lower.Contains("left")))
            {
                prevPlayerButton = btn;
            }
            else if (nextPlayerButton == null &&
                     (lower.Contains("next") || lower.Contains("right")))
            {
                nextPlayerButton = btn;
            }
        }
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    private void HandleNavigationInput()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        float horizontalInput = 0f;
        if (kb != null)
        {
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed)
                horizontalInput = 1f;
            else if (kb.leftArrowKey.isPressed || kb.aKey.isPressed)
                horizontalInput = -1f;
        }

        // Fire once on the leading edge of the key press (debounce)
        if (previousHorizontalInput == 0f)
        {
            if (horizontalInput > 0.5f) Navigate(1);
            else if (horizontalInput < -0.5f) Navigate(-1);
        }
        previousHorizontalInput = horizontalInput;

        // Mouse left/right click also navigates (existing behaviour)
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame) Navigate(1);
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

    private void HandleScrollZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        targetZoomDistance = Mathf.Clamp(
            targetZoomDistance - scroll * scrollSpeed * Time.unscaledDeltaTime,
            minZoomDistance, maxZoomDistance);
    }

    private void HandlePinchZoom()
    {
        var touchscreen = Touchscreen.current;
        if (touchscreen == null) return;

        _activeTouchPositions.Clear();
        foreach (var touch in touchscreen.touches)
        {
            if (touch.press.isPressed)
                _activeTouchPositions.Add(touch.position.ReadValue());
        }

        if (_activeTouchPositions.Count < 2)
        {
            previousPinchDistance = 0f;
            if (mobileBridge != null)
                mobileBridge.SetPinching(false);
            return;
        }

        if (mobileBridge != null)
            mobileBridge.SetPinching(true);

        float currentDistance = Vector2.Distance(
            _activeTouchPositions[0],
            _activeTouchPositions[1]);

        if (previousPinchDistance <= 0f)
        {
            previousPinchDistance = currentDistance;
            return;
        }

        float delta = currentDistance - previousPinchDistance;
        previousPinchDistance = currentDistance;

        float screenDiagonal = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
        float normalisedDelta = (screenDiagonal > 0f) ? delta / screenDiagonal : delta;

        targetZoomDistance = Mathf.Clamp(
            targetZoomDistance - normalisedDelta * pinchSpeed * maxZoomDistance,
            minZoomDistance, maxZoomDistance);
    }

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
        if (livingPlayers.Count == 0) return;

        int normalised = GetNormalisedIndex();
        PlayerSetup target = livingPlayers[normalised];

        Transform pivot = target.GetCameraPivot();

        if (pivot == null)
            pivot = target.transform;

        // Pass null for graphics so the target player's mesh is never hidden
        fpCamera.SetTarget(pivot, null);
    }

    private void RefreshPlayerList()
    {
        livingPlayers.Clear();

        PlayerSetup[] allPlayers = FindObjectsByType<PlayerSetup>(FindObjectsSortMode.None);

        foreach (var p in allPlayers)
        {
            if (p.gameObject == this.gameObject) continue;
            if (p.HasEscaped) continue;

            PlayerHealth health = p.GetComponent<PlayerHealth>();
            if (health == null || !health.IsDeadSafe)
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