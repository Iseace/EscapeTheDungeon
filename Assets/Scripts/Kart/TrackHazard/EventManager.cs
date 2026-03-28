using UnityEngine;
using Fusion;

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
public class EventManager : MonoBehaviour
{
    [Header("Weather Settings")]
    [SerializeField] private WeatherType currentWeather = WeatherType.Sunny;
    [SerializeField] private bool autoChangeWeather = false; // Disabled - weather stays same per race

    [Header("Chilly Day Modifiers")]
    [SerializeField] private float chillyGripMultiplier = 0.25f; // 25% of normal lateral grip
    [SerializeField] private float chillyDragMultiplier = 1.35f; // Slightly heavier feel
    [SerializeField] private float chillySteerMultiplier = 0.65f; // Reduced steering response

    [Header("Rainy Day Modifiers")]
    [SerializeField] private float rainyFogDensity = 0.15f; // Fog density for rain
    [SerializeField] private Color rainyFogColor = new Color(0.7f, 0.7f, 0.8f); // Grayish fog

    private float weatherChangeTimer = 0f;

    private void Start()
    {
        // Apply weather to any karts already present in scene.
        ApplyWeatherEffects();
    }

    private void Update()
    {
        // Weather stays the same throughout the race
    }

    /// <summary>
    /// Randomly changes the current weather to a different type
    /// </summary>
    private void ChangeWeatherRandomly()
    {
        int randomWeather = Random.Range(0, 3);
        currentWeather = (WeatherType)randomWeather;
        ApplyWeatherEffects();
        Debug.Log($"Weather changed to: {currentWeather}");
    }

    /// <summary>
    /// Applies the current weather effects to all karts and environment
    /// </summary>
    public void ApplyWeatherEffects()
    {
        BroomMove[] allKarts = FindObjectsByType<BroomMove>(FindObjectsSortMode.None);

        // Update all karts with new weather conditions
        foreach (BroomMove kart in allKarts)
        {
            if (kart != null)
            {
                kart.SetWeather(currentWeather, this);
            }
        }

        // Apply visual effects
        ApplyVisualEffects();
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

        kart.SetWeather(currentWeather, this);
    }

    /// <summary>
    /// Applies visual effects based on current weather
    /// </summary>
    private void ApplyVisualEffects()
    {
        switch (currentWeather)
        {
            case WeatherType.Sunny:
                RenderSettings.fog = false;
                break;

            case WeatherType.Chilly:
                RenderSettings.fog = false;
                break;

            case WeatherType.Rainy:
                RenderSettings.fog = true;
                RenderSettings.fogDensity = rainyFogDensity;
                RenderSettings.fogColor = rainyFogColor;
                break;
        }
    }

    /// <summary>
    /// Gets the lateral grip multiplier for the current weather
    /// </summary>
    public float GetGripMultiplier()
    {
        return currentWeather == WeatherType.Chilly ? chillyGripMultiplier : 1f;
    }

    /// <summary>
    /// Gets the drag multiplier for the current weather
    /// </summary>
    public float GetDragMultiplier()
    {
        return currentWeather == WeatherType.Chilly ? chillyDragMultiplier : 1f;
    }

    /// <summary>
    /// Gets the steering multiplier for the current weather
    /// </summary>
    public float GetSteerMultiplier()
    {
        return currentWeather == WeatherType.Chilly ? chillySteerMultiplier : 1f;
    }

    /// <summary>
    /// Manually set the weather to a specific type
    /// </summary>
    public void SetWeather(WeatherType newWeather)
    {
        currentWeather = newWeather;
        weatherChangeTimer = 0f;
        ApplyWeatherEffects();
    }

    /// <summary>
    /// Gets the current weather type
    /// </summary>
    public WeatherType GetCurrentWeather()
    {
        return currentWeather;
    }
}
