using UnityEngine;
using Fusion;
using System.Collections.Generic;

/// <summary>
/// Added to the local player's GameObject when they die.
/// Switches the main camera to follow living players.
/// Navigation mirrors the CharacterSelector carousel:
///   Right Arrow / D / Mouse Left  → next player
///   Left  Arrow / A / Mouse Right → previous player
/// </summary>
public class SpectatorSystem : MonoBehaviour
{
    [Header("UI (optional)")]
    [Tooltip("Assign a canvas/panel to show 'SPECTATING' overlay")]
    [SerializeField] private GameObject spectatorHUDPrefab;

    // ── Internal state ─────────────────────────────────────────────────────────
    private List<PlayerSetup> livingPlayers = new List<PlayerSetup>();
    private int currentIndex = 0;
    private int lastKnownCount = 0;

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
        // ── Carousel input (mirrors CharacterSelector) ──────────────────────
        float horizontalInput = 0f;

        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            horizontalInput = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            horizontalInput = -1f;

        // Detect press edge (same GetKeyDown-equivalent as CharacterSelector)
        if (previousHorizontalInput == 0f)
        {
            if (horizontalInput > 0.5f)
                Navigate(1);
            else if (horizontalInput < -0.5f)
                Navigate(-1);
        }
        previousHorizontalInput = horizontalInput;

        // Mouse button single-press
        if (Input.GetMouseButtonDown(0)) Navigate(1);
        if (Input.GetMouseButtonDown(1)) Navigate(-1);

        // Refresh if someone else died while we are spectating
        if (livingPlayers.Count != lastKnownCount)
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

        // Prefer the named CameraPivot; fallback to the player root
        Transform pivot = target.transform.Find("CameraPivot") ?? target.transform;

        // Pass null for graphics so the target player's mesh is never hidden
        fpCamera.SetTarget(pivot, null);

        Debug.Log($"[SpectatorSystem] Now spectating player {normalised} ({target.gameObject.name})");
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

        lastKnownCount = livingPlayers.Count;
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