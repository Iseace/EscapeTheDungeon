using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class PCKeybindUI : MonoBehaviour
{
    [Header("Keybind UI Images")]
    [SerializeField] private GameObject survivorKeys;
    [SerializeField] private GameObject bossKeys;

    private PlayerRole localPlayerRole;
    private bool hasCheckedRole;
    private bool hasSetUIVisibility;
    private bool isSpectating;

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name != "Game")
        {
            gameObject.SetActive(false);
            return;
        }

        bool isPC = Application.platform == RuntimePlatform.WindowsPlayer
                 || Application.platform == RuntimePlatform.LinuxPlayer
                 || Application.platform == RuntimePlatform.OSXPlayer
                 || Application.isEditor;

        if (!isPC) { gameObject.SetActive(false); return; }

        if (survivorKeys == null) survivorKeys = GameObject.Find("SurvivorKeys");
        if (bossKeys     == null) bossKeys     = GameObject.Find("BossKeys");
        if (bossKeys != null) bossKeys.SetActive(false);
    }

    private void Update()
    {
        if (isSpectating) return;

        if (!hasCheckedRole || localPlayerRole == null)
        {
            if (bossKeys != null && bossKeys.activeSelf)
                bossKeys.SetActive(false);
            
            FindLocalPlayerRole();
        }

        if (localPlayerRole == null) return;

        if (!hasSetUIVisibility)
        {
            UpdateUIVisibility();
            hasSetUIVisibility = true;
        }
    }

    private void FindLocalPlayerRole()
    {
        var runner = FindFirstObjectByType<NetworkRunner>();
        if (runner == null || !runner.IsRunning) return;

        var localPlayer = runner.LocalPlayer;
        if (localPlayer.IsNone) return;

        if (runner.TryGetPlayerObject(localPlayer, out NetworkObject playerObj) &&
            playerObj.TryGetComponent<PlayerRole>(out var role))
        {
            localPlayerRole = role;
            hasCheckedRole = true;
        }
    }

    private void UpdateUIVisibility()
    {
        bool isBoss = localPlayerRole.IsBoss;

        if (survivorKeys != null) survivorKeys.SetActive(!isBoss);
        if (bossKeys != null) bossKeys.SetActive(isBoss);
    }

    public void SetSpectatorMode(bool spectating)
    {
        isSpectating = spectating;
        Debug.Log($"[PCKeybindUI] SetSpectatorMode called - spectating: {spectating}");

        if (spectating)
        {
            if (survivorKeys != null) survivorKeys.SetActive(false);
            if (bossKeys != null) bossKeys.SetActive(false);
        }
    }
}