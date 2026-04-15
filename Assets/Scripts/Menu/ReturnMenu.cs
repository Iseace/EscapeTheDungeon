using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;

public class ReturnMenu : MonoBehaviour
{
    public void GoBack()
    {
        // 1. Find the NetworkRunner and shut it down properly
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null)
        {
            runner.Shutdown();
        }

        // 2. Load the MainMenu scene
        SceneManager.LoadScene("MainMenu");
    }
}