using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Instancia prefabs basándose en un DungeonGrid ya generado.
/// Cada celda equivale a 1 unidad de mundo.
/// </summary>
public static class GridPrefabPlacer
{
    public static void Place(
        DungeonGrid grid,
        List<RoomNode> rooms,
        Transform root,
        Vector3 centerOffset,
        GameObject roomPrefab,
        GameObject corridorPrefab,
        GameObject roomFloorTile,
        GameObject corridorFloorTile,
        GameObject wallPiece,
        GameObject pillarPrefab,
        int wallHeight)
    {
        if (grid == null || root == null) return;

        PlaceRooms(grid, rooms, root, centerOffset, roomPrefab, roomFloorTile);
        PlaceCorridors(grid, root, centerOffset, corridorPrefab, corridorFloorTile);
        PlaceWalls(grid, root, centerOffset, wallPiece, wallHeight);
        PlacePillars(grid, root, centerOffset, pillarPrefab, wallHeight);
    }

    private static void PlaceRooms(
        DungeonGrid grid,
        List<RoomNode> rooms,
        Transform root,
        Vector3 offset,
        GameObject roomPrefab,
        GameObject tileFallback)
    {
        if (rooms == null) return;

        if (roomPrefab != null)
        {
            var roomRoot = new GameObject("Rooms");
            roomRoot.transform.parent = root;

            foreach (var room in rooms)
            {
                Vector2 size = new Vector2(
                    room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x,
                    room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y);

                Vector3 worldPos = new Vector3(
                    room.BottomLeftAreaCorner.x + size.x * 0.5f,
                    0,
                    room.BottomLeftAreaCorner.y + size.y * 0.5f) + offset;

                var go = Object.Instantiate(roomPrefab, worldPos, Quaternion.identity, roomRoot.transform);
                Vector3 scale = go.transform.localScale;
                scale.x = size.x;
                scale.z = size.y;
                go.transform.localScale = scale;
            }
        }
        else if (tileFallback != null)
        {
            BuildFloorMesh("RoomFloorCombined", grid, root, offset, tileFallback, includeCorridors: false);
        }
    }

    private static void PlaceCorridors(
        DungeonGrid grid,
        Transform root,
        Vector3 offset,
        GameObject corridorPrefab,
        GameObject tileFallback)
    {
        if (corridorPrefab != null)
        {
            var corrRoot = new GameObject("Corridors");
            corrRoot.transform.parent = root;

            var cells = grid.GetAllCells();
            HashSet<Vector2Int> visited = new HashSet<Vector2Int>();

            foreach (var kvp in cells)
            {
                if (kvp.Value.Type != CellType.Corridor || visited.Contains(kvp.Key))
                    continue;

                int lenH = 1;
                int x = kvp.Key.x + 1;
                while (cells.TryGetValue(new Vector2Int(x, kvp.Key.y), out var c) && c.Type == CellType.Corridor && !visited.Contains(new Vector2Int(x, kvp.Key.y)))
                {
                    lenH++; x++;
                }

                int lenV = 1;
                int y = kvp.Key.y + 1;
                while (cells.TryGetValue(new Vector2Int(kvp.Key.x, y), out var c2) && c2.Type == CellType.Corridor && !visited.Contains(new Vector2Int(kvp.Key.x, y)))
                {
                    lenV++; y++;
                }

                bool horizontal = lenH >= lenV;
                int length = horizontal ? lenH : lenV;

                if (horizontal)
                {
                    for (int i = 0; i < length; i++) visited.Add(new Vector2Int(kvp.Key.x + i, kvp.Key.y));

                    float midX = kvp.Key.x + (length * 0.5f);
                    Vector3 pos = new Vector3(midX, 0, kvp.Key.y + 0.5f) + offset;
                    var go = Object.Instantiate(corridorPrefab, pos, Quaternion.identity, corrRoot.transform);
                    Vector3 scale = go.transform.localScale;
                    scale.x = length;
                    go.transform.localScale = scale;
                }
                else
                {
                    for (int i = 0; i < length; i++) visited.Add(new Vector2Int(kvp.Key.x, kvp.Key.y + i));

                    float midZ = kvp.Key.y + (length * 0.5f);
                    Vector3 pos = new Vector3(kvp.Key.x + 0.5f, 0, midZ) + offset;
                    var go = Object.Instantiate(corridorPrefab, pos, Quaternion.identity, corrRoot.transform);
                    Vector3 scale = go.transform.localScale;
                    scale.z = length;
                    go.transform.localScale = scale;
                }
            }
        }
        else if (tileFallback != null)
        {
            BuildFloorMesh("CorridorFloorCombined", grid, root, offset, tileFallback, includeCorridors: true);
        }
    }

