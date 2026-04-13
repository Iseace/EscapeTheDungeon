using UnityEngine;
using UnityEngine.UI;
using Fusion;
using Fusion.Sockets;
using TMPro;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField hostRoomInput;
    [SerializeField] private Button hostBtn;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button raceBtn;
    [SerializeField] private TMP_InputField raceRoomInput;

    [Header("Network Settings")]
    [SerializeField] private string lobbySceneName = "LobbyRoom";
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string raceSceneName = "Race";
    [SerializeField] private string lobbyListSceneName = "LobbyList";
    [SerializeField] private int maxPlayers = 5;
    [SerializeField] private bool autoReturnToLobbyListOnDisconnect = true;
    [SerializeField] private bool stayInEndMatchAfterDisconnect = true;

    [Header("Session List")]
    [SerializeField] private LobbyListManager LobbyListManager;

    // NEW: session type constants — LobbyUIHandler reads these to decide which scene to load
    public const string SESSION_TYPE_KEY    = "sessionType";
    public const string SESSION_TYPE_NORMAL = "normal";
    public const string SESSION_TYPE_RACE   = "race";

    private NetworkRunner _runner;
    private bool _isReturningToLobbyList;

    // Cached reference to the local player's health so OnInput can check IsDead.
    // Looked up lazily the first time it is needed — avoids a Find() call every frame.
    private PlayerHealth _localHealth;

    private void Start()
    {
        hostBtn.onClick.AddListener(OnHostRoom);

        if (raceBtn != null)
            raceBtn.onClick.AddListener(OnRaceButton);

        if (nicknameInput != null)
        {
            nicknameInput.text = PlayerPrefs.GetString("Nickname", "");
            // Save nickname immediately when player finishes typing so it's always persisted
            nicknameInput.onEndEdit.AddListener(OnNicknameEndEdit);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnJoinLobby();
    }

    // Saves nickname to disk the moment the player finishes typing
    private void OnNicknameEndEdit(string value)
    {
        string nick = value.Trim();
        if (!string.IsNullOrEmpty(nick))
        {
            PlayerPrefs.SetString("Nickname", nick);
            PlayerPrefs.Save(); // Force immediate write to disk
        }
    }

    private void SaveNickname()
    {
        if (nicknameInput == null) return;
        string nick = nicknameInput.text.Trim();
        if (!string.IsNullOrEmpty(nick))
        {
            PlayerPrefs.SetString("Nickname", nick);
            PlayerPrefs.Save(); // Force immediate write to disk
        }
    }

    public async void OnJoinLobby()
    {
        Debug.Log("[NETWORK] Joining lobby to fetch sessions...");

        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this);
        }

        var result = await _runner.JoinSessionLobby(SessionLobby.ClientServer);

        if (result.Ok)
        {
            Debug.Log("[NETWORK] Successfully joined lobby");
        }
        else
        {
            Debug.LogError($"[NETWORK] Failed to join lobby: {result.ShutdownReason}");
            Debug.LogWarning("[NETWORK] Session list will not be available. You can still create rooms.");
        }
    }

    private async void OnHostRoom()
    {
        string roomName = hostRoomInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
            return;

        SaveNickname();

        // If runner is running, shutdown first
        if (_runner != null && _runner.IsRunning)
        {
            await _runner.Shutdown();
        }

        // Check if there's already a NetworkRunner on this GameObject and remove it
        var existingRunners = GetComponents<NetworkRunner>();
        foreach (var runner in existingRunners)
        {
            DestroyImmediate(runner);
        }

        _runner = null;

        // Wait one frame to ensure cleanup
        await System.Threading.Tasks.Task.Yield();

        await StartGame(GameMode.Host, roomName);
    }

    // CHANGED: now goes to LobbyRoom first (same as normal), tagged as "race" via SessionProperties
    // so LobbyUIHandler knows to load the Race scene when countdown ends instead of Game
    private async void OnRaceButton()
    {
        SaveNickname();

        string raceName = (raceRoomInput != null && !string.IsNullOrEmpty(raceRoomInput.text.Trim()))
            ? raceRoomInput.text.Trim()
            : "Race-" + UnityEngine.Random.Range(1000, 9999);

        if (_runner != null && _runner.IsRunning)
            await _runner.Shutdown();

        var existingRunners = GetComponents<NetworkRunner>();
        foreach (var r in existingRunners)
            DestroyImmediate(r);

        _runner = null;
        await System.Threading.Tasks.Task.Yield();

        await StartRaceGame(raceName);
    }

    // NEW: creates a race session that goes to LobbyRoom first, tagged so LobbyUIHandler
    // loads Race scene instead of Game when the countdown expires
    private async System.Threading.Tasks.Task StartRaceGame(string roomName)
    {
        if (!TryGetSceneRefByName(lobbySceneName, out SceneRef lobbySceneRef))
        {
            Debug.LogError($"[RACE] Scene '{lobbySceneName}' is missing from Build Settings.");
            return;
        }

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);
        _runner.ProvideInput = true;

        var simPhysicsRace = _runner.GetComponent<Fusion.Addons.Physics.RunnerSimulatePhysics3D>();
        if (simPhysicsRace == null)
            simPhysicsRace = _runner.gameObject.AddComponent<Fusion.Addons.Physics.RunnerSimulatePhysics3D>();

        var props = new Dictionary<string, SessionProperty>
        {
            { SESSION_TYPE_KEY, SESSION_TYPE_RACE }
        };

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode          = GameMode.Host,
            SessionName       = roomName,
            Scene             = lobbySceneRef,
            SceneManager      = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount       = maxPlayers,
            SessionProperties = props
        });

        if (result.Ok)
            Debug.Log($"[RACE] Session created: {roomName} | going to LobbyRoom first");
        else
            Debug.LogError($"[RACE] Failed to create session: {result.ShutdownReason}");
    }

    public async void JoinGame(SessionInfo sessionInfo)
    {
        Debug.Log($"[NETWORK] Joining session: {sessionInfo.Name}");
        SaveNickname();

        // If runner is running, shutdown first
        if (_runner != null && _runner.IsRunning)
        {
            await _runner.Shutdown();
        }

        // Check if there's already a NetworkRunner on this GameObject and remove it
        var existingRunners = GetComponents<NetworkRunner>();
        foreach (var runner in existingRunners)
        {
            DestroyImmediate(runner);
        }

        _runner = null;

        // Wait one frame to ensure cleanup
        await System.Threading.Tasks.Task.Yield();

        await StartGame(GameMode.Client, sessionInfo.Name);
    }

    private async System.Threading.Tasks.Task StartGame(GameMode mode, string roomName)
    {
        if (!TryGetSceneRefByName(lobbySceneName, out SceneRef lobbySceneRef))
        {
            Debug.LogError($"[NETWORK] Cannot start session. Scene '{lobbySceneName}' is missing from Build Settings.");
            return;
        }

        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);
        _runner.ProvideInput = true;

        if (mode == GameMode.Host)
        {
            var simPhysics = _runner.GetComponent<Fusion.Addons.Physics.RunnerSimulatePhysics3D>();
            if (simPhysics == null)
                _runner.gameObject.AddComponent<Fusion.Addons.Physics.RunnerSimulatePhysics3D>();
        }

        // Normal sessions are tagged as "normal" so LobbyUIHandler loads Game scene
        var props = new Dictionary<string, SessionProperty>
        {
            { SESSION_TYPE_KEY, SESSION_TYPE_NORMAL }
        };

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode          = mode,
            SessionName       = roomName,
            Scene             = lobbySceneRef,
            SceneManager      = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount       = maxPlayers,
            // Only set props when hosting — clients inherit them from the session
            SessionProperties = (mode == GameMode.Host) ? props : null
        });

        if (result.Ok)
        {
            Debug.Log($"[NETWORK] Connected to room: {roomName} as {mode}. Max players set to {maxPlayers}");
        }
        else
        {
            Debug.LogError($"Failed to connect: {result.ShutdownReason}");
        }
    }

    // Method to start the actual game (call this when players are ready)
    public async void StartGameSession()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("[NETWORK] Cannot start game - runner not active");
            return;
        }

        if (_runner.GameMode != GameMode.Host)
        {
            Debug.LogWarning("[NETWORK] Only the host can start the game");
            return;
        }

        Debug.Log("[NETWORK] Starting game session...");

        if (!TryGetSceneRefByName(gameSceneName, out SceneRef gameSceneRef))
        {
            Debug.LogError($"[NETWORK] Cannot start game session. Scene '{gameSceneName}' is missing from Build Settings.");
            return;
        }

        // Set session to closed so no one can join
        _runner.SessionInfo.IsOpen = false;

        // Load the game scene
        await _runner.LoadScene(gameSceneRef);
    }

    // NEW: called by LobbyUIHandler when session type is "race"
    public async void StartRaceSession()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            Debug.LogError("[NETWORK] Cannot start race - runner not active");
            return;
        }

        if (_runner.GameMode != GameMode.Host)
        {
            Debug.LogWarning("[NETWORK] Only the host can start the race");
            return;
        }

        Debug.Log("[NETWORK] Starting race session...");

        if (!TryGetSceneRefByName(raceSceneName, out SceneRef raceSceneRef))
        {
            Debug.LogError($"[NETWORK] Scene '{raceSceneName}' is missing from Build Settings.");
            return;
        }

        _runner.SessionInfo.IsOpen = false;
        await _runner.LoadScene(raceSceneRef);
    }

    private bool TryGetSceneRefByName(string sceneName, out SceneRef sceneRef)
    {
        sceneRef = default;

        // Build settings store scene paths like "Assets/Scenes/Name.unity".
        string scenePath = $"Assets/Scenes/{sceneName}.unity";
        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
        if (sceneIndex < 0)
        {
            return false;
        }

        sceneRef = SceneRef.FromIndex(sceneIndex);
        return true;
    }

    // Used callbacks
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} joined");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason reason)
    {
        Debug.Log($"Runner shutdown: {reason}");

        // Clear cached health so it is re-fetched after a reconnect
        _localHealth = null;

        if (!autoReturnToLobbyListOnDisconnect)
            return;

        TryReturnToLobbyList("shutdown", reason.ToString());
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"[NETWORK] Session list updated. Found {sessionList.Count} sessions");

        if (LobbyListManager == null)
        {
            Debug.LogWarning("[NETWORK] LobbyListManager reference is missing!");
            return;
        }

        if (sessionList.Count == 0)
        {
            LobbyListManager.OnNoSessionFound();
        }
        else
        {
            LobbyListManager.ClearList();
            foreach (SessionInfo session in sessionList)
            {
                Debug.Log($"[NETWORK] Session: {session.Name} - Players: {session.PlayerCount}/{session.MaxPlayers}");
                LobbyListManager.AddToList(session);
            }
        }
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        Debug.Log($"[NETWORK] Scene loaded: {currentSceneName}");

        // Clear cached health — the player object is re-spawned in the new scene
        _localHealth = null;

        if (currentSceneName != lobbySceneName && runner.GameMode == GameMode.Host)
        {
            Debug.Log($"[NETWORK] Game scene '{currentSceneName}' loaded - marking session as started");

            if (runner.SessionInfo != null)
            {
                runner.SessionInfo.IsOpen = false;
                Debug.Log("[NETWORK] Session marked as closed (IsOpen = false)");
            }
        }
    }

    // ── Input action references ────────────────────────────────────────────────
    [Header("Input Action References")]
    public InputActionReference moveAction;
    public InputActionReference attackAction;
    public InputActionReference interactAction;
    public InputActionReference specialAction;
    public InputActionReference jumpAction;

    // Accumulators: capture press via callback so Fusion's OnInput never misses it
    private bool _jumpPressed;
    private bool _attackPressed;
    private bool _specialPressed;

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        _jumpPressed = true;
    }

    private void OnAttackPerformed(InputAction.CallbackContext ctx)
    {
        _attackPressed = true;
    }

    private void OnSpecialPerformed(InputAction.CallbackContext ctx)
    {
        _specialPressed = true;
    }

    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (attackAction != null) attackAction.action.Enable();
        if (interactAction != null) interactAction.action.Enable();
        if (specialAction != null)
        {
            specialAction.action.Enable();
            specialAction.action.performed += OnSpecialPerformed;
        }
        if (jumpAction != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJumpPerformed;
        }
        if (attackAction != null)
        {
            attackAction.action.performed += OnAttackPerformed;
        }
    }

    private void OnDisable()
    {
        if (jumpAction != null)
            jumpAction.action.performed -= OnJumpPerformed;
        if (attackAction != null)
            attackAction.action.performed -= OnAttackPerformed;
        if (specialAction != null)
            specialAction.action.performed -= OnSpecialPerformed;
        if (moveAction != null) moveAction.action.Disable();
        if (attackAction != null) attackAction.action.Disable();
        if (interactAction != null) interactAction.action.Disable();
        if (specialAction != null) specialAction.action.Disable();
        if (jumpAction != null) jumpAction.action.Disable();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var myInput = new PlayerInputData();
        bool optionsMenuOpen = OptionsDeploy.IsAnyOptionsMenuOpen;

        // ── Dead check ────────────────────────────────────────────────────────
        // Lazily find the local player's PlayerHealth once the player object exists.
        // While the player is dead all action inputs stay zero so clicks from the
        // spectator navigation buttons never trigger weapons or abilities.
        if (_localHealth != null && (_localHealth.Object == null || !_localHealth.Object.IsValid))
            _localHealth = null;

        bool isDead = false;
        if (_localHealth != null)
        {
            if (_localHealth.Object == null)
            {
                _localHealth = null;
            }
            else
            {
                _localHealth.TryGetIsDeadSafe(out isDead);
            }
        }

        if (!isDead && !optionsMenuOpen)
        {
            // Movement
            string _sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (_sceneName == "Race" && KartMobileInput.IsActive)
            {
                myInput.MoveDirection = new Vector3(KartInput.Steer, 0, KartInput.Throttle);
            }
            else if (moveAction != null)
            {
                Vector2 moveVal = moveAction.action.ReadValue<Vector2>();
                myInput.MoveDirection = new Vector3(moveVal.x, 0, moveVal.y);
            }

            // Actions: WasPressedThisFrame handles both UI Button taps and Keys
            // Consume the accumulated attack press (same pattern as jump)
            myInput.AttackPressed = _attackPressed;
            _attackPressed = false;

            // Consume accumulated jump press
            myInput.JumpPressed = _jumpPressed;

            if (interactAction != null)
                myInput.InteractPressed = interactAction.action.WasPressedThisFrame();

            myInput.SpecialPressed = _specialPressed;
        }

        // Always consume accumulators so they don't queue up while dead
        // and fire the moment the player respawns.
        _jumpPressed = false;
        _specialPressed = false;

        // Camera rotation is always sent — SpectatorSystem needs it to work.
        if (Camera.main != null)
            myInput.CameraRotation = Camera.main.transform.rotation;

        input.Set(myInput);
    }

    // Required by interface (unused)
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[NETWORK] Disconnected from server: {reason}");

        if (!autoReturnToLobbyListOnDisconnect)
            return;

        TryReturnToLobbyList("disconnect", reason.ToString());
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    private void TryReturnToLobbyList(string source, string reason)
    {
        if (_isReturningToLobbyList)
            return;

        string currentScene = SceneManager.GetActiveScene().name;
        if (!ShouldAutoReturnFromScene(currentScene))
            return;

        _isReturningToLobbyList = true;

        int sceneIndex = ResolveSceneIndex(lobbyListSceneName);
        Debug.LogWarning($"[NETWORK] Auto returning to {lobbyListSceneName} from scene '{currentScene}' after {source}: {reason}");

        if (sceneIndex >= 0)
        {
            SceneManager.LoadScene(sceneIndex);
        }
        else
        {
            SceneManager.LoadScene(lobbyListSceneName);
        }
    }

    private bool ShouldAutoReturnFromScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (string.Equals(sceneName, lobbyListSceneName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(sceneName, "MainMenu", StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.Equals(sceneName, "CharacterSelect", StringComparison.OrdinalIgnoreCase))
            return false;

        // Return from gameplay flow scenes.
        if (string.Equals(sceneName, gameSceneName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(sceneName, "EndMatch", StringComparison.OrdinalIgnoreCase))
            return !stayInEndMatchAfterDisconnect;

        if (string.Equals(sceneName, lobbySceneName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
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
            if (string.Equals(fileName, sceneName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}

public struct PlayerInputData : INetworkInput
{
    public Vector3 MoveDirection;
    public NetworkBool JumpPressed;
    public Quaternion CameraRotation;
    public NetworkBool InteractPressed;
    public NetworkBool AttackPressed;
    public NetworkBool SpecialPressed;
}