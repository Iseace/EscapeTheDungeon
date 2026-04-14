using UnityEngine;
using Fusion;

public enum RoleType { Player, Boss }

public class PlayerRole : NetworkBehaviour
{
    [Networked] public RoleType Role { get; set; }
    [Networked] public NetworkBool IsBoss { get; set; }
    private bool hasSpawned;

    public bool IsNetworkStateReady => hasSpawned && Object != null && Runner != null;
    public bool IsBossSafe => IsNetworkStateReady && IsBoss;

    public override void Spawned()
    {
        hasSpawned = true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawned = false;
    }

    public void SetBoss()
    {
        if (Object != null && Object.HasStateAuthority)
        {
            Role = RoleType.Boss;
            IsBoss = true;
            Debug.Log($"[BOSS ASSIGNED] Player {Object.InputAuthority.PlayerId} is THE BOSS");
        }
    }
}