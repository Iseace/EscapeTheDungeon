using Fusion;
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;

public class PlayerInteraction : NetworkBehaviour
{
    public float range = 3f;
    public LayerMask interactLayer;
    public GameObject signUI;
    public TextMeshProUGUI signText;

    private readonly RaycastHit[] raycastHits = new RaycastHit[48];

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

        if (TryGetBestInteractableHit(ray, true, out _, out _, out IInteractable target) ||
            TryGetBestInteractableHit(ray, false, out _, out _, out target))
        {
            signUI.SetActive(true);
            signText.text = target.GetInteractText();
            return;
        }

        signUI.SetActive(false);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_SendInteraction(Vector3 origin, Vector3 direction)
    {
        // El servidor valida la física basándose en lo que el cliente mandó
        Ray ray = new Ray(origin, direction);

        if (TryGetBestInteractableHit(ray, true, out _, out InteractableItem itemInMask, out IInteractable interactableInMask))
        {
            if (itemInMask != null)
            {
                TryPickupItem(itemInMask);
                return;
            }

            if (interactableInMask != null)
            {
                PlayerSetup player = GetComponent<PlayerSetup>();
                if (player != null)
                {
                    interactableInMask.Interact(player);
                }

                return;
            }
        }

        if (TryGetBestInteractableHit(ray, false, out _, out InteractableItem itemAnyLayer, out IInteractable interactableAnyLayer))
        {
            if (itemAnyLayer != null)
            {
                TryPickupItem(itemAnyLayer);
                return;
            }

            if (interactableAnyLayer != null)
            {
                PlayerSetup player = GetComponent<PlayerSetup>();
                if (player != null)
                {
                    interactableAnyLayer.Interact(player);
                }
            }
        }
    }

    private bool TryGetBestInteractableHit(
        Ray ray,
        bool onlyInteractLayer,
        out RaycastHit bestHit,
        out InteractableItem bestItem,
        out IInteractable bestInteractable)
    {
        bestHit = default;
        bestItem = null;
        bestInteractable = null;

        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, range, ~0, QueryTriggerInteraction.Collide);
        if (hitCount <= 0) return false;

        Array.Sort(raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            Collider col = hit.collider;
            if (col == null) continue;

            if (col.transform.IsChildOf(transform)) continue;

            if (onlyInteractLayer)
            {
                int mask = 1 << col.gameObject.layer;
                if ((mask & interactLayer.value) == 0)
                    continue;
            }

            InteractableItem item = col.GetComponentInParent<InteractableItem>();
            if (item != null)
            {
                bestHit = hit;
                bestItem = item;
                bestInteractable = item;
                return true;
            }

            IInteractable interactable = col.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                bestHit = hit;
                bestItem = null;
                bestInteractable = interactable;
                return true;
            }
        }

        return false;
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }

    private void TryPickupItem(InteractableItem target)
    {
        if (target == null || target.Object == null) return;

        PlayerInventory inv = GetComponent<PlayerInventory>();
        if (inv == null) return;

        // Soltar arma actual si existe y el indice es valido
        if (inv.CurrentWeaponID > 0)
        {
            int oldIndex = inv.CurrentWeaponID - 1;
            if (inv.staffPrefabs != null && oldIndex >= 0 && oldIndex < inv.staffPrefabs.Length)
            {
                Vector3 dropPos = transform.position + transform.forward * 1.2f + Vector3.up;
                Runner.Spawn(inv.staffPrefabs[oldIndex], dropPos, Quaternion.identity);
            }
        }

        // Equipar nueva y borrar del suelo
        inv.CurrentWeaponID = target.itemID;
        Runner.Despawn(target.Object);
    }
}