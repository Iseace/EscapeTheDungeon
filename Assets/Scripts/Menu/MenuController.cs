using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Drives the Main Menu UI: Play button, section navigation arrows, 
/// section label text, and per-section actions.
/// Supports both on-screen touch buttons AND keyboard/gamepad input.
/// Attach to the Canvas (or any convenient GameObject in the MainMenu scene).
/// </summary>
public class MenuController : MonoBehaviour
{
    // ── references ─────────────────────────────────────────
    [Header("Camera")]
    [Tooltip("Drag the Main Camera (with MenuCamera component) here.")]
    public MenuCamera menuCamera;

    [Header("Title Screen UI  (hidden after pressing Play)")]
    [Tooltip("The entire title-screen Canvas (hidden when Play is pressed).")]
    public GameObject titleCanvas;

    [Tooltip("The 'Play' button shown on the title screen.")]
    public Button playButton;

    [Header("Section Navigation UI  (hidden on title screen)")]
    [Tooltip("Canvas or panel that contains the arrows + section label.")]
    public GameObject sectionUI;

    [Tooltip("Left-arrow image/button (RawImage).")]
    public Button arrowLeft;
    [Tooltip("Right-arrow image/button (RawImage).")]
    public Button arrowRight;

    [Tooltip("Text (TMP) that shows the current section name.")]
    public TextMeshProUGUI sectionLabel;

    [Tooltip("Optional button over the section label to enter the selected section.")]
    public Button sectionLabelButton;

    [Header("Section Definitions")]
    [Tooltip("One entry per section, in the same order as MenuCamera.sectionPoints.")]
    public SectionInfo[] sections;

    [Header("Optional – Back Button")]
    [Tooltip("Button to return to the title screen (can be null).")]
    public Button backButton;

    [Header("Input Actions (keyboard / gamepad)")]
    [Tooltip("Horizontal navigation: A/D, Left/Right arrows, or left-stick X.")]
    public InputAction navigateAction = new InputAction("Navigate", InputActionType.Value,
        binding: "<Keyboard>/d",
        interactions: "press");

    [Tooltip("Confirm / Play: Enter, Space, or South button.")]
    public InputAction confirmAction = new InputAction("Confirm", InputActionType.Button,
        binding: "<Keyboard>/enter",
        interactions: "press");

    [Tooltip("Back: Escape or East button.")]
    public InputAction backAction = new InputAction("Back", InputActionType.Button,
        binding: "<Keyboard>/escape",
        interactions: "press");

    // track previous horizontal input so we fire once per press
    private float _previousNavInput = 0f;

    // ────────────────────────────────────────────────────────
    void OnEnable()
    {
        ForceCursorVisible();

        // Add extra bindings so A/D, arrows, and gamepad all work
        SetupBindings();

        navigateAction.Enable();
        confirmAction.Enable();
        backAction.Enable();
    }

    void OnDisable()
    {
        navigateAction.Disable();
        confirmAction.Disable();
        backAction.Disable();
    }

    void Start()
    {
        ForceCursorVisible();

        // wire up buttons (touch / click)
        if (playButton != null) playButton.onClick.AddListener(OnPlay);
        if (arrowLeft != null) arrowLeft.onClick.AddListener(OnArrowLeft);
        if (arrowRight != null) arrowRight.onClick.AddListener(OnArrowRight);
        if (backButton != null) backButton.onClick.AddListener(OnBack);
        if (sectionLabelButton != null) sectionLabelButton.onClick.AddListener(OnEnterSection);

        // listen for camera arrival
        if (menuCamera != null)
            menuCamera.OnSectionReached += OnCameraArrived;

        // initial visibility
        ShowTitleScreen();
    }

    void Update()
    {
        ForceCursorVisible();
        HandleKeyboardInput();
    }

    void OnDestroy()
    {
        if (menuCamera != null)
            menuCamera.OnSectionReached -= OnCameraArrived;
    }

    // ── button callbacks ───────────────────────────────────

    private void OnPlay()
    {
        if (menuCamera == null) return;

        menuCamera.EnterSections();

        // hide entire title canvas, show section UI
        if (titleCanvas != null) titleCanvas.SetActive(false);
        if (sectionUI != null) sectionUI.SetActive(true);
        if (backButton != null) backButton.gameObject.SetActive(true);

        UpdateSectionUI(0);
    }

    private void OnArrowLeft()
    {
        if (menuCamera == null || menuCamera.IsMoving) return;
        menuCamera.PreviousSection();
        UpdateSectionUI(menuCamera.CurrentSection);
    }

    private void OnArrowRight()
    {
        if (menuCamera == null || menuCamera.IsMoving) return;
        menuCamera.NextSection();
        UpdateSectionUI(menuCamera.CurrentSection);
    }

    private void OnBack()
    {
        if (menuCamera == null) return;
        menuCamera.ReturnToMenu();
        ShowTitleScreen();
    }

