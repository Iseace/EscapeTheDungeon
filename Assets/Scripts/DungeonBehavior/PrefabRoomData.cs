using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Define una habitación prefabricada con tamaño específico y puntos de conexión
/// </summary>
[CreateAssetMenu(fileName = "PrefabRoom", menuName = "Dungeon/Prefab Room Data")]
public class PrefabRoomData : ScriptableObject
{
    [Header("Prefab Settings")]
    [Tooltip("El prefab de la habitación completa")]
    public GameObject roomPrefab;
    
    [Tooltip("Nombre descriptivo de la habitación")]
    public string roomName;
    
    [Header("Size Requirements")]
    [Tooltip("Ancho mínimo requerido para esta habitación")]
    public int requiredWidth = 8;
    
    [Tooltip("Alto mínimo requerido para esta habitación")]
    public int requiredHeight = 8;
    
    [Tooltip("Si es true, la habitación debe ser exactamente del tamaño especificado")]
    public bool exactSizeOnly = false;
    
    [Header("Connection Points")]
    [Tooltip("Posiciones relativas donde se pueden conectar corredores (en grid local)")]
    public List<Vector2Int> connectionPoints = new List<Vector2Int>();
    
    [Header("Spawn Settings")]
    [Tooltip("Peso de spawn - mayor número = mayor probabilidad")]
    [Range(0f, 100f)]
    public float spawnWeight = 10f;
    
    [Tooltip("¿Se puede usar múltiples veces en el mismo dungeon?")]
    public bool allowMultiple = true;
    
    [Tooltip("Tipos de habitación compatibles (vacío = cualquiera)")]
    public List<RoomType> compatibleRoomTypes = new List<RoomType>();
    
    [Header("Preview")]
    [Tooltip("Color para visualización en debug")]
    public Color debugColor = Color.cyan;
    
    /// <summary>
    /// Verifica si esta habitación prefab es compatible con el tamaño dado
    /// </summary>
    public bool IsCompatibleWithSize(int width, int height)
    {
        if (exactSizeOnly)
        {
            return width == requiredWidth && height == requiredHeight;
        }
        else
        {
            return width >= requiredWidth && height >= requiredHeight;
        }
    }
    
    /// <summary>
    /// Verifica si es compatible con un tipo de habitación específico
    /// </summary>
    public bool IsCompatibleWithRoomType(RoomType roomType)
    {
        if (compatibleRoomTypes.Count == 0)
            return true; // Compatible con todos si la lista está vacía
            
        return compatibleRoomTypes.Contains(roomType);
    }
}

/// <summary>
/// Configuración de habitaciones prefabricadas para el generador
/// </summary>
[CreateAssetMenu(fileName = "PrefabRoomConfig", menuName = "Dungeon/Prefab Room Configuration")]
public class PrefabRoomConfiguration : ScriptableObject
{
    [Header("Prefab Rooms")]
    [Tooltip("Lista de habitaciones prefabricadas disponibles")]
    public List<PrefabRoomData> prefabRooms = new List<PrefabRoomData>();
    
    [Header("General Settings")]
    [Tooltip("Probabilidad general de usar prefabs (0-1)")]
    [Range(0f, 1f)]
    public float usePrefabChance = 0.3f;
    
    [Tooltip("Priorizar prefabs sobre generación procedural cuando sea posible")]
    public bool prioritizePrefabs = false;
    
    /// <summary>
    /// Obtiene habitaciones prefab compatibles con un tamaño y tipo específico
    /// </summary>
    public List<PrefabRoomData> GetCompatiblePrefabs(int width, int height, RoomType roomType)
    {
        List<PrefabRoomData> compatible = new List<PrefabRoomData>();
        
        foreach (var prefab in prefabRooms)
        {
            if (prefab.roomPrefab != null && 
                prefab.IsCompatibleWithSize(width, height) && 
                prefab.IsCompatibleWithRoomType(roomType))
            {
                compatible.Add(prefab);
            }
        }
        
        return compatible;
    }
    
    /// <summary>
    /// Selecciona un prefab aleatorio basado en pesos
    /// </summary>
    public PrefabRoomData SelectWeightedPrefab(List<PrefabRoomData> options)
    {
        if (options == null || options.Count == 0)
            return null;
        
        float totalWeight = 0f;
        foreach (var option in options)
        {
            totalWeight += option.spawnWeight;
        }
        
        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        
        foreach (var option in options)
        {
            cumulative += option.spawnWeight;
            if (randomValue <= cumulative)
            {
                return option;
            }
        }
        
        return options[options.Count - 1];
    }
}
