using Fusion;
using UnityEngine;

public class BossCombat : NetworkBehaviour
{
    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;

        if (GetInput(out PlayerInputData data) && data.AttackPressed)
        {
            Rpc_PlayMeleeAttack();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void Rpc_PlayMeleeAttack()
    {
        GetComponentInChildren<Animator>().SetTrigger("Attack");
    }
}