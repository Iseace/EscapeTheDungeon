using UnityEngine;
using UnityEditor;

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

        base.OnInspectorGUI();

        DungeonCreator dungeonCreator = (DungeonCreator)target;

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Create Dungeon", GUILayout.Height(30)))
        {
            dungeonCreator.CreateDungeonRandom();
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Seeded Dungeon", EditorStyles.boldLabel);
        inputSeed = EditorGUILayout.IntField("Seed:", inputSeed);

        EditorGUILayout.Space(10);

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
  
        serializedObject.ApplyModifiedProperties();
    }
}
