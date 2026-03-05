using UnityEngine;

public class BossAnimationEvents : MonoBehaviour
{
    private BossSounds _bossSounds;

    private void Awake()
    {
        _bossSounds = GetComponentInParent<BossSounds>();
    }

    public void OnFootstep()
    {
        if (_bossSounds != null)
            _bossSounds.OnFootstep();
    }

    public void OnAttack()
    {
        if (_bossSounds != null)
            _bossSounds.OnAttack();
    }
}