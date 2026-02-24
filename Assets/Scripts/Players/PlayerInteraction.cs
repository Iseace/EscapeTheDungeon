using Fusion;
using UnityEngine;
using TMPro;

public class PlayerInteraction : NetworkBehaviour
{
    public float range = 3f;
    public LayerMask interactLayer;
    public GameObject signUI;
    public TextMeshProUGUI signText;

    // 1. ESTO VA EN FIXEDUPDATENETWORK (Solo para Input y Red)
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerInputData data))
        {
            // EL CAMBIO CLAVE: Solo el dueño del objeto envía el RPC
            // Esto quita el error de "Local simulation is not allowed"
            if (Object.HasInputAuthority && data.InteractPressed)
            {
                Vector3 camPos = Camera.main.transform.position;
                Vector3 camForward = Camera.main.transform.forward;
                Rpc_SendInteraction(camPos, camForward);
            }
        }
    }

    // 2. ESTO VA EN RENDER (Solo para lo visual/UI)
    // Se ejecuta en cada frame, por eso el cliente siempre verá el texto
    public override void Render()
    {
        // Solo hacemos el raycast visual para nosotros mismos
        if (Object.HasInputAuthority)
        {
            HandleRaycastUI();
        }
    }

    // 3. NUEVA FUNCIÓN AUXILIAR PARA LA UI
    private void HandleRaycastUI()
    {
        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(ray, out RaycastHit hit, range, interactLayer))
        {
            IInteractable target = hit.collider.GetComponentInParent<IInteractable>();
            if (target != null)
            {
                signUI.SetActive(true);
                signText.text = target.GetInteractText();
                return;
            }
        }
        signUI.SetActive(false);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SendInteraction(Vector3 origin, Vector3 direction)
    {
        // El servidor valida la física basándose en lo que el cliente mandó
        Ray ray = new Ray(origin, direction);
        if (Physics.Raycast(ray, out RaycastHit hit, range, interactLayer))
        {
            InteractableItem target = hit.collider.GetComponentInParent<InteractableItem>();
            if (target != null)
            {
                PlayerInventory inv = GetComponent<PlayerInventory>();

                // Soltar arma actual si existe
                if (inv.CurrentWeaponID > 0)
                {
                    Vector3 dropPos = transform.position + transform.forward * 1.2f + Vector3.up;
                    Runner.Spawn(inv.staffPrefabs[inv.CurrentWeaponID - 1], dropPos, Quaternion.identity);
                }

                // Equipar nueva y borrar del suelo
                inv.CurrentWeaponID = target.itemID;
                Runner.Despawn(target.Object);
                return;
            }

            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                PlayerSetup player = GetComponent<PlayerSetup>();
                if (player != null)
                {
                    interactable.Interact(player);
                }
            }
        }
    }
}