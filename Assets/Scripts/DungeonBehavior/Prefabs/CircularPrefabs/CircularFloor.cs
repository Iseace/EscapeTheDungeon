using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Circularfloor : MonoBehaviour
{
    public enum RoomType { Floor }
    public RoomType Type { get { return RoomType.Floor; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Floor;

    [Header("Floor Dimensions")]
    [SerializeField] private float width = 10f;
    [SerializeField] private float height = 10f;

    [Header("Corner Rounding")]
    [Range(0f, 5f)]
    [SerializeField] public float cornerRadius = 1f;
    [SerializeField] public int cornerSegments = 8;

    [Header("Mesh Quality")]
    [SerializeField] private int subdivisions = 1;

    [Header("Material Settings")]
    [SerializeField] private Material floorMaterial; // Material to apply to the floor

    private Mesh floorMesh;
    private MeshCollider meshCollider;
    private MeshRenderer meshRenderer;

    private void OnEnable()
    {
        if (floorMesh == null)
        {
            floorMesh = new Mesh();
            floorMesh.name = "Circular Floor";
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
        cornerRadius = Mathf.Max(0, Mathf.Min(cornerRadius, Mathf.Min(width, height) / 2f));
        cornerSegments = Mathf.Max(1, cornerSegments);
        subdivisions = Mathf.Max(1, subdivisions);

        // Generate vertices and triangles
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        var uvs = new System.Collections.Generic.List<Vector2>();

        if (cornerRadius > 0.01f)
        {
            // Create rounded rectangle
            GenerateRoundedRectangle(vertices, triangles, uvs);
        }
        else
        {
            // Create simple square
            GenerateSimpleSquare(vertices, triangles, uvs);
        }

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

    private void GenerateSimpleSquare(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, System.Collections.Generic.List<Vector2> uvs)
    {
        float halfW = width / 2f;
        float halfH = height / 2f;

        // Four corners
        vertices.Add(new Vector3(-halfW, 0, -halfH)); // Bottom-left
        vertices.Add(new Vector3(halfW, 0, -halfH));  // Bottom-right
        vertices.Add(new Vector3(halfW, 0, halfH));   // Top-right
        vertices.Add(new Vector3(-halfW, 0, halfH));  // Top-left

        // UV coordinates
        uvs.Add(new Vector2(0, 0)); // Bottom-left
        uvs.Add(new Vector2(1, 0)); // Bottom-right
        uvs.Add(new Vector2(1, 1)); // Top-right
        uvs.Add(new Vector2(0, 1)); // Top-left

        // Two triangles to form a square
        triangles.AddRange(new int[] { 0, 2, 1 }); // First triangle
        triangles.AddRange(new int[] { 0, 3, 2 }); // Second triangle
    }

    private void GenerateRoundedRectangle(System.Collections.Generic.List<Vector3> vertices, System.Collections.Generic.List<int> triangles, System.Collections.Generic.List<Vector2> uvs)
    {
        float halfW = width / 2f;
        float halfH = height / 2f;
        float radius = cornerRadius;

        // Create a grid of vertices for better triangulation
        int gridResolution = 4 + cornerSegments;
        Vector3[,] grid = new Vector3[gridResolution, gridResolution];

        // Fill the grid with positions, using distance field for rounding
        for (int y = 0; y < gridResolution; y++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                float u = (float)x / (gridResolution - 1);
                float v = (float)y / (gridResolution - 1);

                // Map to rectangle coordinates
                float posX = -halfW + u * width;
                float posZ = -halfH + v * height;

                // Calculate distance from nearest edge
                float distFromLeft = posX + halfW;
                float distFromRight = halfW - posX;
                float distFromTop = halfH - posZ;
                float distFromBottom = posZ + halfH;

                float minDistX = Mathf.Min(distFromLeft, distFromRight);
                float minDistZ = Mathf.Min(distFromTop, distFromBottom);

                // Apply rounding if within corner region
                if (minDistX < radius && minDistZ < radius)
                {
                    // We're in a corner region
                    float cornerX = (distFromLeft < radius) ? (-halfW + radius) : (halfW - radius);
                    float cornerZ = (distFromBottom < radius) ? (-halfH + radius) : (halfH - radius);

                    // Push vertex outward along corner arc
                    float dx = posX - cornerX;
                    float dz = posZ - cornerZ;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);

                    if (dist > 0.001f)
                    {
                        float targetDist = Mathf.Min(dist, radius);
                        posX = cornerX + (dx / dist) * targetDist;
                        posZ = cornerZ + (dz / dist) * targetDist;
                    }
                }

                grid[x, y] = new Vector3(posX, 0, posZ);
            }
        }

        // Convert grid to vertex list
        int vertexIndex = 0;
        int[,] vertexIndices = new int[gridResolution, gridResolution];

        for (int y = 0; y < gridResolution; y++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                vertexIndices[x, y] = vertexIndex++;
                Vector3 vertexPos = grid[x, y];
                vertices.Add(vertexPos);

                // Add UV coordinates based on original rectangular position (static material)
                float u = (vertexPos.x + halfW) / width;
                float v = (vertexPos.z + halfH) / height;
                uvs.Add(new Vector2(u, v));
            }
        }

        // Create triangles from grid
        for (int y = 0; y < gridResolution - 1; y++)
        {
            for (int x = 0; x < gridResolution - 1; x++)
            {
                int v0 = vertexIndices[x, y];
                int v1 = vertexIndices[x + 1, y];
                int v2 = vertexIndices[x + 1, y + 1];
                int v3 = vertexIndices[x, y + 1];

                // Two triangles per quad
                triangles.Add(v0);
                triangles.Add(v2);
                triangles.Add(v1);

                triangles.Add(v0);
                triangles.Add(v3);
                triangles.Add(v2);
            }
        }
    }


    public void SetDimensions(float newWidth, float newHeight, float newCornerRadius)
    {
        width = newWidth;
        height = newHeight;
        cornerRadius = newCornerRadius;
        GenerateFloorMesh();
    }

    public void SetCornerRadius(float newRadius)
    {
        cornerRadius = newRadius;
        GenerateFloorMesh();
    }
}