using UnityEngine;
using System;
using Fusion; // Using Photon Fusion instead of Netcode

/// <summary>
/// Defines the types of weather events that can affect the racetrack
/// </summary>
public enum WeatherType
{
    Sunny,  // Normal conditions, no modifiers
    Chilly, // Slippery floor with less steering control
    Rainy   // Reduced visibility - affects camera fog
}

/// <summary>
/// Manages weather events and applies physics/visual effects to all karts
/// </summary>
public class EventManager : NetworkBehaviour
{
    public event Action<WeatherType> WeatherChanged;

    [Header("Weather Settings")]
    [Networked, OnChangedRender(nameof(OnWeatherChanged))]
    private WeatherType currentWeather { get; set; }

    [Header("Skybox")]
    [SerializeField] private Material sunnySkybox;
    [SerializeField] private Material chillySkybox;
    [SerializeField] private Material rainySkybox;

    [Header("Chilly Day Modifiers")]
    [SerializeField] private float chillyGripMultiplier = 0.25f; // 25% of normal lateral grip
    [SerializeField] private float chillyDragMultiplier = 1.35f; // Slightly heavier feel
    [SerializeField] private float chillySteerMultiplier = 0.65f; // Reduced steering response

    [Header("Rainy Day Modifiers")]
    [SerializeField] private float rainyFogDensity = 0.15f; // Fog density for rain
    [SerializeField] private Color rainyFogColor = new Color(0.7f, 0.7f, 0.8f); // Grayish fog

    private Material defaultSkybox;

    private bool _weatherApplied = false;
    private bool hasSpawned;
    private WeatherType localWeather = WeatherType.Sunny;

    public bool IsNetworkStateReady => hasSpawned && Object != null && Runner != null;

    public override void Spawned()
    {
        hasSpawned = true;
        defaultSkybox = RenderSettings.skybox;

        if (HasStateAuthority)
        {
            currentWeather = GetRandomWeather();
        }

        localWeather = ReadWeatherSafe();
        
        ApplyWeatherEffects();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        hasSpawned = false;
    }

    public override void Render()
    {
        if (!_weatherApplied && IsNetworkStateReady)
        {
            ApplyWeatherEffects();
        }
    }

    public void OnWeatherChanged()
    {
        localWeather = ReadWeatherSafe();
        ApplyWeatherEffects();
    }

    private void Start()
    {
    }

    /// <summary>
    /// Gets a random weather type for the current race
    /// </summary>
    private WeatherType GetRandomWeather()
    {
        int weatherCount = System.Enum.GetValues(typeof(WeatherType)).Length;
        int randomWeather = UnityEngine.Random.Range(0, weatherCount);
        return (WeatherType)randomWeather;
    }

    /// <summary>
    /// Applies the current weather effects to all karts and environment
    /// </summary>
    public void ApplyWeatherEffects()
    {
        _weatherApplied = true;
        WeatherType weather = ReadWeatherSafe();

        BroomMove[] allKarts = FindObjectsByType<BroomMove>(FindObjectsSortMode.None);

        // Update all karts with new weather conditions
        foreach (BroomMove kart in allKarts)
        {
            if (kart != null)
            {
                kart.SetWeather(weather, this);
            }
        }

        // Apply visual effects
        ApplyVisualEffects();

        WeatherChanged?.Invoke(weather);
    }

    /// <summary>
    /// Registers a kart that spawned after scene load and applies current weather.
    /// </summary>
    public void RegisterKart(BroomMove kart)
    {
        if (kart == null)
        {
            return;
        }

        kart.SetWeather(ReadWeatherSafe(), this);
    }

    /// <summary>
    /// Applies visual effects based on current weather
    /// </summary>
    private void ApplyVisualEffects()
    {
        switch (ReadWeatherSafe())
        {
            case WeatherType.Sunny:
                // Always explicitly disable fog so it never bleeds from scene settings
                RenderSettings.fog = false;
                RenderSettings.fogDensity = 0f;
                ApplyConfiguredSkybox(sunnySkybox);
                break;

            case WeatherType.Chilly:
                // Always explicitly disable fog so it never bleeds from scene settings
                RenderSettings.fog = false;
                RenderSettings.fogDensity = 0f;
                ApplyConfiguredSkybox(chillySkybox);
                break;

            case WeatherType.Rainy:
                // Force every fog setting explicitly — scene defaults are unreliable in builds
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = rainyFogDensity;
                RenderSettings.fogColor = rainyFogColor;
                ApplyConfiguredSkybox(rainySkybox);
                break;
        }
    }

    private void ApplyConfiguredSkybox(Material configuredSkybox)
    {
        if (configuredSkybox != null)
        {
            ApplySkybox(configuredSkybox);
            return;
        }

        RestoreDefaultSkybox();
    }

    private void ApplySkybox(Material skyboxMaterial)
    {
        if (RenderSettings.skybox == skyboxMaterial)
        {
            return;
        }

        RenderSettings.skybox = skyboxMaterial;
        DynamicGI.UpdateEnvironment();
    }

    private void RestoreDefaultSkybox()
    {
        if (RenderSettings.skybox == defaultSkybox)
        {
            return;
        }

        RenderSettings.skybox = defaultSkybox;
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Gets the lateral grip multiplier for the current weather
    /// </summary>
    public float GetGripMultiplier()
    {
        return ReadWeatherSafe() == WeatherType.Chilly ? chillyGripMultiplier : 1f;
    }

    /// <summary>
    /// Gets the drag multiplier for the current weather
    /// </summary>
    public float GetDragMultiplier()
    {
        return ReadWeatherSafe() == WeatherType.Chilly ? chillyDragMultiplier : 1f;
    }

    /// <summary>
    /// Gets the steering multiplier for the current weather
    /// </summary>
    public float GetSteerMultiplier()
    {
        return ReadWeatherSafe() == WeatherType.Chilly ? chillySteerMultiplier : 1f;
    }

    /// <summary>
    /// Manually set the weather to a specific type
    /// </summary>
    public void SetWeather(WeatherType newWeather)
    {
        localWeather = newWeather;

        if (Object != null && HasStateAuthority)
        {
            currentWeather = newWeather;
        }

        ApplyWeatherEffects();
    }

    /// <summary>
    /// Gets the current weather type
    /// </summary>
    public WeatherType GetCurrentWeather()
    {
        return ReadWeatherSafe();
    }

    private WeatherType ReadWeatherSafe()
    {
        if (IsNetworkStateReady)
        {
            localWeather = currentWeather;
            return currentWeather;
        }

        return localWeather;
    }
}