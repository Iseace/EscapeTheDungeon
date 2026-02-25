using UnityEngine;
using UnityEngine.InputSystem; 
using Fusion;
using System.Collections.Generic;

public class SpectatorSystem : MonoBehaviour
{
    [Header("UI (optional)")]
    [Tooltip("Assign a canvas/panel to show 'SPECTATING' overlay")]
    [SerializeField] private GameObject spectatorHUDPrefab;

    // ── Internal state ─────────────────────────────────────────────────────────
    private List<PlayerSetup> livingPlayers = new List<PlayerSetup>();
    private int currentIndex = 0;

    private FirstPersonCamera fpCamera;
    private GameObject spectatorHUDInstance;

    // Input debounce – identical to CharacterSelector's previousNavInput pattern
    private float previousHorizontalInput = 0f;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Start()
    {
        fpCamera = Camera.main != null ? Camera.main.GetComponent<FirstPersonCamera>() : null;

        if (fpCamera == null)
        {
            Debug.LogError("[SpectatorSystem] FirstPersonCamera not found on Main Camera!");
            return;
        }

        // Unlock cursor so mouse clicks work for switching
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (spectatorHUDPrefab != null)
            spectatorHUDInstance = Instantiate(spectatorHUDPrefab);

        RefreshPlayerList();

        if (livingPlayers.Count > 0)
            FocusCurrentTarget();

        Debug.Log("[SpectatorSystem] Spectator mode active. Use Arrow Keys / A-D / Mouse Buttons to switch players.");
    }

    void Update()
    {
        if (fpCamera == null) return;

        // ── Carousel input (new Input System) ───────────────────────────────
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

        // Detect press edge
        if (previousHorizontalInput == 0f)
        {
            if (horizontalInput > 0.5f)
                Navigate(1);
            else if (horizontalInput < -0.5f)
                Navigate(-1);
        }
        previousHorizontalInput = horizontalInput;

        // Mouse button single-press
        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame)  Navigate(1);
            if (mouse.rightButton.wasPressedThisFrame) Navigate(-1);
        }

        // ── Keep retrying every frame until we lock onto a living player ─────
        if (livingPlayers.Count == 0)
        {
            RefreshPlayerList();
            if (livingPlayers.Count > 0)
                FocusCurrentTarget();
            return;
        }

        // ── Detect if any player in our list has since died ──────────────────
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
        if (spectatorHUDInstance != null)
            Destroy(spectatorHUDInstance);
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
            Debug.LogWarning($"[SpectatorSystem] GetCameraPivot() returned null for '{target.gameObject.name}'. Using player root as fallback.");
            pivot = target.transform;
        }

        // Pass null for graphics so the target player's mesh is never hidden
        fpCamera.SetTarget(pivot, null);

        Debug.Log($"[SpectatorSystem] Now spectating '{target.gameObject.name}' via pivot '{pivot.name}' world Y={pivot.position.y:F2}");
    }

    private void RefreshPlayerList()
    {
        livingPlayers.Clear();

        PlayerSetup[] allPlayers = FindObjectsByType<PlayerSetup>(FindObjectsSortMode.None);

        foreach (var p in allPlayers)
        {
            if (p.gameObject == this.gameObject) continue; // skip ourselves (dead)

            PlayerHealth health = p.GetComponent<PlayerHealth>();
            if (health == null || !health.IsDead)
                livingPlayers.Add(p);
        }
    }

    /// <summary>
    /// Maps any integer currentIndex to a valid [0, count) index.
    /// Same algorithm as CharacterSelector.GetNormalizedIndex().
    /// </summary>
    private int GetNormalisedIndex()
    {
        if (livingPlayers.Count == 0) return 0;

        int r = currentIndex % livingPlayers.Count;
        return (r < 0) ? r + livingPlayers.Count : r;
    }
}