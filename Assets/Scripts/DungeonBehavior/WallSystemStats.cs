using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra estadísticas de rendimiento para comparar el sistema antiguo vs el nuevo
/// </summary>
public class WallSystemStats : MonoBehaviour
{
    [Header("Referencias")]
    public DungeonCreator dungeonCreator;
    
    [Header("UI (Opcional)")]
    public Text statsText;

    private int totalWallObjects = 0;
    private int totalVertices = 0;
    private int totalTriangles = 0;

    void Start()
    {
        if (dungeonCreator == null)
        {
            dungeonCreator = FindObjectOfType<DungeonCreator>();
        }
    }

    /// <summary>
    /// Cuenta las estadísticas del sistema de paredes actual
    /// </summary>
    public void CalculateStats()
    {
        totalWallObjects = 0;
        totalVertices = 0;
        totalTriangles = 0;

        // Buscar el WallParent
        Transform wallParent = null;
        foreach (Transform child in dungeonCreator.transform)
        {
            if (child.name.Contains("Wall"))
            {
                wallParent = child;
                break;
            }
        }

        if (wallParent == null)
        {
            Debug.LogWarning("No se encontró WallParent");
            return;
        }

        // Contar objetos y estadísticas
        CountWallStats(wallParent);

        // Mostrar resultados
        string stats = $"=== ESTADÍSTICAS DE PAREDES ===\n" +
                      $"Sistema: {(dungeonCreator.useProceduralWalls ? "PROCEDURAL (Optimizado)" : "PREFABS (Antiguo)")}\n" +
                      $"Objetos de pared: {totalWallObjects}\n" +
                      $"Vértices totales: {totalVertices}\n" +
                      $"Triángulos totales: {totalTriangles}\n" +
                      $"Draw Calls estimados: {totalWallObjects}\n" +
                      $"Memoria estimada: {(totalVertices * 48) / 1024f:F2} KB"; // 48 bytes por vértice aprox.

        Debug.Log(stats);

        if (statsText != null)
        {
            statsText.text = stats;
        }
    }

    private void CountWallStats(Transform parent)
    {
        totalWallObjects += parent.childCount;

        foreach (Transform child in parent)
        {
            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                totalVertices += meshFilter.sharedMesh.vertexCount;
                totalTriangles += meshFilter.sharedMesh.triangles.Length / 3;
            }

            // Recursivo para hijos
            if (child.childCount > 0)
            {
                CountWallStats(child);
            }
        }
    }

    /// <summary>
    /// Compara ambos sistemas generando el dungeon con cada uno
    /// </summary>
    [ContextMenu("Comparar Sistemas")]
    public void CompareSystemsPerformance()
    {
        if (dungeonCreator == null)
        {
            Debug.LogError("No hay DungeonCreator asignado");
            return;
        }

        Debug.Log("=== INICIANDO COMPARACIÓN ===\n");

        // Probar sistema antiguo
        Debug.Log("Probando sistema ANTIGUO (Prefabs)...");
        dungeonCreator.useProceduralWalls = false;
        dungeonCreator.CreateDungeon();
        System.Threading.Thread.Sleep(100); // Dar tiempo para que se cree
        CalculateStats();

        // Guardar resultados antiguos
        int oldObjects = totalWallObjects;
        int oldVertices = totalVertices;
        int oldTriangles = totalTriangles;

        // Probar sistema nuevo
        Debug.Log("\nProbando sistema NUEVO (Procedural)...");
        dungeonCreator.useProceduralWalls = true;
        dungeonCreator.CreateDungeon();
        System.Threading.Thread.Sleep(100);
        CalculateStats();

        // Comparación
        float objectReduction = (1f - (float)totalWallObjects / oldObjects) * 100f;
        float vertexReduction = (1f - (float)totalVertices / oldVertices) * 100f;

        string comparison = $"\n=== RESULTADO DE LA COMPARACIÓN ===\n" +
                          $"Reducción de objetos: {objectReduction:F1}% ({oldObjects} → {totalWallObjects})\n" +
                          $"Reducción de vértices: {vertexReduction:F1}% ({oldVertices} → {totalVertices})\n" +
                          $"Mejora en Draw Calls: ~{objectReduction:F1}%\n" +
                          $"Mejora de rendimiento estimada: {objectReduction * 0.8f:F1}%";

        Debug.Log(comparison);
    }

    void Update()
    {
        // Presiona L para ver estadísticas
        if (Input.GetKeyDown(KeyCode.L))
        {
            CalculateStats();
        }

        // Presiona K para comparar sistemas
        if (Input.GetKeyDown(KeyCode.K))
        {
            CompareSystemsPerformance();
        }
    }
}
