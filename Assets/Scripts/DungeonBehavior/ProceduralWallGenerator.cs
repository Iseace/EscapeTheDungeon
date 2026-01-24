using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Genera paredes procedurales como un solo mesh optimizado en lugar de múltiples cubos.
/// Crea huecos automáticamente donde hay puertas.
/// </summary>
public class ProceduralWallGenerator
{
    private int wallHeight;
    private Material wallMaterial;
    private List<Vector3> vertices;
    private List<int> triangles;
    private List<Vector2> uvs;
    
    // Configuración de pilares en esquinas
    private bool useCornerPillars = true;
    private float cornerPillarSize = 0.6f;

    public ProceduralWallGenerator(int wallHeight, Material material, bool useCornerPillars = true, float cornerPillarSize = 0.6f)
    {
        this.wallHeight = wallHeight;
        this.wallMaterial = material;
        this.useCornerPillars = useCornerPillars;
        this.cornerPillarSize = cornerPillarSize;
        vertices = new List<Vector3>();
        triangles = new List<int>();
        uvs = new List<Vector2>();
    }

    /// <summary>
    /// Genera paredes usando el DungeonGrid directamente
    /// Este método es más confiable porque detecta automáticamente los bordes
    /// </summary>
    public GameObject GenerateWallsFromGrid(DungeonGrid grid, Transform parent)
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        var allCells = grid.GetAllCells();
        if (allCells == null || allCells.Count == 0)
        {
            Debug.LogError("El grid está vacío!");
            return null;
        }

        HashSet<WallSegment> horizontalWallSegments = new HashSet<WallSegment>();
        HashSet<WallSegment> verticalWallSegments = new HashSet<WallSegment>();

        // Recorrer todas las celdas del grid
        foreach (var kvp in allCells)
        {
            Vector2Int pos = kvp.Key;
            GridCell cell = kvp.Value;

            // Solo procesar celdas que son transitables (Floor, Corridor, Door)
            if (cell.Type == CellType.Floor || cell.Type == CellType.Corridor)
            {
                // Verificar cada dirección para detectar bordes
                
                // Norte (arriba, +Z)
                GridCell northCell = grid.GetCell(pos + Vector2Int.up);
                if (ShouldPlaceWall(northCell))
                {
                    // Pared horizontal en el borde norte
                    horizontalWallSegments.Add(new WallSegment
                    {
                        start = new Vector3Int(pos.x, 0, pos.y + 1),
                        length = 1,
                        isHorizontal = true
                    });
                }

                // Sur (abajo, -Z)
                GridCell southCell = grid.GetCell(pos + Vector2Int.down);
                if (ShouldPlaceWall(southCell))
                {
                    // Pared horizontal en el borde sur
                    horizontalWallSegments.Add(new WallSegment
                    {
                        start = new Vector3Int(pos.x, 0, pos.y),
                        length = 1,
                        isHorizontal = true
                    });
                }

                // Este (derecha, +X)
                GridCell eastCell = grid.GetCell(pos + Vector2Int.right);
                if (ShouldPlaceWall(eastCell))
                {
                    // Pared vertical en el borde este
                    verticalWallSegments.Add(new WallSegment
                    {
                        start = new Vector3Int(pos.x + 1, 0, pos.y),
                        length = 1,
                        isHorizontal = false
                    });
                }

                // Oeste (izquierda, -X)
                GridCell westCell = grid.GetCell(pos + Vector2Int.left);
                if (ShouldPlaceWall(westCell))
                {
                    // Pared vertical en el borde oeste
                    verticalWallSegments.Add(new WallSegment
                    {
                        start = new Vector3Int(pos.x, 0, pos.y),
                        length = 1,
                        isHorizontal = false
                    });
                }
            }
        }

        // Optimizar: combinar segmentos adyacentes
        var optimizedHorizontal = OptimizeSegments(horizontalWallSegments, true);
        var optimizedVertical = OptimizeSegments(verticalWallSegments, false);

