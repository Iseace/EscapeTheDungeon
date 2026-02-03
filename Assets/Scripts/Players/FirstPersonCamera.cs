using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target;
    public GameObject PlayerGraphics;
    public float MouseSensitivity = 10f;

    // Usamos tu capa existente
    public string InvisibleLayerName = "LocalPlayerHidden";

    private float verticalRotation;
    private float horizontalRotation;
    private bool isInitialized = false;

    public void SetTarget(Transform newTarget, GameObject graphics)
    {
        Target = newTarget;
        PlayerGraphics = graphics;
        isInitialized = false;

        if (newTarget != null)
        {
            horizontalRotation = newTarget.eulerAngles.y;
            verticalRotation = 0f;
            isInitialized = true;

            ApplyInvisibleLayer();
        }
    }

    void LateUpdate()
    {
        if (Target == null || !isInitialized) return;

        // Lo llamamos en LateUpdate para que si cambias de skin (Mage/Assassin),
        // la nueva skin también se vuelva invisible al instante.
        ApplyInvisibleLayer();

        transform.position = Target.position;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        verticalRotation -= mouseY * MouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);
        horizontalRotation += mouseX * MouseSensitivity;

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0);
    }

    void ApplyInvisibleLayer()
    {
        if (PlayerGraphics == null) return;

        int layerIndex = LayerMask.NameToLayer(InvisibleLayerName);

        if (layerIndex != -1)
        {
            SetLayerRecursive(PlayerGraphics, layerIndex);
        }
    }

    private void SetLayerRecursive(GameObject obj, int newLayer)
    {
        // Si ya está en la capa, no hacemos nada (optimización)
        if (obj.layer == newLayer) return;

        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, newLayer);
        }
    }
}