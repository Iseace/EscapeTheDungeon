using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target; // Will be set at runtime
    public GameObject PlayerGraphics; // Will be set at runtime
    public float MouseSensitivity = 10f;

    private float verticalRotation;
    private float horizontalRotation;
    private bool bodyHidden = false;
    private bool isInitialized = false;

    /// <summary>
    /// Call this to assign the local player to this camera
    /// </summary>
    public void SetTarget(Transform newTarget, GameObject graphics)
    {
        Target = newTarget;
        PlayerGraphics = graphics;
        bodyHidden = false;
        isInitialized = false;

        if (newTarget != null)
        {
            horizontalRotation = newTarget.eulerAngles.y;
            verticalRotation = 0f;
            isInitialized = true;
        }
    }

    void LateUpdate()
    {
        if (Target == null) return;

        // Initialize on first frame if not done yet
        if (!isInitialized)
        {
            horizontalRotation = Target.eulerAngles.y;
            verticalRotation = 0f;
            isInitialized = true;
        }

        // Hide the player body for this camera only
        if (!bodyHidden && PlayerGraphics != null)
        {
            HidePlayerGraphics();
            bodyHidden = true;
        }

        // Follow the target position (CameraPivot)
        transform.position = Target.position;

        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        verticalRotation -= mouseY * MouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);

        horizontalRotation += mouseX * MouseSensitivity;

        // Apply rotation to camera
        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0);
    }

    void HidePlayerGraphics()
    {
        Renderer[] renderers = PlayerGraphics.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }
    }
}