using UnityEngine;
using Fusion;

[RequireComponent(typeof(AudioSource))]
public class BossSounds : NetworkBehaviour
{
    [Header("Walk Sound Settings")]
    [SerializeField] private AudioClip walkClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 1.0f;

    [Header("Attack Sound Settings")]
    [SerializeField] private AudioClip attackClip;
    [SerializeField] [Range(0f, 1f)] private float attackVolume = 1.0f;

    private AudioSource _audioSource;

    public override void Spawned()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.clip = walkClip;
        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
        _audioSource.volume = volume;
        _audioSource.spatialBlend = 1f;
    }

    // Called by BossAnimationEvents on footstep frame
    public void OnFootstep()
    {
        if (HasStateAuthority)
            RPC_PlayFootstep();
    }

    // Called by BossAnimationEvents on attack frame
    public void OnAttack()
    {
        if (HasStateAuthority)
            RPC_PlayAttack();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFootstep()
    {
        if (walkClip == null || _audioSource == null) return;
        _audioSource.Stop();
        _audioSource.PlayOneShot(walkClip, volume);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAttack()
    {
        if (attackClip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(attackClip, attackVolume);
    }
}