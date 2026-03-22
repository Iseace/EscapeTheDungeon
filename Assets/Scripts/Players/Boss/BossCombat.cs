using Fusion;
using UnityEngine;

public class BossCombat : NetworkBehaviour
{
    private BossSpecial _bossSpecial;
    private BossHitbox _bossHitbox;

    [Header("Cooldown")]
    [SerializeField] private float attackCooldown = 0.5f;

    // Local rate-limiter (InputAuthority can't write [Networked] props on a
    // client, so we use a simple time gate to stop RPC spam).
    private float _nextAttackTime;

    private void Awake()
    {
        _bossSpecial = GetComponent<BossSpecial>();
        _bossHitbox = GetComponentInChildren<BossHitbox>();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority) return;

        if (DungeonNetworkRunner.Instance != null && DungeonNetworkRunner.Instance.IsBossFrozen) return;

        // Block attacks while the boss is invisible
        if (_bossSpecial != null && _bossSpecial.IsAttackBlocked()) return;

        if (GetInput(out PlayerInputData data) && data.AttackPressed)
        {
            // Local cooldown – prevents spamming RPCs
            if (Time.time < _nextAttackTime) return;
            _nextAttackTime = Time.time + attackCooldown;

            // LOCAL PREDICTION: Play animation immediately on InputAuthority before RPC
            // This eliminates input lag for the attacking player
            PlayAttackAnimation();

            // Only send the RPC on the forward tick, never during resimulation
            if (Runner.IsForward)
                Rpc_PlayMeleeAttack();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void Rpc_PlayMeleeAttack()
    {
        PlayAttackAnimation();
    }

    private void PlayAttackAnimation()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Reset first so triggers never pile up
            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }

        // Mark the attack state on the authority machine so movement
        // penalty and AttackAnimationEnd work correctly.
        if (_bossHitbox != null)
        {
            // Local flag — instant, no replication delay
            _bossHitbox.SetAttackingLocal(true);

            // Networked flag — only writable on StateAuthority
            if (Object.HasStateAuthority)
                _bossHitbox.IsAttacking = true;
        }
    }
}