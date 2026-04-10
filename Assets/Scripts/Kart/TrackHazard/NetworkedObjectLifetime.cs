using Fusion;
using UnityEngine;

public class NetworkedObjectLifetime : NetworkBehaviour
{
    [SerializeField] private float lifetime = 3f;
    [Networked] private TickTimer lifeTimer { get; set; }

    public override void Spawned()
    {
        // Only the StateAuthority needs to manage timers for destruction
        if (Object.HasStateAuthority)
        {
            lifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Check if the timer is expired on the authoritative instance
        if (Object.HasStateAuthority && lifeTimer.Expired(Runner))
        {
            // Despawn the object so it is removed for everyone
            Runner.Despawn(Object);
        }
    }
}
