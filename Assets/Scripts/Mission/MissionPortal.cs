using Fusion;
using UnityEngine;

public class MissionPortal : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerSetup player = other.GetComponentInParent<PlayerSetup>();
        if (player == null) return;
        if (player.Object == null || !player.Object.HasStateAuthority) return;
        if (player.HasEscaped) return;

        player.HasEscaped = true;
    }
}
