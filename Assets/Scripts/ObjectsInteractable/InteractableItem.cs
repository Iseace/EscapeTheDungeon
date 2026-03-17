using Fusion;
using UnityEngine;

public class InteractableItem : NetworkBehaviour, IInteractable
{
    [Header("Ajustes de Inventario")]
    // ESTA ES LA VARIABLE QUE TE PIDE EL ERROR:
    public int itemID;

    [SerializeField] private string itemName = "Staff de Fuego";
    [Header("Interaction Collider")]
    [Tooltip("Si se asigna, este collider será el único usado en runtime para interacción/física del item")]
    [SerializeField] private Collider primaryCollider;
    [SerializeField] private bool sanitizeCollidersOnSpawn = true;
    [SerializeField] private bool debugColliderSanitization = false;

    public override void Spawned()
    {
        if (sanitizeCollidersOnSpawn)
        {
            SanitizeColliders();
        }
    }

    private void Awake()
    {
        // Also sanitize in non-network/editor flows where Spawned might not run.
        if (!Application.isPlaying) return;
        if (sanitizeCollidersOnSpawn)
        {
            SanitizeColliders();
        }
    }

    public string GetInteractText() => itemName;

    public void Interact(PlayerSetup player)
    {
        // El servidor suele manejar esto desde el Raycast del jugador
        // para poder soltar el arma anterior antes de destruir esta.
    }

    private void SanitizeColliders()
    {
        Collider[] all = GetComponentsInChildren<Collider>(true);
        if (all == null || all.Length <= 1) return;

        Collider selected = primaryCollider;
        if (selected == null)
        {
            selected = GetComponent<Collider>();
        }

        if (selected == null)
        {
            // Fallback: first active non-trigger collider.
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].isTrigger) continue;
                selected = all[i];
                break;
            }
        }

        if (selected == null)
        {
            selected = all[0];
        }

        int disabledCount = 0;
        for (int i = 0; i < all.Length; i++)
        {
            Collider col = all[i];
            if (col == null) continue;

            bool keep = col == selected;
            col.enabled = keep;
            if (!keep) disabledCount++;
        }

        if (selected != null)
        {
            selected.enabled = true;
        }

        if (debugColliderSanitization)
        {
            string selectedName = selected != null ? selected.name : "<none>";
            Debug.Log($"[InteractableItem] Collider sanitize on {name}: selected={selectedName}, disabled={disabledCount}");
        }
    }
}