using UnityEngine;
using Fusion;

public class BossChaseTheme : NetworkBehaviour
{
    [Header("Chase Music")]
    [SerializeField] private AudioClip chaseClip;

    [SerializeField] [Range(0f, 1f)] private float maxVolume = 0.8f;

    [Header("3D Spatial Range")]
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 10f;

    [Header("Audio Source")]
    [SerializeField] private AudioSource _musicSource;

    public override void Spawned()
    {
        if (_musicSource == null)
        {
            Debug.LogError("[BossChaseTheme] No AudioSource assigned in the Inspector! " +
                           "Please drag a dedicated AudioSource into the Music Source slot.");
            return;
        }

        // Configure as a 3D spatial looping music source
        _musicSource.clip = chaseClip;
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 1f;      // Full 3D — survivors hear it positionally
        _musicSource.volume = maxVolume;
        _musicSource.minDistance = minDistance;
        _musicSource.maxDistance = maxDistance;
        _musicSource.rolloffMode = AudioRolloffMode.Linear; // Smooth linear fade with distance

        // Only the boss player mutes it — survivors always hear it
        if (HasInputAuthority)
        {
            _musicSource.mute = true;
        }

        _musicSource.Play();
    }

    public void StopChaseTheme()
    {
        if (_musicSource != null)
            _musicSource.Stop();
    }

    public void SetMuted(bool muted)
    {
        if (_musicSource == null) return;

        // Never unmute for the boss player
        if (HasInputAuthority) return;

        _musicSource.mute = muted;
    }
}