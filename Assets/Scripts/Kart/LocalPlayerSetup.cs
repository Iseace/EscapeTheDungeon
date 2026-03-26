using UnityEngine;
using Fusion; // Make sure we are using the Fusion library

// We inherit from NetworkBehaviour so we can use Fusion's Spawned() method
public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Camera Setup")]
    [Tooltip("Drag the Camera Socket from inside THIS prefab here")]
    public Transform myCameraSocket;

    // This runs automatically when Fusion spawns this object over the network
    public override void Spawned()
    {
        // CRITICAL: We only want the camera to follow THIS player if this instance
        // belongs to the local player playing on this computer.
        if (HasInputAuthority)
        {
            // Find the Main Camera in the scene
            Camera mainCam = Camera.main;

            if (mainCam != null)
            {
                // Get our CameraFollow script from it
                CameraFollow camFollow = mainCam.GetComponent<CameraFollow>();

                if (camFollow != null)
                {
                    // Hand over the socket!
                    camFollow.SetTarget(myCameraSocket);
                }
            }
            else
            {
                Debug.LogError("No Main Camera found! Make sure your camera has the 'MainCamera' tag.");
            }
        }
    }
}