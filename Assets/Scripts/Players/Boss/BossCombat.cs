using Fusion;
using UnityEngine;

public class BossCombat : NetworkBehaviour
{
    public override void FixedUpdateNetwork()
    {
        // Usamos tu estructura PlayerInputData
        if (GetInput(out PlayerInputData data) && data.AttackPressed)
        {
            Rpc_PlayMeleeAttack();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void Rpc_PlayMeleeAttack()
    {
        // Solo disparamos el trigger. La animación hará el resto.
        GetComponentInChildren<Animator>().SetTrigger("Attack");
    }
}