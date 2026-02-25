using System.Collections.Generic;
using UnityEngine;

public class AnchorGenerationConfig
{
    public Vector2Int AnchorSize = new Vector2Int(12, 12);
    public int MaxRooms = 16;
    public int MaxAttemptsPerRoom = 40;
    public int Padding = 1;
    public int CorridorWidth = 3;
    public int MinDistanceFromCenter = 8;
    public int MaxDistanceFromCenter = 28;
    public int ExtraConnections = 1;
    public Vector2Int RoomSizeMin = new Vector2Int(8, 8);
    public Vector2Int RoomSizeMax = new Vector2Int(16, 16);
    public int AdditionalOuterRooms = 0;
    public float OuterRoomStartNormalized = 0.65f;
    public float OuterRoomBias = 0.75f;
    public float OuterDistanceMultiplier = 1.35f;
}

/// <summary>
/// Generador basado en sala ancla central + MST de conexiones y pasillos en L.
/// </summary>
public class AnchorDungeonGenerator
{
    private readonly int width;
    private readonly int height;

    public DungeonGrid Grid { get; private set; }
    public List<RoomNode> RoomList { get; private set; } = new List<RoomNode>();
    public RoomNode CentralRoom { get; private set; }

    public AnchorDungeonGenerator(int dungeonWidth, int dungeonLength)
    {
        width = dungeonWidth;
        height = dungeonLength;
        Grid = new DungeonGrid(width, height);
    }

    public Vector3 GetCenterOffset()
    {
        return new Vector3(-width / 2f, 0f, -height / 2f);
    }

    public List<RoomNode> Generate(AnchorGenerationConfig config)
    {
        Grid = new DungeonGrid(width, height);
        RoomList = new List<RoomNode>();

        PlaceAnchor(config);
        PlaceSecondaryRooms(config);

        var edges = BuildAllEdges(RoomList);
        var mst = BuildMstEdges(RoomList, edges);
        var connections = AddExtraConnections(edges, mst, config.ExtraConnections);

        foreach (var e in connections)
        {
            CarveCorridor(RoomList[e.A], RoomList[e.B], config.CorridorWidth);
        }

        RepairConnectivity(config.CorridorWidth);

        return RoomList;
    }

    private void PlaceAnchor(AnchorGenerationConfig config)
    {
        Vector2Int center = new Vector2Int(width / 2, height / 2);
        Vector2Int half = new Vector2Int(Mathf.Max(1, config.AnchorSize.x) / 2, Mathf.Max(1, config.AnchorSize.y) / 2);
        Vector2Int bl = new Vector2Int(center.x - half.x, center.y - half.y);
        Vector2Int tr = new Vector2Int(center.x + half.x, center.y + half.y);

        bl.x = Mathf.Clamp(bl.x, 0, width - 1);
        bl.y = Mathf.Clamp(bl.y, 0, height - 1);
        tr.x = Mathf.Clamp(tr.x, bl.x + 1, width);
        tr.y = Mathf.Clamp(tr.y, bl.y + 1, height);

        CentralRoom = new RoomNode(bl, tr, null, RoomList.Count);
        RoomList.Add(CentralRoom);
        PaintRoom(CentralRoom);
    }

    private void PlaceSecondaryRooms(AnchorGenerationConfig config)
    {
        int baseTarget = Mathf.Max(0, config.MaxRooms - 1);
        int outerTarget = Mathf.Max(0, config.AdditionalOuterRooms);

        TryPlaceRooms(config, baseTarget, forceOuterBand: false);
        TryPlaceRooms(config, outerTarget, forceOuterBand: true);
    }

    private void TryPlaceRooms(AnchorGenerationConfig config, int target, bool forceOuterBand)
    {
        Vector2 center = new Vector2(width / 2f, height / 2f);

        int placedCount = 0;
        int totalAttemptsBudget = Mathf.Max(1, target) * Mathf.Max(1, config.MaxAttemptsPerRoom) * (forceOuterBand ? 4 : 2);

        for (int attempts = 0; attempts < totalAttemptsBudget && placedCount < target; attempts++)
        {
            int w = Random.Range(config.RoomSizeMin.x, config.RoomSizeMax.x + 1);
            int h = Random.Range(config.RoomSizeMin.y, config.RoomSizeMax.y + 1);

            // Fallback progresivo: en intentos tardíos reducimos tamaño para poder encajar más salas.
            float latePhase = target > 0 ? (float)placedCount / target : 1f;
            bool tightenRooms = forceOuterBand && (attempts > totalAttemptsBudget * 0.5f || latePhase < 0.8f);
            if (tightenRooms)
            {
                w = Mathf.Max(4, Mathf.RoundToInt(w * 0.8f));
                h = Mathf.Max(4, Mathf.RoundToInt(h * 0.8f));
            }

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = SampleDistance(config, forceOuterBand);
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            Vector2Int c = new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));

