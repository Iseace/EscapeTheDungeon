using Fusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Health Bar Reference")]
    [SerializeField] private GameObject healthBarObject; // Assign HUD_Canvas or HealthBar_HUD from the prefab

    [Networked, OnChangedRender(nameof(HealthChanged))]
    public float CurrentHealth { get; set; }

    private Slider localHealthSlider;
    private string currentSceneName;
    private bool isHealthInitialized = false;

    public override void Spawned()
    {
        // Only the Server/Host sets the initial value once
        if (Object.HasStateAuthority && !isHealthInitialized)
        {
            CurrentHealth = maxHealth;
            isHealthInitialized = true;
        }

        currentSceneName = SceneManager.GetActiveScene().name;

        // Subscribe to scene changes
        SceneManager.activeSceneChanged += OnSceneChanged;

        // Only configure for the local player (the one we control)
        if (HasInputAuthority)
        {
            SetupHealthBar();
            ConfigureHUDForScene();
        }
        else
        {
            // For other players, make sure their health bar is always hidden
            if (healthBarObject != null)
            {
                healthBarObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void SetupHealthBar()
    {
        if (healthBarObject == null)
        {
            Debug.LogError("Health Bar Object not assigned in PlayerHealth!");
            return;
        }

        // Find the slider component
        localHealthSlider = healthBarObject.GetComponentInChildren<Slider>();

        if (localHealthSlider != null)
        {
            localHealthSlider.maxValue = maxHealth;
            localHealthSlider.minValue = 0;
            localHealthSlider.value = CurrentHealth;
        }
        else
        {
            Debug.LogError("Slider component not found in health bar!");
        }
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        currentSceneName = newScene.name;

        if (HasInputAuthority)
        {
            ConfigureHUDForScene();
        }
    }

    private void ConfigureHUDForScene()
    {
        if (healthBarObject == null) return;

        // Show health bar only in game scenes, hide in lobby
        if (currentSceneName == "LobbyRoom")
        {
            healthBarObject.SetActive(false);
        }
        else
        {
            // In game scene - show the health bar
            healthBarObject.SetActive(true);
            UpdateHUD();
        }
    }

    void HealthChanged()
    {
        UpdateHUD();
    }

    private void UpdateHUD()
    {
        if (HasInputAuthority && localHealthSlider != null && healthBarObject != null && healthBarObject.activeSelf)
        {
            localHealthSlider.value = CurrentHealth;
        }
    }

    public void DealDamage(float damage)
    {
        if (!Object.HasStateAuthority) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
    }
}