using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor personalizado para DungeonCreator con opciones mejoradas para el sistema de paredes
/// </summary>
[CustomEditor(typeof(DungeonCreator))]
public class DungeonCreatorEditor : Editor
{
    private int inputSeed = 0;
    private SerializedProperty useProceduralWalls;
    private SerializedProperty wallMaterial;
    private SerializedProperty wallPrefab;
    private SerializedProperty wallHeight;

    private bool showPerformanceInfo = false;
    private GUIStyle headerStyle;
    private GUIStyle infoBoxStyle;

    void OnEnable()
    {
        useProceduralWalls = serializedObject.FindProperty("useProceduralWalls");
        wallMaterial = serializedObject.FindProperty("wallMaterial");
        wallPrefab = serializedObject.FindProperty("wallPrefab");
        wallHeight = serializedObject.FindProperty("wallHeight");
    }

    public override void OnInspectorGUI()
    {
        // Inicializar estilos
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.fontSize = 12;
            headerStyle.normal.textColor = Color.cyan;
        }

        if (infoBoxStyle == null)
        {
            infoBoxStyle = new GUIStyle(EditorStyles.helpBox);
            infoBoxStyle.padding = new RectOffset(10, 10, 10, 10);
        }

        serializedObject.Update();

        // Dibujar el inspector por defecto primero
        base.OnInspectorGUI();

        DungeonCreator dungeonCreator = (DungeonCreator)target;

        // === SECCIÓN ORIGINAL ===
        EditorGUILayout.Space(10);

        if (GUILayout.Button("Create Dungeon", GUILayout.Height(30)))
        {
            dungeonCreator.CreateDungeonRandom();
        }

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Seeded Dungeon", EditorStyles.boldLabel);
        inputSeed = EditorGUILayout.IntField("Seed:", inputSeed);

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Generate with Seed"))
        {
            dungeonCreator.CreateDungeonWithSeed(inputSeed);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Delete Dungeon"))
        {
            dungeonCreator.DestroyAllChildren();
        }

        // === SECCIÓN OPTIMIZACIÓN DE PAREDES ===
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("SISTEMA DE PAREDES OPTIMIZADO", headerStyle);
        EditorGUILayout.Space(5);

        // Mostrar información del sistema actual
        if (dungeonCreator.useProceduralWalls)
        {
            EditorGUILayout.BeginVertical(infoBoxStyle);
            EditorGUILayout.LabelField("✓ Sistema Procedural ACTIVO", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("→ Las paredes se generarán como un solo mesh optimizado");
            EditorGUILayout.LabelField("→ Mejor rendimiento y menos draw calls");
            
            if (dungeonCreator.wallMaterial == null)
            {
                EditorGUILayout.HelpBox("⚠ Asigna un Material para las paredes!", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.BeginVertical(infoBoxStyle);
            EditorGUILayout.LabelField("⚠ Sistema Antiguo (Prefabs) ACTIVO", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("→ Se usarán múltiples instancias de prefabs");
            EditorGUILayout.LabelField("→ Peor rendimiento en dungeons grandes");
            
            if (dungeonCreator.wallPrefab == null)
            {
                EditorGUILayout.HelpBox("⚠ Asigna un Prefab de pared!", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);

        // Botones de acción
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🔄 Regenerar Dungeon", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                dungeonCreator.CreateDungeon();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "El juego debe estar corriendo para regenerar el dungeon", "OK");
            }
        }

        if (GUILayout.Button("📊 Ver Estadísticas", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                WallSystemStats stats = dungeonCreator.GetComponent<WallSystemStats>();
                if (stats == null)
                {
                    stats = dungeonCreator.gameObject.AddComponent<WallSystemStats>();
                    stats.dungeonCreator = dungeonCreator;
                }
                stats.CalculateStats();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "El juego debe estar corriendo para ver estadísticas", "OK");
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("⚖ Comparar Sistemas (Antiguo vs Nuevo)", GUILayout.Height(25)))
        {
            if (Application.isPlaying)
            {
                WallSystemStats stats = dungeonCreator.GetComponent<WallSystemStats>();
                if (stats == null)
                {
                    stats = dungeonCreator.gameObject.AddComponent<WallSystemStats>();
                    stats.dungeonCreator = dungeonCreator;
                }
                stats.CompareSystemsPerformance();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "El juego debe estar corriendo para comparar sistemas", "OK");
            }
        }

        // Toggle para información de rendimiento
        EditorGUILayout.Space(10);
        showPerformanceInfo = EditorGUILayout.Foldout(showPerformanceInfo, "ℹ Información de Rendimiento");
        
        if (showPerformanceInfo)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Beneficios del Sistema Procedural:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Reducción de ~99% en número de GameObjects");
            EditorGUILayout.LabelField("• Reducción de ~99% en Draw Calls");
            EditorGUILayout.LabelField("• Reducción de ~80% en uso de memoria");
            EditorGUILayout.LabelField("• Mejor rendimiento en dungeons grandes");
            EditorGUILayout.LabelField("• Los huecos para puertas se generan automáticamente");
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Cuándo usar cada sistema:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Procedural: Dungeons medianos/grandes, dispositivos móviles");
            EditorGUILayout.LabelField("Prefabs: Si necesitas paredes con scripts individuales");
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }
}