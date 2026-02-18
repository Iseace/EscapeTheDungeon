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

    [Tooltip("Delay in seconds before the projectile spawns (match your animation Hit frame)")]
    [SerializeField] private float spawnDelay = 0.4f;

    // Networked state
    [Networked] private TickTimer _attackTimer { get; set; }
    [Networked] private int _savedWeaponID { get; set; }

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _animator = GetComponentInChildren<Animator>();
    }

    public override void FixedUpdateNetwork()
    {
        // 1. Check if the delayed spawn timer just expired
        if (_attackTimer.Expired(Runner))
        {
            _attackTimer = TickTimer.None;
            SpawnProjectile();
        }

        // 2. Check for new attack input
        if (GetInput(out PlayerInputData data) && data.AttackPressed)
        {
            if (_inventory != null && _inventory.CurrentWeaponID > 0)
            {
                _savedWeaponID = _inventory.CurrentWeaponID;
                _attackTimer = TickTimer.CreateFromSeconds(Runner, spawnDelay);
                
                if (HasStateAuthority) 
                  Rpc_PlayAttack();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void Rpc_PlayAttack()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        if (_animator != null)
            _animator.SetTrigger("Attack");
    }

    private void SpawnProjectile()
    {
        if (!HasStateAuthority) return;
        if (_savedWeaponID <= 0) return;

        int index = _savedWeaponID - 1;
        if (index < 0 || index >= projectilePrefabs.Length) return;

        // Use the SpellSocket's live world position and rotation
        Vector3 spawnPos = spellSocket != null ? spellSocket.position : transform.position + Vector3.up * 1.2f;
        Quaternion spawnRot = spellSocket != null ? spellSocket.rotation : transform.rotation;

        Runner.Spawn(projectilePrefabs[index], spawnPos, spawnRot, Object.InputAuthority);
    }

    /// <summary>
    /// Still called by AnimationEventReceiver to suppress the "no receiver" warning.
    /// </summary>
    public void OnAttackHit() { }
}
