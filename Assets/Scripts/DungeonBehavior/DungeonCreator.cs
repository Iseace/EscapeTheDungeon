using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class DungeonCreator : MonoBehaviour
{
    [HideInInspector]
    public bool useRandomSeed = true;

    [Header("Seed Settings")]
    public int seed = 0;
    [SerializeField] private int lastUsedSeed;

    [Header("Dungeon Settings")]
    public int dungeonWidth;
    public int dungeonLength;
    public int roomWidthMin;
    public int roomLengthMin;
    public int wallHeight = 3;
    public int corridorWidth;
    public int maxIterations;

    [Range(0.0f, 0.3f)]
    public float roomBottomCornerModifier = 0.1f;
    [Range(0.7f, 1.0f)]
    public float roomTopCornerMidifier = 0.9f;
    [Range(0, 2)]
    public int roomOffset = 1;

    [Header("Textures")]
    public Material material;
    public GameObject wallPrefab;
    
    [Header("Wall Generation")]
    [Tooltip("Usar sistema de paredes procedurales optimizado (un solo mesh) en lugar de múltiples prefabs")]
    public bool useProceduralWalls = true;
    public Material wallMaterial;
    
    [Tooltip("Añadir pilares en las esquinas para evitar huecos")]
    public bool useCornerPillars = true;
    
    [Tooltip("Tamaño de los pilares en las esquinas (0.3-0.8 recomendado)")]
    [Range(0.3f, 2.0f)]
    public float cornerPillarSize = 0.6f;

    [Header("Room Types")]
    public RoomTypeConfiguration roomTypeConfiguration;
    public bool enableRoomTypes = true;

    [Header("Room Shapes")]
    [Tooltip("Habilitar formas variadas para las habitaciones (L, T, Cruz, Recesos)")]
    public bool enableVariedShapes = false;
    
    [Tooltip("Configuración de probabilidades y parámetros para las formas de habitaciones")]
    public RoomShapeConfig roomShapeConfig = new RoomShapeConfig();

    [Header("Prefab Rooms")]
    [Tooltip("Habilitar uso de habitaciones prefabricadas completas")]
    public bool enablePrefabRooms = false;
    
    [Tooltip("Configuración de habitaciones prefabricadas")]
    public PrefabRoomConfiguration prefabRoomConfig;

    [Header("Procedural Objects")]
    public bool spawnObjects = true;
    public List<SpawnableObject> genericObjects = new List<SpawnableObject>();

    [Header("Debug")]
    public bool showRoomTypes = true;
    public bool showGrid = false;

    // Referencias privadas
    private DugeonGenerator generator;
    private ProceduralObjectSpawner objectSpawner;
    private List<Vector3Int> possibleDoorVerticalPosition;
    private List<Vector3Int> possibleDoorHorizontalPosition;
    private List<Vector3Int> possibleWallHorizontalPosition;
    private List<Vector3Int> possibleWallVerticalPosition;

    void Start()
    {
        CreateDungeon();
    }

    public void CreateDungeon()
    {
        if (useRandomSeed)
        {
            seed = Random.Range(0, int.MaxValue);
        }

        lastUsedSeed = seed;
        Random.InitState(seed);

        DestroyAllChildren();

        // Crear generador con grid
        generator = new DugeonGenerator(dungeonWidth, dungeonLength);

        var listOfRooms = generator.CalculateDungeon(
            maxIterations,
            roomWidthMin,
            roomLengthMin,
            roomBottomCornerModifier,
            roomTopCornerMidifier,
            roomOffset,
            corridorWidth,
            enableRoomTypes ? roomTypeConfiguration : null,
            enableVariedShapes ? roomShapeConfig : null,
            enablePrefabRooms ? prefabRoomConfig : null
        );

        GameObject wallParent = new GameObject("WallParent");
        wallParent.transform.parent = transform;

        GameObject objectParent = new GameObject("ObjectParent");
        objectParent.transform.parent = transform;

        possibleDoorVerticalPosition = new List<Vector3Int>();
        possibleDoorHorizontalPosition = new List<Vector3Int>();
        possibleWallHorizontalPosition = new List<Vector3Int>();
        possibleWallVerticalPosition = new List<Vector3Int>();

        // Crear meshes y procesar habitaciones
        for (int i = 0; i < listOfRooms.Count; i++)
        {
            if (listOfRooms[i] is RoomNode roomNode)
            {
                // Crear suelo basado en el grid (respeta formas variadas)
                CreateFloorMeshFromGrid(roomNode);
                
                // Prioridad 1: Instanciar habitación prefab completa si está asignada
                if (roomNode.AssignedPrefab != null)
                {
                    PrefabRoomApplicator.InstantiatePrefabRoom(roomNode, objectParent.transform);
                }
                // Prioridad 2: Aplicar prefab del RoomTypeData (sistema antiguo)
                else if (roomNode.RoomTypeData != null)
                {
                    ApplyRoomPrefab(roomNode, objectParent.transform);
                }
            }
            else
            {
                // Para corredores, usar método tradicional
                CreateMesh(listOfRooms[i].BottomLeftAreaCorner, listOfRooms[i].TopRightAreaCorner);
            }
        }

        CreateWalls(wallParent);

        // Spawn objetos procedurales
        if (spawnObjects && generator.RoomList != null)
        {
            objectSpawner = new ProceduralObjectSpawner(generator.Grid, objectParent.transform);
            SpawnAllObjects();
        }

        // Debug visual
        if (showRoomTypes)
        {
            VisualizeRoomTypes();
        }
    }

    private void ApplyRoomPrefab(RoomNode room, Transform parent)
    {
        if (room.RoomTypeData.roomPrefab != null)
        {
            Vector3 roomCenter = new Vector3(
                (room.BottomLeftAreaCorner.x + room.TopRightAreaCorner.x) / 2f,
                0,
                (room.BottomLeftAreaCorner.y + room.TopRightAreaCorner.y) / 2f
            );

            GameObject prefabInstance = Instantiate(
                room.RoomTypeData.roomPrefab,
                roomCenter,
                Quaternion.identity,
                parent
            );

            prefabInstance.name = $"{room.RoomType}_Prefab_{room.RoomID}";
        }
    }

    private void SpawnAllObjects()
    {
        foreach (var room in generator.RoomList)
        {
            // Spawn objetos espec�ficos del tipo de habitaci�n
            objectSpawner.SpawnObjectsInRoom(room);

            // Spawn objetos gen�ricos si est�n configurados
            if (genericObjects.Count > 0)
            {
                objectSpawner.SpawnObjects(room, genericObjects);
            }

            // Spawn especiales seg�n el tipo
            SpawnSpecialObjectsForRoomType(room);
        }
    }

    private void SpawnSpecialObjectsForRoomType(RoomNode room)
    {
        switch (room.RoomType)
        {
            case RoomType.Boss:
                // Ejemplo: spawn boss en el centro
                Debug.Log($"Boss room at {room.RoomID}");
                break;

            case RoomType.Normal:
                // Ejemplo: spawn cofre
                Debug.Log($"Normal room at {room.RoomID}");
                break;

            case RoomType.Start:
                Debug.Log($"Start room at {room.RoomID}");
                break;
        }
    }

    private void VisualizeRoomTypes()
    {
        if (generator.RoomList == null) return;

        foreach (var room in generator.RoomList)
        {
            Color roomColor = room.RoomTypeData?.debugColor ?? Color.white;
            Vector3 center = new Vector3(
                (room.BottomLeftAreaCorner.x + room.TopRightAreaCorner.x) / 2f,
                0.1f,
                (room.BottomLeftAreaCorner.y + room.TopRightAreaCorner.y) / 2f
            );

            Debug.DrawLine(
                new Vector3(room.BottomLeftAreaCorner.x, 0.1f, room.BottomLeftAreaCorner.y),
                new Vector3(room.TopRightAreaCorner.x, 0.1f, room.BottomLeftAreaCorner.y),
                roomColor, 100f
            );
        }
    }

    // M�todos originales mantenidos
    public void CreateDungeonRandom()
    {
        useRandomSeed = true;
        CreateDungeon();
    }

    public void CreateDungeonWithSeed(int specificSeed)
    {
        useRandomSeed = false;
        seed = specificSeed;
        CreateDungeon();
    }

    private void CreateWalls(GameObject wallParent)
    {
        if (useProceduralWalls)
        {
            // Sistema nuevo: generar un solo mesh optimizado usando el Grid
            Material matToUse = wallMaterial != null ? wallMaterial : material;
            ProceduralWallGenerator wallGenerator = new ProceduralWallGenerator(
                wallHeight, 
                matToUse, 
                useCornerPillars, 
                cornerPillarSize
            );
            
            // Usar el grid directamente para detectar bordes - más confiable
            if (generator?.Grid != null)
            {
                GameObject walls = wallGenerator.GenerateWallsFromGrid(
                    generator.Grid,
                    wallParent.transform
                );
            }
            else
            {
                Debug.LogWarning("Grid no disponible, usando método alternativo");
                // Fallback al método anterior si no hay grid
                GameObject walls = wallGenerator.GenerateOptimizedWalls(
                    possibleWallHorizontalPosition,
                    possibleWallVerticalPosition,
                    possibleDoorHorizontalPosition,
                    possibleDoorVerticalPosition,
                    wallParent.transform
                );
            }
        }
        else
        {
            // Sistema antiguo: usar prefabs individuales
            foreach (var wallPosition in possibleWallHorizontalPosition)
            {
                if (!possibleDoorHorizontalPosition.Contains(wallPosition))
                {
                    CreateWall(wallParent, wallPosition, 0f);
                }
            }

            foreach (var wallPosition in possibleWallVerticalPosition)
            {
                if (!possibleDoorVerticalPosition.Contains(wallPosition))
                {
                    CreateWall(wallParent, wallPosition, 0f);
                }
            }
        }
    }

    private void CreateWall(GameObject wallParent, Vector3Int wallPosition, float yRotation)
    {
        Quaternion rotation = Quaternion.Euler(0, yRotation, 0);
        GameObject wall = Instantiate(wallPrefab, wallPosition, rotation, wallParent.transform);

        Vector3 scale = wall.transform.localScale;
        scale.y = wallHeight;
        wall.transform.localScale = scale;
    }

    /// <summary>
    /// Crea el mesh del suelo basándose en el grid (respeta formas variadas como L, T, Cruz)
    /// </summary>
    private void CreateFloorMeshFromGrid(RoomNode room)
    {
        if (generator?.Grid == null)
        {
            // Fallback al método tradicional si no hay grid
            CreateMesh(room.BottomLeftAreaCorner, room.TopRightAreaCorner);
            return;
        }

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // Recorrer cada celda de la habitación en el grid
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                GridCell cell = generator.Grid.GetCell(pos);

                // Solo crear suelo para celdas tipo Floor
                if (cell != null && cell.Type == CellType.Floor)
                {
                    // Crear un quad (2 triángulos) para esta celda
                    int vertexIndex = vertices.Count;

                    // Vértices de la celda (1x1 unidad)
                    vertices.Add(new Vector3(x, 0, y));         // Bottom-left
                    vertices.Add(new Vector3(x + 1, 0, y));     // Bottom-right
                    vertices.Add(new Vector3(x, 0, y + 1));     // Top-left
                    vertices.Add(new Vector3(x + 1, 0, y + 1)); // Top-right

                    // UVs
                    uvs.Add(new Vector2(x, y));
                    uvs.Add(new Vector2(x + 1, y));
                    uvs.Add(new Vector2(x, y + 1));
                    uvs.Add(new Vector2(x + 1, y + 1));

                    // Triángulos (orden correcto para normales hacia arriba)
                    triangles.Add(vertexIndex + 2); // Top-left
                    triangles.Add(vertexIndex + 3); // Top-right
                    triangles.Add(vertexIndex + 0); // Bottom-left

                    triangles.Add(vertexIndex + 0); // Bottom-left
                    triangles.Add(vertexIndex + 3); // Top-right
                    triangles.Add(vertexIndex + 1); // Bottom-right
                }
            }
        }

        // Si no hay vértices, no crear nada
        if (vertices.Count == 0)
            return;

        // Crear el mesh
        Mesh mesh = new Mesh();
        mesh.name = $"RoomFloor_{room.RoomID}";
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();

        // Crear GameObject
        GameObject floorObject = new GameObject(
            $"Floor_{room.RoomType}_{room.RoomID}",
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(MeshCollider)
        );

        floorObject.transform.position = Vector3.zero;
        floorObject.transform.localScale = Vector3.one;
        floorObject.GetComponent<MeshFilter>().mesh = mesh;
        floorObject.GetComponent<MeshRenderer>().material = material;
        floorObject.GetComponent<MeshCollider>().sharedMesh = mesh;
        floorObject.transform.parent = transform;
    }

    private void CreateMesh(Vector2 bottomLeftCorner, Vector2 topRightCorner)
    {
        Vector3 bottomLeftV = new Vector3(bottomLeftCorner.x, 0, bottomLeftCorner.y);
        Vector3 bottomRightV = new Vector3(topRightCorner.x, 0, bottomLeftCorner.y);
        Vector3 topLeftV = new Vector3(bottomLeftCorner.x, 0, topRightCorner.y);
        Vector3 topRightV = new Vector3(topRightCorner.x, 0, topRightCorner.y);

        Vector3[] vertices = new Vector3[]
        {
            topLeftV, topRightV, bottomLeftV, bottomRightV
        };

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        int[] triangles = new int[] { 0, 1, 2, 2, 1, 3 };

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        GameObject dungeonFloor = new GameObject(
            "Mesh" + bottomLeftCorner,
            typeof(MeshFilter),
            typeof(MeshRenderer),
            typeof(MeshCollider)
        );

        dungeonFloor.transform.position = Vector3.zero;
        dungeonFloor.transform.localScale = Vector3.one;
        dungeonFloor.GetComponent<MeshFilter>().mesh = mesh;
        dungeonFloor.GetComponent<MeshRenderer>().material = material;
        dungeonFloor.GetComponent<MeshCollider>().sharedMesh = mesh;
        dungeonFloor.transform.parent = transform;

        for (int row = (int)bottomLeftV.x; row <= (int)bottomRightV.x; row++)
        {
            var wallPosition = new Vector3(row, 0, bottomLeftV.z);
            AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int row = (int)topLeftV.x; row <= (int)topRightCorner.x; row++)
        {
            var wallPosition = new Vector3(row, 0, topRightV.z);
            AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int col = (int)bottomLeftV.z; col <= (int)topLeftV.z; col++)
        {
            var wallPosition = new Vector3(bottomLeftV.x, 0, col);
            AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
        for (int col = (int)bottomRightV.z; col <= (int)topRightV.z; col++)
        {
            var wallPosition = new Vector3(bottomRightV.x, 0, col);
            AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
    }

    private void AddWallPositionToList(Vector3 wallPosition, List<Vector3Int> wallList, List<Vector3Int> doorList)
    {
        Vector3Int point = Vector3Int.CeilToInt(wallPosition);
        if (wallList.Contains(point))
        {
            doorList.Add(point);
            wallList.Remove(point);
        }
        else
        {
            wallList.Add(point);
        }
    }

    public void DestroyAllChildren()
    {
        while (transform.childCount != 0)
        {
            foreach (Transform item in transform)
            {
                DestroyImmediate(item.gameObject);
            }
        }
    }

    // M�todos p�blicos para acceder a informaci�n del dungeon
    public RoomNode GetStartRoom()
    {
        return generator?.GetRoomByType(RoomType.Start);
    }

    public RoomNode GetBossRoom()
    {
        return generator?.GetRoomByType(RoomType.Boss);
    }

    public DungeonGrid GetGrid()
    {
        return generator?.Grid;
    }

    private void OnDrawGizmos()
    {
        if (!showGrid || generator?.Grid == null) return;

        var allCells = generator.Grid.GetAllCells();
        foreach (var kvp in allCells)
        {
            Vector3 pos = new Vector3(kvp.Key.x + 0.5f, 0.1f, kvp.Key.y + 0.5f);

            switch (kvp.Value.Type)
            {
                case CellType.Floor:
                    Gizmos.color = new Color(0, 1, 0, 0.2f);
                    break;
                case CellType.Wall:
                    Gizmos.color = new Color(1, 0, 0, 0.2f);
                    break;
                case CellType.Corridor:
                    Gizmos.color = new Color(0, 0, 1, 0.2f);
                    break;
                default:
                    continue;
            }

            if (kvp.Value.IsOccupied)
            {
                Gizmos.color = new Color(1, 1, 0, 0.5f);
            }

            Gizmos.DrawCube(pos, Vector3.one * 0.8f);
        }
    }
}