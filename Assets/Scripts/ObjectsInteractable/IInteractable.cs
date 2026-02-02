public interface IInteractable
{
    string GetInteractText(); // El texto para el letrero (ej: "Agarrar Varita")
    void Interact(PlayerSetup player); // La acción que ocurrirá
}