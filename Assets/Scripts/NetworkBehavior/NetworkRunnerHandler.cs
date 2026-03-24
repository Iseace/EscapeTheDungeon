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

    [Header("Network Settings")]
    [SerializeField] private string lobbySceneName = "LobbyRoom";
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private int maxPlayers = 5;

    [Header("Session List")]
    [SerializeField] private LobbyListManager LobbyListManager;

    private NetworkRunner _runner;

    // Cached reference to the local player's health so OnInput can check IsDead.
    // Looked up lazily the first time it is needed — avoids a Find() call every frame.
    private PlayerHealth _localHealth;

    private void Start()
    {
        hostBtn.onClick.AddListener(OnHostRoom);

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
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.AddCallbacks(this);
        _runner.ProvideInput = true;

        var result = await _runner.StartGame(new StartGameArgs
        {
            GameMode = mode,
            SessionName = roomName,
            Scene = SceneRef.FromIndex(
                SceneUtility.GetBuildIndexByScenePath("Scenes/" + lobbySceneName)
            ),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            PlayerCount = maxPlayers
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

        // Set session to closed so no one can join
        _runner.SessionInfo.IsOpen = false;

        // Load the game scene
        await _runner.LoadScene(SceneRef.FromIndex(
            SceneUtility.GetBuildIndexByScenePath("Scenes/" + gameSceneName)
        ));
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
            if (moveAction != null)
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
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
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