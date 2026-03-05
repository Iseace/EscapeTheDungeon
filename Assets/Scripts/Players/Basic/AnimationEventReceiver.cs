using UnityEngine;

/// <summary>
/// Place this on the Graphics child (same GameObject as the Animator).
/// </summary>
public class AnimationEventReceiver : MonoBehaviour
{
    private AnimatorBasic _combat;

    private void Awake()
    {
        _combat = GetComponentInParent<AnimatorBasic>();
    }
    public void Hit()
    {
        if (_combat != null)
            _combat.OnAttackHit();
    }
}