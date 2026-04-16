using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement; // Necesario si usas LoadSceneMode

public class RaceManager : NetworkBehaviour
{
    // Usar PlayerRef es la forma "pro" de Fusion
    [Networked] public PlayerRef WinnerPlayerRef { get; set; } = PlayerRef.None;
    [Networked] public int WinnerCharacterIndex { get; set; } = -1;

    [SerializeField] private LapTracker lapTracker;

    public override void Spawned()
    {
        // Esto hace que el objeto persista entre escenas en red
        Runner.MakeDontDestroyOnLoad(gameObject);

        if (lapTracker != null)
            lapTracker.OnRaceFinished += HandleRaceFinished;
    }

    private void HandleRaceFinished(BroomMove racer)
    {
        // Solo el Host/Server decide quién gana
        if (Object.HasStateAuthority && WinnerPlayerRef == PlayerRef.None)
        {
            WinnerPlayerRef = racer.Object.InputAuthority;

            // 1. Intentamos obtener el ID del personaje del PlayerSetup del ganador (Networked variable)
            var playerSetup = racer.GetComponent<PlayerSetup>() ?? racer.GetComponentInChildren<PlayerSetup>();

            if (playerSetup == null)
            {
                if (Runner.TryGetPlayerObject(WinnerPlayerRef, out NetworkObject playerObj))
                {
                    playerSetup = playerObj.GetComponent<PlayerSetup>();
                }
            }

            if (playerSetup != null)
            {
                WinnerCharacterIndex = playerSetup.SelectedCharacterIndex;
                Debug.Log($"[MATCH] Found PlayerSetup for {WinnerPlayerRef}. Character Index: {WinnerCharacterIndex}");
            }
            else
            {
                // FALLBACK A: Intentamos con LocalPlayerSetup (el script que usan los Karts)
                var kartSetup = racer.GetComponent<LocalPlayerSetup>() ?? racer.GetComponentInChildren<LocalPlayerSetup>();
                if (kartSetup != null)
                {
                    WinnerCharacterIndex = kartSetup.SelectedCharacterIndex;
                    Debug.Log($"[MATCH] Winner is using LocalPlayerSetup. Index: {WinnerCharacterIndex}");
                }
                else
                {
                    // FALLBACK B: Si somos el Host y el ganador soy yo, podemos leer PlayerPrefs directamente.
                    if (WinnerPlayerRef == Runner.LocalPlayer)
                    {
                        WinnerCharacterIndex = PlayerPrefs.GetInt("SelectedCharacterID", 0);
                        Debug.Log($"[MATCH] Winner is Local Host. Reading from PlayerPrefs: {WinnerCharacterIndex}");
                    }
                    else
                    {
                        // FALLBACK C: Buscamos por todo el mundo
                        var allSetups = FindObjectsByType<LocalPlayerSetup>(FindObjectsSortMode.None);
                        foreach (var setup in allSetups)
                        {
                            if (setup.Object != null && setup.Object.InputAuthority == WinnerPlayerRef)
                            {
                                WinnerCharacterIndex = setup.SelectedCharacterIndex;
                                Debug.Log($"[MATCH] Found LocalPlayerSetup via search for {WinnerPlayerRef}. Index: {WinnerCharacterIndex}");
                                break;
                            }
                        }

                        if (WinnerCharacterIndex == -1)
                        {
                            Debug.LogWarning($"[MATCH] No se encontró Setup para el ganador remoto {WinnerPlayerRef}. Usando index 0.");
                            WinnerCharacterIndex = 0;
                        }
                    }
                }
            }

            Debug.Log($"[MATCH] Winner determined! Player: {WinnerPlayerRef}, Character Index: {WinnerCharacterIndex}");

            // Esperamos 2 segundos para que no sea un corte brusco
            Invoke(nameof(LoadVictory), 2f);
        }
    }

    private void LoadVictory()
    {
        // En Fusion, la forma más compatible de cargar escenas es por nombre directo o SceneRef
        // Si el Runner tiene un SceneManager configurado, simplemente usa el nombre.
        // Asegúrate de que "RacePodium" esté en las Build Settings.
        try
        {
            Runner.LoadScene("RacePodium");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[FUSION] Error al cargar escena 'RacePodium': {e.Message}. ¿Está añadida a las Build Settings?");
        }
    }
}