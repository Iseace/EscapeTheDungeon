using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    private PlayerSounds _playerSounds;

    private void Awake()
    {
        _playerSounds = GetComponentInParent<PlayerSounds>();
    }

    public void OnFootstep()
    {
        if (_playerSounds != null)
            _playerSounds.OnFootstep();
    }

    public void OnWandAttack()
    {
        if (_playerSounds != null)
            _playerSounds.OnWandAttack();
    }
}