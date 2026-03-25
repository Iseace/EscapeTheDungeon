using System.Collections.Generic;
using UnityEngine;

public class EndMatchEscapeVegetationGenerator : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool generateOnlyWhenSurvivorsEscaped = true;
    [Tooltip("Si no hay snapshot (por ejemplo, al probar EndMatch directamente), permite generar de todas formas.")]
    [SerializeField] private bool allowGenerationWithoutSnapshot = true;

    [Header("Prefabs")]
    [SerializeField] private List<GameObject> vegetationPrefabs = new List<GameObject>();
    [SerializeField] private int spawnCount = 120;

    [Header("Area")]
    [Tooltip("Area center for spawn volume. If null, this transform is used.")]
    [SerializeField] private Transform areaCenter;
    [SerializeField] private Vector2 xRange = new Vector2(-30f, 30f);
    [SerializeField] private Vector2 zRange = new Vector2(0f, 160f);
    [SerializeField] private float yOffset = 0f;

    [Header("Runner Lane Exclusion")]
    [SerializeField] private bool avoidRunnerLane = true;
    [SerializeField] private float laneHalfWidth = 3f;

    [Header("Placement")]
    [SerializeField] private bool randomizeYaw = true;
    [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.9f, 1.25f);
    [SerializeField] private LayerMask blockingMask;
    [SerializeField] private float minDistanceBetweenObjects = 1.5f;

    [Header("Endless Loop")]
    [SerializeField] private bool enableEndlessRecycle = false;
    [Tooltip("If null, it will try to find EndMatchEscapeRunnerController in scene.")]
    [SerializeField] private Transform recycleReference;
    [SerializeField] private float recycleBehindDistance = 15f;
    [SerializeField] private float recycleAheadDistance = 120f;

    [Header("Seed")]
    [SerializeField] private bool useCustomSeed = false;
    [SerializeField] private int customSeed = 12345;

    [Header("Debug")]
    [SerializeField] private bool clearGeneratedBeforeSpawn = true;
    [SerializeField] private bool debugLogs = false;

    private Transform generatedRoot;
    private readonly List<Transform> spawnedItems = new List<Transform>();

    private void Start()
    {
        if (!generateOnStart)
            return;

        if (!CanGenerateForCurrentVariant())
            return;

        GenerateVegetation();
    }

    [ContextMenu("Force Generate Vegetation (Ignore Variant)")]
    public void ForceGenerateVegetation()
    {
        bool previous = generateOnlyWhenSurvivorsEscaped;
        generateOnlyWhenSurvivorsEscaped = false;
        GenerateVegetation();
        generateOnlyWhenSurvivorsEscaped = previous;
    }

    private bool CanGenerateForCurrentVariant()
    {
        if (!generateOnlyWhenSurvivorsEscaped)
            return true;

        MatchEndSnapshot snapshot = MatchEndRuntimeContext.LatestSnapshot;
        if (snapshot == null)
        {
            if (allowGenerationWithoutSnapshot)
                return true;

            if (debugLogs)
                Debug.Log("[EndMatchEscapeVegetationGenerator] Sin snapshot y allowGenerationWithoutSnapshot=false. No se genera vegetacion.");

            return false;
        }

        EndCinematicVariant variant = MatchEndSnapshotEvaluator.ResolveLocalVariant(snapshot);
        return variant == EndCinematicVariant.SurvivorsEscaped;
    }

    [ContextMenu("Generate Vegetation")]
    public void GenerateVegetation()
    {
        if (vegetationPrefabs == null || vegetationPrefabs.Count == 0)
        {
            if (debugLogs)
                Debug.LogWarning("[EndMatchEscapeVegetationGenerator] No hay prefabs asignados.");
            return;
        }

        EnsureRoot();

        if (clearGeneratedBeforeSpawn)
            ClearRoot();

        if (useCustomSeed)
            Random.InitState(customSeed);

        Transform center = areaCenter != null ? areaCenter : transform;
        Vector3 centerPos = center.position;
        List<Vector3> acceptedPositions = new List<Vector3>(spawnCount);

        int attempts = 0;
        int maxAttempts = Mathf.Max(spawnCount * 12, 200);

        while (acceptedPositions.Count < spawnCount && attempts < maxAttempts)
        {
            attempts++;

            float x = Random.Range(Mathf.Min(xRange.x, xRange.y), Mathf.Max(xRange.x, xRange.y));
            float z = Random.Range(Mathf.Min(zRange.x, zRange.y), Mathf.Max(zRange.x, zRange.y));

            if (avoidRunnerLane && Mathf.Abs(x) <= laneHalfWidth)
                continue;

            Vector3 spawnPos = new Vector3(centerPos.x + x, centerPos.y + yOffset, centerPos.z + z);

            if (IsBlocked(spawnPos))
                continue;

            if (!HasDistanceFromOthers(spawnPos, acceptedPositions))
                continue;

            acceptedPositions.Add(spawnPos);
            SpawnSingle(spawnPos);
        }

        if (debugLogs)
            Debug.Log($"[EndMatchEscapeVegetationGenerator] Generados {acceptedPositions.Count}/{spawnCount} objetos en {attempts} intentos.");
    }

    private void Update()
    {
        if (!enableEndlessRecycle)
            return;

        if (spawnedItems.Count == 0)
            return;

        Transform reference = ResolveReference();
        if (reference == null)
            return;

        float thresholdBehind = reference.position.z - Mathf.Max(1f, recycleBehindDistance);
        float respawnMinZ = reference.position.z + Mathf.Max(5f, recycleAheadDistance * 0.35f);
        float respawnMaxZ = reference.position.z + Mathf.Max(respawnMinZ + 1f, recycleAheadDistance);

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            Transform item = spawnedItems[i];
            if (item == null)
                continue;

            Vector3 pos = item.position;
            if (pos.z >= thresholdBehind)
                continue;

            Vector3 newPos = BuildRecycledPosition(respawnMinZ, respawnMaxZ);
            item.position = newPos;

            if (randomizeYaw)
                item.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    }

    [ContextMenu("Clear Generated Vegetation")]
    public void ClearGeneratedVegetation()
    {
        EnsureRoot();
        ClearRoot();
    }

    private void SpawnSingle(Vector3 position)
    {
        int prefabIndex = Random.Range(0, vegetationPrefabs.Count);
        GameObject prefab = vegetationPrefabs[prefabIndex];
        if (prefab == null)
            return;

        Quaternion rotation = randomizeYaw
            ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            : Quaternion.identity;

        GameObject instance = Instantiate(prefab, position, rotation, generatedRoot);

        float minScale = Mathf.Min(uniformScaleRange.x, uniformScaleRange.y);
        float maxScale = Mathf.Max(uniformScaleRange.x, uniformScaleRange.y);
        float scale = Random.Range(minScale, maxScale);
        instance.transform.localScale *= scale;
        spawnedItems.Add(instance.transform);
    }

    private Vector3 BuildRecycledPosition(float minZ, float maxZ)
    {
        Transform center = areaCenter != null ? areaCenter : transform;
        Vector3 centerPos = center.position;

        for (int i = 0; i < 20; i++)
        {
            float x = Random.Range(Mathf.Min(xRange.x, xRange.y), Mathf.Max(xRange.x, xRange.y));
            if (avoidRunnerLane && Mathf.Abs(x) <= laneHalfWidth)
                continue;

            float z = Random.Range(minZ, maxZ);
            Vector3 candidate = new Vector3(centerPos.x + x, centerPos.y + yOffset, z);
            if (!IsBlocked(candidate))
                return candidate;
        }

        return new Vector3(centerPos.x, centerPos.y + yOffset, Random.Range(minZ, maxZ));
    }

    private bool IsBlocked(Vector3 position)
    {
        if (blockingMask.value == 0)
            return false;

        return Physics.CheckSphere(position, 0.45f, blockingMask, QueryTriggerInteraction.Ignore);
    }

    private bool HasDistanceFromOthers(Vector3 position, List<Vector3> existing)
    {
        float minDistSqr = minDistanceBetweenObjects * minDistanceBetweenObjects;

        for (int i = 0; i < existing.Count; i++)
        {
            if ((existing[i] - position).sqrMagnitude < minDistSqr)
                return false;
        }

        return true;
    }

    private void EnsureRoot()
    {
        if (generatedRoot != null)
            return;

        Transform existing = transform.Find("_GeneratedVegetation");
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        GameObject root = new GameObject("_GeneratedVegetation");
        generatedRoot = root.transform;
        generatedRoot.SetParent(transform, false);
    }

    private void ClearRoot()
    {
        if (generatedRoot == null)
            return;

        spawnedItems.Clear();

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = generatedRoot.GetChild(i);
            if (child != null)
                Destroy(child.gameObject);
        }
    }

    private Transform ResolveReference()
    {
        if (recycleReference != null)
            return recycleReference;

        EndMatchEscapeRunnerController controller = FindAnyObjectByType<EndMatchEscapeRunnerController>();
        if (controller != null)
            recycleReference = controller.transform;

        return recycleReference;
    }
}
