using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.EventSystems;

/// <summary>
/// Mobile controls using EventSystem for GUARANTEED touch detection
/// This WILL work on all devices
/// </summary>
public class MobileControlsBridge : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Joystick (Hierarchy: Joystick -> pad)")]
    [SerializeField] private RectTransform joystickPad; 
    [SerializeField] private float movementRange = 50f;
    
    [Header("Buttons (Empty Parents with Images inside)")]
    [SerializeField] private GameObject attackParent;
    [SerializeField] private GameObject jumpParent;
    
    [Header("Camera Touch Settings")]
    [SerializeField] private float cameraSensitivity = 0.5f;
    [SerializeField] private float joystickRadius = 350f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    public Vector2 CameraLookDelta { get; private set; }
    
    private Vector2 lastPointerPos;
    private bool isDragging = false;
    private Vector2 joystickCenter;
    private Image backgroundImage;

    private void Awake()
    {
        // Add invisible background image to capture touches
        backgroundImage = gameObject.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
        }
        backgroundImage.color = new Color(0, 0, 0, 0); // Completely transparent
        backgroundImage.raycastTarget = true;
        
        // Make this RectTransform fill the screen
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
        
        // Setup Joystick
        if (joystickPad != null)
        {
            var stick = joystickPad.gameObject.GetComponent<OnScreenStick>() 
                ?? joystickPad.gameObject.AddComponent<OnScreenStick>();
            stick.controlPath = "<Gamepad>/leftStick";
            stick.movementRange = movementRange;
            
            joystickCenter = RectTransformUtility.WorldToScreenPoint(null, joystickPad.position);
            
            if (showDebugInfo)
            {
                Debug.Log($"<color=yellow>[MOBILE] Joystick center: {joystickCenter}, Exclusion radius: {joystickRadius}px</color>");
            }
        }
        else
        {
            Debug.LogError("[MOBILE] Joystick Pad not assigned!");
        }

        // Setup Buttons
        SetupOnScreenControl(attackParent, "<Gamepad>/buttonWest");
        SetupOnScreenControl(jumpParent, "<Gamepad>/buttonSouth");
        
        if (showDebugInfo)
        {
            Debug.Log("<color=yellow>[MOBILE] EventSystem-based touch detection initialized</color>");
        }
    }

    private void SetupOnScreenControl(GameObject parent, string path)
    {
        if (parent == null) return;

        var img = parent.GetComponent<Image>();
        if (img == null)
        {
            img = parent.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.3f);
        }
        img.raycastTarget = true;

        var osb = parent.GetComponent<OnScreenButton>() 
            ?? parent.AddComponent<OnScreenButton>();
        osb.controlPath = path;
    }

    private bool IsPositionInJoystickArea(Vector2 screenPos)
    {
        if (joystickPad == null) return false;
        
        float distance = Vector2.Distance(screenPos, joystickCenter);
        return distance < joystickRadius;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Vector2 touchPos = eventData.position;
        
        // Check if touch is in joystick area
        if (IsPositionInJoystickArea(touchPos))
        {
            // Touch is in joystick zone - don't use for camera
            isDragging = false;
            
            if (showDebugInfo)
            {
                float dist = Vector2.Distance(touchPos, joystickCenter);
                Debug.Log($"<color=cyan>[JOYSTICK ZONE] Touch at ({touchPos.x:F0}, {touchPos.y:F0}) - Distance: {dist:F0}px - IGNORED for camera</color>");
            }
        }
        else
        {
            // Touch is outside joystick - use for camera
            isDragging = true;
            lastPointerPos = touchPos;
            
            if (showDebugInfo)
            {
                Debug.Log($"<color=green>[CAMERA START] Touch at ({touchPos.x:F0}, {touchPos.y:F0})</color>");
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        
        Vector2 currentPos = eventData.position;
        Vector2 delta = (currentPos - lastPointerPos) * cameraSensitivity;
        
        CameraLookDelta = delta;
        lastPointerPos = currentPos;
        
        if (showDebugInfo && Time.frameCount % 20 == 0)
        {
            Debug.Log($"<color=green>[CAMERA DRAG] Delta: ({delta.x:F2}, {delta.y:F2})</color>");
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isDragging && showDebugInfo)
        {
            Debug.Log($"<color=green>[CAMERA END] Touch released at ({eventData.position.x:F0}, {eventData.position.y:F0})</color>");
        }
        
        isDragging = false;
        CameraLookDelta = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (!isDragging)
        {
            CameraLookDelta = Vector2.zero;
        }
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.normal.textColor = Color.white;
        
        // Black background for readability
        Texture2D bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.8f));
        bgTex.Apply();
        
        // Info panel
        GUI.DrawTexture(new Rect(5, 5, 400, 140), bgTex);
        
        GUI.Label(new Rect(10, 10, 400, 25), $"Platform: {Application.platform}", style);
        GUI.Label(new Rect(10, 35, 400, 25), $"Is Mobile: {Application.isMobilePlatform}", style);
        GUI.Label(new Rect(10, 60, 400, 25), $"Touch Count: {Input.touchCount}", style);
        GUI.Label(new Rect(10, 85, 400, 25), $"Is Dragging: {isDragging}", style);
        GUI.Label(new Rect(10, 110, 400, 25), $"Delta: ({CameraLookDelta.x:F2}, {CameraLookDelta.y:F2})", style);
        
        // Draw joystick exclusion zone
        Texture2D circleTex = new Texture2D(1, 1);
        circleTex.SetPixel(0, 0, new Color(0, 1, 1, 0.2f));
        circleTex.Apply();
        
        float guiY = Screen.height - joystickCenter.y;
        GUI.DrawTexture(
            new Rect(
                joystickCenter.x - joystickRadius, 
                guiY - joystickRadius, 
                joystickRadius * 2, 
                joystickRadius * 2
            ), 
            circleTex
        );
        
        // Label for joystick zone
        style.fontSize = 14;
        style.normal.textColor = Color.cyan;
        GUI.Label(
            new Rect(joystickCenter.x - 60, guiY - 10, 120, 25), 
            "JOYSTICK ZONE", 
            style
        );
    }
}