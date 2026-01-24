using UnityEngine;

/// <summary>
/// Script de prueba rápida para verificar el sistema de paredes optimizado
/// Añádelo temporalmente a tu escena para probar funcionalidades
/// </summary>
public class WallSystemQuickTest : MonoBehaviour
{
    [Header("Referencias")]
    public DungeonCreator dungeonCreator;

    [Header("Opciones de Prueba")]
    [Tooltip("Regenerar dungeon automáticamente cada X segundos (0 = desactivado)")]
    public float autoRegenerateInterval = 0f;

    [Tooltip("Alternar entre sistemas automáticamente")]
    public bool autoToggleSystems = false;

    [Tooltip("Mostrar info en pantalla")]
    public bool showOnScreenInfo = true;

    private float timer = 0f;
    private bool currentSystem = true;
    private WallSystemStats stats;

    void Start()
    {
        if (dungeonCreator == null)
        {
            dungeonCreator = FindObjectOfType<DungeonCreator>();
        }

        if (dungeonCreator == null)
        {
            Debug.LogError("No se encontró DungeonCreator en la escena!");
            enabled = false;
            return;
        }

        stats = GetComponent<WallSystemStats>();
        if (stats == null)
        {
            stats = gameObject.AddComponent<WallSystemStats>();
            stats.dungeonCreator = dungeonCreator;
        }

        Debug.Log("=== WALL SYSTEM QUICK TEST INICIADO ===");
        Debug.Log("Controles:");
        Debug.Log("  K - Comparar sistemas");
        Debug.Log("  L - Ver estadísticas");
        Debug.Log("  R - Regenerar dungeon");
        Debug.Log("  T - Alternar sistema (Procedural <-> Prefabs)");
        Debug.Log("  I - Mostrar información");
    }

    void Update()
    {
        // Auto-regeneración
        if (autoRegenerateInterval > 0)
        {
            timer += Time.deltaTime;
            if (timer >= autoRegenerateInterval)
            {
                timer = 0f;
                
                if (autoToggleSystems)
                {
                    currentSystem = !currentSystem;
                    dungeonCreator.useProceduralWalls = currentSystem;
                    Debug.Log($"Cambiando a sistema: {(currentSystem ? "PROCEDURAL" : "PREFABS")}");
                }

                RegenerateDungeon();
            }
        }

        // Controles manuales
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("Comparando sistemas...");
            stats.CompareSystemsPerformance();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.Log("Mostrando estadísticas...");
            stats.CalculateStats();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RegenerateDungeon();
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleWallSystem();
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            ShowInfo();
        }
    }

    void RegenerateDungeon()
    {
        Debug.Log($"Regenerando dungeon con sistema {(dungeonCreator.useProceduralWalls ? "PROCEDURAL" : "PREFABS")}...");
        dungeonCreator.CreateDungeon();
        
        // Mostrar estadísticas automáticamente después de regenerar
        Invoke("ShowStatsDelayed", 0.5f);
    }

    void ShowStatsDelayed()
    {
        if (stats != null)
        {
            stats.CalculateStats();
        }
    }

    void ToggleWallSystem()
    {
        dungeonCreator.useProceduralWalls = !dungeonCreator.useProceduralWalls;
        Debug.Log($"Sistema cambiado a: {(dungeonCreator.useProceduralWalls ? "PROCEDURAL ✓" : "PREFABS")}");
        Debug.Log("Presiona R para regenerar con el nuevo sistema");
    }

    void ShowInfo()
    {
        string info = "\n=== INFORMACIÓN DEL SISTEMA ACTUAL ===\n";
        info += $"Sistema activo: {(dungeonCreator.useProceduralWalls ? "PROCEDURAL (Optimizado)" : "PREFABS (Antiguo)")}\n";
        info += $"Wall Height: {dungeonCreator.wallHeight}\n";
        info += $"Material asignado: {(dungeonCreator.wallMaterial != null ? "✓" : "✗")}\n";
        info += $"Prefab asignado: {(dungeonCreator.wallPrefab != null ? "✓" : "✗")}\n";
        info += "\nCONTROLES:\n";
        info += "  K - Comparar sistemas\n";
        info += "  L - Ver estadísticas\n";
        info += "  R - Regenerar dungeon\n";
        info += "  T - Alternar sistema\n";
        info += "  I - Mostrar esta información\n";

        Debug.Log(info);
    }

    void OnGUI()
    {
        if (!showOnScreenInfo) return;

        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperLeft;
        style.padding = new RectOffset(10, 10, 10, 10);

        string displayText = $"<b>Sistema de Paredes:</b> {(dungeonCreator.useProceduralWalls ? "PROCEDURAL ✓" : "PREFABS")}\n";
        displayText += "\n<b>Controles:</b>\n";
        displayText += "K: Comparar | L: Stats | R: Regenerar | T: Toggle | I: Info";

        GUI.Box(new Rect(10, 10, 350, 100), displayText, style);
    }

    // Test de estrés - genera múltiples dungeons y mide el tiempo
    [ContextMenu("Test de Estrés (10 generaciones)")]
    public void StressTest()
    {
        Debug.Log("\n=== INICIANDO TEST DE ESTRÉS ===");

        // Test con sistema procedural
        dungeonCreator.useProceduralWalls = true;
        float proceduralTime = MeasureGenerationTime(10);

        // Test con sistema antiguo
        dungeonCreator.useProceduralWalls = false;
        float prefabTime = MeasureGenerationTime(10);

        // Resultados
        float improvement = ((prefabTime - proceduralTime) / prefabTime) * 100f;
        Debug.Log($"\n=== RESULTADOS DEL TEST DE ESTRÉS ===");
        Debug.Log($"Sistema Procedural: {proceduralTime:F2}ms promedio");
        Debug.Log($"Sistema Prefabs: {prefabTime:F2}ms promedio");
        Debug.Log($"Mejora de rendimiento: {improvement:F1}%");

        // Restaurar sistema procedural
        dungeonCreator.useProceduralWalls = true;
    }

    private float MeasureGenerationTime(int iterations)
    {
        float totalTime = 0f;

        for (int i = 0; i < iterations; i++)
        {
            float startTime = Time.realtimeSinceStartup;
            dungeonCreator.CreateDungeon();
            float endTime = Time.realtimeSinceStartup;
            
            totalTime += (endTime - startTime) * 1000f; // Convertir a ms
        }

        return totalTime / iterations;
    }
}
