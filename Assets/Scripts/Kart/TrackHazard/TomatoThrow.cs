using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

public class TomatoThrower : NetworkBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject tomatoPrefab; // This MUST have a NetworkObject component
    [SerializeField] private Transform throwSocket;

    [Header("Input Settings")]
    [SerializeField] private InputActionReference throwAction;

    [Header("Throw Parameters")]
    [SerializeField] private float throwForce = 20f;
    [SerializeField] private float spawnOffset = 0.5f;

    private Camera mainCamera;

    public override void Spawned()
    {
        mainCamera = Camera.main;

        // Habilitamos el input solo si somos el dueño de esta instancia local
        // Nota: VictorySpawner habilita este script solo para los perdedores.
        if (throwAction != null) throwAction.action.Enable();
    }

    private void Update()
    {
        // Asegurarnos de tener la cámara
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // El VictorySpawner habilita este componente localmente para los "perdedores".
        if (throwAction != null && throwAction.action != null && throwAction.action.WasPressedThisFrame())
        {
            // 1. EL CLIENTE calcula a dónde quiere disparar localmente
            Vector2 pointerPosition = Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
            Ray ray = mainCamera.ScreenPointToRay(pointerPosition);
            Vector3 targetPoint;

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                targetPoint = hit.point;
            }
            else
            {
                targetPoint = ray.GetPoint(100f);
            }

            // 2. Enviamos el punto de impacto exacto al servidor
            RPC_ThrowTomato(targetPoint);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ThrowTomato(Vector3 targetPoint)
    {
        // Solo el servidor (StateAuthority) ejecuta el spawn real
        if (!Runner.IsServer) return;

        ThrowTomatoAtPoint(targetPoint);
    }

    private void ThrowTomatoAtPoint(Vector3 targetPoint)
    {
        if (tomatoPrefab == null || throwSocket == null) return;

        // 4. Calculate spawn position and direction using the point sent by the client
        Vector3 spawnPosition = throwSocket.position + (throwSocket.forward * spawnOffset);
        Vector3 throwDirection = (targetPoint - spawnPosition).normalized;

        // 5. Networked Spawn (Executed on Server)
        Runner.Spawn(tomatoPrefab, spawnPosition, Quaternion.identity, Object.StateAuthority, (runner, obj) =>
        {
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(throwDirection * throwForce, ForceMode.Impulse);
            }
        });
    }
}