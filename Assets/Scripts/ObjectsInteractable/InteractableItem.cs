using Fusion;
using UnityEngine;

public class InteractableItem : NetworkBehaviour, IInteractable
{
    [Header("Ajustes de Inventario")]
    // ESTA ES LA VARIABLE QUE TE PIDE EL ERROR:
    public int itemID;

    [SerializeField] private string itemName = "Staff de Fuego";

    public string GetInteractText() => itemName;

    public void Interact(PlayerSetup player)
    {
        if (player == null || !Object.HasStateAuthority) return;

        PlayerInventory inv = player.GetComponent<PlayerInventory>();
        if (inv == null) return;

        Vector3 dropPos = player.transform.position + player.transform.forward * 1.2f + Vector3.up;
        inv.SwapWeapon(itemID, dropPos);
        Runner.Despawn(Object);
    }
}