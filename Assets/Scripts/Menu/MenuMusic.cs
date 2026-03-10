using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuMusic : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string stopAtScene = "Game";
    [SerializeField] private float fadeDuration = 1.5f; // Seconds to fade out

    private AudioSource _audioSource;
    private static MenuMusic _instance;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        _audioSource = GetComponent<AudioSource>();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == stopAtScene)
            StartCoroutine(FadeOutAndDestroy());
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float startVolume = _audioSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
            yield return null;
        }

        _audioSource.Stop();
        _instance = null;
        Destroy(gameObject);
    }
}