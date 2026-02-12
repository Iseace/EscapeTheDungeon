using UnityEngine;

public class AnimationBridge : MonoBehaviour
{
    // Arrastra aquí el objeto de la espada que tiene el script BossHitbox
    [SerializeField] private BossHitbox hitbox;

    // Estos nombres DEBEN coincidir con los de tu error en la consola
    public void EnableCollider()
    {
        if (hitbox != null) hitbox.EnableCollider();
    }

    public void DisableCollider()
    {
        if (hitbox != null) hitbox.DisableCollider();
    }

    public void AttackAnimationEnd()
    {
    }
}