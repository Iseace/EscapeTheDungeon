using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a weather icon (sun/snow/cloud) based on EventManager current weather.
/// Attach this to a UI GameObject with an Image component.
/// </summary>
[RequireComponent(typeof(Image))]
public class WeatherIconHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EventManager eventManager;
    [SerializeField] private Image weatherIconImage;

    [Header("Sprites")]
    [SerializeField] private Sprite sunnySprite;
    [SerializeField] private Sprite chillySprite;
    [SerializeField] private Sprite rainySprite;

    private void Awake()
    {
        if (weatherIconImage == null)
        {
            weatherIconImage = GetComponent<Image>();
        }

        if (eventManager == null)
        {
            eventManager = FindAnyObjectByType<EventManager>();
        }
    }

    private void OnEnable()
    {
        if (eventManager == null)
        {
            eventManager = FindAnyObjectByType<EventManager>();
        }

        if (eventManager != null)
        {
            eventManager.WeatherChanged += OnWeatherChanged;

            // Si el EventManager ya está válido en red, actualizamos inmediatamente.
            // Si no, el WeatherChanged se disparará cuando el EventManager haga su Spawned()
            if (eventManager.Object != null && eventManager.Object.IsValid)
            {
                UpdateIcon(eventManager.GetCurrentWeather());
            }
        }
        else
        {
            Debug.LogWarning("[WeatherIconHUD] EventManager not found in scene.", this);
        }
    }

    private void OnDisable()
    {
        if (eventManager != null)
        {
            eventManager.WeatherChanged -= OnWeatherChanged;
        }
    }

    private void OnWeatherChanged(WeatherType weather)
    {
        UpdateIcon(weather);
    }

    private void UpdateIcon(WeatherType weather)
    {
        if (weatherIconImage == null)
        {
            return;
        }

        switch (weather)
        {
            case WeatherType.Sunny:
                weatherIconImage.sprite = sunnySprite;
                break;

            case WeatherType.Chilly:
                weatherIconImage.sprite = chillySprite;
                break;

            case WeatherType.Rainy:
                weatherIconImage.sprite = rainySprite;
                break;
        }

        weatherIconImage.enabled = weatherIconImage.sprite != null;
    }
}
