using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles mobile touch input EXCLUSIVELY during spectator mode.
///
/// This script lives on its own GameObject inside the ControlsUI Canvas
/// (a sibling of Joystick, Atack, Jump, pickUp, and Spectator Controller).
/// PlayerHealth enables it on death and disables it on respawn.
///
/// Responsibilities while enabled:
///   • Owns a full-screen transparent Image that receives camera-look drag events.
///   • Feeds the drag delta to MobileControlsBridge.SetExternalLookDelta() so
///     FirstPersonCamera keeps working without any changes.
///   • Shows the LastPlayer / NextPlayer navigation buttons (auto-found by name
///     inside the "Spectator Controller" GameObject).
///   • Disables itself cleanly: removes its raycast coverage and hides nav buttons.
///
/// Setup in Unity:
///   1. Inside your ControlsUI Canvas create an empty child GameObject, e.g. "SpectatorInput".
///   2. Add this script to it.
///   3. Make sure the GameObject starts INACTIVE or the component starts DISABLED —
///      PlayerHealth will enable it when the player dies.
///   4. (Optional) Drag the LastPlayer / NextPlayer buttons into the Inspector slots;
///      if left empty the script finds them automatically by name.
///
/// Scene hierarchy expected:
///   ControlsUI
///     ├── [MobileControlsBridge GO]   ← gameplay drag, disabled when spectating
///     ├── Joystick
///     ├── Atack
///     ├── Jump
///     ├── pickUp
///     ├── Spectator Controller        ← shown by PlayerHealth.SetDeadUI()
///     │     ├── LastPlayer            ← Navigate(-1)
///     │     └── NextPlayer            ← Navigate(+1)
///     └── SpectatorInput             ← THIS script's GameObject
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SpectatorMobileInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Camera")]
    [Tooltip("How fast the camera moves per pixel of drag. Matches MobileControlsBridge's default (0.5).")]
    [SerializeField] private float cameraSensitivity = 0.5f;

    [Header("Nav Buttons (auto-found if left empty)")]
    [Tooltip("The 'LastPlayer' button inside 'Spectator Controller'. Auto-found by name if not assigned.")]
    [SerializeField] private GameObject lastPlayerButton;

    [Tooltip("The 'NextPlayer' button inside 'Spectator Controller'. Auto-found by name if not assigned.")]
    [SerializeField] private GameObject nextPlayerButton;

    // ── Runtime refs ───────────────────────────────────────────────────────────
    private MobileControlsBridge _bridge;   // camera delta is written here so FirstPersonCamera needs no changes
    private Image _touchArea;               // the full-screen transparent drag-catcher
    private Vector2 _lastPointerPos;
    private bool _isDragging;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        // Non-mobile: nothing to do — the component will just sit disabled
        bool isMobile = Application.platform == RuntimePlatform.Android
                     || Application.platform == RuntimePlatform.IPhonePlayer
                     || Application.isEditor;

        if (!isMobile) { enabled = false; return; }

        // ── Full-screen transparent Image (raycastTarget starts OFF) ──────────
        // OnEnable turns it on; OnDisable turns it off.
        // This prevents any overlap with MobileControlsBridge during gameplay.
        _touchArea = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        _touchArea.color = new Color(0, 0, 0, 0);
        _touchArea.raycastTarget = false;   // OFF until we go spectating

        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // ── Find MobileControlsBridge (camera delta bus) ───────────────────────
        _bridge = FindFirstObjectByType<MobileControlsBridge>();
        if (_bridge == null)
            Debug.LogWarning("[SpectatorMobileInput] MobileControlsBridge not found. Camera drag won't work.");

        // ── Auto-find nav buttons ──────────────────────────────────────────────
        // They live inside "Spectator Controller" which may be inactive right now,
        // so we use includeInactive=true when searching.
        if (lastPlayerButton == null || nextPlayerButton == null)
        {
            GameObject spectatorCanvas = FindInactiveByName("Spectator Controller");
            if (spectatorCanvas != null)
            {
                if (lastPlayerButton == null)
                    lastPlayerButton = FindChildContains(spectatorCanvas, "last");

                if (nextPlayerButton == null)
                    nextPlayerButton = FindChildContains(spectatorCanvas, "next");
            }
            else
            {
                Debug.LogWarning("[SpectatorMobileInput] Could not find 'Spectator Controller' in scene. " +
                                 "Assign lastPlayerButton / nextPlayerButton manually in the Inspector.");
            }
        }

        // Nav buttons start hidden; OnEnable shows them when we go spectating
        SetNavButtonsVisible(false);
    }

    /// <summary>
    /// Called by PlayerHealth.SetDeadUI() → enables this component.
    /// Activates the touch area and shows the navigation buttons.
    /// </summary>
    private void OnEnable()
    {
        if (_touchArea != null)
            _touchArea.raycastTarget = true;

        SetNavButtonsVisible(true);

        // Reset state so there's no sticky delta from the previous session
        _isDragging = false;
        if (_bridge != null)
            _bridge.SetExternalLookDelta(Vector2.zero);

        Debug.Log("[SpectatorMobileInput] Spectator touch input ENABLED.");
    }

    /// <summary>
    /// Called by PlayerHealth.SetAliveUI() → disables this component.
    /// Releases the touch area so MobileControlsBridge can reclaim it.
    /// </summary>
    private void OnDisable()
    {
        if (_touchArea != null)
            _touchArea.raycastTarget = false;

        SetNavButtonsVisible(false);

        _isDragging = false;
        if (_bridge != null)
            _bridge.SetExternalLookDelta(Vector2.zero);

        Debug.Log("[SpectatorMobileInput] Spectator touch input DISABLED.");
    }

    private void LateUpdate()
    {
        // Clear delta every frame when not actively dragging so the camera doesn't drift
        if (!_isDragging && _bridge != null)
            _bridge.SetExternalLookDelta(Vector2.zero);
    }

    // ── Pointer events (camera drag) ───────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDragging = true;
        _lastPointerPos = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;

        Vector2 delta = (eventData.position - _lastPointerPos) * cameraSensitivity;
        _lastPointerPos = eventData.position;

        if (_bridge != null)
            _bridge.SetExternalLookDelta(delta);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging = false;

        if (_bridge != null)
            _bridge.SetExternalLookDelta(Vector2.zero);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetNavButtonsVisible(bool visible)
    {
        if (lastPlayerButton != null) lastPlayerButton.SetActive(visible);
        if (nextPlayerButton != null) nextPlayerButton.SetActive(visible);
    }

    /// <summary>
    /// Finds a root or scene GameObject by exact name, including inactive ones.
    /// GameObject.Find() skips inactive objects, so we iterate all roots instead.
    /// </summary>
    private static GameObject FindInactiveByName(string targetName)
    {
        // Search active objects first (fast path)
        GameObject active = GameObject.Find(targetName);
        if (active != null) return active;

        // Walk every root and their full hierarchy (handles inactive parents)
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            GameObject found = FindInHierarchy(root, targetName);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject FindInHierarchy(GameObject root, string targetName)
    {
        if (root.name == targetName) return root;
        foreach (Transform child in root.transform)
        {
            GameObject found = FindInHierarchy(child.gameObject, targetName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Finds the first child (including inactive) whose name contains <paramref name="keyword"/>
    /// (case-insensitive).
    /// </summary>
    private static GameObject FindChildContains(GameObject root, string keyword)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject.name.ToLower().Contains(keyword.ToLower()))
                return t.gameObject;
        }
        return null;
    }
}