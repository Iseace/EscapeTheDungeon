using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndMatchRoomSafetyTimer : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private bool enableSafetyShutdown = true;
    [SerializeField] private float safetyShutdownDelaySeconds = 60f;
    [SerializeField] private bool onlyWhenInEndMatchScene = true;
    [SerializeField] private string endMatchSceneName = "EndMatch";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool started;

    private void Start()
    {
        if (!enableSafetyShutdown) return;
        if (started) return;

        if (onlyWhenInEndMatchScene)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (!string.Equals(scene, endMatchSceneName, System.StringComparison.OrdinalIgnoreCase))
                return;
        }

        started = true;
        _ = RunSafetyShutdownAsync();
    }

    private async Task RunSafetyShutdownAsync()
    {
        float delay = Mathf.Max(5f, safetyShutdownDelaySeconds);
        if (debugLogs)
            Debug.Log($"[EndMatchRoomSafetyTimer] Safety shutdown armed: {delay:0}s");

        await Task.Delay(Mathf.CeilToInt(delay * 1000f));

        NetworkRunner[] runners = FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (runners == null || runners.Length == 0)
            return;

        for (int i = 0; i < runners.Length; i++)
        {
            NetworkRunner runner = runners[i];
            if (runner == null || !runner.IsRunning)
                continue;

            // Only server/host can keep the room alive; clients are harmless for room lifecycle.
            if (!runner.IsServer)
                continue;

            if (debugLogs)
                Debug.Log($"[EndMatchRoomSafetyTimer] Safety shutdown executing on host runner '{runner.name}'.");

            try
            {
                await runner.Shutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[EndMatchRoomSafetyTimer] Shutdown failed: {e.Message}");
            }
        }
    }
}
