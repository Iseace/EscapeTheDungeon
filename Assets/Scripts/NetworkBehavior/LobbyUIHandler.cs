using UnityEngine;
using UnityEngine.UI;
using Fusion;
using TMPro;
using System.Collections.Generic;
using System;
using System.Linq; // FIXED: Added for .Count() support

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

    public override void Spawned()
    {
        if (countdownText != null) countdownText.text = "";
        
        readyBtn.onClick.AddListener(OnReadyClicked);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnReadyClicked()
    {
        RPC_SetPlayerReady(Runner.LocalPlayer, !GetLocalReadyStatus());
    }

    private bool GetLocalReadyStatus()
    {
        return _readyPlayers.ContainsKey(Runner.LocalPlayer) && _readyPlayers[Runner.LocalPlayer];
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerReady(PlayerRef player, bool isReady)
    {
        _readyPlayers[player] = isReady;
        CheckAllReady();
    }

    private void CheckAllReady()
    {
        if (!Runner.IsServer) return;

        bool ready = true;
        
        // Ensure there is at least one player
        if (Runner.ActivePlayers.Count() == 0) ready = false;

        foreach (var p in Runner.ActivePlayers)
        {
            if (!_readyPlayers.ContainsKey(p) || !_readyPlayers[p])
            {
                ready = false;
                break;
            }
        }

        if (ready)
        {
            if (!CountdownTimer.IsRunning)
            {
                CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownDuration);
            }
        }
        else
        {
            CountdownTimer = TickTimer.None;
        }
    }

    public override void Render()
    {
        if (readyBtnText != null)
            readyBtnText.text = GetLocalReadyStatus() ? "NOT READY" : "READY";

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

    public override void FixedUpdateNetwork()
    {
        if (Runner.IsServer && CountdownTimer.Expired(Runner))
        {
            CountdownTimer = TickTimer.None;
            
            // FIXED: Use LoadScene instead of SetActiveScene
            Runner.LoadScene(SceneRef.FromIndex(
                UnityEngine.SceneManagement.SceneUtility.GetBuildIndexByScenePath("Scenes/" + gameSceneName)
            ));
        }
    }
}