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

    [Header("Network Settings")]
    [SerializeField] private string lobbySceneName = "LobbyRoom";
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private int maxPlayers = 5;

    [Header("Session List")]
    [SerializeField] private LobbyListManager LobbyListManager;

    private NetworkRunner _runner;

    private void Start()
    {
        hostBtn.onClick.AddListener(OnHostRoom);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        OnJoinLobby();
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
        // Check if we just loaded the game scene (not the lobby scene)
        string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        Debug.Log($"[NETWORK] Scene loaded: {currentSceneName}");
        
        if (currentSceneName != lobbySceneName && runner.GameMode == GameMode.Host)
        {
            Debug.Log($"[NETWORK] Game scene '{currentSceneName}' loaded - marking session as started");
            
            // Close the session so no new players can join
            if (runner.SessionInfo != null)
            {
                runner.SessionInfo.IsOpen = false;
                Debug.Log("[NETWORK] Session marked as closed (IsOpen = false)");
            }
        }
    }

    // Input handling
    // Primero, necesitas las referencias a tus acciones (se asignan en el Inspector)
    [Header("Input Action References")]
    public InputActionReference moveAction;
    public InputActionReference attackAction;
    public InputActionReference interactAction;
    public InputActionReference specialAction;

    // Debajo de tus variables, añade esto:
    private void OnEnable()
    {
        if (moveAction != null) moveAction.action.Enable();
        if (attackAction != null) attackAction.action.Enable();
        if (interactAction != null) interactAction.action.Enable();
        if (specialAction != null) specialAction.action.Enable();
    }

    private void OnDisable()
    {
        // Es buena práctica apagarlas cuando el objeto se destruye
        if (moveAction != null) moveAction.action.Disable();
        if (attackAction != null) attackAction.action.Disable();
        if (interactAction != null) interactAction.action.Disable();
        if (specialAction != null) specialAction.action.Disable();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var myInput = new PlayerInputData();

        // El "Move" ahora lee el Vector2 del joystick o WASD automáticamente
        if (moveAction != null)
        {
            Vector2 moveVal = moveAction.action.ReadValue<Vector2>();
            myInput.MoveDirection = new Vector3(moveVal.x, 0, moveVal.y);
        }

        // Las acciones detectan si se presionó el botón en pantalla O la tecla
        if (attackAction != null)
            myInput.AttackPressed = attackAction.action.WasPressedThisFrame();
        
        if (interactAction != null)
            myInput.InteractPressed = interactAction.action.WasPressedThisFrame();
        
        if (specialAction != null)
            myInput.SpecialPressed = specialAction.action.WasPressedThisFrame();

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
    public NetworkBool InteractPressed; //  (Tecla E / Especial 1)
    public NetworkBool AttackPressed;   //  (Click / Ataque Básico)
    public NetworkBool SpecialPressed;  //  (Tecla Q / Especial 2)
}