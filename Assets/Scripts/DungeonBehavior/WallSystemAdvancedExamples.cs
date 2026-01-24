using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Ejemplos de casos de uso avanzados para el sistema de paredes procedurales
/// </summary>
public class WallSystemAdvancedExamples : MonoBehaviour
{
    [Header("Referencias")]
    public DungeonCreator dungeonCreator;

    #region Ejemplo 1: Cambiar Material Dinámicamente
    
    /// <summary>
    /// Cambia el material de las paredes en tiempo de ejecución
    /// Útil para temas visuales o efectos especiales
    /// </summary>
    public void ChangeWallMaterial(Material newMaterial)
    {
        // Encontrar el objeto de paredes procedurales
        Transform wallParent = dungeonCreator.transform.Find("WallParent");
        if (wallParent == null) return;

        Transform proceduralWalls = wallParent.Find("OptimizedProceduralWalls");
        if (proceduralWalls == null)
        {
            proceduralWalls = wallParent.Find("ProceduralWalls");
        }

        if (proceduralWalls != null)
        {
            MeshRenderer renderer = proceduralWalls.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = newMaterial;
                Debug.Log("Material de paredes cambiado exitosamente");
            }
        }
    }

    /// <summary>
    /// Ejemplo de uso: cambiar material según el tipo de zona
    /// </summary>
    [ContextMenu("Ejemplo: Material por Zona")]
    public void ExampleZoneMaterial()
    {
        // Material normal para zonas seguras
        Material normalMaterial = dungeonCreator.wallMaterial;
        
        // Material rojo para zona de peligro (ejemplo)
        Material dangerMaterial = new Material(Shader.Find("Standard"));
        dangerMaterial.color = Color.red;
        
        // Cambiar según alguna condición
        bool isDangerZone = Random.value > 0.5f;
        ChangeWallMaterial(isDangerZone ? dangerMaterial : normalMaterial);
    }

    #endregion

    #region Ejemplo 2: Obtener Mesh para Modificaciones

    /// <summary>
    /// Obtiene el mesh de las paredes para modificaciones avanzadas
    /// </summary>
    public Mesh GetWallMesh()
    {
        Transform wallParent = dungeonCreator.transform.Find("WallParent");
        if (wallParent == null) return null;

        Transform proceduralWalls = wallParent.Find("OptimizedProceduralWalls");
        if (proceduralWalls == null)
        {
            proceduralWalls = wallParent.Find("ProceduralWalls");
        }

        if (proceduralWalls != null)
        {
            MeshFilter meshFilter = proceduralWalls.GetComponent<MeshFilter>();
            return meshFilter?.sharedMesh;
        }

        return null;
    }

    /// <summary>
    /// Añade un efecto de vertex color al mesh (ejemplo)
    /// </summary>
    [ContextMenu("Ejemplo: Añadir Vertex Colors")]
    public void ExampleVertexColors()
    {
        Mesh mesh = GetWallMesh();
        if (mesh == null)
        {
            Debug.LogWarning("No se encontró el mesh de paredes");
            return;
        }

        // Crear array de colores
        Color[] colors = new Color[mesh.vertexCount];
        
        // Gradiente de ejemplo (azul en la base, blanco arriba)
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
        {
            float heightFactor = vertices[i].y / dungeonCreator.wallHeight;
            colors[i] = Color.Lerp(Color.blue, Color.white, heightFactor);
        }

        mesh.colors = colors;
        Debug.Log("Vertex colors aplicados al mesh de paredes");
    }

    #endregion

    #region Ejemplo 3: Detección de Colisión con Paredes

    /// <summary>
    /// Verifica si una posición está cerca de una pared
    /// Útil para spawning de objetos o detección de espacio
    /// </summary>
    public bool IsNearWall(Vector3 position, float distance = 1f)
    {
        Transform wallParent = dungeonCreator.transform.Find("WallParent");
        if (wallParent == null) return false;

        Transform proceduralWalls = wallParent.Find("OptimizedProceduralWalls");
        if (proceduralWalls == null) return false;

        MeshCollider collider = proceduralWalls.GetComponent<MeshCollider>();
        if (collider != null)
        {
            // Raycast desde la posición en todas direcciones
            Vector3[] directions = {
                Vector3.forward, Vector3.back,
                Vector3.left, Vector3.right
            };

            foreach (Vector3 dir in directions)
            {
                if (Physics.Raycast(position, dir, distance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    #endregion

    #region Ejemplo 4: Regeneración Condicional

    /// <summary>
    /// Regenera solo las paredes sin regenerar todo el dungeon
    /// (Útil para eventos especiales o cambios dinámicos)
    /// </summary>
    public void RegenerateWallsOnly()
    {
        // Nota: Esto requeriría acceso a las listas privadas de DungeonCreator
        // Para implementarlo completamente, necesitarías hacer públicas algunas variables
        
        Debug.Log("Para implementar regeneración parcial, considera hacer públicas " +
                 "las listas de posiciones de paredes en DungeonCreator");
        
        // Alternativa: regenerar todo el dungeon con la misma seed
        dungeonCreator.CreateDungeonWithSeed(dungeonCreator.seed);
    }

    #endregion

    #region Ejemplo 5: Integración con Sistema de Iluminación

    /// <summary>
    /// Configura el mesh de paredes para recibir mejor iluminación
    /// </summary>
    [ContextMenu("Ejemplo: Optimizar para Iluminación")]
    public void OptimizeForLighting()
    {
        Transform wallParent = dungeonCreator.transform.Find("WallParent");
        if (wallParent == null) return;

        Transform proceduralWalls = wallParent.Find("OptimizedProceduralWalls");
        if (proceduralWalls == null) return;

        // Configurar el renderer para mejor iluminación
        MeshRenderer renderer = proceduralWalls.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            // Habilitar sombras
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // Configurar lightmapping
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

            Debug.Log("Paredes optimizadas para iluminación");
        }

        // Recalcular normales y tangentes para mejor iluminación
        MeshFilter meshFilter = proceduralWalls.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            Mesh mesh = meshFilter.mesh;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
        }
    }

    #endregion

    #region Ejemplo 6: Efectos Visuales

    /// <summary>
    /// Añade un efecto de outline a las paredes
    /// </summary>
    [ContextMenu("Ejemplo: Añadir Outline")]
    public void AddOutlineEffect()
    {
        Transform wallParent = dungeonCreator.transform.Find("WallParent");
        if (wallParent == null) return;

        Transform proceduralWalls = wallParent.Find("OptimizedProceduralWalls");
        if (proceduralWalls == null) return;

        // Crear copia del objeto para el outline
        GameObject outlineObj = new GameObject("WallOutline");
        outlineObj.transform.parent = proceduralWalls;
        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localScale = Vector3.one * 1.02f; // Ligeramente más grande

        // Copiar mesh y material
        MeshFilter originalMeshFilter = proceduralWalls.GetComponent<MeshFilter>();
        if (originalMeshFilter != null)
        {
            MeshFilter outlineMeshFilter = outlineObj.AddComponent<MeshFilter>();
            outlineMeshFilter.mesh = originalMeshFilter.mesh;

            MeshRenderer outlineRenderer = outlineObj.AddComponent<MeshRenderer>();
            Material outlineMaterial = new Material(Shader.Find("Standard"));
            outlineMaterial.color = Color.black;
            outlineRenderer.material = outlineMaterial;

            // Invertir normales para el efecto outline
            Mesh outlineMesh = Instantiate(originalMeshFilter.mesh);
            int[] triangles = outlineMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i];
                triangles[i] = triangles[i + 2];
                triangles[i + 2] = temp;
            }
            outlineMesh.triangles = triangles;
            outlineMeshFilter.mesh = outlineMesh;

            Debug.Log("Efecto de outline añadido a las paredes");
        }
    }

    #endregion

    #region Ejemplo 7: Análisis de Geometría

    /// <summary>
    /// Analiza las estadísticas del mesh de paredes
    /// </summary>
    [ContextMenu("Ejemplo: Analizar Geometría")]
    public void AnalyzeWallGeometry()
    {
        Mesh mesh = GetWallMesh();
        if (mesh == null)
        {
            Debug.LogWarning("No se encontró mesh de paredes");
            return;
        }

        // Recopilar estadísticas
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3 boundsMin = mesh.bounds.min;
        Vector3 boundsMax = mesh.bounds.max;

        // Calcular área de superficie aproximada
        float surfaceArea = 0f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];
            
            Vector3 side1 = v1 - v0;
            Vector3 side2 = v2 - v0;
            surfaceArea += Vector3.Cross(side1, side2).magnitude * 0.5f;
        }

        // Mostrar resultados
        Debug.Log("=== ANÁLISIS DE GEOMETRÍA DE PAREDES ===");
        Debug.Log($"Vértices: {vertices.Length}");
        Debug.Log($"Triángulos: {triangles.Length / 3}");
        Debug.Log($"Bounds: Min{boundsMin} - Max{boundsMax}");
        Debug.Log($"Área de superficie: {surfaceArea:F2} unidades²");
        Debug.Log($"Memoria del mesh: ~{(vertices.Length * 48) / 1024f:F2} KB");
    }

    #endregion

    #region Ejemplo 8: Serialización y Guardado

    /// <summary>
    /// Guarda los datos del mesh para cargarlo más tarde
    /// </summary>
    [System.Serializable]
    public class WallMeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Vector2[] uvs;
    }

    [ContextMenu("Ejemplo: Guardar Mesh")]
    public void SaveWallMesh()
    {
        Mesh mesh = GetWallMesh();
        if (mesh == null) return;

        WallMeshData data = new WallMeshData
        {
            vertices = mesh.vertices,
            triangles = mesh.triangles,
            uvs = mesh.uv
        };

        string json = JsonUtility.ToJson(data);
        System.IO.File.WriteAllText(Application.persistentDataPath + "/wallmesh.json", json);
        
        Debug.Log($"Mesh guardado en: {Application.persistentDataPath}/wallmesh.json");
    }

    [ContextMenu("Ejemplo: Cargar Mesh")]
    public void LoadWallMesh()
    {
        string path = Application.persistentDataPath + "/wallmesh.json";
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("No hay mesh guardado");
            return;
        }

        string json = System.IO.File.ReadAllText(path);
        WallMeshData data = JsonUtility.FromJson<WallMeshData>(json);

        // Crear nuevo mesh con los datos cargados
        Mesh mesh = new Mesh
        {
            vertices = data.vertices,
            triangles = data.triangles,
            uv = data.uvs
        };
        mesh.RecalculateNormals();

        Debug.Log("Mesh cargado desde archivo");
    }

    #endregion

    #region Ejemplo 9: Performance Profiling

    /// <summary>
    /// Mide el tiempo de generación de paredes
    /// </summary>
    [ContextMenu("Ejemplo: Medir Rendimiento")]
    public void ProfileWallGeneration()
    {
        int iterations = 10;
        float totalTime = 0f;

        Debug.Log($"Iniciando profiling con {iterations} iteraciones...");

        for (int i = 0; i < iterations; i++)
        {
            float startTime = Time.realtimeSinceStartup;
            dungeonCreator.CreateDungeon();
            float endTime = Time.realtimeSinceStartup;
            
            totalTime += (endTime - startTime);
        }

        float averageTime = (totalTime / iterations) * 1000f; // ms
        
        Debug.Log("=== PROFILING RESULTS ===");
        Debug.Log($"Tiempo promedio de generación: {averageTime:F2}ms");
        Debug.Log($"Iteraciones: {iterations}");
        Debug.Log($"Sistema usado: {(dungeonCreator.useProceduralWalls ? "Procedural" : "Prefabs")}");
    }

    #endregion

    #region Ejemplo 10: Integración con NavMesh

    /// <summary>
    /// Actualiza el NavMesh cuando se generan nuevas paredes
    /// Requiere Unity NavMesh
    /// </summary>
    public void UpdateNavMeshForWalls()
    {
        #if UNITY_AI
        UnityEngine.AI.NavMeshSurface surface = GetComponent<UnityEngine.AI.NavMeshSurface>();
        if (surface != null)
        {
            surface.BuildNavMesh();
            Debug.Log("NavMesh actualizado para nuevas paredes");
        }
        else
        {
            Debug.LogWarning("No hay NavMeshSurface en este GameObject");
        }
        #else
        Debug.LogWarning("Unity AI Navigation no está instalado");
        #endif
    }

    #endregion
}
