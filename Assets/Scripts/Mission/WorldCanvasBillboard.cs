using UnityEngine;

/// <summary>
/// Makes a world-space canvas face the local camera.
/// Attach this to the canvas root (for example: UI_Progress).
/// </summary>
public class WorldCanvasBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool yawOnly = true;
    [SerializeField] private bool invertForward = false;

    private void LateUpdate()
    {
        Camera cam = ResolveCamera();
        if (cam == null) return;

        Vector3 direction = transform.position - cam.transform.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        if (yawOnly)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
        }

        if (invertForward)
        {
            direction = -direction;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;

        targetCamera = Camera.main;
        if (targetCamera != null) return targetCamera;

        Camera[] cameras = Camera.allCameras;
        if (cameras != null && cameras.Length > 0)
        {
            targetCamera = cameras[0];
        }

        return targetCamera;
    }
}
