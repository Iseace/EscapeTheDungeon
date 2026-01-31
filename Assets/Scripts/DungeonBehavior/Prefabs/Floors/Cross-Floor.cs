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
    [SerializeField] private float horizontalWidth = 10f;   // Width of the horizontal bar
    [SerializeField] private float horizontalHeight = 3f;   // Height of the horizontal bar
    [SerializeField] private float verticalWidth = 3f;      // Width of the vertical bar
    [SerializeField] private float verticalHeight = 10f;    // Height of the vertical bar

    [Header("Material Settings")]
    [SerializeField] private Material floorMaterial; // Material to apply to the floor
    [SerializeField] private float uvScale = 1f; // UV scale factor (1 = 1 Unity unit = 1 texture repeat)

    private Mesh floorMesh;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    private void OnEnable()
    {
        if (floorMesh == null)
        {
            floorMesh = new Mesh();
            floorMesh.name = "Floor";
            GetComponent<MeshFilter>().mesh = floorMesh;
        }

        meshCollider = GetComponent<MeshCollider>();
        meshRenderer = GetComponent<MeshRenderer>();
        GenerateFloorMesh();
        ApplyMaterial();
    }

    private void OnValidate()
    {
        // Regenerate mesh when values change in editor
        if (Application.isEditor && !Application.isPlaying)
        {
            OnEnable();
        }
    }

    private void ApplyMaterial()
    {
        // Apply material if one is assigned
        if (floorMaterial != null && meshRenderer != null)
        {
            meshRenderer.sharedMaterial = floorMaterial;
        }
    }

    private void GenerateFloorMesh()
    {
        floorMesh.Clear();

        // Validate inputs
        horizontalWidth = Mathf.Max(0.1f, horizontalWidth);
        horizontalHeight = Mathf.Max(0.1f, horizontalHeight);
        verticalWidth = Mathf.Max(0.1f, verticalWidth);
        verticalHeight = Mathf.Max(0.1f, verticalHeight);

        // Generate vertices and triangles
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var uvs = new System.Collections.Generic.List<Vector2>();

        GenerateCrossShape(vertices, triangles, uvs);

        // Apply to mesh
        floorMesh.vertices = vertices.ToArray();
        floorMesh.triangles = triangles.ToArray();
        floorMesh.uv = uvs.ToArray();
        floorMesh.RecalculateNormals();
        floorMesh.RecalculateBounds();

        // Update collider
        if (meshCollider != null)
        {
            meshCollider.convex = false;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = floorMesh;
        }
    }

    private void GenerateCrossShape(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, System.Collections.Generic.List<Vector2> uvs)
    {
        float halfHW = horizontalWidth / 2f;
        float halfHH = horizontalHeight / 2f;
        float halfVW = verticalWidth / 2f;
        float halfVH = verticalHeight / 2f;

        // Define the 12 vertices of the cross shape (starting from bottom, going clockwise)
        // Bottom vertical bar
        vertices.Add(new Vector3(-halfVW, 0, -halfVH));        // 0: Bottom-left of vertical bar
        vertices.Add(new Vector3(halfVW, 0, -halfVH));         // 1: Bottom-right of vertical bar
        
        // Bottom-right transition to horizontal bar
        vertices.Add(new Vector3(halfVW, 0, -halfHH));         // 2: Right side where vertical meets horizontal (bottom)
        vertices.Add(new Vector3(halfHW, 0, -halfHH));         // 3: Far right of horizontal bar (bottom)
        
        // Right side of horizontal bar
        vertices.Add(new Vector3(halfHW, 0, halfHH));          // 4: Far right of horizontal bar (top)
        vertices.Add(new Vector3(halfVW, 0, halfHH));          // 5: Right side where horizontal meets vertical (top)
        
        // Top of vertical bar
        vertices.Add(new Vector3(halfVW, 0, halfVH));          // 6: Top-right of vertical bar
        vertices.Add(new Vector3(-halfVW, 0, halfVH));         // 7: Top-left of vertical bar
        
        // Top-left transition to horizontal bar
        vertices.Add(new Vector3(-halfVW, 0, halfHH));         // 8: Left side where vertical meets horizontal (top)
        vertices.Add(new Vector3(-halfHW, 0, halfHH));         // 9: Far left of horizontal bar (top)
        
        // Left side of horizontal bar
        vertices.Add(new Vector3(-halfHW, 0, -halfHH));        // 10: Far left of horizontal bar (bottom)
        vertices.Add(new Vector3(-halfVW, 0, -halfHH));        // 11: Left side where horizontal meets vertical (bottom)

        // UV coordinates (planar mapping)
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = vertices[i];
            uvs.Add(new Vector2(v.x * uvScale, v.z * uvScale));
        }

        // Triangulate the cross shape (create triangles from center point)
        // We'll create a fan triangulation from the center
        Vector3 center = Vector3.zero;
        int centerIndex = vertices.Count;
        vertices.Add(center);
        uvs.Add(new Vector2(0, 0));

        // Create triangles around the perimeter
        for (int i = 0; i < 12; i++)
        {
            int next = (i + 1) % 12;
            triangles.AddRange(new int[] { i, next, centerIndex });
        }
    }

    public void SetDimensions(float newHorizontalWidth, float newHorizontalHeight, float newVerticalWidth, float newVerticalHeight)
    {
        horizontalWidth = newHorizontalWidth;
        horizontalHeight = newHorizontalHeight;
        verticalWidth = newVerticalWidth;
        verticalHeight = newVerticalHeight;
        GenerateFloorMesh();
    }

    public float GetHorizontalWidth() { return horizontalWidth; }
    public float GetHorizontalHeight() { return horizontalHeight; }
    public float GetVerticalWidth() { return verticalWidth; }
    public float GetVerticalHeight() { return verticalHeight; }
}