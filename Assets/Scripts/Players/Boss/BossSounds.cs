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

    [Header("Invisibility Sound Settings")]
    [SerializeField] private AudioClip invisibilityClip;
    [SerializeField] [Range(0f, 1f)] private float invisibilityVolume = 1.0f;

    [Header("Glow Sound Settings")]
    [SerializeField] private AudioClip glowClip;
    [SerializeField] [Range(0f, 1f)] private float glowVolume = 1.0f;

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

    // Called by BossSpecial when invisibility actually toggles (StateAuthority side)
    public void OnInvisibilityToggle()
    {
        if (HasStateAuthority)
            RPC_PlayInvisibility();
    }

    // Called by GlowPlayer when glow actually activates (InputAuthority side)
    public void OnGlowActivate()
    {
        if (HasInputAuthority)
            RPC_RequestGlowSound();
    }

    // ----- RPCs -----

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

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayInvisibility()
    {
        if (invisibilityClip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(invisibilityClip, invisibilityVolume);
    }

    // InputAuthority asks StateAuthority to broadcast the glow sound to all clients
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestGlowSound()
    {
        RPC_PlayGlow();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayGlow()
    {
        if (glowClip == null || _audioSource == null) return;
        _audioSource.PlayOneShot(glowClip, glowVolume);
    }
}