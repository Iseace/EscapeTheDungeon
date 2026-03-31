using UnityEngine;
using UnityEngine.InputSystem;

public class TomatoThrower : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject tomatoPrefab;
    [SerializeField] private Transform throwSocket;

    [Header("Input Settings")]
    [SerializeField] private InputActionReference throwAction;

    [Header("Throw Parameters")]
    [SerializeField] private float throwForce = 20f;
    [SerializeField] private float spawnOffset = 0.5f;
    [SerializeField] private float tomatoLifetime = 3f;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void OnEnable()
    {
        if (throwAction != null) throwAction.action.Enable();
    }

    private void Update()
    {
        if (throwAction != null && throwAction.action.triggered)
        {
            ThrowTomatoAtPointer();
        }
    }

    private void ThrowTomatoAtPointer()
    {
        if (tomatoPrefab == null || throwSocket == null || mainCamera == null) return;

        // 1. Get the screen position (Works for both Mouse and Touch)
        Vector2 pointerPosition = Pointer.current.position.ReadValue();

        // 2. Create a ray from the camera to the world
        Ray ray = mainCamera.ScreenPointToRay(pointerPosition);
        Vector3 targetPoint;

        // 3. Determine where we are aiming
        // We check if the ray hits something (like the winner or the floor)
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            targetPoint = hit.point;
        }
        else
        {
            // If we hit nothing (the sky), we pick a point far away in that direction
            targetPoint = ray.GetPoint(100f);
        }

        // 4. Calculate spawn position and direction
        Vector3 spawnPosition = throwSocket.position + (throwSocket.forward * spawnOffset);
        Vector3 throwDirection = (targetPoint - spawnPosition).normalized;

        // 5. Instantiate and Launch
        GameObject newTomato = Instantiate(tomatoPrefab, spawnPosition, Quaternion.identity);
        Destroy(newTomato, tomatoLifetime);

        Rigidbody rb = newTomato.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // We apply force towards the target we clicked/touched
            rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);
        }
    }
}