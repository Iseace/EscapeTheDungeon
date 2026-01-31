using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class TShapedFloor : MonoBehaviour
{
    public enum RoomType { Floor }
    public RoomType Type { get { return RoomType.Floor; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Floor;

    [Header("T-Shape Dimensions")]
    [SerializeField] private float topWidth = 10f;      // Width of the top horizontal bar
    [SerializeField] private float topHeight = 3f;      // Height of the top horizontal bar
    [SerializeField] private float stemWidth = 3f;      // Width of the vertical stem
    [SerializeField] private float stemHeight = 7f;     // Height of the vertical stem

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
        topWidth = Mathf.Max(0.1f, topWidth);
        topHeight = Mathf.Max(0.1f, topHeight);
        stemWidth = Mathf.Max(0.1f, stemWidth);
        stemHeight = Mathf.Max(0.1f, stemHeight);

        // Generate vertices and triangles
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var uvs = new System.Collections.Generic.List<Vector2>();

        GenerateTShape(vertices, triangles, uvs);

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

    private void GenerateTShape(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, System.Collections.Generic.List<Vector2> uvs)
    {
        float halfTopW = topWidth / 2f;
        float halfStemW = stemWidth / 2f;
        
        // Calculate positions (T shape centered at origin)
        float topY = stemHeight / 2f;
        float bottomY = -stemHeight / 2f;
        float topBarBottom = topY - topHeight;

        // Define the 8 vertices of the T-shape (clockwise from bottom-left)
        // Bottom of stem
        vertices.Add(new Vector3(-halfStemW, 0, bottomY));     // 0: Bottom-left of stem
        vertices.Add(new Vector3(halfStemW, 0, bottomY));      // 1: Bottom-right of stem
        
        // Where stem meets top bar
        vertices.Add(new Vector3(halfStemW, 0, topBarBottom));  // 2: Right side of stem at top bar
        vertices.Add(new Vector3(halfTopW, 0, topBarBottom));   // 3: Right edge of top bar (bottom)
        
        // Top of top bar
        vertices.Add(new Vector3(halfTopW, 0, topY));           // 4: Top-right of top bar
        vertices.Add(new Vector3(-halfTopW, 0, topY));          // 5: Top-left of top bar
        
        // Left side of top bar
        vertices.Add(new Vector3(-halfTopW, 0, topBarBottom));  // 6: Left edge of top bar (bottom)
        vertices.Add(new Vector3(-halfStemW, 0, topBarBottom)); // 7: Left side of stem at top bar

        // UV coordinates (simplified planar mapping)
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = vertices[i];
            uvs.Add(new Vector2(v.x * uvScale, v.z * uvScale));
        }

        // Triangulate the T-shape
        // We need to divide this into triangles
        triangles.AddRange(new int[] { 0, 2, 1 }); // Bottom stem triangle 1
        triangles.AddRange(new int[] { 0, 7, 2 }); // Bottom stem triangle 2
        triangles.AddRange(new int[] { 7, 3, 2 }); // Middle section triangle 1
        triangles.AddRange(new int[] { 7, 6, 3 }); // Middle section triangle 2
        triangles.AddRange(new int[] { 6, 4, 3 }); // Top bar triangle 1
        triangles.AddRange(new int[] { 6, 5, 4 }); // Top bar triangle 2
    }

    public void SetDimensions(float newTopWidth, float newTopHeight, float newStemWidth, float newStemHeight)
    {
        topWidth = newTopWidth;
        topHeight = newTopHeight;
        stemWidth = newStemWidth;
        stemHeight = newStemHeight;
        GenerateFloorMesh();
    }

    public float GetTopWidth() { return topWidth; }
    public float GetTopHeight() { return topHeight; }
    public float GetStemWidth() { return stemWidth; }
    public float GetStemHeight() { return stemHeight; }
}