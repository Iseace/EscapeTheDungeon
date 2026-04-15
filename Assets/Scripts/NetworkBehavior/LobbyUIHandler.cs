using UnityEngine;
using UnityEngine.UI;
using Fusion;
using TMPro;
using System.Collections.Generic;
using System;
using System.Linq;

public class LobbyUIHandler : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Button readyBtn;
    [SerializeField] private TextMeshProUGUI readyBtnText;

    [Header("Settings")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string raceSceneName = "Race";
    [SerializeField] private float countdownDuration = 5f;

    [Networked] private TickTimer CountdownTimer { get; set; }

    private Dictionary<PlayerRef, bool> _readyPlayers = new Dictionary<PlayerRef, bool>();

    // LOCAL state
    private bool _isLocalPlayerReady = false;

    public override void Spawned()
    {
        if (countdownText != null)
        {
            countdownText.text = "";
            countdownText.outlineWidth = 0.2f;
            countdownText.outlineColor = new Color32(0x2D, 0x2F, 0x39, 0xFF);
        }

        if (readyBtn != null) readyBtn.onClick.AddListener(OnReadyClicked);

        // Reset local state when spawning in the lobby
        _isLocalPlayerReady = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnReadyClicked()
    {
        // Toggle local visual state immediately for responsiveness
        _isLocalPlayerReady = !_isLocalPlayerReady;

        // Update the Button Text immediately
        if (readyBtnText != null)
            readyBtnText.text = _isLocalPlayerReady ? "NOT READY" : "READY";

        // Tell the server our new state
        RPC_SetPlayerReady(Runner.LocalPlayer, _isLocalPlayerReady);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerReady(PlayerRef player, bool isReady)
    {
        _readyPlayers[player] = isReady;
        Debug.Log($"[LOBBY] Player {player.PlayerId} is now {(isReady ? "READY" : "NOT READY")}");
        CheckAllReady();
    }

    private void CheckAllReady()
    {
        if (!Runner.IsServer) return;

        // Get all active players
        var activePlayers = Runner.ActivePlayers.ToList();
        if (activePlayers.Count == 0) return;

        bool allReady = true;
        foreach (var p in activePlayers)
        {
            // If player isn't in dict or is false, they aren't ready
            if (!_readyPlayers.TryGetValue(p, out bool isReady) || !isReady)
            {
                allReady = false;
                break;
            }
        }

        if (allReady)
        {
            if (!CountdownTimer.IsRunning)
            {
                CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownDuration);
                Debug.Log("[LOBBY] All players ready, starting countdown");
            }
        }
        else
        {
            // Stop countdown if someone unreadies
            CountdownTimer = TickTimer.None;
        }
    }

    public override void Render()
    {
        if (countdownText != null)
        {
            if (CountdownTimer.IsRunning)
            {
                float? remaining = CountdownTimer.RemainingTime(Runner);
                if (remaining.HasValue)
                {
                    countdownText.text = $"Game Starts in: {Mathf.CeilToInt(remaining.Value)}";
                }
            }
            else
            {
                countdownText.text = "Waiting for all players...";
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Only the Server/Host handles the scene transition
        if (!Runner.IsServer || !CountdownTimer.Expired(Runner)) return;

        CountdownTimer = TickTimer.None;

        // Read the session type set by the host to decide which scene to load
        string targetScene = gameSceneName; // default — normal game

        if (Runner.SessionInfo != null &&
            Runner.SessionInfo.Properties != null &&
            Runner.SessionInfo.Properties.TryGetValue(NetworkRunnerHandler.SESSION_TYPE_KEY, out SessionProperty prop))
        {
            if ((string)prop == NetworkRunnerHandler.SESSION_TYPE_RACE)
            {
                targetScene = raceSceneName;
                Debug.Log("[LOBBY] Race session detected — loading Race scene");
            }
            else
            {
                Debug.Log("[LOBBY] Normal session — loading Game scene");
            }
        }

        int sceneIndex = UnityEngine.SceneManagement.SceneUtility
            .GetBuildIndexByScenePath($"Assets/Scenes/{targetScene}.unity");

        if (sceneIndex < 0)
        {
            Debug.LogError($"[LOBBY] Cannot load scene '{targetScene}'. Add it to Build Settings.");
            return;
        }

        Debug.Log($"[LOBBY] Countdown complete, loading '{targetScene}'");
        Runner.LoadScene(SceneRef.FromIndex(sceneIndex));
    }
}