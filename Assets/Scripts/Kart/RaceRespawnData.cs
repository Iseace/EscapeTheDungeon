using UnityEngine;
using Fusion;

public class RaceRespawnData : NetworkBehaviour
{
    [SerializeField] private float killDepth = -6f;
    [SerializeField] private bool enableDebugLogs = true;

    private const float RespawnCooldown = 2f;
    private float _lastRespawnTime = -999f;

    public int LastCheckpointIndex { get; private set; } = -1;

    private Vector3 _checkpointPosition;
    private Quaternion _checkpointRotation;
    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private LapTracker _lapTracker;

    public override void Spawned()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation;

        _lapTracker = FindAnyObjectByType<LapTracker>();
        if (_lapTracker != null)
            _lapTracker.OnLapCompleted += OnLapCompleted;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_lapTracker != null)
            _lapTracker.OnLapCompleted -= OnLapCompleted;
    }

    private void OnLapCompleted(BroomMove racer, int lap)
    {
        if (racer == GetComponent<BroomMove>())
            ResetForNewLap();
    }

    private void ResetForNewLap()
    {
        LastCheckpointIndex = -1;

        if (enableDebugLogs)
            Debug.Log($"[RESPAWN] {name} started new lap, checkpoint index reset.");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer) return;

        if (transform.position.y < killDepth)
        {
            if (Time.time - _lastRespawnTime < RespawnCooldown) return;

            _lastRespawnTime = Time.time;

            if (enableDebugLogs)
                Debug.Log($"[RESPAWN] {name} fell below {killDepth}. Respawning at checkpoint {LastCheckpointIndex}.");

            Respawn();
        }
    }

    public void SaveCheckpoint(Vector3 position, Quaternion rotation, int index)
    {
        if (index <= LastCheckpointIndex) return;

        _checkpointPosition = position;
        _checkpointRotation = rotation;
        LastCheckpointIndex = index;

        if (enableDebugLogs)
            Debug.Log($"[RESPAWN] {name} saved checkpoint {index} at {position}.");
    }

    public void Respawn()
    {
        Vector3 targetPos;
        Quaternion targetRot;

        if (LastCheckpointIndex >= 0)
        {
            targetPos = _checkpointPosition;
            targetRot = _checkpointRotation;
        }
        else
        {
            targetPos = _startPosition;
            targetRot = _startRotation;
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}