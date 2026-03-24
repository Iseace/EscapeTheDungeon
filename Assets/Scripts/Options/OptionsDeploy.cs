using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OptionsDeploy : MonoBehaviour
{
    public static bool IsAnyOptionsMenuOpen { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button exitButton;

    [Header("Input")]
    [SerializeField] private InputActionReference optionsAction;

    [Header("Flow")]
    [SerializeField] private string mainMenuSceneName = "LobbyList";

    private bool _isMenuOpen;
    private bool _isExiting;
    private CursorLockMode _previousCursorLockMode;
    private bool _previousCursorVisible;

    private void Awake()
    {
        if (optionsPanel == null)
        {
            Transform panel = transform.Find("Panel");
            if (panel != null)
                optionsPanel = panel.gameObject;
        }

        if (resumeButton == null)
        {
            Transform resume = transform.Find("Resume");
            if (resume != null)
                resumeButton = resume.GetComponent<Button>();
        }

        if (exitButton == null)
        {
            Transform quit = transform.Find("Quit");
            if (quit != null)
                exitButton = quit.GetComponent<Button>();
        }
    }

    private void Start()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (exitButton != null)
            exitButton.onClick.AddListener(ExitToMainMenu);

        SetMenuOpen(false);
    }

    private void OnEnable()
    {
        if (optionsAction != null)
        {
            optionsAction.action.Enable();
            optionsAction.action.performed += OnOptionsPressed;
        }
    }

    private void OnDisable()
    {
        if (optionsAction != null)
        {
            optionsAction.action.performed -= OnOptionsPressed;
            optionsAction.action.Disable();
        }

        if (_isMenuOpen)
            SetMenuOpen(false);
    }

    private void OnDestroy()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeGame);

        if (exitButton != null)
            exitButton.onClick.RemoveListener(ExitToMainMenu);
    }

    private void OnOptionsPressed(InputAction.CallbackContext context)
    {
        if (_isExiting)
            return;

        SetMenuOpen(!_isMenuOpen);
    }

    public void ResumeGame()
    {
        if (_isExiting)
            return;

        SetMenuOpen(false);
    }

    public async void ExitToMainMenu()
    {
        if (_isExiting)
            return;

        _isExiting = true;

        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null && runner.IsRunning)
        {
            await runner.Shutdown();
            await Task.Yield();
        }

        int sceneIndex = ResolveSceneIndexByName(mainMenuSceneName);
        if (sceneIndex < 0)
        {
            Debug.LogError($"[OptionsDeploy] Scene '{mainMenuSceneName}' was not found in Build Settings.");
            _isExiting = false;
            return;
        }

        SetMenuOpen(false);
        SceneManager.LoadScene(sceneIndex);
    }

    private void SetMenuOpen(bool open)
    {
        _isMenuOpen = open;
        IsAnyOptionsMenuOpen = open;

        if (optionsPanel != null)
            optionsPanel.SetActive(open);

        if (open)
        {
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = _previousCursorLockMode;
            Cursor.visible = _previousCursorVisible;
        }
    }

    private static int ResolveSceneIndexByName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return -1;

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (string.IsNullOrEmpty(path))
                continue;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (string.Equals(fileName, sceneName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
