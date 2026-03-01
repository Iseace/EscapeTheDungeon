using UnityEngine;

public class MissionPortal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (MissionObjectiveManager.Instance != null && !MissionObjectiveManager.Instance.IsEscapeWindowOpen)
            return;

        PlayerSetup player = other.GetComponentInParent<PlayerSetup>();
        if (player == null) return;
        if (player.Object == null || !player.Object.HasStateAuthority) return;
        if (player.HasEscaped) return;

        player.HasEscaped = true;
    }
}