    private static void PlaceWalls(
        DungeonGrid grid,
        Transform root,
        Vector3 offset,
        GameObject wallPiece,
        int wallHeight)
    {
        if (wallPiece == null) return;

        var wallRoot = new GameObject("Walls");
        wallRoot.transform.parent = root;

        var cells = grid.GetAllCells();
        if (cells.Count == 0) return;

        int xMin = int.MaxValue, xMax = int.MinValue, zMin = int.MaxValue, zMax = int.MinValue;
        foreach (var kvp in cells)
        {
            xMin = Mathf.Min(xMin, kvp.Key.x);
            xMax = Mathf.Max(xMax, kvp.Key.x);
            zMin = Mathf.Min(zMin, kvp.Key.y);
            zMax = Mathf.Max(zMax, kvp.Key.y);
        }

        for (int z = zMin; z <= zMax; z++)
        {
            ScanAndSpawnHorizontalEdge(wallPiece, wallRoot.transform, cells, offset, z, true, xMin, xMax, wallHeight);
            ScanAndSpawnHorizontalEdge(wallPiece, wallRoot.transform, cells, offset, z, false, xMin, xMax, wallHeight);
        }

        for (int x = xMin; x <= xMax; x++)
        {
            ScanAndSpawnVerticalEdge(wallPiece, wallRoot.transform, cells, offset, x, true, zMin, zMax, wallHeight);
            ScanAndSpawnVerticalEdge(wallPiece, wallRoot.transform, cells, offset, x, false, zMin, zMax, wallHeight);
        }
    }

    private static void ScanAndSpawnHorizontalEdge(
        GameObject wallPiece,
        Transform parent,
        Dictionary<Vector2Int, GridCell> cells,
        Vector3 offset,
        int zRow,
        bool isBottomEdge,
        int xMin,
        int xMax,
        int wallHeight)
    {
        int runStart = -1;
        for (int x = xMin; x <= xMax; x++)
        {
            bool walkable = IsWalkable(cells, x, zRow);
            bool neighborWalkable = IsWalkable(cells, x, isBottomEdge ? zRow - 1 : zRow + 1);

            bool needsWall = walkable && !neighborWalkable;

            if (needsWall && runStart == -1)
                runStart = x;
            if ((!needsWall || x == xMax) && runStart != -1)
            {
                int runEnd = needsWall && x == xMax ? x : x - 1;
                int length = runEnd - runStart + 1;
                float midX = runStart + length * 0.5f;
                float z = isBottomEdge ? zRow : zRow + 1f;
                Vector3 pos = new Vector3(midX, 0, z) + offset;
                var go = Object.Instantiate(wallPiece, pos, Quaternion.identity, parent);
                Vector3 scale = go.transform.localScale;
                scale.x = length;
                scale.y = wallHeight;
                go.transform.localScale = scale;
                runStart = -1;
            }
        }
    }

