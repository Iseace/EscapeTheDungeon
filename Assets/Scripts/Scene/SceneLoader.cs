using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Attach to a dedicated always-active GameObject in the LobbyList scene
/// (NOT inside the LobbyListItem prefab).
/// LobbyListUIHandler finds it via FindAnyObjectByType at runtime.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Header("Loading Bar")]
    [Tooltip("Seconds to fill the bar.")]
    [SerializeField] private float fillDuration = 2.5f;

    private GameObject _lobbyListPanel;
    private GameObject _loadingScreen;
    private Slider     _progressionBar;
    private bool       _isLoading;

    private const string LOBBY_LIST_NAME      = "LobbyList";
    private const string LOADING_SCREEN_NAME  = "Loading-Screen";
    private const string PROGRESSION_BAR_NAME = "ProgressionBar";

    private void Awake()
    {
        ResolvePanels();

        if (_loadingScreen != null) _loadingScreen.SetActive(false);
        if (_lobbyListPanel != null) _lobbyListPanel.SetActive(true);
    }

    // ── Reference resolution ──────────────────────────────────────────────────

    private void ResolvePanels()
    {
        if (_lobbyListPanel == null)
            _lobbyListPanel = FindInScene(LOBBY_LIST_NAME);

        if (_loadingScreen == null)
            _loadingScreen = FindInScene(LOADING_SCREEN_NAME);
    }

    private void ResolveSlider()
    {
        if (_progressionBar != null || _loadingScreen == null) return;

        // Search all sliders including inactive children
        foreach (Slider s in _loadingScreen.GetComponentsInChildren<Slider>(true))
        {
            if (s.gameObject.name == PROGRESSION_BAR_NAME)
            {
                _progressionBar = s;
                return;
            }
        }

        // Fallback: first slider found
        Slider[] all = _loadingScreen.GetComponentsInChildren<Slider>(true);
        if (all.Length > 0)
        {
            _progressionBar = all[0];
            Debug.LogWarning($"[SceneLoader] '{PROGRESSION_BAR_NAME}' not found by name — using '{_progressionBar.gameObject.name}'.");
        }
        else
        {
            Debug.LogError("[SceneLoader] No Slider found inside Loading-Screen.");
        }
    }

    private static GameObject FindInScene(string objectName)
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.gameObject.name == objectName) return t.gameObject;
        return null;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by LobbyListUIHandler when the player clicks Join.</summary>
    public void ShowLoadingScreen()
    {
        if (_isLoading) return;
        _isLoading = true;

        ResolvePanels();

        if (_lobbyListPanel != null) _lobbyListPanel.SetActive(false);

        if (_loadingScreen != null)
        {
            _loadingScreen.SetActive(true);
            // Resolve slider AFTER activating so GetComponentsInChildren sees full hierarchy
            ResolveSlider();
        }
        else
        {
            Debug.LogWarning("[SceneLoader] Loading-Screen not found.");
        }

        if (_progressionBar != null) _progressionBar.value = 0f;

        // Safe to call — this GameObject lives directly in the scene and is always active
        StartCoroutine(AnimateBar());
    }

    /// <summary>Resets to lobby state (call on connection failure).</summary>
    public void HideLoadingScreen()
    {
        StopAllCoroutines();
        _isLoading      = false;
        _progressionBar = null;

        if (_loadingScreen  != null) _loadingScreen.SetActive(false);
        if (_lobbyListPanel != null) _lobbyListPanel.SetActive(true);
    }

    // ── Bar animation ─────────────────────────────────────────────────────────

    private IEnumerator AnimateBar()
    {
        if (_progressionBar == null) yield break;

        float elapsed = 0f;
        while (elapsed < fillDuration)
        {
            elapsed += Time.deltaTime;
            _progressionBar.value = Mathf.Clamp01(elapsed / fillDuration);
            yield return null;
        }

        _progressionBar.value = 1f;
        // Fusion handles the actual scene transition — no manual load here.
    }
}