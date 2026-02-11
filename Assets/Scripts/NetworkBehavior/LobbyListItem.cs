using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System;

public class LobbyListUIHandler : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI sessionNameText;
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI statusText;
    public Button joinButton;

    private SessionInfo _sessionInfo;

    // Event to notify when join button is clicked
    public event Action<SessionInfo> OnJoinSession;

    private void Update()
    {
        // Continuously update the display if we have session info
        if (_sessionInfo != null)
        {
            UpdateDisplay();
        }
    }

    public void SetInformation(SessionInfo info)
    {
        Debug.Log($"[SESSION ITEM] Setting information for session: {info.Name}");
        _sessionInfo = info;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_sessionInfo == null) return;

        sessionNameText.text = _sessionInfo.Name;
        playerCountText.text = $"{_sessionInfo.PlayerCount}/{_sessionInfo.MaxPlayers}";

        // Check if game has started
        bool isGameStarted = false;
        if (_sessionInfo.Properties != null && _sessionInfo.Properties.TryGetValue("GameStarted", out var gameStartedProp))
        {
            isGameStarted = System.Convert.ToInt32(gameStartedProp.PropertyValue) != 0;
        }

        // Determine session status
        bool isFull = _sessionInfo.PlayerCount >= _sessionInfo.MaxPlayers;
        bool isOpen = _sessionInfo.IsOpen && _sessionInfo.IsVisible;

        // Priority: Started > Full > Waiting
        if (isGameStarted || (!isOpen && _sessionInfo.PlayerCount > 0))
        {
            statusText.text = "Started";
            SetButtonVisibility(false);
        }
        else if (isFull)
        {
            statusText.text = "Full";
            SetButtonVisibility(false);
        }
        else
        {
            statusText.text = "Waiting";
            SetButtonVisibility(true);
        }
    }

    private void SetButtonVisibility(bool visible)
    {
        // Disable button interactivity
        joinButton.interactable = visible;

        // Make button invisible 
        var canvasGroup = joinButton.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
        }
        else
        {
            // Fallback: change image alpha if no CanvasGroup
            var buttonImage = joinButton.GetComponent<Image>();
            if (buttonImage != null)
            {
                var color = buttonImage.color;
                color.a = visible ? 1f : 0f;
                buttonImage.color = color;
            }

            // Also hide the text
            var buttonText = joinButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                var textColor = buttonText.color;
                textColor.a = visible ? 1f : 0f;
                buttonText.color = textColor;
            }
        }
    }

    public void OnJoinButtonClicked()
    {
        Debug.Log("[SESSION ITEM] JOIN BUTTON CLICKED!");
        if (_sessionInfo != null)
        {
            Debug.Log($"[SESSION ITEM] Invoking join for session: {_sessionInfo.Name}");
            OnJoinSession?.Invoke(_sessionInfo);
        }
        else
        {
            Debug.LogError("[SESSION ITEM] SessionInfo is null! Cannot join.");
        }
    }
}