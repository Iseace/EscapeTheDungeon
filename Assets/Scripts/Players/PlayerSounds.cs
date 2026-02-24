using UnityEngine;
using Fusion;

[RequireComponent(typeof(AudioSource))]
public class PlayerSounds : NetworkBehaviour
{
    [Header("Walk Sound Settings")]
    [SerializeField] private AudioClip walkClip;
    [SerializeField] [Range(0f, 1f)] private float volume = 1.0f;

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

    public void OnFootstep()
    {
        if (HasStateAuthority)
            RPC_PlayFootstep();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayFootstep()
    {
        if (walkClip == null || _audioSource == null) return;
        _audioSource.Stop();
        _audioSource.PlayOneShot(walkClip, volume);
    }
}