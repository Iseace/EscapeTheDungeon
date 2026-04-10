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
            }
            else
            {
                // 2. FALLBACK ESPECIAL: Si somos el Host y el ganador soy yo, podemos leer PlayerPrefs directamente.
                // Si el ganador es un cliente y no encontramos su PlayerSetup, el Host no tiene acceso a sus PlayerPrefs.
                if (WinnerPlayerRef == Runner.LocalPlayer)
                {
                    WinnerCharacterIndex = PlayerPrefs.GetInt("SelectedCharacterID", 0);
                    Debug.Log($"[MATCH] Winner is Local Host. Reading from PlayerPrefs: {WinnerCharacterIndex}");
                }
                else
                {
                    Debug.LogWarning($"[MATCH] No se encontró PlayerSetup para el ganador remoto {WinnerPlayerRef}. Usando index 0 por defecto.");
                    WinnerCharacterIndex = 0;
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