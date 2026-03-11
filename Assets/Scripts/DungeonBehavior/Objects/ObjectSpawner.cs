using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnableObject
{
    public GameObject prefab;
    public string objectName;
    [Range(0f, 100f)]
    public float spawnChance = 50f;
    public bool needsClearSpace = true; // Needs clear space around it
    public int clearanceRadius = 1;
    public bool avoidWalls = true;
    [Range(0, 6)]
    public int wallClearanceRadius = 1;

    [Header("Random Rotation")]
    public bool randomizeRotationX = false;
    public Vector2 rotationRangeX = Vector2.zero;
    public bool randomizeRotationY = true;
    public Vector2 rotationRangeY = new Vector2(0f, 360f);
}

public class ProceduralObjectSpawner
{
    private DungeonGrid grid;
    private Transform parentTransform;
    private Vector3 centerOffset;

    public ProceduralObjectSpawner(DungeonGrid dungeonGrid, Transform parent, Vector3 centerOffsetValue = default)
    {
        grid = dungeonGrid;
        parentTransform = parent;
        centerOffset = centerOffsetValue;
    }

    // Spawn objects with custom list
    public void SpawnObjects(RoomNode room, List<SpawnableObject> spawnableObjects, int minObjects = 0, int maxObjects = 5)
    {
        List<Vector2Int> availableCells = grid.GetAvailableCellsInRoom(room);

        if (availableCells.Count == 0) return;

        int objectCount = Random.Range(minObjects, maxObjects + 1);

        for (int i = 0; i < objectCount && i < spawnableObjects.Count; i++)
        {
            if (availableCells.Count == 0) break;

            var spawnableObj = spawnableObjects[Random.Range(0, spawnableObjects.Count)];

            if (Random.Range(0f, 100f) > spawnableObj.spawnChance)
                continue;

            if (!TryFindSpawnPosition(availableCells, room, spawnableObj, out var spawnPos))
                continue;

            SpawnObject(spawnableObj, spawnPos, room);
            availableCells.Remove(spawnPos);
        }
    }

    private bool TryFindSpawnPosition(List<Vector2Int> availableCells, RoomNode room, SpawnableObject spawnableObj, out Vector2Int position)
    {
        position = default;

        if (availableCells == null || availableCells.Count == 0 || spawnableObj == null)
            return false;

        // Shuffle to randomize
        List<Vector2Int> shuffled = new List<Vector2Int>(availableCells);
        for (int i = 0; i < shuffled.Count; i++)
        {
            int randomIndex = Random.Range(i, shuffled.Count);
            Vector2Int temp = shuffled[i];
            shuffled[i] = shuffled[randomIndex];
            shuffled[randomIndex] = temp;
        }

        foreach (var pos in shuffled)
        {
            bool requiresClearSpace = spawnableObj.needsClearSpace;
            bool requiresWallClearance = spawnableObj.avoidWalls;

            if (requiresClearSpace && !HasClearSpace(pos, spawnableObj.clearanceRadius))
                continue;

            if (requiresWallClearance && !HasWallClearance(pos, room, spawnableObj.wallClearanceRadius))
                continue;

            position = pos;
            return true;
        }

        return false;
    }

    private bool HasWallClearance(Vector2Int center, RoomNode room, int radius)
    {
        if (room == null) return true;

        int clampedRadius = Mathf.Max(0, radius);

        for (int x = -clampedRadius; x <= clampedRadius; x++)
        {
            for (int y = -clampedRadius; y <= clampedRadius; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(checkPos);

                if (cell == null) return false;
                if (cell.Type != CellType.Floor) return false;
                if (cell.ParentRoom != room) return false;
            }
        }

        return true;
    }

    private bool HasClearSpace(Vector2Int center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                GridCell cell = grid.GetCell(checkPos);

                if (cell == null || cell.IsOccupied || cell.Type != CellType.Floor)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void SpawnObject(SpawnableObject spawnableObj, Vector2Int gridPos, RoomNode room)
    {
        if (spawnableObj == null) return;

        GameObject prefab = spawnableObj.prefab;
        if (prefab == null) return;

        Vector3 worldPos = new Vector3(gridPos.x + 0.5f, 0, gridPos.y + 0.5f) + centerOffset;
        Quaternion rotation = BuildSpawnRotation(spawnableObj);
        GameObject spawnedObj = Object.Instantiate(prefab, worldPos, rotation, parentTransform);
        spawnedObj.name = $"{prefab.name}_{room.RoomID}";

        grid.OccupyCell(gridPos, spawnedObj);
    }

    private Quaternion BuildSpawnRotation(SpawnableObject spawnableObj)
    {
        float x = 0f;
        float y = 0f;

        if (spawnableObj.randomizeRotationX)
        {
            float minX = Mathf.Min(spawnableObj.rotationRangeX.x, spawnableObj.rotationRangeX.y);
            float maxX = Mathf.Max(spawnableObj.rotationRangeX.x, spawnableObj.rotationRangeX.y);
            x = Random.Range(minX, maxX);
        }

        if (spawnableObj.randomizeRotationY)
        {
            float minY = Mathf.Min(spawnableObj.rotationRangeY.x, spawnableObj.rotationRangeY.y);
            float maxY = Mathf.Max(spawnableObj.rotationRangeY.x, spawnableObj.rotationRangeY.y);
            y = Random.Range(minY, maxY);
        }

        return Quaternion.Euler(x, y, 0f);
    }

    // Spawn a specific object at room center
    public void SpawnCenterObject(RoomNode room, GameObject prefab)
    {
        Vector2Int center = room.GetCenterPosition();

        if (grid.IsCellAvailable(center))
        {
            SpawnObject(new SpawnableObject { prefab = prefab, randomizeRotationY = true, rotationRangeY = new Vector2(0f, 360f) }, center, room);
        }
    }

    // Spawn objects at room corners
    public void SpawnCornerObjects(RoomNode room, GameObject prefab)
    {
        List<Vector2Int> corners = new List<Vector2Int>
        {
            room.BottomLeftAreaCorner + Vector2Int.one,
            new Vector2Int(room.TopRightAreaCorner.x - 1, room.BottomLeftAreaCorner.y + 1),
            new Vector2Int(room.BottomLeftAreaCorner.x + 1, room.TopRightAreaCorner.y - 1),
            room.TopRightAreaCorner - Vector2Int.one
        };

        foreach (var corner in corners)
        {
            if (grid.IsCellAvailable(corner))
            {
                SpawnObject(new SpawnableObject { prefab = prefab, randomizeRotationY = true, rotationRangeY = new Vector2(0f, 360f) }, corner, room);
            }
        }
    }
}