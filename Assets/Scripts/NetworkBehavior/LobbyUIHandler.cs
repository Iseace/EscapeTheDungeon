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
    [SerializeField] private float countdownDuration = 5f;
    [Networked] private TickTimer CountdownTimer { get; set; }

    private Dictionary<PlayerRef, bool> _readyPlayers = new Dictionary<PlayerRef, bool>();

    // LOCAL state
    private bool _isLocalPlayerReady = false;

    public override void Spawned()
    {
        if (countdownText != null) countdownText.text = "";
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
        if (Runner.IsServer && CountdownTimer.Expired(Runner))
        {
            CountdownTimer = TickTimer.None;
            Debug.Log("[LOBBY] Countdown complete, loading Game scene");

            // Build settings store scene paths as "Assets/Scenes/Name.unity".
            int gameSceneIndex = UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{gameSceneName}.unity");
            if (gameSceneIndex < 0)
            {
                Debug.LogError($"[LOBBY] Cannot load game scene '{gameSceneName}'. Add it to Build Settings.");
                return;
            }

            Runner.LoadScene(SceneRef.FromIndex(gameSceneIndex));
        }
    }
}