    private static void ScanAndSpawnVerticalEdge(
        GameObject wallPiece,
        Transform parent,
        Dictionary<Vector2Int, GridCell> cells,
        Vector3 offset,
        int xCol,
        bool isLeftEdge,
        int zMin,
        int zMax,
        int wallHeight)
    {
        int runStart = -1;
        for (int z = zMin; z <= zMax; z++)
        {
            bool walkable = IsWalkable(cells, xCol, z);
            bool neighborWalkable = IsWalkable(cells, isLeftEdge ? xCol - 1 : xCol + 1, z);

            bool needsWall = walkable && !neighborWalkable;

            if (needsWall && runStart == -1)
                runStart = z;
            if ((!needsWall || z == zMax) && runStart != -1)
            {
                int runEnd = needsWall && z == zMax ? z : z - 1;
                int length = runEnd - runStart + 1;
                float midZ = runStart + length * 0.5f;
                float x = isLeftEdge ? xCol : xCol + 1f;
                Vector3 pos = new Vector3(x, 0, midZ) + offset;
                var go = Object.Instantiate(wallPiece, pos, Quaternion.Euler(0f, 90f, 0f), parent);
                Vector3 scale = go.transform.localScale;
                // El prefab asume longitud en eje X; mantenemos Z para el grosor
                scale.x = length;
                scale.y = wallHeight;
                go.transform.localScale = scale;
                runStart = -1;
            }
        }
    }

    private static bool IsWalkable(Dictionary<Vector2Int, GridCell> cells, int x, int z)
    {
        if (cells.TryGetValue(new Vector2Int(x, z), out var cell))
        {
            return WallCellAnalyzer.IsWalkable(cell);
        }
        return false;
    }

    private static void PlacePillars(
        DungeonGrid grid,
        Transform root,
        Vector3 offset,
        GameObject pillarPrefab,
        int wallHeight)
    {
        if (pillarPrefab == null) return;

        var pillarRoot = new GameObject("Pillars");
        pillarRoot.transform.parent = root;

        HashSet<Vector3Int> corners = WallCellAnalyzer.DetectCorners(grid, grid.GetAllCells());
        foreach (var c in corners)
        {
            Vector3 wp = new Vector3(c.x, 0, c.z) + offset;
            var go = Object.Instantiate(pillarPrefab, wp, Quaternion.identity, pillarRoot.transform);
            go.name = $"Pillar_{c.x}_{c.z}";
        }
    }

    private static void BuildFloorMesh(
        string name,
        DungeonGrid grid,
        Transform parent,
        Vector3 offset,
        GameObject tileSource,
        bool includeCorridors)
    {
        var cells = grid.GetAllCells();
        List<Vector2Int> targets = new List<Vector2Int>();
        foreach (var kvp in cells)
        {
            var cell = kvp.Value;
            if (includeCorridors)
            {
                if (cell.Type == CellType.Corridor)
                    targets.Add(kvp.Key);
            }
            else
            {
                if (cell.Type == CellType.Floor && cell.ParentRoom != null)
                    targets.Add(kvp.Key);
            }
        }

        if (targets.Count == 0) return;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        foreach (var p in targets)
        {
            int idx = vertices.Count;
            vertices.Add(new Vector3(p.x, 0, p.y) + offset);
            vertices.Add(new Vector3(p.x + 1, 0, p.y) + offset);
            vertices.Add(new Vector3(p.x, 0, p.y + 1) + offset);
            vertices.Add(new Vector3(p.x + 1, 0, p.y + 1) + offset);

            uvs.Add(new Vector2(p.x, p.y));
            uvs.Add(new Vector2(p.x + 1, p.y));
            uvs.Add(new Vector2(p.x, p.y + 1));
            uvs.Add(new Vector2(p.x + 1, p.y + 1));

            triangles.Add(idx + 2);
            triangles.Add(idx + 3);
            triangles.Add(idx + 0);

            triangles.Add(idx + 0);
            triangles.Add(idx + 3);
            triangles.Add(idx + 1);
        }

        Mesh mesh = new Mesh();
        mesh.name = name;
        mesh.indexFormat = vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        go.transform.parent = parent;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        var mc = go.GetComponent<MeshCollider>();
        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;

        var srcRenderer = tileSource.GetComponentInChildren<Renderer>();
        if (srcRenderer != null)
        {
            mr.sharedMaterial = srcRenderer.sharedMaterial;
        }
    }
}
