using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using System.Collections.Generic;

public class LobbyListManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyListItem;

    [Header("References")]
    [SerializeField] private NetworkRunnerHandler networkRunnerHandler;

    private List<GameObject> _spawnedSessionItems = new List<GameObject>();

    public void AddToList(SessionInfo sessionInfo)
    {
        // Instantiate the session UI prefab
        GameObject sessionItem = Instantiate(lobbyListItem, lobbyListContainer);
        _spawnedSessionItems.Add(sessionItem);

        // Get the handler component and set it up
        LobbyListUIHandler handler = sessionItem.GetComponent<LobbyListUIHandler>();
        if (handler != null)
        {
            handler.SetInformation(sessionInfo);
            handler.OnJoinSession += OnSessionJoinRequested;
        }
        else
        {
            Debug.LogError("lobbyListItem is missing LobbyListUIHandler component!");
        }
    }

    // clean up method
    public void ClearList()
    {
        foreach (GameObject item in _spawnedSessionItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        _spawnedSessionItems.Clear();
    }

    public void OnNoSessionFound()
    {
        ClearList();
    }

    private void OnSessionJoinRequested(SessionInfo sessionInfo)
    {
        Debug.Log($"[SESSION LIST] Attempting to join session: {sessionInfo.Name}");
        if (networkRunnerHandler != null)
        {
            networkRunnerHandler.JoinGame(sessionInfo);
        }
        else
        {
            Debug.LogError("NetworkRunnerHandler reference is missing!");
        }
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        foreach (GameObject item in _spawnedSessionItems)
        {
            if (item != null)
            {
                LobbyListUIHandler handler = item.GetComponent<LobbyListUIHandler>();
                if (handler != null)
                {
                    handler.OnJoinSession -= OnSessionJoinRequested;
                }
            }
        }
    }
}