using System.Collections.Generic;
using UnityEngine;

public class ProceduralWallGenerator
{
    private int wallHeight;
    private float pillarSize;
    private Material wallMaterial;

    private readonly List<Vector3> vertices = new();
    private readonly List<int> triangles = new();
    private readonly List<Vector2> uvs = new();

    public ProceduralWallGenerator(int wallHeight, float pillarSize, Material wallMaterial)
    {
        this.wallHeight = wallHeight;
        this.pillarSize = pillarSize;
        this.wallMaterial = wallMaterial;
    }

    public ProceduralWallGenerator(int wallHeight, Material wallMaterial)
        : this(wallHeight, 0.6f, wallMaterial) { }

    public GameObject GenerateWalls(
        List<Vector3Int> horizontalWalls,
        List<Vector3Int> verticalWalls,
        HashSet<Vector3Int> pillarPositions,
        Transform parent)
    {
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();

        foreach (var pos in horizontalWalls)
        {
            AddHorizontalWall(pos, pillarPositions);
        }

        foreach (var pos in verticalWalls)
        {
            AddVerticalWall(pos, pillarPositions);
        }

        return BuildMesh(parent);
    }

    private GameObject BuildMesh(Transform parent)
    {
        GameObject go = new("ProceduralWalls");
        go.transform.parent = parent;

        Mesh mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            uv = uvs.ToArray()
        };

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>().material = wallMaterial;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;

        return go;
    }

    #region Wall geometry

    private void AddHorizontalWall(Vector3Int pos, HashSet<Vector3Int> pillars)
    {
        float cut = pillarSize * 0.5f;

        Vector3 center = new(pos.x + 0.5f, 0, pos.z);
        float left = pillars.Contains(new Vector3Int(pos.x, 0, pos.z)) ? cut : 0f;
        float right = pillars.Contains(new Vector3Int(pos.x + 1, 0, pos.z)) ? cut : 0f;

        CreateWallBox(
            center + Vector3.right * (right - left) * 0.5f,
            new Vector3(1f - left - right, wallHeight, 0.3f)
        );
    }

    private void AddVerticalWall(Vector3Int pos, HashSet<Vector3Int> pillars)
    {
        float cut = pillarSize * 0.5f;

        Vector3 center = new(pos.x, 0, pos.z + 0.5f);
        float down = pillars.Contains(new Vector3Int(pos.x, 0, pos.z)) ? cut : 0f;
        float up = pillars.Contains(new Vector3Int(pos.x, 0, pos.z + 1)) ? cut : 0f;

        CreateWallBox(
            center + Vector3.forward * (up - down) * 0.5f,
            new Vector3(0.3f, wallHeight, 1f - down - up)
        );
    }

    private void CreateWallBox(Vector3 center, Vector3 size)
    {
        if (size.x <= 0 || size.z <= 0) return;

        Vector3 h = size * 0.5f;

        Vector3[] v =
        {
            center + new Vector3(-h.x, 0, -h.z),
            center + new Vector3( h.x, 0, -h.z),
            center + new Vector3( h.x, 0,  h.z),
            center + new Vector3(-h.x, 0,  h.z),

            center + new Vector3(-h.x, size.y, -h.z),
            center + new Vector3( h.x, size.y, -h.z),
            center + new Vector3( h.x, size.y,  h.z),
            center + new Vector3(-h.x, size.y,  h.z),
        };

        AddFace(v[3], v[2], v[6], v[7]);
        AddFace(v[1], v[0], v[4], v[5]);
        AddFace(v[0], v[3], v[7], v[4]);
        AddFace(v[2], v[1], v[5], v[6]);
        AddFace(v[7], v[6], v[5], v[4]);
    }

    private void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int i = vertices.Count;
        vertices.AddRange(new[] { a, b, c, d });
        triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        uvs.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
    }

    #endregion
}
