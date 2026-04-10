using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Setup")]
    // We don't drag this in anymore, the player script will set it
    public Transform cameraSocket;

    [Header("Smoothness Settings")]
    public float positionSmoothSpeed = 10f;
    public float rotationSmoothSpeed = 10f;

    private void LateUpdate()
    {
        // If the player hasn't spawned yet, just do nothing
        if (cameraSocket == null) return;

        // Smoothly move the camera toward the socket's position
        transform.position = Vector3.Lerp(
            transform.position,
            cameraSocket.position,
            positionSmoothSpeed * Time.deltaTime
        );

        // Smoothly rotate the camera to match the socket's rotation
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            cameraSocket.rotation,
            rotationSmoothSpeed * Time.deltaTime
        );
    }

    // The newly spawned player will call this method
    public void SetTarget(Transform newSocket)
    {
        cameraSocket = newSocket;

        // Snap immediately to the socket so the camera doesn't visibly fly 
        // across the map when the player first spawns.
        transform.position = cameraSocket.position;
        transform.rotation = cameraSocket.rotation;
    }
}