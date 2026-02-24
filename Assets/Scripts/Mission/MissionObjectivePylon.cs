using UnityEngine;

public class MissionObjectivePylon : MonoBehaviour, IInteractable
{
    [Header("Activation")]
    [SerializeField] private float activationRadius = 2.5f;
    [SerializeField] private string interactText = "Activar pylon";
    [SerializeField] private string activatedText = "Pylon activado";

    public bool IsActivated { get; private set; }

    public string GetInteractText()
    {
        return IsActivated ? activatedText : interactText;
    }

    public void Interact(PlayerSetup player)
    {
        if (IsActivated || player == null) return;

        float distance = Vector3.Distance(player.transform.position, transform.position);
        if (distance > activationRadius) return;

        IsActivated = true;
        MissionObjectiveManager.Instance?.NotifyPylonActivated(this);
    }
}
