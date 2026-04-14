using System.Collections;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
public class EndMatchReturnToLobbyButton : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string lobbyListSceneName = "LobbyList";

    [Header("Countdown")]
    [SerializeField] private float countdownSeconds = 10f;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private string countdownPrefix = "leaving in";
    [SerializeField] private string countdownSuffix = "s";
    [SerializeField] private string leavingText = "leaving...";

    [Header("Cinematic Sync")]
    [SerializeField] private bool startCountdownAfterTimeline = true;
    [Tooltip("Opcional: si esta vacio, se usan todos los PlayableDirector activos en la escena.")]
    [SerializeField] private PlayableDirector[] directorsToWatch;
    [SerializeField] private float directorStartGraceSeconds = 2f;
    [SerializeField] private float maxWaitForTimelineSeconds = 90f;
    [SerializeField] private float extraDelayAfterTimelineSeconds = 0f;

    [Header("Flow")]
    [SerializeField] private float loadDelaySecondsAfterShutdown = 0.05f;
    [SerializeField] private bool debugLogs = true;

    private bool isLeaving;

    private void Start()
    {
        if (debugLogs)
            Debug.Log($"[EndMatchReturnToLobbyButton] Countdown start: seconds={countdownSeconds:0.##}, scene={SceneManager.GetActiveScene().name}");

        StartCoroutine(CountdownThenLeaveFlow());
    }

    private IEnumerator CountdownThenLeaveFlow()
    {
        if (startCountdownAfterTimeline)
            yield return WaitForTimelineToFinish();

        int remaining = Mathf.Max(1, Mathf.CeilToInt(countdownSeconds));

        while (remaining > 0 && !isLeaving)
        {
            UpdateCountdownText(remaining);

            if (debugLogs)
                Debug.Log($"[EndMatchReturnToLobbyButton] Countdown tick: {remaining}s");

            yield return new WaitForSecondsRealtime(1f);
            remaining--;
        }

        if (isLeaving)
            yield break;

        if (debugLogs)
            Debug.Log("[EndMatchReturnToLobbyButton] Countdown completed. Starting return flow.");

        yield return ReturnFlow();
    }

    private IEnumerator WaitForTimelineToFinish()
    {
        PlayableDirector[] directors = GetDirectorsToObserve();
        if (directors == null || directors.Length == 0)
        {
            if (debugLogs)
                Debug.Log("[EndMatchReturnToLobbyButton] No hay PlayableDirector para observar. El countdown iniciara inmediatamente.");
            yield break;
        }

        float maxWait = Mathf.Max(0f, maxWaitForTimelineSeconds);
        float grace = Mathf.Max(0f, directorStartGraceSeconds);
        float elapsed = 0f;
        bool anyDirectorPlayed = false;

        while (true)
        {
            bool anyPlaying = IsAnyDirectorPlaying(directors);
            if (anyPlaying)
            {
                anyDirectorPlayed = true;
            }
            else
            {
                if (anyDirectorPlayed)
                    break;

                if (elapsed >= grace)
                    break;
            }

            if (maxWait > 0f && elapsed >= maxWait)
            {
                if (debugLogs)
                    Debug.LogWarning("[EndMatchReturnToLobbyButton] Timeout esperando timeline. Se inicia countdown por seguridad.");
                break;
            }

            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        if (extraDelayAfterTimelineSeconds > 0f)
            yield return new WaitForSecondsRealtime(extraDelayAfterTimelineSeconds);

        if (debugLogs)
            Debug.Log("[EndMatchReturnToLobbyButton] Timeline finalizado. Iniciando countdown.");
    }

    private PlayableDirector[] GetDirectorsToObserve()
    {
        if (directorsToWatch != null && directorsToWatch.Length > 0)
            return directorsToWatch;

        return FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private static bool IsAnyDirectorPlaying(PlayableDirector[] directors)
    {
        if (directors == null || directors.Length == 0)
            return false;

        for (int i = 0; i < directors.Length; i++)
        {
            PlayableDirector director = directors[i];
            if (director == null)
                continue;

            if (director.state == PlayState.Playing)
                return true;
        }

        return false;
    }

    private IEnumerator ReturnFlow()
    {
        isLeaving = true;
        WriteLeavingText();

        yield return ShutdownAllRunners();

        if (loadDelaySecondsAfterShutdown > 0f)
            yield return new WaitForSecondsRealtime(loadDelaySecondsAfterShutdown);

        int index = ResolveSceneIndex(lobbyListSceneName);
        if (index >= 0)
        {
            if (debugLogs)
                Debug.Log($"[EndMatchReturnToLobbyButton] Loading scene index={index} ({lobbyListSceneName}).");
            SceneManager.LoadScene(index);
        }
        else
        {
            if (debugLogs)
                Debug.LogWarning($"[EndMatchReturnToLobbyButton] Scene index not found for '{lobbyListSceneName}'. Trying by name.");
            SceneManager.LoadScene(lobbyListSceneName);
        }
    }

    private void UpdateCountdownText(int remainingSeconds)
    {
        if (countdownText == null)
            return;

        countdownText.text = string.Format("{0} {1}{2}", countdownPrefix, Mathf.Max(0, remainingSeconds), countdownSuffix);
    }

    private void WriteLeavingText()
    {
        if (countdownText == null)
            return;

        countdownText.text = leavingText;
    }

    private IEnumerator ShutdownAllRunners()
    {
        NetworkRunner[] runners = FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (runners == null || runners.Length == 0)
            yield break;

        for (int i = 0; i < runners.Length; i++)
        {
            NetworkRunner runner = runners[i];
            if (runner == null || !runner.IsRunning)
                continue;

            if (debugLogs)
                Debug.Log($"[EndMatchReturnToLobbyButton] Shutting down runner '{runner.name}'.");

            var task = runner.Shutdown();
            while (!task.IsCompleted)
                yield return null;

            if (task.IsFaulted && debugLogs)
                Debug.LogError($"[EndMatchReturnToLobbyButton] Runner shutdown failed: {task.Exception}");
        }
    }

    private static int ResolveSceneIndex(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return -1;

        int index = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + sceneName + ".unity");
        if (index >= 0)
            return index;

        index = SceneUtility.GetBuildIndexByScenePath("Scenes/" + sceneName);
        if (index >= 0)
            return index;

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, sceneName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
