using UnityEngine;
using Fusion;

public class FirstPersonCamera : NetworkBehaviour
{
    public Transform player;
    public float mouseSensitivity = 2f;
    float verticalRotation = 0f;
    float horizontalRotation = 0f;

    public override void Spawned()
    {
        if (!HasInputAuthority)
        {
            gameObject.SetActive(false);
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!HasInputAuthority) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

        horizontalRotation += mouseX;
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -90f, 90f);

        // Cámara (arriba / abajo)
        transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        // Player (izquierda / derecha)
        player.rotation = Quaternion.Euler(0f, horizontalRotation, 0f);
    }
}