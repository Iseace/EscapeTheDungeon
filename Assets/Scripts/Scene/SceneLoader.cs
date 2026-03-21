using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Hides the LobbyList panel, reveals the Loading-Screen,
/// and animates the ProgressionBar slider from 0 → 1.
/// All scene references are resolved automatically at runtime,
/// so this script works inside a prefab with no manual Inspector wiring.
/// Wire SceneLoader.ShowLoadingScreen() to the JoinBtn's second OnClick slot.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Header("Loading Bar")]
    [Tooltip("Seconds it takes for the bar to fill completely.")]
    [SerializeField] private float fillDuration = 2.5f;

    // ── Runtime-resolved references ───────────────────────────────────────────
    private GameObject _lobbyListPanel;   // Canvas → LobbyList
    private GameObject _loadingScreen;    // Canvas → Loading-Screen
    private Slider     _progressionBar;   // Canvas → Loading-Screen → ... → ProgressionBar

    // Prevents double-triggering if the button is clicked more than once
    private bool _isLoading = false;

    // ── Names to search ───────────────────────────────────────────────────────
    private const string LOBBY_LIST_NAME      = "LobbyList";
    private const string LOADING_SCREEN_NAME  = "Loading-Screen";
    private const string PROGRESSION_BAR_NAME = "ProgressionBar";

    private void Awake()
    {
        ResolveSceneReferences();

        // Keep loading screen hidden until needed
        if (_loadingScreen != null)
            _loadingScreen.SetActive(false);

        // Make sure the lobby list is visible at the start
        if (_lobbyListPanel != null)
            _lobbyListPanel.SetActive(true);

        // Reset bar
        if (_progressionBar != null)
            _progressionBar.value = 0f;
    }

    /// <summary>
    /// Searches the active scene for the required UI objects by name.
    /// Called once in Awake and again in ShowLoadingScreen as a safety fallback.
    /// </summary>
    private void ResolveSceneReferences()
    {
        // ── LobbyList panel ───────────────────────────────────────────────────
        if (_lobbyListPanel == null)
        {
            GameObject found = FindInScene(LOBBY_LIST_NAME);
            if (found != null)
            {
                _lobbyListPanel = found;
                Debug.Log($"[SCENE LOADER] Found '{LOBBY_LIST_NAME}'.");
            }
            else
            {
                Debug.LogWarning($"[SCENE LOADER] Could not find '{LOBBY_LIST_NAME}' in the scene. " +
                                 "Make sure the GameObject name matches exactly.");
            }
        }

        // ── Loading-Screen panel ──────────────────────────────────────────────
        if (_loadingScreen == null)
        {
            GameObject found = FindInScene(LOADING_SCREEN_NAME);
            if (found != null)
            {
                _loadingScreen = found;
                Debug.Log($"[SCENE LOADER] Found '{LOADING_SCREEN_NAME}'.");
            }
            else
            {
                Debug.LogWarning($"[SCENE LOADER] Could not find '{LOADING_SCREEN_NAME}' in the scene. " +
                                 "Make sure the GameObject name matches exactly.");
            }
        }

        // ── ProgressionBar slider ─────────────────────────────────────────────
        if (_progressionBar == null)
        {
            // Search all Sliders in the scene and match by GameObject name
            Slider[] allSliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Slider s in allSliders)
            {
                if (s.gameObject.name == PROGRESSION_BAR_NAME)
                {
                    _progressionBar = s;
                    Debug.Log($"[SCENE LOADER] Found '{PROGRESSION_BAR_NAME}' slider.");
                    break;
                }
            }

            if (_progressionBar == null)
            {
                Debug.LogWarning($"[SCENE LOADER] Could not find a Slider named '{PROGRESSION_BAR_NAME}' in the scene.");
            }
        }
    }

    /// <summary>
    /// Searches the entire scene hierarchy for a GameObject with the given name,
    /// including inactive objects.
    /// </summary>
    private static GameObject FindInScene(string objectName)
    {
        Transform[] allTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.name == objectName)
                return t.gameObject;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from the JoinBtn's OnClick (second slot).
    /// Hides the lobby list, shows the loading screen, and animates the bar.
    /// Fusion handles the actual scene transition once the connection is ready.
    /// </summary>
    public void ShowLoadingScreen()
    {
        if (_isLoading) return;

        // Safety: try to resolve again in case Awake ran before the scene was ready
        ResolveSceneReferences();

        _isLoading = true;
        Debug.Log("[SCENE LOADER] Showing loading screen.");

        // Hide the lobby list
        if (_lobbyListPanel != null)
            _lobbyListPanel.SetActive(false);
        else
            Debug.LogWarning("[SCENE LOADER] LobbyList panel not found — skipping hide.");

        // Reveal the loading screen
        if (_loadingScreen != null)
            _loadingScreen.SetActive(true);
        else
            Debug.LogWarning("[SCENE LOADER] Loading-Screen not found — skipping show.");

        // Begin the visual fill animation
        StartCoroutine(AnimateProgressBar());
    }

    /// <summary>
    /// Resets the loading screen back to its initial state.
    /// Useful if the connection fails and the player returns to the lobby.
    /// </summary>
    public void HideLoadingScreen()
    {
        StopAllCoroutines();
        _isLoading = false;

        if (_progressionBar != null)
            _progressionBar.value = 0f;

        if (_loadingScreen != null)
            _loadingScreen.SetActive(false);

        if (_lobbyListPanel != null)
            _lobbyListPanel.SetActive(true);

        Debug.Log("[SCENE LOADER] Loading screen hidden, lobby restored.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator AnimateProgressBar()
    {
        if (_progressionBar == null)
        {
            Debug.LogWarning("[SCENE LOADER] ProgressionBar reference is missing — bar animation skipped.");
            yield break;
        }

        _progressionBar.value = 0f;
        float elapsed = 0f;

        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            _progressionBar.value = Mathf.Clamp01(elapsed / fillDuration);
            yield return null;
        }

        _progressionBar.value = 1f;
        Debug.Log("[SCENE LOADER] Bar filled — waiting for Fusion to load the scene.");

        // No manual scene load here.
        // Fusion (NetworkRunnerHandler.JoinGame → StartGame) handles the
        // scene transition and this scene will be replaced automatically.
    }
}