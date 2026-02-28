using UnityEngine;
using Fusion;

[RequireComponent(typeof(AudioSource))]
public class PlayerSounds : NetworkBehaviour
{
    [Header("Walk Sound Settings")]
    [SerializeField] private AudioClip walkClip;
    [SerializeField] [Range(0f, 1f)] private float walkVolume = 1.0f;

    [Header("Wand Attack Sounds")]
    [SerializeField] private AudioClip fireWandAttackClip;
    [SerializeField] private AudioClip waterWandAttackClip;
    [SerializeField] private AudioClip earthWandAttackClip;
    [SerializeField] [Range(0f, 1f)] private float attackVolume = 1.0f;

    private AudioSource _audioSource;
    private PlayerInventory _inventory;

    public override void Spawned()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = walkClip;
        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
        _audioSource.volume = walkVolume;
        _audioSource.spatialBlend = 1f;

        _inventory = GetComponent<PlayerInventory>();
    }

    public void OnFootstep()
    {
        if (HasStateAuthority)
            RPC_PlayFootstep();
    }

    public void OnWandAttack()
    {
        if (!HasStateAuthority) return;
        if (_inventory == null) return;

        int weaponID = _inventory.CurrentWeaponID;
        RPC_PlayWandAttack(weaponID);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFootstep()
    {
        if (walkClip == null || _audioSource == null) return;
        _audioSource.Stop();
        _audioSource.PlayOneShot(walkClip, walkVolume);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayWandAttack(int weaponID)
    {
        if (_audioSource == null) return;

        AudioClip clipToPlay = weaponID switch
        {
            1 => fireWandAttackClip,
            2 => earthWandAttackClip,
            3 => waterWandAttackClip,
            _ => null
        };

        if (clipToPlay == null) return;
        _audioSource.PlayOneShot(clipToPlay, attackVolume);
    }
}