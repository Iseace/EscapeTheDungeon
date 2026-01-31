using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class RectangularFloor : MonoBehaviour
{
    public enum RoomType { Floor }
    public RoomType Type { get { return RoomType.Floor; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Floor;

    [Header("Floor Dimensions")]
    [SerializeField] private float width = 10f;
    [SerializeField] private float height = 10f;

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
        width = Mathf.Max(0.1f, width);
        height = Mathf.Max(0.1f, height);

        // Generate vertices and triangles
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var uvs = new System.Collections.Generic.List<Vector2>();

        GenerateRectangle(vertices, triangles, uvs);

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

    private void GenerateRectangle(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, System.Collections.Generic.List<Vector2> uvs)
    {
        float halfW = width / 2f;
        float halfH = height / 2f;

        // Four corners
        vertices.Add(new Vector3(-halfW, 0, -halfH)); // Bottom-left
        vertices.Add(new Vector3(halfW, 0, -halfH));  // Bottom-right
        vertices.Add(new Vector3(halfW, 0, halfH));   // Top-right
        vertices.Add(new Vector3(-halfW, 0, halfH));  // Top-left

        // UV coordinates - scaled by actual world dimensions
        uvs.Add(new Vector2(0, 0) * uvScale);                    // Bottom-left
        uvs.Add(new Vector2(width, 0) * uvScale);                // Bottom-right
        uvs.Add(new Vector2(width, height) * uvScale);           // Top-right
        uvs.Add(new Vector2(0, height) * uvScale);               // Top-left

        // Two triangles to form a rectangle
        triangles.AddRange(new int[] { 0, 2, 1 }); // First triangle
        triangles.AddRange(new int[] { 0, 3, 2 }); // Second triangle
    }

    public void SetDimensions(float newWidth, float newHeight)
    {
        width = newWidth;
        height = newHeight;
        GenerateFloorMesh();
    }

    public float GetWidth()
    {
        return width;
    }

    public float GetHeight()
    {
        return height;
    }
}