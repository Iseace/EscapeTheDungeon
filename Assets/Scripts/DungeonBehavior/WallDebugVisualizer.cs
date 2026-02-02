using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Herramienta de debug para visualizar las posiciones de paredes y puertas
/// Ayuda a identificar huecos y problemas de alineación
/// </summary>
public class WallDebugVisualizer : MonoBehaviour
{
    [Header("Referencias")]
    public DungeonCreator dungeonCreator;

    [Header("Configuración de Visualización")]
    public bool showWallPositions = true;
    public bool showDoorPositions = true;
    public bool showWallNumbers = false;
    public float gizmoSize = 0.3f;

    [Header("Colores")]
    public Color horizontalWallColor = Color.red;
    public Color verticalWallColor = Color.blue;
    public Color horizontalDoorColor = Color.green;
    public Color verticalDoorColor = Color.cyan;

    private void OnDrawGizmos()
    {
        if (dungeonCreator == null) return;
        if (!Application.isPlaying) return;

        // Usar reflexión para acceder a las listas privadas (solo para debug)
        var dungeonCreatorType = dungeonCreator.GetType();
        
        var horizontalWallsField = dungeonCreatorType.GetField("possibleWallHorizontalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var verticalWallsField = dungeonCreatorType.GetField("possibleWallVerticalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var horizontalDoorsField = dungeonCreatorType.GetField("possibleDoorHorizontalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var verticalDoorsField = dungeonCreatorType.GetField("possibleDoorVerticalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (horizontalWallsField == null) return;

        var horizontalWalls = horizontalWallsField.GetValue(dungeonCreator) as List<Vector3Int>;
        var verticalWalls = verticalWallsField.GetValue(dungeonCreator) as List<Vector3Int>;
        var horizontalDoors = horizontalDoorsField.GetValue(dungeonCreator) as List<Vector3Int>;
        var verticalDoors = verticalDoorsField.GetValue(dungeonCreator) as List<Vector3Int>;

        // Dibujar paredes horizontales
        if (showWallPositions && horizontalWalls != null)
        {
            Gizmos.color = horizontalWallColor;
            for (int i = 0; i < horizontalWalls.Count; i++)
            {
                Vector3 pos = new Vector3(horizontalWalls[i].x, 0.5f, horizontalWalls[i].z);
                Gizmos.DrawCube(pos, Vector3.one * gizmoSize);
                
                if (showWallNumbers)
                {
                    DrawLabel(pos, $"H{i}");
                }
            }
        }

        // Dibujar paredes verticales
        if (showWallPositions && verticalWalls != null)
        {
            Gizmos.color = verticalWallColor;
            for (int i = 0; i < verticalWalls.Count; i++)
            {
                Vector3 pos = new Vector3(verticalWalls[i].x, 0.5f, verticalWalls[i].z);
                Gizmos.DrawCube(pos, Vector3.one * gizmoSize);
                
                if (showWallNumbers)
                {
                    DrawLabel(pos, $"V{i}");
                }
            }
        }

        // Dibujar puertas horizontales
        if (showDoorPositions && horizontalDoors != null)
        {
            Gizmos.color = horizontalDoorColor;
            foreach (var door in horizontalDoors)
            {
                Vector3 pos = new Vector3(door.x, 0.5f, door.z);
                Gizmos.DrawWireSphere(pos, gizmoSize * 1.5f);
            }
        }

        // Dibujar puertas verticales
        if (showDoorPositions && verticalDoors != null)
        {
            Gizmos.color = verticalDoorColor;
            foreach (var door in verticalDoors)
            {
                Vector3 pos = new Vector3(door.x, 0.5f, door.z);
                Gizmos.DrawWireSphere(pos, gizmoSize * 1.5f);
            }
        }
    }

    private void DrawLabel(Vector3 position, string text)
    {
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(position + Vector3.up * 0.5f, text);
        #endif
    }

    [ContextMenu("Imprimir Estadísticas de Paredes")]
    public void PrintWallStats()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("El juego debe estar corriendo");
            return;
        }

        var dungeonCreatorType = dungeonCreator.GetType();
        
        var horizontalWallsField = dungeonCreatorType.GetField("possibleWallHorizontalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var verticalWallsField = dungeonCreatorType.GetField("possibleWallVerticalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var horizontalDoorsField = dungeonCreatorType.GetField("possibleDoorHorizontalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var verticalDoorsField = dungeonCreatorType.GetField("possibleDoorVerticalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var horizontalWalls = horizontalWallsField.GetValue(dungeonCreator) as List<Vector3Int>;
        var verticalWalls = verticalWallsField.GetValue(dungeonCreator) as List<Vector3Int>;
        var horizontalDoors = horizontalDoorsField.GetValue(dungeonCreator) as List<Vector3Int>;
        var verticalDoors = verticalDoorsField.GetValue(dungeonCreator) as List<Vector3Int>;

        Debug.Log("=== ESTADÍSTICAS DE PAREDES ===");
        Debug.Log($"Paredes Horizontales: {horizontalWalls?.Count ?? 0}");
        Debug.Log($"Paredes Verticales: {verticalWalls?.Count ?? 0}");
        Debug.Log($"Puertas Horizontales: {horizontalDoors?.Count ?? 0}");
        Debug.Log($"Puertas Verticales: {verticalDoors?.Count ?? 0}");
        Debug.Log($"Total de paredes: {(horizontalWalls?.Count ?? 0) + (verticalWalls?.Count ?? 0)}");
        Debug.Log($"Total de puertas: {(horizontalDoors?.Count ?? 0) + (verticalDoors?.Count ?? 0)}");
    }

    [ContextMenu("Detectar Huecos en Paredes")]
    public void DetectWallGaps()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("El juego debe estar corriendo");
            return;
        }

        var dungeonCreatorType = dungeonCreator.GetType();
        
        var horizontalWallsField = dungeonCreatorType.GetField("possibleWallHorizontalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var verticalWallsField = dungeonCreatorType.GetField("possibleWallVerticalPosition", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var horizontalWalls = horizontalWallsField.GetValue(dungeonCreator) as List<Vector3Int>;
        var verticalWalls = verticalWallsField.GetValue(dungeonCreator) as List<Vector3Int>;

        Debug.Log("=== DETECCIÓN DE HUECOS ===");

        // Detectar huecos en paredes horizontales
        if (horizontalWalls != null && horizontalWalls.Count > 0)
        {
            var sorted = new List<Vector3Int>(horizontalWalls);
            sorted.Sort((a, b) =>
            {
                int zCompare = a.z.CompareTo(b.z);
                return zCompare != 0 ? zCompare : a.x.CompareTo(b.x);
            });

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                // Si están en la misma línea Z
                if (sorted[i].z == sorted[i + 1].z)
                {
                    int gap = sorted[i + 1].x - sorted[i].x;
                    if (gap > 1)
                    {
                        Debug.LogWarning($"Hueco detectado en pared horizontal entre {sorted[i]} y {sorted[i + 1]} (gap: {gap})");
                    }
                }
            }
        }

        // Detectar huecos en paredes verticales
        if (verticalWalls != null && verticalWalls.Count > 0)
        {
            var sorted = new List<Vector3Int>(verticalWalls);
            sorted.Sort((a, b) =>
            {
                int xCompare = a.x.CompareTo(b.x);
                return xCompare != 0 ? xCompare : a.z.CompareTo(b.z);
            });

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                // Si están en la misma línea X
                if (sorted[i].x == sorted[i + 1].x)
                {
                    int gap = sorted[i + 1].z - sorted[i].z;
                    if (gap > 1)
                    {
                        Debug.LogWarning($"Hueco detectado en pared vertical entre {sorted[i]} y {sorted[i + 1]} (gap: {gap})");
                    }
                }
            }
        }

        Debug.Log("Detección de huecos completada. Revisa los warnings arriba.");
    }
}
