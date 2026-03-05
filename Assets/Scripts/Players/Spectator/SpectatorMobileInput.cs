using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Handles mobile touch input EXCLUSIVELY during spectator mode.
/// PlayerHealth enables this component on death, disables it on respawn.
///
/// Scene hierarchy expected:
///   ControlsUI
///     ├── [MobileControlsBridge GO]   ← gameplay drag; its Image raycastTarget=false when spectating
///     ├── Joystick / Atack / Jump / pickUp
///     ├── SpectatorInput             ← THIS script's GameObject (always active, component toggled)
///     └── Spectator Controller        ← shown by PlayerHealth.SetDeadUI()
///           ├── LastPlayer
///           └── NextPlayer
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
    private MobileControlsBridge _bridge;
    private Image _touchArea;
    private Vector2 _lastPointerPos;
    private bool _isDragging;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        bool isMobile = Application.platform == RuntimePlatform.Android
                     || Application.platform == RuntimePlatform.IPhonePlayer
                     || Application.isEditor;   // isEditor covers the Unity Simulator

        if (!isMobile) { enabled = false; return; }

        // ── Full-screen transparent Image — raycastTarget starts OFF ──────────
        // OnEnable turns it on; OnDisable turns it off.
        _touchArea = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        _touchArea.color = new Color(0, 0, 0, 0);
        _touchArea.raycastTarget = false;   // OFF until we go spectating

        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // ── Find MobileControlsBridge ──────────────────────────────────────────
        _bridge = FindFirstObjectByType<MobileControlsBridge>();

        // ── Auto-find nav buttons ──────────────────────────────────────────────
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
        }

        // Buttons start hidden; OnEnable shows them when we go spectating
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

        _isDragging = false;
        if (_bridge != null)
            _bridge.SetExternalLookDelta(Vector2.zero);
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
    /// Finds a GameObject by exact name, including inactive ones.
    /// GameObject.Find() skips inactive objects, so we walk all scene roots instead.
    /// </summary>
    private static GameObject FindInactiveByName(string targetName)
    {
        GameObject active = GameObject.Find(targetName);
        if (active != null) return active;

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
    /// Finds the first child (including inactive) whose name contains the keyword (case-insensitive).
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