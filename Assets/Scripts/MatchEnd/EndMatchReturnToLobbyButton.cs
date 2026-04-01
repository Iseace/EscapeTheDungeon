using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndMatchReturnToLobbyButton : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string lobbyListSceneName = "LobbyList";

    [Header("Button")]
    [SerializeField] private Button returnButton;
    [SerializeField] private bool wireOnStart = true;

    [Header("Network")]
    [SerializeField] private bool shutdownRunnerBeforeLoad = true;
    [SerializeField] private float loadDelaySecondsAfterShutdown = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    private bool isLeaving;

    private void Start()
    {
        if (returnButton == null)
            returnButton = GetComponent<Button>();

        if (!wireOnStart)
            return;

        if (returnButton == null)
        {
            if (debugLogs)
                Debug.LogWarning("[EndMatchReturnToLobbyButton] No Button asignado.");
            return;
        }

        returnButton.onClick.RemoveListener(OnReturnClicked);
        returnButton.onClick.AddListener(OnReturnClicked);
    }

    private void OnDestroy()
    {
        if (returnButton != null)
            returnButton.onClick.RemoveListener(OnReturnClicked);
    }

    public void OnReturnClicked()
    {
        if (isLeaving)
            return;

        StartCoroutine(ReturnFlow());
    }

    [ContextMenu("Return To LobbyList")]
    public void ReturnToLobbyFromContextMenu()
    {
        OnReturnClicked();
    }

    private IEnumerator ReturnFlow()
    {
        isLeaving = true;

        if (returnButton != null)
            returnButton.interactable = false;

        if (shutdownRunnerBeforeLoad)
            yield return ShutdownAllRunners();

        if (loadDelaySecondsAfterShutdown > 0f)
            yield return new WaitForSeconds(loadDelaySecondsAfterShutdown);

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
