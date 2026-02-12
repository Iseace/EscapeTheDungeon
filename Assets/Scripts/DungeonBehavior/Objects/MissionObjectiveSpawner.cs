using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MissionObjectiveConfig
{
    public GameObject prefab;
    [Range(0f, 1f)] public float spawnChance = 0.6f;
    [Tooltip("Maximo total de este prefab en toda la dungeon")]
    public int maxCount = 5;
    [Tooltip("Area maxima de la sala para considerar (tiles)")]
    public int maxRoomArea = 80;
    [Tooltip("Separacion minima a paredes (en tiles)")]
    public int clearanceFromWall = 1;
}

public class MissionObjectiveSpawner
{
    public void SpawnObjectives(
        DungeonGrid grid,
        List<RoomNode> rooms,
        Transform parent,
        Vector3 offset,
        List<MissionObjectiveConfig> configs)
    {
        if (grid == null || rooms == null || parent == null) return;
        if (configs == null || configs.Count == 0) return;

        foreach (var cfg in configs)
        {
            if (cfg.prefab == null || cfg.maxCount <= 0) continue;

            List<(RoomNode room, List<Vector2Int> spots)> eligible = BuildEligibleRooms(grid, rooms, cfg);
            if (eligible.Count == 0)
            {
                // Intento relajado: ignora maxRoomArea pero respeta clearance y tamaño mínimo.
                eligible = BuildEligibleRooms(grid, rooms, cfg, relaxedArea: true);
                if (eligible.Count == 0) continue;
            }

            Shuffle(eligible);

            int maxToPlace = Mathf.Min(cfg.maxCount, eligible.Count);
            int placed = 0;

            for (int i = 0; i < eligible.Count && placed < maxToPlace; i++)
            {
                var entry = eligible[i];
                if (entry.spots.Count == 0) continue;

                bool mustPlace = (eligible.Count - i) <= (maxToPlace - placed);
                if (!mustPlace && Random.value > cfg.spawnChance) continue;

                Vector2Int? spot = PickSpot(entry.spots);
                if (!spot.HasValue) continue;

                Vector3 worldPos = new Vector3(spot.Value.x + 0.5f, 0f, spot.Value.y + 0.5f) + offset;
                var go = Object.Instantiate(cfg.prefab, worldPos, Quaternion.identity, parent);
                go.transform.localPosition = parent.InverseTransformPoint(worldPos);
                go.transform.localRotation = Quaternion.identity;
                go.name = cfg.prefab.name;
                grid.OccupyCell(spot.Value, go);
                placed++;
            }
        }
    }

    private List<(RoomNode room, List<Vector2Int> spots)> BuildEligibleRooms(DungeonGrid grid, List<RoomNode> rooms, MissionObjectiveConfig cfg, bool relaxedArea = false)
    {
        List<(RoomNode room, List<Vector2Int> spots)> result = new List<(RoomNode, List<Vector2Int>)>();

        List<RoomNode> sortedRooms = new List<RoomNode>(rooms);
        sortedRooms.Sort((a, b) => (a.Width * a.Length).CompareTo(b.Width * b.Length));

        int minSpan = cfg.clearanceFromWall * 2 + 1; // ancho/alto mínimo para alojar el clearance
        int minAreaNeeded = minSpan * minSpan;
        int effectiveMaxArea = relaxedArea ? int.MaxValue : Mathf.Max(cfg.maxRoomArea, minAreaNeeded);

        foreach (var room in sortedRooms)
        {
            int area = room.Width * room.Length;
            if (room.Width < minSpan || room.Length < minSpan) continue;
            if (area > effectiveMaxArea) continue;

            List<Vector2Int> spots = CollectSpots(grid, room, cfg.clearanceFromWall);
            if (spots.Count == 0) continue;

            result.Add((room, spots));
        }

        return result;
    }

    private List<Vector2Int> CollectSpots(DungeonGrid grid, RoomNode room, int clearance)
    {
        clearance = Mathf.Max(0, clearance);
        List<Vector2Int> candidates = grid.GetAvailableCellsInRoom(room);
        if (candidates.Count == 0) return candidates;

        List<Vector2Int> spots = new List<Vector2Int>();
        Shuffle(candidates);

        foreach (var pos in candidates)
        {
            if (HasClearance(grid, pos, room, clearance))
            {
                spots.Add(pos);
            }
        }
        return spots;
    }

    private Vector2Int? PickSpot(List<Vector2Int> spots)
    {
        if (spots == null || spots.Count == 0) return null;
        int idx = Random.Range(0, spots.Count);
        return spots[idx];
    }

    private bool HasClearance(DungeonGrid grid, Vector2Int pos, RoomNode room, int clearance)
    {
        for (int dx = -clearance; dx <= clearance; dx++)
        {
            for (int dy = -clearance; dy <= clearance; dy++)
            {
                Vector2Int p = new Vector2Int(pos.x + dx, pos.y + dy);
                var cell = grid.GetCell(p);
                if (cell == null || cell.Type != CellType.Floor || cell.ParentRoom != room)
                    return false;
            }
        }
        return true;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }
}
