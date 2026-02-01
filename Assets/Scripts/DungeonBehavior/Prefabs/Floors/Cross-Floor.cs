using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class CrossShapedFloor : MonoBehaviour
{
    public enum RoomType { Floor }
    public RoomType Type { get { return RoomType.Floor; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Floor;

    [Header("Cross-Shape Dimensions")]
    [SerializeField] private float horizontalWidth = 14f;   // Total width of the horizontal bar (X axis)
    [SerializeField] private float horizontalHeight = 6f;   // Thickness of the horizontal bar (Z axis)
    [SerializeField] private float verticalWidth = 6f;      // Thickness of the vertical bar (X axis)
    [SerializeField] private float verticalHeight = 14f;    // Total height of the vertical bar (Z axis)

    [Header("Material Settings")]
    [SerializeField] private Material floorMaterial;
    [SerializeField] private float uvScale = 1f;

    private Mesh floorMesh;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    private void OnEnable()
    {
        if (floorMesh == null)
        {
            floorMesh = new Mesh();
            floorMesh.name = "CrossFloor";
            GetComponent<MeshFilter>().mesh = floorMesh;
        }

        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        GenerateFloorMesh();
        ApplyMaterial();
    }

    private void OnValidate()
    {
        if (Application.isEditor && !Application.isPlaying)
        {
            OnEnable();
        }
    }

    private void ApplyMaterial()
    {
        if (floorMaterial != null && meshRenderer != null)
        {
            meshRenderer.sharedMaterial = floorMaterial;
        }
    }

    private void GenerateFloorMesh()
    {
        floorMesh.Clear();

        // Clamp all dimensions to a safe minimum
        horizontalWidth  = Mathf.Max(0.1f, horizontalWidth);
        horizontalHeight = Mathf.Max(0.1f, horizontalHeight);
        verticalWidth    = Mathf.Max(0.1f, verticalWidth);
        verticalHeight   = Mathf.Max(0.1f, verticalHeight);

        var vertices  = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var uvs       = new System.Collections.Generic.List<Vector2>();

        GenerateCrossShape(vertices, triangles, uvs);

        floorMesh.vertices  = vertices.ToArray();
        floorMesh.triangles = triangles.ToArray();
        floorMesh.uv        = uvs.ToArray();
        floorMesh.RecalculateNormals();
        floorMesh.RecalculateBounds();

        if (meshCollider != null)
        {
            meshCollider.convex     = false;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = floorMesh;
        }
    }

    /*
     * Cross shape – 12 vertices, numbered clockwise from the bottom-left
     * of the vertical bar.  This layout is the exact order that Pillars.cs
     * uses in GetCrossShapedFloorCorners(), so pillar snapping works out
     * of the box.
     *
     *          5 ────────── 6
     *          |            |
     *   9 ─── 8            7 ─── 4          (not to scale)
     *   |                        |
     *  10 ──  11           2 ─── 3
     *          |            |
     *          0 ────────── 1
     *
     *  halfHW  = horizontalWidth  / 2   (outer X)
     *  halfHH  = horizontalHeight / 2   (inner Z, where horiz bar sits)
     *  halfVW  = verticalWidth    / 2   (inner X, where vert bar sits)
     *  halfVH  = verticalHeight   / 2   (outer Z)
     */
    private void GenerateCrossShape(
        System.Collections.Generic.List<Vector3> vertices,
        System.Collections.Generic.List<int>     triangles,
        System.Collections.Generic.List<Vector2> uvs)
    {
        float halfHW = horizontalWidth  / 2f;
        float halfHH = horizontalHeight / 2f;
        float halfVW = verticalWidth    / 2f;
        float halfVH = verticalHeight   / 2f;

        // --- 12 vertices (y = 0 plane, clockwise from bottom-left) ---
        vertices.Add(new Vector3(-halfVW,  0, -halfVH));  //  0  bottom-left  of vertical bar
        vertices.Add(new Vector3( halfVW,  0, -halfVH));  //  1  bottom-right of vertical bar
        vertices.Add(new Vector3( halfVW,  0, -halfHH));  //  2  inner corner bottom-right
        vertices.Add(new Vector3( halfHW,  0, -halfHH));  //  3  outer corner bottom-right
        vertices.Add(new Vector3( halfHW,  0,  halfHH));  //  4  outer corner top-right
        vertices.Add(new Vector3( halfVW,  0,  halfHH));  //  5  inner corner top-right  (note: see diagram – this is vertex 5 in the ring)
        vertices.Add(new Vector3( halfVW,  0,  halfVH));  //  6  top-right of vertical bar
        vertices.Add(new Vector3(-halfVW,  0,  halfVH));  //  7  top-left  of vertical bar
        vertices.Add(new Vector3(-halfVW,  0,  halfHH));  //  8  inner corner top-left
        vertices.Add(new Vector3(-halfHW,  0,  halfHH));  //  9  outer corner top-left
        vertices.Add(new Vector3(-halfHW,  0, -halfHH));  // 10  outer corner bottom-left
        vertices.Add(new Vector3(-halfVW,  0, -halfHH));  // 11  inner corner bottom-left

        // --- UV coordinates (planar world-space mapping, same style as T-Floor) ---
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = vertices[i];
            uvs.Add(new Vector2(v.x * uvScale, v.z * uvScale));
        }

        // --- Triangulation (fan from vertex 0 is not convex, so we split manually) ---
        // The cross can be divided into 3 rectangles that share edges cleanly:
        //   A) Bottom arm   : verts 0, 1, 2, 11
        //   B) Horizontal bar (full width): verts 11, 2, 5, 6 … actually easier as
        //      the full horizontal strip including the center:  10, 3, 4, 9
        //      But overlapping the center is fine if we just triangulate the 12-gon.
        //
        // Simplest correct approach: split into 10 triangles via a central fan
        // anchored at vertex 0.  However vertex 0 does NOT see all other vertices
        // (the shape is non-convex), so we use an ear-based decomposition instead.
        //
        // We break the cross into 5 non-overlapping quads (2 tri each = 10 tri total):
        //   Quad A – bottom vertical arm    : 0,  1,  2, 11
        //   Quad B – right horizontal arm   : 2,  3,  4,  5
        //   Quad C – center rectangle       : 11, 2,  5,  8   (reuses shared edges)
        //   Quad D – top vertical arm       : 8,  5,  6,  7   (note: 5→6 is the shared edge)
        //   Quad E – left horizontal arm    : 10, 11, 8,  9

        // Quad A – bottom arm
        triangles.AddRange(new int[] { 0, 2, 1 });
        triangles.AddRange(new int[] { 0, 11, 2 });

        // Quad B – right arm
        triangles.AddRange(new int[] { 2, 4, 3 });
        triangles.AddRange(new int[] { 2, 5, 4 });

        // Quad C – center
        triangles.AddRange(new int[] { 11, 5, 2 });
        triangles.AddRange(new int[] { 11, 8, 5 });

        // Quad D – top arm
        triangles.AddRange(new int[] { 8, 6, 5 });
        triangles.AddRange(new int[] { 8, 7, 6 });

        // Quad E – left arm
        triangles.AddRange(new int[] { 10, 8, 11 });
        triangles.AddRange(new int[] { 10, 9, 8 });
    }

    // ─── Public setters / getters expected by Door.cs and Pillars.cs ────────

    public void SetDimensions(float newHorizontalWidth, float newHorizontalHeight,
                              float newVerticalWidth,   float newVerticalHeight)
    {
        horizontalWidth  = newHorizontalWidth;
        horizontalHeight = newHorizontalHeight;
        verticalWidth    = newVerticalWidth;
        verticalHeight   = newVerticalHeight;
        GenerateFloorMesh();
    }

    public float GetHorizontalWidth()  { return horizontalWidth; }
    public float GetHorizontalHeight() { return horizontalHeight; }
    public float GetVerticalWidth()    { return verticalWidth; }
    public float GetVerticalHeight()   { return verticalHeight; }
}