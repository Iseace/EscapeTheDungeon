using UnityEngine;
using Fusion;

/// <summary>
/// Attach this to your Player prefab alongside NetworkCharacterController
/// </summary>
public class PlayerSetup : NetworkBehaviour
{
    [Header("Camera Setup")]
    [SerializeField] private Transform cameraPivot; // Optional: leave empty to use player transform
    [SerializeField] private GameObject graphics; // The Graphics GameObject with visual model
    [SerializeField] private float cameraHeight = 1.6f; // Height offset if no pivot

    public override void Spawned()
    {
        // Only configure the camera for the LOCAL player
        if (HasInputAuthority)
        {
            SetupCamera();
            LockCursor();
        }
    }

    private void SetupCamera()
    {
        // Find the Main Camera in the scene
        Camera mainCam = Camera.main;

        if (mainCam == null)
        {
            Debug.LogError("Main Camera not found in scene!");
            return;
        }

        // Get the FirstPersonCamera component
        FirstPersonCamera fpCamera = mainCam.GetComponent<FirstPersonCamera>();

        if (fpCamera == null)
        {
            Debug.LogError("FirstPersonCamera component not found on Main Camera!");
            return;
        }

        // Use camera pivot if assigned, otherwise create one at runtime
        Transform cameraTarget = cameraPivot;

        if (cameraTarget == null)
        {
            // Create a pivot at runtime
            GameObject pivot = new GameObject("CameraPivot_Runtime");
            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = new Vector3(0, cameraHeight, 0);
            pivot.transform.localRotation = Quaternion.identity;
            cameraTarget = pivot.transform;

            Debug.Log($"Created runtime camera pivot at height {cameraHeight}");
        }

        // Assign this player to the camera
        fpCamera.SetTarget(cameraTarget, graphics);

        Debug.Log($"Camera set to follow local player: {gameObject.name}");
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}