using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.EventSystems;
using Fusion;

public class MobileControlsBridge : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick")]
    [SerializeField] private RectTransform joystickPad;
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
    private Vector2 joystickCenter;
    private PlayerRole localPlayerRole;
    private bool hasCheckedRole;
    private bool hasAppliedBossUI;

    private void Awake()
    {
        bool isMobile = Application.platform == RuntimePlatform.Android
                     || Application.platform == RuntimePlatform.IPhonePlayer;
                     //|| Application.isEditor;

        if (!isMobile) { gameObject.SetActive(false); return; }

        // Transparent full-screen image to receive pointer events
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

    private bool IsInJoistickArea(Vector2 screenPos) =>
        joystickPad != null && Vector2.Distance(screenPos, joystickCenter) < joystickRadius;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInJoistickArea(eventData.position))
        {
            isDragging = true;
            lastPointerPos = eventData.position;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
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