    private void OnEnterSection()
    {
        if (menuCamera == null || menuCamera.IsMoving) return;

        int idx = menuCamera.CurrentSection;
        if (sections == null || idx >= sections.Length) return;

        SectionInfo info = sections[idx];

        switch (info.action)
        {
            case SectionAction.LoadScene:
                if (!string.IsNullOrEmpty(info.sceneName))
                {
                    Debug.Log($"[MenuController] Loading scene: {info.sceneName}");
                    SceneManager.LoadScene(info.sceneName);
                }
                else
                {
                    Debug.LogWarning($"[MenuController] No scene assigned for section '{info.sectionName}'.");
                }
                break;

            case SectionAction.OpenURL:
                if (!string.IsNullOrEmpty(info.url))
                {
                    Debug.Log($"[MenuController] Opening URL: {info.url}");
                    Application.OpenURL(info.url);
                }
                else
                {
                    Debug.LogWarning($"[MenuController] No URL assigned for section '{info.sectionName}'.");
                }
                break;

            case SectionAction.QuitGame:
                Debug.Log("[MenuController] Quitting game.");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                break;
        }
    }

    // ── camera event ───────────────────────────────────────

    private void OnCameraArrived(int sectionIndex)
    {
        UpdateSectionUI(sectionIndex);
    }

    // ── helpers ────────────────────────────────────────────

    private void ShowTitleScreen()
    {
        if (titleCanvas != null) titleCanvas.SetActive(true);
        if (sectionUI != null) sectionUI.SetActive(false);
        if (backButton != null) backButton.gameObject.SetActive(false);
    }

    private void UpdateSectionUI(int index)
    {
        // update label
        if (sectionLabel != null && sections != null && index < sections.Length)
            sectionLabel.text = sections[index].sectionName;

        // arrows always visible (sections loop around)
        if (arrowLeft != null) arrowLeft.gameObject.SetActive(true);
        if (arrowRight != null) arrowRight.gameObject.SetActive(true);
    }

    // ── keyboard / gamepad input ───────────────────────────

    private void HandleKeyboardInput()
    {
        // ── Confirm (Enter / Space / South button) ──
        if (confirmAction.WasPerformedThisFrame())
        {
            if (menuCamera != null && menuCamera.IsAtMenu)
                OnPlay();
            else if (menuCamera != null && !menuCamera.IsAtMenu)
                OnEnterSection();
        }

        // ── Back (Escape / East button) ──
        if (backAction.WasPerformedThisFrame())
        {
            if (menuCamera != null && !menuCamera.IsAtMenu)
                OnBack();
        }

        // ── Horizontal navigation (A/D, arrows, stick) ──
        float navInput = navigateAction.ReadValue<float>();

        // fire once on fresh press (dead-zone at 0.5)
        if (_previousNavInput < 0.5f && _previousNavInput > -0.5f)
        {
            if (navInput >= 0.5f)
                OnArrowRight();
            else if (navInput <= -0.5f)
                OnArrowLeft();
        }

        _previousNavInput = navInput;
    }

    /// <summary>
    /// Adds composite and extra bindings so that A/D, Left/Right arrows
    /// and gamepad left-stick all feed into navigateAction as a –1 / +1 axis.
    /// </summary>
    private void SetupBindings()
    {
        // ── Navigate: 1D Axis from A/D ──
        navigateAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/a")
            .With("Positive", "<Keyboard>/d");

        // ── Navigate: 1D Axis from arrow keys ──
        navigateAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/leftArrow")
            .With("Positive", "<Keyboard>/rightArrow");

        // ── Navigate: Gamepad left stick X ──
        navigateAction.AddBinding("<Gamepad>/leftStick/x");

        // ── Confirm extras ──
        confirmAction.AddBinding("<Keyboard>/space");
        confirmAction.AddBinding("<Gamepad>/buttonSouth");

        // ── Back extras ──
        backAction.AddBinding("<Gamepad>/buttonEast");
    }

    private static void ForceCursorVisible()
    {
        if (Application.isMobilePlatform)
            return;

        if (Cursor.lockState != CursorLockMode.None)
            Cursor.lockState = CursorLockMode.None;

        if (!Cursor.visible)
            Cursor.visible = true;
    }
}

/// <summary>
/// What happens when a section is selected.
/// </summary>
public enum SectionAction
{
    LoadScene,
    OpenURL,
    QuitGame
}

/// <summary>
/// Serialisable data for one menu section.
/// </summary>
[System.Serializable]
public class SectionInfo
{
    [Tooltip("Display name shown on the section label.")]
    public string sectionName;

    [Tooltip("What happens when the player enters this section.")]
    public SectionAction action = SectionAction.LoadScene;

    [Tooltip("Scene to load (used when action = LoadScene).")]
    public string sceneName;

    [Tooltip("URL to open in the browser (used when action = OpenURL).")]
    public string url;
}