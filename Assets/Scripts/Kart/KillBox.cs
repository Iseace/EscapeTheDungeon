using UnityEngine;
using Fusion;

public class KillBox : SimulationBehaviour
{
    [SerializeField] private float killDepth = -10f;
    [SerializeField] private bool enableDebugLogs = true;

    // Short cooldown so a respawning player doesn't immediately trigger again
    private const float RespawnCooldown = 2f;
    private float _lastRespawnTime = -999f;

    public override void FixedUpdateNetwork()
    {
        // Only the server authorizes respawns
        if (!Runner.IsServer) return;

        if (transform.position.y < killDepth)
        {
            if (Time.time - _lastRespawnTime < RespawnCooldown) return;

            RaceRespawnData respawn = GetComponent<RaceRespawnData>();
            if (respawn == null) return;

            _lastRespawnTime = Time.time;

            if (enableDebugLogs)
                Debug.Log($"[KILLBOX] {name} fell below {killDepth}. Respawning at checkpoint {respawn.LastCheckpointIndex}.");

            respawn.Respawn();
        }
    }
}