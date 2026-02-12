using UnityEngine;
using Fusion;

public enum RoleType { Player, Boss }

public class PlayerRole : NetworkBehaviour
{
    [Networked] public RoleType Role { get; set; }
    [Networked] public NetworkBool IsBoss { get; set; }

    public void SetBoss()
    {
        if (Object.HasStateAuthority)
        {
            Role = RoleType.Boss;
            IsBoss = true;
            Debug.Log($"[BOSS ASSIGNED] Player {Object.InputAuthority.PlayerId} is THE BOSS");
        }
    }
}