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
    public float postAttackPenaltyDuration = 2f;

    [Header("Knockback Settings")]
    public float knockbackForce = 250f; // TRULY DRAMATIC PUSH
    public float knockbackUpwardBonus = 15f; // Launches into air to take advantage of low air-friction

    private Collider _myCollider;
    private Transform _bossRoot;
    private Animator _animator;

    [Networked] private TickTimer AttackCooldownTimer { get; set; }
    [Networked] private TickTimer MovePenaltyTimer { get; set; }
    [Networked] public NetworkBool IsAttacking { get; set; } // Changed to public so PlayerMovement can access it

    // Local (non-networked) mirror of IsAttacking.  Set immediately on every
    // machine via the RPC / animation events so the InputAuthority client
    // doesn't have to wait for networked-state replication.
    private bool _isAttackingLocal;
    private bool _isMovePenaltyLocal;
    private float _movePenaltyEndTimeLocal;

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
            _animator.ResetTrigger(attackAnimationName);
            _animator.SetTrigger(attackAnimationName);
        }
    }

    private void OnTriggerEnter(Collider other) => HandleHit(other);

    // OnTriggerStay covers the case where the target is already overlapping
    // when the collider is enabled – OnTriggerEnter won't fire in that
    // situation, but OnTriggerStay will on the next physics step.
    private void OnTriggerStay(Collider other) => HandleHit(other);

    private void HandleHit(Collider other)
    {
        // Already scored a hit this swing – ignore further contacts
        if (!_myCollider.enabled) return;

        // Ignore hits with myself
        if (other.transform.root == _bossRoot) return;

        if (other.TryGetComponent<PlayerHealth>(out var health))
        {
            // Only the server/host deals damage
            if (Object.HasStateAuthority)
            {
                var setup = health.GetComponent<PlayerSetup>();
                if (setup != null && setup.HasEscaped)
                {
                    _myCollider.enabled = false;
                    return;
                }

                health.DealDamage(damage);
                Debug.Log($"Boss hit {other.name}!");

                // Apply knockback to the player
                var playerMovement = other.GetComponentInParent<PlayerMovement>();
                if (playerMovement != null)
                {
                    Vector3 knockbackDirection = (other.transform.position - _bossRoot.position).normalized;
                    playerMovement.ApplyKnockback(knockbackDirection, knockbackForce, knockbackUpwardBonus);
                }
            }

            // Disable after first hit to prevent multiple damage in one swing
            DisableCollider();
        }
    }

    /// <summary>
    /// Called from BossCombat.Rpc_PlayMeleeAttack (runs on ALL machines)
    /// so the movement penalty applies instantly without waiting for
    /// networked-state replication.
    /// </summary>
    public void SetAttackingLocal(bool attacking)
    {
        _isAttackingLocal = attacking;
    }

    // Animation Events - Called from the Animation timeline
    public void EnableCollider()
    {
        _myCollider.enabled = true;
    }

    public void DisableCollider()
    {
        _myCollider.enabled = false;
        StartMovePenalty();
    }

    public void AttackAnimationEnd()
    {
        // Runs on every machine via animation event
        _isAttackingLocal = false;

        // Networked flag update is authoritative
        if (Object.HasStateAuthority)
        {
            IsAttacking = false;
        }
    }

    // Public methods for external scripts
    public bool CanAttack() => _canAttack;
    public float GetMoveSpeedMultiplier()
    {
        if (_isMovePenaltyLocal && Time.time >= _movePenaltyEndTimeLocal)
        {
            _isMovePenaltyLocal = false;
        }

        bool hasNetworkPenalty = MovePenaltyTimer.IsRunning && !MovePenaltyTimer.Expired(Runner);
        bool hasLocalPenalty = _isMovePenaltyLocal;
        return (hasNetworkPenalty || hasLocalPenalty) ? attackMoveSpeedMultiplier : 1f;
    }

    private void StartMovePenalty()
    {
        _isMovePenaltyLocal = true;
        _movePenaltyEndTimeLocal = Time.time + postAttackPenaltyDuration;

        if (Object.HasStateAuthority)
        {
            MovePenaltyTimer = TickTimer.CreateFromSeconds(Runner, postAttackPenaltyDuration);
        }
    }

    // Optional: Get current cooldown percentage for UI
    public float GetCooldownPercent()
    {
        if (_canAttack) return 0f;

        float remaining = (float)AttackCooldownTimer.RemainingTime(Runner);
        return Mathf.Clamp01(remaining / attackCooldown);
    }
}