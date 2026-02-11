using UnityEngine;

/// <summary>
/// Place this on the Graphics child (same GameObject as the Animator).
/// It receives AnimationEvents from clips and forwards them to AnimatorBasic on the parent.
/// </summary>
public class AnimationEventReceiver : MonoBehaviour
{
    private AnimatorBasic _combat;

    private void Awake()
    {
        _combat = GetComponentInParent<AnimatorBasic>();
    }

    // Called by the "Hit" AnimationEvent baked into Attack1
    public void Hit()
    {
        if (_combat != null)
            _combat.OnAttackHit();
    }
}
