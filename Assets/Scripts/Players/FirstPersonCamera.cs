using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target;
    public float MouseSensitivity = 10f;
    public LayerMask CullingMask; // Optional: set camera culling

    private float verticalRotation;
    private float horizontalRotation;
    private bool bodyHidden = false;

    void HideLocalPlayerBody()
    {
        if (Target == null || bodyHidden) return;

        // Get the root player object (Target might be CameraPivot child)
        Transform playerRoot = Target.root;

        // Get all renderers in the player body from the root
        Renderer[] renderers = playerRoot.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            // Skip specific objects if needed (e.g., weapons)
            //if (renderer.gameObject.CompareTag("Weapon")) continue;

            renderer.enabled = false;
        }

        bodyHidden = true;
    }

    void LateUpdate()
    {
        if (Target == null) return;

        // Hide body once Target is assigned
        if (!bodyHidden)
        {
            HideLocalPlayerBody();
        }

        transform.position = Target.position;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        verticalRotation -= mouseY * MouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);

        horizontalRotation += mouseX * MouseSensitivity;

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0);
    }
}