using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class FirstPersonCamera : MonoBehaviour
{
    // ===== PUBLIC CONFIGURATION =====

    public Transform Target;                     // The player transform the camera follows
    public GameObject PlayerGraphics;            // The visible player model
    public float MouseSensitivity = 10f;         // Sensitivity for mouse input
    public float MobileSensitivity = 1.0f;       // Sensitivity for mobile input
    public InputActionReference lookAction;      // Input System reference (mouse/gamepad)
    public MobileControlsBridge mobileBridge;    // Mobile look input provider
    public string InvisibleLayerName = "LocalPlayerHidden"; // Layer to hide local player mesh
    public string GameplaySceneName = "Game";


    // ===== PRIVATE STATE =====

    private float verticalRotation;              // X-axis rotation (up/down)
    private float horizontalRotation;            // Y-axis rotation (left/right)
    private bool isInitialized = false;          // Prevents running before setup
    private int invisibleLayer = -1;             // Cached layer index
    private GameObject previousGraphics;         // Used to restore old model layer

    // Spectator zoom: 0 = first-person (on pivot), positive = pull back (third-person)
    // Set by SpectatorSystem; ignored during normal gameplay (stays 0).
    public float ZoomDistance { get; set; } = 0f;


    // =========================================================
    // AWAKE → Runs before Start()
    // =========================================================
    private void Awake()
    {
        // Cache the layer index for performance (avoids calling NameToLayer every frame)
        invisibleLayer = LayerMask.NameToLayer(InvisibleLayerName);

        // Auto-find mobile bridge if not assigned
        if (mobileBridge == null)
            mobileBridge = FindFirstObjectByType<MobileControlsBridge>();
    }


    // =========================================================
    // START → Lock cursor on PC
    // =========================================================
    private void Start()
    {
        if (!ShouldLockCursorInCurrentScene())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (!Application.isMobilePlatform)
        {
            if (OptionsDeploy.IsAnyOptionsMenuOpen)
                return;

            Cursor.lockState = CursorLockMode.Locked;  // Locks cursor to center
            Cursor.visible = false;                    // Hides cursor
        }
    }


    // =========================================================
    // UPDATE → Re-lock cursor if clicked
    // =========================================================
    private void Update()
    {
        if (!ShouldLockCursorInCurrentScene())
        {
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible)
                Cursor.visible = true;
            return;
        }

        if (!Application.isMobilePlatform)
        {
            if (OptionsDeploy.IsAnyOptionsMenuOpen)
                return;

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }


    // =========================================================
    // INPUT SYSTEM ENABLE / DISABLE
    // =========================================================
    private void OnEnable() => lookAction?.action.Enable();
    private void OnDisable() => lookAction?.action.Disable();

    private bool ShouldLockCursorInCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return string.Equals(sceneName, GameplaySceneName, System.StringComparison.OrdinalIgnoreCase);
    }


    // =========================================================
    // SET TARGET (Like Rider switching forms 👀)
    // =========================================================
    public void SetTarget(Transform newTarget, GameObject graphics)
    {
        // Restore previous graphics layer if switching characters
        if (previousGraphics != null && previousGraphics != graphics)
            SetLayerRecursive(previousGraphics, 0); // Default layer

        Target = newTarget;
        PlayerGraphics = graphics;
        previousGraphics = graphics;
        isInitialized = false;

        if (newTarget != null)
        {
            horizontalRotation = newTarget.eulerAngles.y;
            verticalRotation = 0f;
            isInitialized = true;

            if (PlayerGraphics != null)
                ApplyInvisibleLayer();
        }
    }


    // =========================================================
    // LATE UPDATE → Camera rotation & positioning
    // =========================================================
    private void LateUpdate()
    {
        if (Target == null || !isInitialized)
            return;

        if (OptionsDeploy.IsAnyOptionsMenuOpen)
            return;

        if (PlayerGraphics != null)
            ApplyInvisibleLayer();

        // Get input
        Vector2 lookInput = GetLookInput();
        float mouseX = lookInput.x * MouseSensitivity * 0.1f;
        float mouseY = lookInput.y * MouseSensitivity * 0.1f;

        // Vertical (X axis)
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);

        // Horizontal (Y axis)
        horizontalRotation += mouseX;

        // Apply rotation FIRST so transform.forward is up-to-date this frame
        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f);

        // Then set position using the freshly-updated forward vector.
        // ZoomDistance is 0 during normal play so this has no effect then.
        transform.position = Target.position - transform.forward * ZoomDistance;
    }


    // =========================================================
    // INPUT HANDLING
    // =========================================================
    private Vector2 GetLookInput()
    {
        // Removed Input.touchSupported — it can return true on PC builds and break mouse input
        if (Application.isMobilePlatform)
        {
            if (mobileBridge != null)
                return mobileBridge.CameraLookDelta * MobileSensitivity;
        }
        else
        {
            if (lookAction != null && lookAction.action != null)
                return lookAction.action.ReadValue<Vector2>();

            // Fallback: read mouse delta directly if lookAction reference is missing
            if (Mouse.current != null)
                return Mouse.current.delta.ReadValue();
        }

        return Vector2.zero;
    }


    // =========================================================
    // APPLY INVISIBLE LAYER TO PLAYER MODEL
    // =========================================================
    private void ApplyInvisibleLayer()
    {
        if (PlayerGraphics == null)
            return;

        if (invisibleLayer != -1)
            SetLayerRecursive(PlayerGraphics, invisibleLayer);
    }


    // =========================================================
    // RECURSIVE LAYER SETTER
    // =========================================================
    private void SetLayerRecursive(GameObject obj, int newLayer)
    {
        if (obj.layer == newLayer)
            return;

        obj.layer = newLayer;

        // Loop through all children
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, newLayer);
    }
}