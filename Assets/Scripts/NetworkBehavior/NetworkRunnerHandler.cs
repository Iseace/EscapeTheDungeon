using UnityEngine;
using UnityEngine.UI;
using Fusion;
using Fusion.Sockets;
using TMPro;
using System.Collections.Generic;
using System;

public class NetworkRunnerHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField hostRoomInput;
    [SerializeField] private Button hostBtn;
    [SerializeField] private TMP_InputField clientRoomInput;
    [SerializeField] private Button clientBtn;
    
    [Header("Network Settings")]
    [SerializeField] private string gameSceneName = "main";
    private NetworkRunner _runner;
    private void Start()
    {
        hostBtn.onClick.AddListener(OnHostRoom);
        clientBtn.onClick.AddListener(OnJoinRoom);
    }
    
    private async void OnHostRoom()
    {
        string roomName = hostRoomInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
            return;
        
        await StartGame(GameMode.Host, roomName);
    }
    
    private async void OnJoinRoom()
    {
        string roomName = clientRoomInput.text.Trim();
        if (string.IsNullOrEmpty(roomName))
            return;
        
        await StartGame(GameMode.Client, roomName);
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
                UnityEngine.SceneManagement.SceneUtility
                    .GetBuildIndexByScenePath("Scenes/" + gameSceneName)
            ),
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
        
        if (result.Ok)
            Debug.Log($"Connected to room: {roomName} as {mode}");
        else
            Debug.LogError($"Failed to connect: {result.ShutdownReason}");
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
    
    // Input handling
   public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var myInput = new PlayerInputData();
        myInput.MoveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        myInput.JumpPressed = Input.GetButton("Jump");
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
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}

// NetworkInputData struct
public struct PlayerInputData : INetworkInput
{
    public Vector3 MoveDirection;
    public NetworkBool JumpPressed;
    public Quaternion CameraRotation;
}