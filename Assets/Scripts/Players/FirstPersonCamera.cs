using UnityEngine;
using UnityEngine.InputSystem;

public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target;
    public GameObject PlayerGraphics;
    public float MouseSensitivity = 10f;
    public float MobileSensitivity = 1.0f;
    public InputActionReference lookAction;
    public MobileControlsBridge mobileBridge;
    public string InvisibleLayerName = "LocalPlayerHidden";

    private float verticalRotation;
    private float horizontalRotation;
    private bool isInitialized = false;
    private int invisibleLayer = -1;
    private GameObject previousGraphics;

    private void Awake()
    {
        invisibleLayer = LayerMask.NameToLayer(InvisibleLayerName);
        
        if (mobileBridge == null)
            mobileBridge = FindFirstObjectByType<MobileControlsBridge>();
    }

    private void OnEnable() => lookAction?.action.Enable();
    private void OnDisable() => lookAction?.action.Disable();

    public void SetTarget(Transform newTarget, GameObject graphics)
    {

      if (previousGraphics != null && previousGraphics != graphics)
        SetLayerRecursive(previousGraphics, 0);
            
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

    void LateUpdate()
    {
        if (Target == null || !isInitialized) return;

        if (PlayerGraphics != null)
            ApplyInvisibleLayer();
        transform.position = Target.position;

        Vector2 lookInput = GetLookInput();

        float mouseX = lookInput.x * MouseSensitivity * 0.1f;
        float mouseY = lookInput.y * MouseSensitivity * 0.1f;

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);
        horizontalRotation += mouseX;

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0);
    }

    private Vector2 GetLookInput()
    {
        if (Application.isMobilePlatform || Input.touchSupported)
        {
            if (mobileBridge != null)
                return mobileBridge.CameraLookDelta * MobileSensitivity;
        }
        else
        {
            if (lookAction != null && lookAction.action != null)
                return lookAction.action.ReadValue<Vector2>();
        }
        return Vector2.zero;
    }

    void ApplyInvisibleLayer()
    {
        if (PlayerGraphics == null) return;

        if (invisibleLayer != -1)
            SetLayerRecursive(PlayerGraphics, invisibleLayer);
    }

    private void SetLayerRecursive(GameObject obj, int newLayer)
    {
        if (obj.layer == newLayer) return;

        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, newLayer);
    }
}