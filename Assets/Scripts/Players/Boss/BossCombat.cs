using Fusion;
using UnityEngine;

public class BossCombat : NetworkBehaviour
{
    private BossSpecial _bossSpecial;

    private void Awake()
    {
        _bossSpecial = GetComponent<BossSpecial>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;

        // Block attacks while the boss is invisible
        if (_bossSpecial != null && _bossSpecial.IsAttackBlocked()) return;

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