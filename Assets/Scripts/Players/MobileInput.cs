using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.EventSystems;

public class MobileControlsBridge : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick")]
    [SerializeField] private RectTransform joystickPad;
    [SerializeField] private float movementRange = 50f;

    [Header("Buttons")]
    [SerializeField] private GameObject attackParent;
    [SerializeField] private GameObject jumpParent;
    [SerializeField] private GameObject pickupParent;

    [Header("Camera Settings")]
    [SerializeField] private float cameraSensitivity = 0.5f;
    [SerializeField] private float joystickRadius = 350f;

    public Vector2 CameraLookDelta { get; private set; }

    private Vector2 lastPointerPos;
    private bool isDragging = false;
    private Vector2 joystickCenter;

    private void Awake()
    {
        bool isMobile = Application.platform == RuntimePlatform.Android || 
                        Application.platform == RuntimePlatform.IPhonePlayer ;
                        //3|| Application.isEditor;


        if (!isMobile)
        {
            gameObject.SetActive(false);
            return;
        }

        Image backgroundImage = gameObject.GetComponent<Image>();
        if (backgroundImage == null)
            backgroundImage = gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0, 0, 0, 0);
        backgroundImage.raycastTarget = true;

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
            OnScreenStick stick = joystickPad.gameObject.GetComponent<OnScreenStick>();
            if (stick == null)
                stick = joystickPad.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = movementRange;
            joystickCenter = RectTransformUtility.WorldToScreenPoint(null, joystickPad.position);
        }

        SetupButton(attackParent, "<Gamepad>/buttonWest");
        SetupButton(jumpParent, "<Gamepad>/buttonSouth");
        SetupButton(pickupParent, "<Gamepad>/buttonNorth");
    }

    private void SetupButton(GameObject parent, string path)
    {
        if (parent == null) return;

        Image img = parent.GetComponent<Image>();
        if (img == null)
        {
            img = parent.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
        }
        img.raycastTarget = true;

        OnScreenButton osb = parent.GetComponent<OnScreenButton>();
        if (osb == null)
            osb = parent.AddComponent<OnScreenButton>();
        osb.controlPath = path;
    }

    private bool IsInJoystickArea(Vector2 screenPos)
    {
        if (joystickPad == null) return false;
        return Vector2.Distance(screenPos, joystickCenter) < joystickRadius;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsInJoystickArea(eventData.position))
            isDragging = false;
        else
        {
            isDragging = true;
            lastPointerPos = eventData.position;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        Vector2 delta = (eventData.position - lastPointerPos) * cameraSensitivity;
        CameraLookDelta = delta;
        lastPointerPos = eventData.position;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        CameraLookDelta = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (!isDragging)
            CameraLookDelta = Vector2.zero;
    }
}