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
        // El servidor suele manejar esto desde el Raycast del jugador
        // para poder soltar el arma anterior antes de destruir esta.
    }
}