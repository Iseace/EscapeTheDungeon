using Fusion;
using UnityEngine;

public class KartHazardController : NetworkBehaviour
{
    private const int SocketCount = 20;

    [Header("Hazard Sockets")]
    [SerializeField] private Transform[] spawnSockets = new Transform[SocketCount];

    [Header("Hazard Prefabs")]
    [SerializeField] private NetworkPrefabRef[] hazardPrefabs;

    [Header("Startup")]
    [SerializeField] private bool triggerOnSceneStart = true;

    [Networked]
    private NetworkBool HasTriggeredStartupEvent { get; set; }

    public override void Spawned()
    {
        if (!HasStateAuthority || !triggerOnSceneStart || HasTriggeredStartupEvent)
        {
            return;
        }

        HasTriggeredStartupEvent = true;
        TriggerHazardEvent();
    }

    public void TriggerHazardEvent()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        if (hazardPrefabs == null || hazardPrefabs.Length == 0)
        {
            Debug.LogWarning("Hazard event was triggered, but no hazard prefabs were assigned.", this);
            return;
        }

        for (int i = 0; i < SocketCount; i++)
        {
            if (spawnSockets == null || i >= spawnSockets.Length)
            {
                continue;
            }

            Transform socket = spawnSockets[i];
            if (socket == null)
            {
                continue;
            }

            if (Random.value <= 0.5f)
            {
                NetworkPrefabRef selectedHazard = hazardPrefabs[Random.Range(0, hazardPrefabs.Length)];
                Runner.Spawn(selectedHazard, socket.position, socket.rotation);
            }
        }
    }

    private void OnValidate()
    {
        if (spawnSockets == null || spawnSockets.Length != SocketCount)
        {
            Transform[] resized = new Transform[SocketCount];

            if (spawnSockets != null)
            {
                int copyLength = Mathf.Min(spawnSockets.Length, SocketCount);
                for (int i = 0; i < copyLength; i++)
                {
                    resized[i] = spawnSockets[i];
                }
            }

            spawnSockets = resized;
        }
    }
}