        // Generar geometría
        foreach (var segment in optimizedHorizontal)
        {
            if (segment.length == 1)
            {
                AddHorizontalWallSegment(segment.start);
            }
            else
            {
                AddHorizontalWallStretch(segment.start, segment.length);
            }
        }

        foreach (var segment in optimizedVertical)
        {
            if (segment.length == 1)
            {
                AddVerticalWallSegment(segment.start);
            }
            else
            {
                AddVerticalWallStretch(segment.start, segment.length);
            }
        }

        // Detectar y añadir pilares en las esquinas
        if (useCornerPillars)
        {
            AddCornerPillars(allCells, grid);
        }

        // Crear el GameObject con el mesh combinado
        GameObject wallObject = new GameObject("ProceduralWalls_FromGrid");
        wallObject.transform.parent = parent;
        wallObject.transform.position = Vector3.zero;

        MeshFilter meshFilter = wallObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = wallObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = wallObject.AddComponent<MeshCollider>();

        Mesh wallMesh = new Mesh();
        wallMesh.name = "GridBasedWallsMesh";
        wallMesh.vertices = vertices.ToArray();
        wallMesh.triangles = triangles.ToArray();
        wallMesh.uv = uvs.ToArray();
        
        wallMesh.RecalculateNormals();
        wallMesh.RecalculateBounds();
        wallMesh.Optimize();

        meshFilter.mesh = wallMesh;
        meshRenderer.material = wallMaterial;
        meshCollider.sharedMesh = wallMesh;

        Debug.Log($"Generated walls from grid: {optimizedHorizontal.Count + optimizedVertical.Count} segments, " +
                  $"{vertices.Count} vertices, {triangles.Count / 3} triangles");

