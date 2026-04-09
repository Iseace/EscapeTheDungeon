using Fusion;
using UnityEngine;

public class AnimatorBasic : NetworkBehaviour
{
    private PlayerInventory _inventory;
    private Animator _animator;

    [Header("Projectile prefabs (one per staff, index 0 = Staff1, etc.)")]
    [SerializeField] private NetworkPrefabRef[] projectilePrefabs;

    [Header("Spawn settings")]
    [Tooltip("Empty GameObject on the player where projectiles spawn from")]
    [SerializeField] private Transform spellSocket;

    [Header("Cooldown")]
    [Tooltip("Minimum seconds between attacks")]
    [SerializeField] private float attackCooldown = 0.5f;

    [Networked] private int _savedWeaponID { get; set; }
    [Networked] private TickTimer _attackCooldownTimer { get; set; }

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
    }

    public override void Spawned()
    {
        RefreshAnimator();
    }

    private void RefreshAnimator()
    {
        if (Object == null || !Object.IsValid) return;

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerInputData data) && data.AttackPressed)
        {
            // Skip if still on cooldown
            if (!_attackCooldownTimer.ExpiredOrNotRunning(Runner)) return;

            if (_inventory != null && _inventory.CurrentWeaponID > 0)
            {
                _savedWeaponID = _inventory.CurrentWeaponID;
                _attackCooldownTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);

                // Only send the RPC on the forward tick, never during resimulation
                if (HasStateAuthority && Runner.IsForward)
                    Rpc_PlayAttack();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayAttack()
    {
        RefreshAnimator();

        if (_animator != null)
        {
            // Reset first so triggers never pile up
            _animator.ResetTrigger("Attack");
            _animator.SetTrigger("Attack");
        }
    }

    public void OnAttackHit()
    {
        SpawnProjectile();
    }

    private void SpawnProjectile()
    {
        if (!HasStateAuthority) return;
        if (_savedWeaponID <= 0) return;

        int index = _savedWeaponID - 1;
        if (index < 0 || index >= projectilePrefabs.Length) return;

        Vector3 spawnPos = spellSocket != null ? spellSocket.position : transform.position + Vector3.up * 1.2f;
        Quaternion spawnRot = spellSocket != null ? spellSocket.rotation : transform.rotation;

        Runner.Spawn(projectilePrefabs[index], spawnPos, spawnRot, Object.InputAuthority);
    }
}