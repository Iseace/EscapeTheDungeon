using UnityEngine;

/// <summary>
/// Place this on the Graphics child (same GameObject as the Animator).
/// </summary>
public class AnimationEventReceiver : MonoBehaviour
{
    private AnimatorBasic _combat;
    private PlayerSounds _sounds;

    private void Awake()
    {
        _combat = GetComponentInParent<AnimatorBasic>();
        _sounds = GetComponentInParent<PlayerSounds>();
    }

    public void Hit()
    {
        if (_combat != null)
            _combat.OnAttackHit();
    }

    public void OnWandAttack()
    {
        if (_sounds != null)
            _sounds.OnWandAttack();
    }
}