        return wallObject;
    }

    /// <summary>
    /// Detecta esquinas y añade pilares para rellenar huecos
    /// </summary>
    private void AddCornerPillars(Dictionary<Vector2Int, GridCell> allCells, DungeonGrid grid)
    {
        HashSet<Vector2Int> addedPillars = new HashSet<Vector2Int>();

        foreach (var kvp in allCells)
        {
            Vector2Int pos = kvp.Key;
            GridCell cell = kvp.Value;

            // Solo procesar celdas transitables
            if (cell.Type != CellType.Floor && cell.Type != CellType.Corridor)
                continue;

            // Verificar las 4 esquinas de esta celda
            // Cada esquina se verifica viendo si hay un cambio de dirección de 90 grados
            
            // Esquina Suroeste (abajo-izquierda)
            CheckCorner(pos, grid, Vector2Int.zero, Vector2Int.left, Vector2Int.down, addedPillars);
            
            // Esquina Sureste (abajo-derecha)
            CheckCorner(pos, grid, new Vector2Int(1, 0), Vector2Int.right, Vector2Int.down, addedPillars);
            
            // Esquina Noroeste (arriba-izquierda)
            CheckCorner(pos, grid, new Vector2Int(0, 1), Vector2Int.left, Vector2Int.up, addedPillars);
            
            // Esquina Noreste (arriba-derecha)
            CheckCorner(pos, grid, new Vector2Int(1, 1), Vector2Int.right, Vector2Int.up, addedPillars);
        }
    }

    /// <summary>
    /// Verifica si una esquina específica necesita un pilar
    /// </summary>
    private void CheckCorner(Vector2Int cellPos, DungeonGrid grid, Vector2Int cornerOffset,
                            Vector2Int dir1, Vector2Int dir2, HashSet<Vector2Int> addedPillars)
    {
        Vector2Int cornerWorldPos = cellPos + cornerOffset;
        
        // No procesar si ya añadimos un pilar aquí
        if (addedPillars.Contains(cornerWorldPos))
            return;

        // La esquina está entre esta celda y 3 celdas adyacentes
        GridCell currentCell = grid.GetCell(cellPos);
        GridCell cell1 = grid.GetCell(cellPos + dir1);
        GridCell cell2 = grid.GetCell(cellPos + dir2);
        GridCell cellDiagonal = grid.GetCell(cellPos + dir1 + dir2);

        bool currentWalkable = IsWalkable(currentCell);
        bool cell1Walkable = IsWalkable(cell1);
        bool cell2Walkable = IsWalkable(cell2);
        bool diagonalWalkable = IsWalkable(cellDiagonal);

        // Esquina exterior: esta celda es transitable, pero las dos direcciones perpendiculares no lo son
        bool isOuterCorner = currentWalkable && !cell1Walkable && !cell2Walkable;

        // Esquina interior: esta celda no es transitable, pero las dos direcciones perpendiculares sí lo son
        bool isInnerCorner = !currentWalkable && cell1Walkable && cell2Walkable && diagonalWalkable;

        // Esquina en conexión: detectar donde hay un cambio de dirección en áreas transitables
        // Esto cubre pasillos, habitaciones, y transiciones entre ellos
        bool isConnectionCorner = false;
        if (currentWalkable)
        {
            // Detectar giros: hay área transitable en ambas direcciones pero no en la diagonal
            // O hay pared en ambas direcciones (esquina exterior)
            if (!cell1Walkable && !cell2Walkable)
            {
                // Esquina exterior (ya cubierto por isOuterCorner, pero lo dejamos por claridad)
                isConnectionCorner = true;
            }
            else if (cell1Walkable && cell2Walkable && !diagonalWalkable)
            {
                // Giro en L: hay área transitable a ambos lados pero la diagonal es pared
                // Esto detecta giros de pasillos Y conexiones habitación-pasillo
                isConnectionCorner = true;
            }
        }

        // Caso especial: esquina en transición pasillo-habitación
        // Cuando hay Floor en una dirección y Corridor en otra perpendicular
        bool isTransitionCorner = false;
        if (currentWalkable && cell1Walkable && cell2Walkable)
        {
            // Verificar si hay una transición entre diferentes tipos de celdas transitables
            CellType currentType = currentCell?.Type ?? CellType.Empty;
            CellType type1 = cell1?.Type ?? CellType.Empty;
            CellType type2 = cell2?.Type ?? CellType.Empty;
            
            bool hasFloorOrCorridor = (currentType == CellType.Floor || currentType == CellType.Corridor) &&
                                     (type1 == CellType.Floor || type1 == CellType.Corridor) &&
                                     (type2 == CellType.Floor || type2 == CellType.Corridor);
            
            // Si hay mezcla de Floor y Corridor, y la diagonal no es transitable, es una esquina
            if (hasFloorOrCorridor && !diagonalWalkable)
            {
                bool hasMix = (currentType != type1 || currentType != type2 || type1 != type2);
                if (hasMix)
                {
                    isTransitionCorner = true;
                }
            }
        }

        if (isOuterCorner || isInnerCorner || isConnectionCorner || isTransitionCorner)
        {
            AddCornerPillarMesh(new Vector3(cornerWorldPos.x, 0, cornerWorldPos.y));
            addedPillars.Add(cornerWorldPos);
        }
    }

    /// <summary>
    /// Verifica si una celda es transitable
    /// </summary>
    private bool IsWalkable(GridCell cell)
    {
        if (cell == null) return false;
        return cell.Type == CellType.Floor || cell.Type == CellType.Corridor || cell.Type == CellType.Door;
    }

    /// <summary>
    /// Añade un pilar en una esquina
    /// </summary>
    private void AddCornerPillarMesh(Vector3 position)
    {
        // Pilar más grueso que las paredes para cubrir bien la esquina
        float size = cornerPillarSize;
        float height = wallHeight;

        // Crear un cubo centrado en la posición de la esquina
        Vector3 v0 = position + new Vector3(-size / 2, 0, -size / 2);
        Vector3 v1 = position + new Vector3(size / 2, 0, -size / 2);
        Vector3 v2 = position + new Vector3(size / 2, 0, size / 2);
        Vector3 v3 = position + new Vector3(-size / 2, 0, size / 2);
        Vector3 v4 = position + new Vector3(-size / 2, height, -size / 2);
        Vector3 v5 = position + new Vector3(size / 2, height, -size / 2);
        Vector3 v6 = position + new Vector3(size / 2, height, size / 2);
        Vector3 v7 = position + new Vector3(-size / 2, height, size / 2);

        AddCubeFaces(v0, v1, v2, v3, v4, v5, v6, v7);
    }

    /// <summary>
    /// Determina si se debe colocar una pared basándose en el tipo de celda adyacente
    /// </summary>
    private bool ShouldPlaceWall(GridCell cell)
    {
        // Colocar pared si la celda es null (fuera del grid), Empty, o Wall
        if (cell == null) return true;
        if (cell.Type == CellType.Empty) return true;
        if (cell.Type == CellType.Wall) return true;
        
        // No colocar pared si es Floor, Corridor, o Door
        return false;
    }

    /// <summary>
    /// Optimiza segmentos combinando los que son adyacentes y continuos
    /// </summary>
    private List<WallSegment> OptimizeSegments(HashSet<WallSegment> segments, bool isHorizontal)
    {
        List<WallSegment> result = new List<WallSegment>();
        HashSet<Vector3Int> processed = new HashSet<Vector3Int>();

        // Convertir a lista y ordenar
        List<WallSegment> sortedSegments = new List<WallSegment>(segments);
        
        if (isHorizontal)
        {
            sortedSegments.Sort((a, b) =>
            {
                int zCompare = a.start.z.CompareTo(b.start.z);
                return zCompare != 0 ? zCompare : a.start.x.CompareTo(b.start.x);
            });
        }
        else
        {
            sortedSegments.Sort((a, b) =>
            {
                int xCompare = a.start.x.CompareTo(b.start.x);
                return xCompare != 0 ? xCompare : a.start.z.CompareTo(b.start.z);
            });
        }

        foreach (var segment in sortedSegments)
        {
            if (processed.Contains(segment.start))
                continue;

            Vector3Int currentStart = segment.start;
            int length = 1;
            processed.Add(currentStart);

            // Buscar segmentos continuos
            Vector3Int nextPos = isHorizontal
                ? new Vector3Int(currentStart.x + 1, 0, currentStart.z)
                : new Vector3Int(currentStart.x, 0, currentStart.z + 1);

            while (segments.Contains(new WallSegment { start = nextPos, length = 1, isHorizontal = isHorizontal }) 
                   && !processed.Contains(nextPos))
            {
                length++;
                processed.Add(nextPos);
                
                nextPos = isHorizontal
                    ? new Vector3Int(nextPos.x + 1, 0, nextPos.z)
                    : new Vector3Int(nextPos.x, 0, nextPos.z + 1);
            }

            result.Add(new WallSegment
            {
                start = currentStart,
                length = length,
                isHorizontal = isHorizontal
            });
        }

        return result;
    }

    /// <summary>
    /// Genera paredes procedurales como un solo mesh optimizado en lugar de múltiples cubos.
    /// Crea huecos automáticamente donde hay puertas.
    /// </summary>
    public GameObject GenerateWalls(
        List<Vector3Int> horizontalWalls,
        List<Vector3Int> verticalWalls,
        List<Vector3Int> horizontalDoors,
        List<Vector3Int> verticalDoors,
        Transform parent)
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        // Generar paredes horizontales (orientadas en el eje X)
        foreach (var wallPos in horizontalWalls)
        {
            if (!horizontalDoors.Contains(wallPos))
            {
                AddHorizontalWallSegment(wallPos);
            }
        }

        // Generar paredes verticales (orientadas en el eje Z)
        foreach (var wallPos in verticalWalls)
        {
            if (!verticalDoors.Contains(wallPos))
            {
                AddVerticalWallSegment(wallPos);
            }
        }

        // Crear el GameObject con el mesh combinado
        GameObject wallObject = new GameObject("ProceduralWalls");
        wallObject.transform.parent = parent;
        wallObject.transform.position = Vector3.zero;

        MeshFilter meshFilter = wallObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = wallObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = wallObject.AddComponent<MeshCollider>();

        Mesh wallMesh = new Mesh();
        wallMesh.name = "DungeonWallsMesh";
        wallMesh.vertices = vertices.ToArray();
        wallMesh.triangles = triangles.ToArray();
        wallMesh.uv = uvs.ToArray();
        
        // Recalcular normales para iluminación correcta
        wallMesh.RecalculateNormals();
        
        // Recalcular bounds para culling correcto
        wallMesh.RecalculateBounds();
        
        // Optimizar el mesh
        wallMesh.Optimize();

        meshFilter.mesh = wallMesh;
        meshRenderer.material = wallMaterial;
        meshCollider.sharedMesh = wallMesh;

        Debug.Log($"Generated walls mesh with {vertices.Count} vertices and {triangles.Count / 3} triangles");

        return wallObject;
    }

    /// <summary>
    /// Añade un segmento de pared horizontal (orientado en el eje X)
    /// </summary>
    private void AddHorizontalWallSegment(Vector3Int position)
    {
        // La pared horizontal va de position.x - 0.5 a position.x + 0.5 en el eje X
        // Se coloca en position.z en el eje Z
        Vector3 basePos = new Vector3(position.x, 0, position.z);

        // Dimensiones del cubo de pared
        float width = 1f;
        float height = wallHeight;
        float depth = 0.3f; // Grosor de la pared (aumentado para mejor cobertura)

        // Definir las 8 esquinas del cubo - centrado en la posición
        Vector3 v0 = basePos + new Vector3(-width / 2, 0, -depth / 2);
        Vector3 v1 = basePos + new Vector3(width / 2, 0, -depth / 2);
        Vector3 v2 = basePos + new Vector3(width / 2, 0, depth / 2);
        Vector3 v3 = basePos + new Vector3(-width / 2, 0, depth / 2);
        Vector3 v4 = basePos + new Vector3(-width / 2, height, -depth / 2);
        Vector3 v5 = basePos + new Vector3(width / 2, height, -depth / 2);
        Vector3 v6 = basePos + new Vector3(width / 2, height, depth / 2);
        Vector3 v7 = basePos + new Vector3(-width / 2, height, depth / 2);

        AddCubeFaces(v0, v1, v2, v3, v4, v5, v6, v7);
    }

    /// <summary>
    /// Añade un segmento de pared vertical (orientado en el eje Z)
    /// </summary>
    private void AddVerticalWallSegment(Vector3Int position)
    {
        // La pared vertical va de position.z - 0.5 a position.z + 0.5 en el eje Z
        // Se coloca en position.x en el eje X
        Vector3 basePos = new Vector3(position.x, 0, position.z);

        // Dimensiones del cubo de pared (rotado 90 grados respecto al horizontal)
        float width = 0.3f; // Grosor de la pared (aumentado para mejor cobertura)
        float height = wallHeight;
        float depth = 1f;

        // Definir las 8 esquinas del cubo - centrado en la posición
        Vector3 v0 = basePos + new Vector3(-width / 2, 0, -depth / 2);
        Vector3 v1 = basePos + new Vector3(width / 2, 0, -depth / 2);
        Vector3 v2 = basePos + new Vector3(width / 2, 0, depth / 2);
        Vector3 v3 = basePos + new Vector3(-width / 2, 0, depth / 2);
        Vector3 v4 = basePos + new Vector3(-width / 2, height, -depth / 2);
        Vector3 v5 = basePos + new Vector3(width / 2, height, -depth / 2);
        Vector3 v6 = basePos + new Vector3(width / 2, height, depth / 2);
        Vector3 v7 = basePos + new Vector3(-width / 2, height, depth / 2);

        AddCubeFaces(v0, v1, v2, v3, v4, v5, v6, v7);
    }

    /// <summary>
    /// Añade las 6 caras de un cubo al mesh
    /// v0-v3 son la base (y=0), v4-v7 son la parte superior (y=height)
    /// </summary>
    private void AddCubeFaces(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                              Vector3 v4, Vector3 v5, Vector3 v6, Vector3 v7)
    {
        // Front face (hacia +Z) - exterior
        AddQuad(v3, v2, v6, v7);

        // Back face (hacia -Z) - exterior
        AddQuad(v1, v0, v4, v5);

        // Left face (hacia -X) - exterior
        AddQuad(v0, v3, v7, v4);

        // Right face (hacia +X) - exterior
        AddQuad(v2, v1, v5, v6);

        // Top face - exterior
        AddQuad(v7, v6, v5, v4);

        // Bottom face - no necesaria generalmente, pero la incluimos por completitud
        // AddQuad(v0, v1, v2, v3);
    }

    /// <summary>
    /// Añade un quad (4 vértices, 2 triángulos) al mesh
    /// Los vértices deben estar en orden: bottom-left, bottom-right, top-right, top-left (sentido antihorario)
    /// </summary>
    private void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        int startIndex = vertices.Count;

        vertices.Add(v0);
        vertices.Add(v1);
        vertices.Add(v2);
        vertices.Add(v3);

        // Primer triángulo (sentido antihorario desde afuera)
        triangles.Add(startIndex);
        triangles.Add(startIndex + 1);
        triangles.Add(startIndex + 2);

        // Segundo triángulo (sentido antihorario desde afuera)
        triangles.Add(startIndex);
        triangles.Add(startIndex + 2);
        triangles.Add(startIndex + 3);

        // UVs básicos para texturizado
        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(1, 0));
        uvs.Add(new Vector2(1, 1));
        uvs.Add(new Vector2(0, 1));
    }

    /// <summary>
    /// Genera paredes optimizadas con combinación de segmentos continuos
    /// Esta versión intenta unir paredes adyacentes para reducir aún más los polígonos
    /// </summary>
    public GameObject GenerateOptimizedWalls(
        List<Vector3Int> horizontalWalls,
        List<Vector3Int> verticalWalls,
        List<Vector3Int> horizontalDoors,
        List<Vector3Int> verticalDoors,
        Transform parent)
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        // Agrupar paredes horizontales continuas
        var horizontalSegments = GroupContinuousWalls(horizontalWalls, horizontalDoors, true);
        foreach (var segment in horizontalSegments)
        {
            AddHorizontalWallStretch(segment.start, segment.length);
        }

        // Agrupar paredes verticales continuas
        var verticalSegments = GroupContinuousWalls(verticalWalls, verticalDoors, false);
        foreach (var segment in verticalSegments)
        {
            AddVerticalWallStretch(segment.start, segment.length);
        }

        // Crear el GameObject con el mesh combinado
        GameObject wallObject = new GameObject("OptimizedProceduralWalls");
        wallObject.transform.parent = parent;
        wallObject.transform.position = Vector3.zero;

        MeshFilter meshFilter = wallObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = wallObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = wallObject.AddComponent<MeshCollider>();

        Mesh wallMesh = new Mesh();
        wallMesh.name = "OptimizedDungeonWallsMesh";
        wallMesh.vertices = vertices.ToArray();
        wallMesh.triangles = triangles.ToArray();
        wallMesh.uv = uvs.ToArray();
        
        // Recalcular normales para iluminación correcta
        wallMesh.RecalculateNormals();
        
        // Recalcular bounds para culling correcto
        wallMesh.RecalculateBounds();
        
        // Optimizar el mesh
        wallMesh.Optimize();

        meshFilter.mesh = wallMesh;
        meshRenderer.material = wallMaterial;
        meshCollider.sharedMesh = wallMesh;

        Debug.Log($"Generated optimized walls: {horizontalSegments.Count + verticalSegments.Count} segments, " +
                  $"{vertices.Count} vertices, {triangles.Count / 3} triangles");

        return wallObject;
    }

    /// <summary>
    /// Agrupa paredes continuas en segmentos largos
    /// </summary>
    private List<WallSegment> GroupContinuousWalls(List<Vector3Int> walls, List<Vector3Int> doors, bool isHorizontal)
    {
        List<WallSegment> segments = new List<WallSegment>();
        HashSet<Vector3Int> processed = new HashSet<Vector3Int>();

        // Ordenar paredes según la dirección
        List<Vector3Int> sortedWalls = new List<Vector3Int>(walls);
        if (isHorizontal)
        {
            sortedWalls.Sort((a, b) =>
            {
                int zCompare = a.z.CompareTo(b.z);
                return zCompare != 0 ? zCompare : a.x.CompareTo(b.x);
            });
        }
        else
        {
            sortedWalls.Sort((a, b) =>
            {
                int xCompare = a.x.CompareTo(b.x);
                return xCompare != 0 ? xCompare : a.z.CompareTo(b.z);
            });
        }

        foreach (var wall in sortedWalls)
        {
            if (processed.Contains(wall) || doors.Contains(wall))
                continue;

            // Encontrar cuántas paredes continuas hay desde este punto
            int length = 1;
            Vector3Int current = wall;
            processed.Add(current);

            // Buscar hacia adelante
            while (true)
            {
                Vector3Int next = isHorizontal
                    ? new Vector3Int(current.x + 1, current.y, current.z)
                    : new Vector3Int(current.x, current.y, current.z + 1);

                if (walls.Contains(next) && !doors.Contains(next) && !processed.Contains(next))
                {
                    length++;
                    processed.Add(next);
                    current = next;
                }
                else
                {
                    break;
                }
            }

            segments.Add(new WallSegment { start = wall, length = length });
        }

        return segments;
    }

    /// <summary>
    /// Añade un tramo largo de pared horizontal
    /// </summary>
    private void AddHorizontalWallStretch(Vector3Int startPos, int length)
    {
        // Crear un rectángulo desde startPos - 0.5 hasta startPos + length - 0.5
        Vector3 basePos = new Vector3(startPos.x - 0.5f, 0, startPos.z);
        float width = length; // La pared se extiende por 'length' unidades
        float height = wallHeight;
        float depth = 0.3f; // Grosor consistente

        Vector3 v0 = basePos + new Vector3(0, 0, -depth / 2);
        Vector3 v1 = basePos + new Vector3(width, 0, -depth / 2);
        Vector3 v2 = basePos + new Vector3(width, 0, depth / 2);
        Vector3 v3 = basePos + new Vector3(0, 0, depth / 2);
        Vector3 v4 = basePos + new Vector3(0, height, -depth / 2);
        Vector3 v5 = basePos + new Vector3(width, height, -depth / 2);
        Vector3 v6 = basePos + new Vector3(width, height, depth / 2);
        Vector3 v7 = basePos + new Vector3(0, height, depth / 2);

        AddCubeFaces(v0, v1, v2, v3, v4, v5, v6, v7);
    }

    /// <summary>
    /// Añade un tramo largo de pared vertical
    /// </summary>
    private void AddVerticalWallStretch(Vector3Int startPos, int length)
    {
        // Crear un rectángulo desde startPos - 0.5 hasta startPos + length - 0.5
        Vector3 basePos = new Vector3(startPos.x, 0, startPos.z - 0.5f);
        float width = 0.3f; // Grosor consistente
        float height = wallHeight;
        float depth = length; // La pared se extiende por 'length' unidades

        Vector3 v0 = basePos + new Vector3(-width / 2, 0, 0);
        Vector3 v1 = basePos + new Vector3(width / 2, 0, 0);
        Vector3 v2 = basePos + new Vector3(width / 2, 0, depth);
        Vector3 v3 = basePos + new Vector3(-width / 2, 0, depth);
        Vector3 v4 = basePos + new Vector3(-width / 2, height, 0);
        Vector3 v5 = basePos + new Vector3(width / 2, height, 0);
        Vector3 v6 = basePos + new Vector3(width / 2, height, depth);
        Vector3 v7 = basePos + new Vector3(-width / 2, height, depth);

        AddCubeFaces(v0, v1, v2, v3, v4, v5, v6, v7);
    }

    private struct WallSegment
    {
        public Vector3Int start;
        public int length;
        public bool isHorizontal;

        public override bool Equals(object obj)
        {
            if (!(obj is WallSegment)) return false;
            WallSegment other = (WallSegment)obj;
            return start.Equals(other.start) && isHorizontal == other.isHorizontal;
        }

        public override int GetHashCode()
        {
            return start.GetHashCode() ^ isHorizontal.GetHashCode();
        }
    }
}