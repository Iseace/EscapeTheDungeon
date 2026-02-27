using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.EventSystems;
using Fusion;

public class MobileControlsBridge : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick")]
    [Tooltip("Drag the ROOT 'Joystick' GameObject here — it contains 'background' and 'pad' as children. " +
             "Hiding this one object hides the whole joystick (background + pad) at once in spectator mode.")]
    [SerializeField] private GameObject joystickParent;   // ROOT 'Joystick' — parent of 'background' AND 'pad'

    [Tooltip("Drag the 'pad' child of Joystick here — used for OnScreenStick setup and joystick radius check only.")]
    [SerializeField] private RectTransform joystickPad;   // 'pad' child — NOT hidden separately; joystickParent covers it
    [SerializeField] private RectTransform knobTransform;
    [SerializeField] private float movementRange = 50f;

    [Header("Buttons")]
    [SerializeField] private GameObject attackParent;
    [SerializeField] private GameObject jumpParent;
    [SerializeField] private GameObject pickupParent;

    [Header("Camera Settings")]
    [SerializeField] private float cameraSensitivity = 0.5f;
    [SerializeField] private float joystickRadius = 350f;

    [Header("Boss UI Textures")]
    [SerializeField] private Texture2D bossJoystickBackgroundTexture;
    [SerializeField] private Texture2D bossJoystickKnobTexture;
    [SerializeField] private Texture2D bossAttackButtonTexture;

    public Vector2 CameraLookDelta { get; private set; }

    private Vector2 lastPointerPos;
    private bool isDragging;
    private bool isSpectating;  // set by PlayerHealth — hides all widgets but keeps drag alive
    private bool isPinching;    // set by SpectatorSystem — suppresses single-finger drag during pinch
    private Vector2 joystickCenter;
    private PlayerRole localPlayerRole;
    private bool hasCheckedRole;
    private bool hasAppliedBossUI;

    private void Awake()
    {
        bool isMobile = Application.platform == RuntimePlatform.Android
                     || Application.platform == RuntimePlatform.IPhonePlayer
                     || Application.isEditor;

        if (!isMobile) { gameObject.SetActive(false); return; }

        // ── Auto-find scene widgets that can't be assigned on a network prefab ──
        // joystickParent lives in the scene's ControlsUI canvas, not on the player
        // prefab, so Inspector assignment is impossible on spawned instances.
        // We search by name as a reliable fallback.
        if (joystickParent == null)
        {
            // Walk up to find the root ControlsUI canvas, then find 'Joystick' inside it
            // Prefer the transform parent hierarchy first (works if this script IS inside ControlsUI)
            joystickParent = GameObject.Find("Joystick");
            if (joystickParent != null)
                Debug.Log("[MobileControlsBridge] joystickParent auto-found: 'Joystick'");
        }

        // joystickPad is 'pad' child of Joystick — auto-find if not assigned
        if (joystickPad == null && joystickParent != null)
        {
            Transform padTransform = joystickParent.transform.Find("pad");
            if (padTransform != null)
            {
                joystickPad = padTransform as RectTransform;
                Debug.Log("[MobileControlsBridge] joystickPad auto-found: 'pad' child of Joystick");
            }
        }

        // Auto-find attack / jump / pickup parents by name if not assigned
        if (attackParent == null) attackParent = GameObject.Find("Atack");   // note: matches your scene spelling
        if (jumpParent   == null) jumpParent   = GameObject.Find("Jump");
        if (pickupParent == null) pickupParent = GameObject.Find("pickUp");

        // ── Transparent full-screen image to receive pointer events ──────────
        // This GameObject must NEVER be deactivated — only its child widgets are hidden
        // in spectator mode so drag events keep arriving for camera look and pinch zoom.
        Image bg = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0);
        bg.raycastTarget = true;

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        if (joystickPad != null)
        {
            OnScreenStick stick = joystickPad.gameObject.GetComponent<OnScreenStick>()
                               ?? joystickPad.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = movementRange;
            joystickCenter = RectTransformUtility.WorldToScreenPoint(null, joystickPad.position);
        }

        SetupButton(attackParent, "<Gamepad>/buttonWest");
        SetupButton(jumpParent,   "<Gamepad>/buttonSouth");
        SetupButton(pickupParent, "<Gamepad>/buttonNorth");
    }

    private void Update()
    {
        // While spectating we skip all role/button logic — only drag events matter
        if (isSpectating) return;

        if (!hasCheckedRole || localPlayerRole == null)
            FindLocalPlayerRole();

        if (localPlayerRole == null) return;

        UpdateButtonVisibility();

        if (localPlayerRole.IsBoss && !hasAppliedBossUI)
        {
            ApplyBossUI();
            hasAppliedBossUI = true;
        }
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerHealth when the local player dies (spectating=true) or respawns (false).
    ///
    /// Expected hierarchy (matches your scene):
    ///   ControlsUI
    ///     └── Joystick           ← joystickParent  ← assign this in Inspector
    ///           ├── background                        this disappears automatically as a child
    ///           └── pad          ← joystickPad     ← assign this for OnScreenStick only
    ///     ├── Atack              ← attackParent
    ///     ├── Jump               ← jumpParent
    ///     └── pickUp             ← pickupParent
    ///
    /// This MobileControlsBridge GameObject stays ACTIVE so its transparent full-screen
    /// Image keeps receiving drag events for camera look and pinch-to-zoom in spectator mode.
    /// </summary>
    public void SetSpectatorMode(bool spectating)
    {
        isSpectating = spectating;

        // Hiding joystickParent ('Joystick' root) hides BOTH its children:
        //   'background' → the visible circle image
        //   'pad'        → the draggable stick area
        // This is why joystickParent must be assigned — hiding only joystickPad
        // leaves the 'background' image still visible on screen.
        if (joystickParent != null)
        {
            joystickParent.SetActive(!spectating);
        }
        else
        {
            // joystickParent not wired up → 'background' will stay visible!
            if (joystickPad != null) joystickPad.gameObject.SetActive(!spectating);
            Debug.LogWarning("[MobileControlsBridge] joystickParent is NOT assigned in the Inspector. " +
                             "Drag the 'Joystick' root GameObject into the joystickParent slot so that " +
                             "the 'background' child also hides correctly in spectator mode.");
        }

        if (attackParent != null)  attackParent.SetActive(!spectating);
        if (jumpParent != null)    jumpParent.SetActive(!spectating);
        if (pickupParent != null)  pickupParent.SetActive(!spectating);

        // Cancel any in-progress drag so there is no sticky delta when switching modes
        if (spectating)
        {
            isDragging = false;
            CameraLookDelta = Vector2.zero;
        }
    }

    /// <summary>
    /// Called by SpectatorSystem when a two-finger pinch starts (true) or ends (false).
    /// While pinching, single-finger camera drag is suppressed so the two gestures
    /// don't fight each other and cause camera jitter.
    /// </summary>
    public void SetPinching(bool pinching)
    {
        isPinching = pinching;

        if (pinching)
        {
            isDragging = false;
            CameraLookDelta = Vector2.zero;
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void FindLocalPlayerRole()
    {
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner == null || !runner.IsRunning) return;

        var localPlayer = runner.LocalPlayer;
        if (localPlayer.IsNone) return;

        if (runner.TryGetPlayerObject(localPlayer, out NetworkObject playerObj) &&
            playerObj.TryGetComponent<PlayerRole>(out var role))
        {
            localPlayerRole = role;
            hasCheckedRole = true;
        }
    }

    private void UpdateButtonVisibility()
    {
        bool isBoss = localPlayerRole.IsBoss;
        if (jumpParent != null)   jumpParent.SetActive(!isBoss);
        if (pickupParent != null) pickupParent.SetActive(!isBoss);
    }

    private void ApplyBossUI()
    {
        if (joystickPad != null   && bossJoystickBackgroundTexture != null) ApplyTexture(joystickPad.gameObject, bossJoystickBackgroundTexture);
        if (knobTransform != null && bossJoystickKnobTexture != null)        ApplyTexture(knobTransform.gameObject, bossJoystickKnobTexture);
        if (attackParent != null  && bossAttackButtonTexture != null)        ApplyTexture(attackParent, bossAttackButtonTexture);
    }

    // Search order: target → child named "background" → any descendant
    private void ApplyTexture(GameObject target, Texture2D texture)
    {
        RawImage raw = target.GetComponent<RawImage>()
                    ?? target.transform.Find("background")?.GetComponent<RawImage>()
                    ?? target.GetComponentInChildren<RawImage>();

        if (raw != null) { raw.texture = texture; return; }

        Image img = target.GetComponent<Image>()
                 ?? target.transform.Find("background")?.GetComponent<Image>()
                 ?? target.GetComponentInChildren<Image>();

        if (img != null)
        {
            img.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return;
        }

        Debug.LogWarning($"[MobileControlsBridge] No Image or RawImage found on '{target.name}' or its children.");
    }

    private void SetupButton(GameObject parent, string path)
    {
        if (parent == null) return;

        Image img = parent.GetComponent<Image>() ?? parent.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = true;

        OnScreenButton osb = parent.GetComponent<OnScreenButton>() ?? parent.AddComponent<OnScreenButton>();
        osb.controlPath = path;
    }

    // Returns true only when the joystick is visible and the tap lands inside its radius
    private bool IsInJoistickArea(Vector2 screenPos) =>
        joystickPad != null
        && joystickPad.gameObject.activeSelf
        && Vector2.Distance(screenPos, joystickCenter) < joystickRadius;

    // ── Pointer events (camera drag) ───────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isPinching) return;   // two-finger pinch is handled by SpectatorSystem
        if (!IsInJoistickArea(eventData.position))
        {
            isDragging = true;
            lastPointerPos = eventData.position;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || isPinching) return;
        CameraLookDelta = (eventData.position - lastPointerPos) * cameraSensitivity;
        lastPointerPos = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        CameraLookDelta = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (!isDragging) CameraLookDelta = Vector2.zero;
    }
}