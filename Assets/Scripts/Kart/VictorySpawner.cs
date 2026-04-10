using Fusion;
using UnityEngine;

public class VictorySpawner : NetworkBehaviour
{
    [Header("Winner Display")]
    [SerializeField] private GameObject[] characterSkins; // Lista de prefabs/objetos en la escena para cada ID

    private bool _hasSetup = false;

    // Usamos Spawned en lugar de FixedUpdateNetwork para reaccionar al entrar en la escena
    public override void Spawned()
    {
        // Si ya estamos en la escena y hay un ganador, intentamos configurar
        CheckAndSetup();
    }

    public override void FixedUpdateNetwork()
    {
        if (!_hasSetup)
        {
            CheckAndSetup();
        }
    }

    private void CheckAndSetup()
    {
        if (_hasSetup) return;
        if (Runner == null) return; // Safety check

        // 1. Buscamos al RaceManager
        var raceManager = FindAnyObjectByType<RaceManager>();
        if (raceManager == null || raceManager.WinnerPlayerRef == PlayerRef.None)
        {
            // Opcional: Log de depuración para saber por qué no avanza
            // Debug.Log("[VICTORY] Esperando que el RaceManager tenga un ganador...");
            return;
        }

        // Si el Runner.LocalPlayer aún no es válido, esperamos el siguiente frame
        if (Runner.LocalPlayer == PlayerRef.None) return;

        // Log para depuración
        Debug.Log($"[VICTORY] Intentando configurar. Local: {Runner.LocalPlayer}, Winner: {raceManager.WinnerPlayerRef}, Index: {raceManager.WinnerCharacterIndex}");

        _hasSetup = true;

        // 2. Lógica de Display
        // TODOS activan la skin del personaje que ganó
        int skinId = raceManager.WinnerCharacterIndex;
        if (skinId >= 0 && skinId < characterSkins.Length)
        {
            GameObject selectedSkin = characterSkins[skinId];
            if (selectedSkin != null)
            {
                selectedSkin.SetActive(true);
                Debug.Log($"[VICTORY] Activando skin index {skinId} para el ganador.");
            }
        }
        else
        {
            Debug.LogWarning($"[VICTORY] ID de skin inválida ({skinId}). WinnerPlayerRef: {raceManager.WinnerPlayerRef}. ¿Están asignadas en el array?");
        }

        // 3. Lógica de Tomato Enabling
        if (Runner.LocalPlayer != raceManager.WinnerPlayerRef)
        {
            Debug.Log($"[VICTORY] No soy el ganador. Activando lanzador de tomates...");
            SetupLoserCamera();
        }
    }

    private void SetupLoserCamera()
    {
        // El VictorySpawner debe estar en un objeto con un NetworkObject
        // para que cada cliente tenga su propia instancia que controle su TomatoThrower.
        Debug.Log($"[VICTORY] Activando lanzador de tomates para {Runner.LocalPlayer}");

        var thrower = GetComponent<TomatoThrower>();
        if (thrower != null)
        {
            thrower.enabled = true;
        }
    }
}