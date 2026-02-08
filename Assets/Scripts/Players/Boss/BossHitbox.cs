using Fusion;
using UnityEngine;

public class BossHitbox : NetworkBehaviour
{
    [Header("Attack Settings")]
    public float damage = 50f;
    public float attackCooldown = 2f;
    public string attackAnimationName = "Attack";

    [Header("Movement Penalty")]
    public float attackMoveSpeedMultiplier = 0.3f; // 30% of normal speed during attack

    private Collider _myCollider;
    private Transform _bossRoot;
    private Animator _animator;

    [Networked] private TickTimer AttackCooldownTimer { get; set; }
    [Networked] public NetworkBool IsAttacking { get; set; } // Changed to public so PlayerMovement can access it

    private bool _canAttack = true;

    private void Awake()
    {
        _myCollider = GetComponent<Collider>();
        _bossRoot = transform.root;
        _animator = _bossRoot.GetComponent<Animator>();

        _myCollider.enabled = false;
    }

    public override void FixedUpdateNetwork()
    {
        // Update cooldown
        if (AttackCooldownTimer.Expired(Runner))
        {
            _canAttack = true;
        }
    }

    // Call this method to trigger an attack (e.g., from input or AI)
    public void TryAttack()
    {
        if (!Object.HasStateAuthority) return;
        if (!_canAttack) return;

        PerformAttack();
    }

    private void PerformAttack()
    {
        // Set cooldown
        AttackCooldownTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
        _canAttack = false;
        IsAttacking = true;

        // Trigger animation on all clients
        RPC_PlayAttackAnimation();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttackAnimation()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(attackAnimationName);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Security: Ignore hits with myself
        if (other.transform.root == _bossRoot) return;

        if (other.TryGetComponent<PlayerHealth>(out var health))
        {
            // Only the server/host deals damage
            if (Object.HasStateAuthority)
            {
                health.DealDamage(damage);
                Debug.Log($"¡Impacto! El Boss golpeó a {other.name}");
            }

            // Disable after first hit to prevent multiple damage in one swing
            _myCollider.enabled = false;
        }
    }

    // Animation Events - Called from the Animation timeline
    public void EnableCollider()
    {
        _myCollider.enabled = true;
    }

    public void DisableCollider()
    {
        _myCollider.enabled = false;
    }

    public void AttackAnimationEnd()
    {
        // Called at the end of attack animation
        if (Object.HasStateAuthority)
        {
            IsAttacking = false;
        }
    }

    // Public methods for external scripts
    public bool CanAttack() => _canAttack;
    public float GetMoveSpeedMultiplier() => IsAttacking ? attackMoveSpeedMultiplier : 1f;

    // Optional: Get current cooldown percentage for UI
    public float GetCooldownPercent()
    {
        if (_canAttack) return 0f;

        float remaining = (float)AttackCooldownTimer.RemainingTime(Runner);
        return Mathf.Clamp01(remaining / attackCooldown);
    }
}