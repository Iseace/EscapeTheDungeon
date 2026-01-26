using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnableObject
{
    public GameObject prefab;
    public string objectName;
    [Range(0f, 100f)]
    public float spawnChance = 50f;
    public bool needsClearSpace = true; // Necesita espacio libre alrededor
    public int clearanceRadius = 1;
}

public class ProceduralObjectSpawner
{
    private DungeonGrid grid;
    private Transform parentTransform;

    public ProceduralObjectSpawner(DungeonGrid dungeonGrid, Transform parent)
    {
        grid = dungeonGrid;
        parentTransform = parent;
    }

    // Spawn objetos basados en el tipo de habitación
    public void SpawnObjectsInRoom(RoomNode room)
    {
        if (room.RoomTypeData == null || room.RoomTypeData.possibleObjectPrefabs.Count == 0)
            return;

        int objectCount = Random.Range(
            room.RoomTypeData.minObjects,
            room.RoomTypeData.maxObjects + 1
        );

        List<Vector2Int> availableCells = grid.GetAvailableCellsInRoom(room);

        if (availableCells.Count == 0) return;

        for (int i = 0; i < objectCount; i++)
        {
            if (availableCells.Count == 0) break;

            GameObject prefab = room.RoomTypeData.possibleObjectPrefabs[
                Random.Range(0, room.RoomTypeData.possibleObjectPrefabs.Count)
            ];

            Vector2Int spawnPos = availableCells[Random.Range(0, availableCells.Count)];
            SpawnObject(prefab, spawnPos, room);

            // Remover la celda usada
            availableCells.Remove(spawnPos);
        }
    }

    // Spawn objetos genéricos con lista personalizada
    public void SpawnObjects(RoomNode room, List<SpawnableObject> spawnableObjects)
    {
        List<Vector2Int> availableCells = grid.GetAvailableCellsInRoom(room);

        foreach (var spawnableObj in spawnableObjects)
        {
            if (Random.Range(0f, 100f) > spawnableObj.spawnChance)
                continue;

            if (availableCells.Count == 0) break;

            Vector2Int spawnPos;

            if (spawnableObj.needsClearSpace)
            {
                spawnPos = FindClearPosition(availableCells, spawnableObj.clearanceRadius);
                if (spawnPos == Vector2Int.zero) continue;
            }
            else
            {
                spawnPos = availableCells[Random.Range(0, availableCells.Count)];
            }

            SpawnObject(spawnableObj.prefab, spawnPos, room);
            availableCells.Remove(spawnPos);
        }
    }

    private Vector2Int FindClearPosition(List<Vector2Int> availableCells, int clearanceRadius)
    {
        // Shuffle para aleatorizar
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
            if (HasClearSpace(pos, clearanceRadius))
            {
                return pos;
            }
        }

        return Vector2Int.zero;
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

    private void SpawnObject(GameObject prefab, Vector2Int gridPos, RoomNode room)
    {
        if (prefab == null) return;

        Vector3 worldPos = new Vector3(gridPos.x + 0.5f, 0, gridPos.y + 0.5f);
        GameObject spawnedObj = Object.Instantiate(prefab, worldPos, Quaternion.identity, parentTransform);
        spawnedObj.name = $"{prefab.name}_{room.RoomID}";

        grid.OccupyCell(gridPos, spawnedObj);
    }

    // Spawn un objeto específico en el centro de la habitación
    public void SpawnCenterObject(RoomNode room, GameObject prefab)
    {
        Vector2Int center = room.GetCenterPosition();

        if (grid.IsCellAvailable(center))
        {
            SpawnObject(prefab, center, room);
        }
    }

    // Spawn objetos en las esquinas de la habitación
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
                SpawnObject(prefab, corner, room);
            }
        }
    }
}