            Vector2Int bl = new Vector2Int(c.x - w / 2, c.y - h / 2);
            Vector2Int tr = new Vector2Int(bl.x + w, bl.y + h);

            int effectivePadding = forceOuterBand ? Mathf.Max(0, config.Padding - 1) : config.Padding;
            if (!Fits(bl, tr)) continue;
            if (Overlaps(bl, tr, effectivePadding)) continue;

            RoomNode room = new RoomNode(bl, tr, null, RoomList.Count);
            RoomList.Add(room);
            PaintRoom(room);
            placedCount++;
        }
    }

    private float SampleDistance(AnchorGenerationConfig config, bool forceOuterBand)
    {
        float minDist = Mathf.Max(0f, config.MinDistanceFromCenter);
        float maxDist = Mathf.Max(minDist, config.MaxDistanceFromCenter);

        if (forceOuterBand)
        {
            float maxByMap = Mathf.Min(width, height) * 0.48f;
            maxDist = Mathf.Min(maxByMap, maxDist * Mathf.Max(1f, config.OuterDistanceMultiplier));
            minDist = Mathf.Min(minDist, maxDist);
        }

        if (Mathf.Approximately(minDist, maxDist))
            return minDist;

        float t;
        if (forceOuterBand)
        {
            float outerStart = Mathf.Clamp01(config.OuterRoomStartNormalized);
            float outerT = Mathf.Pow(Random.value, Mathf.Max(0.01f, config.OuterRoomBias));
            t = Mathf.Lerp(outerStart, 1f, outerT);
        }
        else
        {
            t = Random.value;
        }

        return Mathf.Lerp(minDist, maxDist, t);
    }

    private void PaintRoom(RoomNode room)
    {
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                Grid.SetCellType(new Vector2Int(x, y), CellType.Floor, room);
            }
        }
    }

    private bool Fits(Vector2Int bl, Vector2Int tr)
    {
        return bl.x >= 0 && bl.y >= 0 && tr.x <= width && tr.y <= height;
    }

    private bool Overlaps(Vector2Int bl, Vector2Int tr, int padding)
    {
        for (int i = 0; i < RoomList.Count; i++)
        {
            var r = RoomList[i];
            if (RectOverlap(bl, tr, r.BottomLeftAreaCorner - Vector2Int.one * padding, r.TopRightAreaCorner + Vector2Int.one * padding))
            {
                return true;
            }
        }
        return false;
    }

    private bool RectOverlap(Vector2Int blA, Vector2Int trA, Vector2Int blB, Vector2Int trB)
    {
        bool separate = trA.x <= blB.x || trB.x <= blA.x || trA.y <= blB.y || trB.y <= blA.y;
        return !separate;
    }

    private struct Edge
    {
        public int A;
        public int B;
        public float Dist;
    }

    private List<Edge> BuildAllEdges(List<RoomNode> rooms)
    {
        var edges = new List<Edge>();
        for (int i = 0; i < rooms.Count; i++)
        {
            for (int j = i + 1; j < rooms.Count; j++)
            {
                float d = Vector2.Distance(rooms[i].GetCenterPosition(), rooms[j].GetCenterPosition());
                edges.Add(new Edge { A = i, B = j, Dist = d });
            }
        }
        return edges;
    }

    private List<Edge> BuildMstEdges(List<RoomNode> rooms, List<Edge> allEdges)
    {
        var sorted = new List<Edge>(allEdges);
        sorted.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        var uf = new UnionFind(rooms.Count);
        var mst = new List<Edge>();

        foreach (var e in sorted)
        {
            if (uf.Union(e.A, e.B))
            {
                mst.Add(e);
                if (mst.Count == rooms.Count - 1) break;
            }
        }

        return mst;
    }

    private List<Edge> AddExtraConnections(List<Edge> allEdges, List<Edge> baseEdges, int extra)
    {
        var chosen = new List<Edge>(baseEdges);
        if (extra <= 0) return chosen;

        var pool = new List<Edge>(allEdges);
        pool.Sort((a, b) => a.Dist.CompareTo(b.Dist));

        int added = 0;
        for (int i = 0; i < pool.Count && added < extra; i++)
        {
            if (ContainsEdge(chosen, pool[i])) continue;
            chosen.Add(pool[i]);
            added++;
        }

        return chosen;
    }

    private bool ContainsEdge(List<Edge> list, Edge edge)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if ((e.A == edge.A && e.B == edge.B) || (e.A == edge.B && e.B == edge.A))
            {
                return true;
            }
        }
        return false;
    }

    private void CarveCorridor(RoomNode a, RoomNode b, int widthCells)
    {
        Vector2Int start = a.GetCenterPosition();
        Vector2Int end = b.GetCenterPosition();
        bool horizontalFirst = Random.value > 0.5f;
        int half = widthCells / 2;
        int halfUp = (widthCells + 1) / 2;
        // Añadimos un parche cuadrado en el codo para evitar esquinas "huecas".

        if (horizontalFirst)
        {
            PaintCorridorRect(new Vector2Int(Mathf.Min(start.x, end.x), start.y - widthCells / 2), new Vector2Int(Mathf.Max(start.x, end.x) + 1, start.y + (widthCells + 1) / 2));
            PaintCorridorRect(new Vector2Int(end.x - widthCells / 2, Mathf.Min(start.y, end.y)), new Vector2Int(end.x + (widthCells + 1) / 2, Mathf.Max(start.y, end.y) + 1));

            Vector2Int elbowCenter = new Vector2Int(end.x, start.y);
            PaintCorridorRect(
                new Vector2Int(elbowCenter.x - half, elbowCenter.y - half),
                new Vector2Int(elbowCenter.x + halfUp, elbowCenter.y + halfUp));
        }
        else
        {
            PaintCorridorRect(new Vector2Int(start.x - widthCells / 2, Mathf.Min(start.y, end.y)), new Vector2Int(start.x + (widthCells + 1) / 2, Mathf.Max(start.y, end.y) + 1));
            PaintCorridorRect(new Vector2Int(Mathf.Min(start.x, end.x), end.y - widthCells / 2), new Vector2Int(Mathf.Max(start.x, end.x) + 1, end.y + (widthCells + 1) / 2));

            Vector2Int elbowCenter = new Vector2Int(start.x, end.y);
            PaintCorridorRect(
                new Vector2Int(elbowCenter.x - half, elbowCenter.y - half),
                new Vector2Int(elbowCenter.x + halfUp, elbowCenter.y + halfUp));
        }
    }

    private void PaintCorridorRect(Vector2Int bl, Vector2Int tr)
    {
        for (int x = bl.x; x < tr.x; x++)
        {
            for (int y = bl.y; y < tr.y; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                var cell = Grid.GetCell(pos);
                if (cell == null) continue;
                if (cell.Type == CellType.Floor) continue;
                Grid.SetCellType(pos, CellType.Corridor);
            }
        }
    }

    private void RepairConnectivity(int corridorWidth)
    {
        if (CentralRoom == null) return;

        var reachable = FloodFill(CentralRoom.GetCenterPosition());
        for (int i = 0; i < RoomList.Count; i++)
        {
            var room = RoomList[i];
            Vector2Int center = room.GetCenterPosition();
            if (reachable.Contains(center)) continue;

            int nearest = FindNearestReachableRoom(center, reachable);
            if (nearest >= 0)
            {
                CarveCorridor(room, RoomList[nearest], corridorWidth);
                reachable = FloodFill(CentralRoom.GetCenterPosition());
            }
        }
    }

    private HashSet<Vector2Int> FloodFill(Vector2Int start)
    {
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var d in dirs)
            {
                Vector2Int np = p + d;
                if (np.x < 0 || np.x >= width || np.y < 0 || np.y >= height) continue;
                if (visited.Contains(np)) continue;
                var cell = Grid.GetCell(np);
                if (cell == null) continue;
                if (cell.Type != CellType.Floor && cell.Type != CellType.Corridor) continue;
                visited.Add(np);
                queue.Enqueue(np);
            }
        }

        return visited;
    }

    private int FindNearestReachableRoom(Vector2Int center, HashSet<Vector2Int> reachable)
    {
        int nearest = -1;
        float best = float.MaxValue;
        for (int i = 0; i < RoomList.Count; i++)
        {
            Vector2Int c = RoomList[i].GetCenterPosition();
            if (!reachable.Contains(c)) continue;
            float d = Vector2.Distance(center, c);
            if (d < best)
            {
                best = d;
                nearest = i;
            }
        }
        return nearest;
    }

    private class UnionFind
    {
        private readonly int[] parent;
        private readonly int[] rank;

        public UnionFind(int size)
        {
            parent = new int[size];
            rank = new int[size];
            for (int i = 0; i < size; i++)
            {
                parent[i] = i;
                rank[i] = 0;
            }
        }

        public int Find(int x)
        {
            if (parent[x] != x)
                parent[x] = Find(parent[x]);
            return parent[x];
        }

        public bool Union(int a, int b)
        {
            int pa = Find(a);
            int pb = Find(b);
            if (pa == pb) return false;
            if (rank[pa] < rank[pb])
            {
                parent[pa] = pb;
            }
            else if (rank[pa] > rank[pb])
            {
                parent[pb] = pa;
            }
            else
            {
                parent[pb] = pa;
                rank[pa]++;
            }
            return true;
        }
    }
}
