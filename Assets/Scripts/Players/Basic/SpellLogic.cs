using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class SpellLogic : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 15f;

    [Header("Lifetime")]
    [Tooltip("Auto-destroy after this many seconds if it hits nothing")]
    [SerializeField] private float maxLifetime = 5f;

    [Header("Damage")]
    [SerializeField] private float damage = 20f;

    [Networked] private TickTimer _lifeTimer { get; set; }

    public override void Spawned()
    {
        _lifeTimer = TickTimer.CreateFromSeconds(Runner, maxLifetime);
    }

    public override void FixedUpdateNetwork()
    {
        // Move forward every network tick
        transform.position += transform.forward * speed * Runner.DeltaTime;

        // Auto-despawn after lifetime expires
        if (_lifeTimer.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!HasStateAuthority) return;

        // Ignore other projectiles
        if (other.GetComponent<SpellLogic>() != null) return;

        // Deal damage if the target has a PlayerHealth component
        var health = other.GetComponentInParent<PlayerHealth>();
        if (health != null)
        {
            var setup = health.GetComponent<PlayerSetup>();
            if (setup != null && setup.HasEscapedSafe)
            {
                Runner.Despawn(Object);
                return;
            }

            health.DealDamage(damage);
        }

        Runner.Despawn(Object);
    }
}
