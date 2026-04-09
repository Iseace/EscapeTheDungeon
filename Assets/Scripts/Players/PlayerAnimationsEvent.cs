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

    public void OnLanding()
    {
        if (_playerSounds != null)
            _playerSounds.OnLanding();
    }

    // ADDED: For spell spawning from AnimatorBasic
    public void Hit()
    {
        var combat = GetComponentInParent<AnimatorBasic>();
        if (combat != null)
            combat.OnAttackHit();
    }
}