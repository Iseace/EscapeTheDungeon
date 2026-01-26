using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Aplica habitaciones prefabricadas a las habitaciones generadas
/// </summary>
public class PrefabRoomApplicator
{
    private PrefabRoomConfiguration config;
    private Dictionary<PrefabRoomData, int> usedPrefabs;
    
    public PrefabRoomApplicator(PrefabRoomConfiguration configuration)
    {
        config = configuration;
        usedPrefabs = new Dictionary<PrefabRoomData, int>();
    }
    
    /// <summary>
    /// Intenta asignar prefabs a las habitaciones generadas
    /// </summary>
    public void AssignPrefabsToRooms(List<RoomNode> rooms)
    {
        if (config == null || config.prefabRooms.Count == 0)
            return;
            
        foreach (var room in rooms)
        {
            // Decidir si usar prefab basado en probabilidad
            if (Random.value > config.usePrefabChance && !config.prioritizePrefabs)
                continue;
                
            TryAssignPrefabToRoom(room);
        }
    }
    
    /// <summary>
    /// Intenta asignar un prefab compatible a una habitación específica
    /// </summary>
    private void TryAssignPrefabToRoom(RoomNode room)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;
        
        // Obtener prefabs compatibles
        List<PrefabRoomData> compatible = config.GetCompatiblePrefabs(width, height, room.RoomType);
        
        // Filtrar prefabs que no permiten múltiples instancias
        List<PrefabRoomData> available = new List<PrefabRoomData>();
        foreach (var prefab in compatible)
        {
            if (prefab.allowMultiple || !usedPrefabs.ContainsKey(prefab))
            {
                available.Add(prefab);
            }
        }
        
        if (available.Count == 0)
            return;
            
        // Seleccionar prefab basado en peso
        PrefabRoomData selectedPrefab = config.SelectWeightedPrefab(available);
        
        if (selectedPrefab != null)
        {
            room.AssignedPrefab = selectedPrefab;
            
            // Registrar uso
            if (usedPrefabs.ContainsKey(selectedPrefab))
                usedPrefabs[selectedPrefab]++;
            else
                usedPrefabs[selectedPrefab] = 1;
        }
    }
    
    /// <summary>
    /// Instancia un prefab de habitación en el mundo
    /// </summary>
    public static GameObject InstantiatePrefabRoom(RoomNode room, Transform parent)
    {
        if (room.AssignedPrefab == null || room.AssignedPrefab.roomPrefab == null)
            return null;
            
        // Calcular posición central de la habitación
        Vector3 roomCenter = new Vector3(
            (room.BottomLeftAreaCorner.x + room.TopRightAreaCorner.x) / 2f,
            0f,
            (room.BottomLeftAreaCorner.y + room.TopRightAreaCorner.y) / 2f
        );
        
        // Instanciar el prefab
        GameObject instance = Object.Instantiate(
            room.AssignedPrefab.roomPrefab,
            roomCenter,
            Quaternion.identity,
            parent
        );
        
        instance.name = $"PrefabRoom_{room.AssignedPrefab.roomName}_{room.RoomID}";
        
        // Agregar componente de identificación si no existe
        var roomComponent = instance.GetComponent<PrefabRoomInstance>();
        if (roomComponent == null)
        {
            roomComponent = instance.AddComponent<PrefabRoomInstance>();
        }
        
        roomComponent.Initialize(room, room.AssignedPrefab);
        
        return instance;
    }
}

/// <summary>
/// Componente que se agrega a las instancias de habitaciones prefab
/// </summary>
public class PrefabRoomInstance : MonoBehaviour
{
    public RoomNode RoomData { get; private set; }
    public PrefabRoomData PrefabData { get; private set; }
    
    public void Initialize(RoomNode roomNode, PrefabRoomData prefabData)
    {
        RoomData = roomNode;
        PrefabData = prefabData;
    }
    
    /// <summary>
    /// Obtiene los puntos de conexión en coordenadas del mundo
    /// </summary>
    public List<Vector3> GetWorldConnectionPoints()
    {
        if (PrefabData == null || RoomData == null)
            return new List<Vector3>();
            
        List<Vector3> worldPoints = new List<Vector3>();
        
        foreach (var localPoint in PrefabData.connectionPoints)
        {
            Vector3 worldPoint = new Vector3(
                RoomData.BottomLeftAreaCorner.x + localPoint.x,
                0f,
                RoomData.BottomLeftAreaCorner.y + localPoint.y
            );
            worldPoints.Add(worldPoint);
        }
        
        return worldPoints;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (PrefabData != null)
        {
            // Visualizar puntos de conexión
            Gizmos.color = Color.yellow;
            foreach (var point in GetWorldConnectionPoints())
            {
                Gizmos.DrawWireSphere(point, 0.5f);
            }
            
            // Dibujar bounds de la habitación
            if (RoomData != null)
            {
                Gizmos.color = PrefabData.debugColor;
                Vector3 center = new Vector3(
                    (RoomData.BottomLeftAreaCorner.x + RoomData.TopRightAreaCorner.x) / 2f,
                    0.5f,
                    (RoomData.BottomLeftAreaCorner.y + RoomData.TopRightAreaCorner.y) / 2f
                );
                Vector3 size = new Vector3(
                    RoomData.TopRightAreaCorner.x - RoomData.BottomLeftAreaCorner.x,
                    1f,
                    RoomData.TopRightAreaCorner.y - RoomData.BottomLeftAreaCorner.y
                );
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
