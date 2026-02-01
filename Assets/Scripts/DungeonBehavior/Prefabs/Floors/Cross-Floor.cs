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
    [SerializeField] private float horizontalHeight = 7f;   // Height of the horizontal bar
    [SerializeField] private float verticalWidth = 7f;      // Width of the vertical bar
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
        float halfHorzW = horizontalWidth / 2f;
        float halfHorzH = horizontalHeight / 2f;
        float halfVertW = verticalWidth / 2f;
        float halfVertH = verticalHeight / 2f;
        
        // Define the 12 vertices of the cross shape (clockwise from bottom-left)
        // The cross is centered at origin
        
        // Bottom vertical bar (bottom section)
        vertices.Add(new Vector3(-halfVertW, 0, -halfVertH));      // 0: Bottom-left of vertical bar
        vertices.Add(new Vector3(halfVertW, 0, -halfVertH));       // 1: Bottom-right of vertical bar
        
        // Where vertical bar meets horizontal bar (bottom-left transition)
        vertices.Add(new Vector3(halfVertW, 0, -halfHorzH));       // 2: Right side of vertical bar at horizontal level
        vertices.Add(new Vector3(halfHorzW, 0, -halfHorzH));       // 3: Right edge of horizontal bar (bottom)
        
        // Right side of horizontal bar
        vertices.Add(new Vector3(halfHorzW, 0, halfHorzH));        // 4: Top-right of horizontal bar
        vertices.Add(new Vector3(halfVertW, 0, halfHorzH));        // 5: Right side of vertical bar (top transition)
        
        // Top vertical bar (top section)
        vertices.Add(new Vector3(halfVertW, 0, halfVertH));        // 6: Top-right of vertical bar
        vertices.Add(new Vector3(-halfVertW, 0, halfVertH));       // 7: Top-left of vertical bar
        
        // Left side of vertical bar (top transition)
        vertices.Add(new Vector3(-halfVertW, 0, halfHorzH));       // 8: Left side of vertical bar at horizontal level
        vertices.Add(new Vector3(-halfHorzW, 0, halfHorzH));       // 9: Left edge of horizontal bar (top)
        
        // Left side of horizontal bar
        vertices.Add(new Vector3(-halfHorzW, 0, -halfHorzH));      // 10: Bottom-left of horizontal bar
        vertices.Add(new Vector3(-halfVertW, 0, -halfHorzH));      // 11: Left side of vertical bar (bottom transition)

        // UV coordinates (planar mapping based on world positions)
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v = vertices[i];
            uvs.Add(new Vector2(v.x * uvScale, v.z * uvScale));
        }

        // Triangulate the cross shape - divide into triangles
        // Center quad (intersection of cross)
        triangles.AddRange(new int[] { 11, 2, 5 });  // Triangle 1
        triangles.AddRange(new int[] { 11, 5, 8 });  // Triangle 2
        
        // Bottom vertical section
        triangles.AddRange(new int[] { 0, 2, 1 });   // Triangle 3
        triangles.AddRange(new int[] { 0, 11, 2 });  // Triangle 4
        
        // Right horizontal section
        triangles.AddRange(new int[] { 2, 4, 3 });   // Triangle 5
        triangles.AddRange(new int[] { 2, 5, 4 });   // Triangle 6
        
        // Top vertical section
        triangles.AddRange(new int[] { 5, 7, 6 });   // Triangle 7
        triangles.AddRange(new int[] { 5, 8, 7 });   // Triangle 8
        
        // Left horizontal section
        triangles.AddRange(new int[] { 8, 10, 11 }); // Triangle 9
        triangles.AddRange(new int[] { 8, 9, 10 });  // Triangle 10
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
    
    // Get corner positions in local space for pillar attachment
    // Cross shape has 12 corners
    public Vector3 GetCornerLocal(int cornerIndex)
    {
        float halfHorzW = horizontalWidth / 2f;
        float halfHorzH = horizontalHeight / 2f;
        float halfVertW = verticalWidth / 2f;
        float halfVertH = verticalHeight / 2f;
        
        switch (cornerIndex)
        {
            case 0: return new Vector3(-halfVertW, 0, -halfVertH);     // Bottom-left of vertical bar
            case 1: return new Vector3(halfVertW, 0, -halfVertH);      // Bottom-right of vertical bar
            case 2: return new Vector3(halfVertW, 0, -halfHorzH);      // Right vertical at horizontal level (bottom)
            case 3: return new Vector3(halfHorzW, 0, -halfHorzH);      // Right edge of horizontal bar (bottom)
            case 4: return new Vector3(halfHorzW, 0, halfHorzH);       // Right edge of horizontal bar (top)
            case 5: return new Vector3(halfVertW, 0, halfHorzH);       // Right vertical at horizontal level (top)
            case 6: return new Vector3(halfVertW, 0, halfVertH);       // Top-right of vertical bar
            case 7: return new Vector3(-halfVertW, 0, halfVertH);      // Top-left of vertical bar
            case 8: return new Vector3(-halfVertW, 0, halfHorzH);      // Left vertical at horizontal level (top)
            case 9: return new Vector3(-halfHorzW, 0, halfHorzH);      // Left edge of horizontal bar (top)
            case 10: return new Vector3(-halfHorzW, 0, -halfHorzH);    // Left edge of horizontal bar (bottom)
            case 11: return new Vector3(-halfVertW, 0, -halfHorzH);    // Left vertical at horizontal level (bottom)
            default: return Vector3.zero;
        }
    }
    
    // Get border edge information for door attachment
    // Returns true if point is near any edge of the cross
    public bool GetClosestPointOnBorder(Vector3 localPoint, out Vector3 closestPoint, out Vector3 normal)
    {
        closestPoint = Vector3.zero;
        normal = Vector3.forward;
        
        float minDistance = float.MaxValue;
        
        float halfHorzW = horizontalWidth / 2f;
        float halfHorzH = horizontalHeight / 2f;
        float halfVertW = verticalWidth / 2f;
        float halfVertH = verticalHeight / 2f;
        
        // Define all edges of the cross (12 edges total)
        // Each edge is defined by two corners and an outward normal
        
        // Bottom vertical bar - bottom edge
        CheckEdge(new Vector3(-halfVertW, 0, -halfVertH), new Vector3(halfVertW, 0, -halfVertH), 
                  Vector3.back, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Bottom vertical bar - right edge (partial)
        CheckEdge(new Vector3(halfVertW, 0, -halfVertH), new Vector3(halfVertW, 0, -halfHorzH), 
                  Vector3.right, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Horizontal bar - right edge (bottom section)
        CheckEdge(new Vector3(halfVertW, 0, -halfHorzH), new Vector3(halfHorzW, 0, -halfHorzH), 
                  Vector3.back, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Horizontal bar - right edge (vertical)
        CheckEdge(new Vector3(halfHorzW, 0, -halfHorzH), new Vector3(halfHorzW, 0, halfHorzH), 
                  Vector3.right, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Horizontal bar - right edge (top section)
        CheckEdge(new Vector3(halfHorzW, 0, halfHorzH), new Vector3(halfVertW, 0, halfHorzH), 
                  Vector3.forward, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Top vertical bar - right edge (partial)
        CheckEdge(new Vector3(halfVertW, 0, halfHorzH), new Vector3(halfVertW, 0, halfVertH), 
                  Vector3.right, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Top vertical bar - top edge
        CheckEdge(new Vector3(halfVertW, 0, halfVertH), new Vector3(-halfVertW, 0, halfVertH), 
                  Vector3.forward, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Top vertical bar - left edge (partial)
        CheckEdge(new Vector3(-halfVertW, 0, halfVertH), new Vector3(-halfVertW, 0, halfHorzH), 
                  Vector3.left, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Horizontal bar - left edge (top section)
        CheckEdge(new Vector3(-halfVertW, 0, halfHorzH), new Vector3(-halfHorzW, 0, halfHorzH), 
                  Vector3.forward, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Horizontal bar - left edge (vertical)
        CheckEdge(new Vector3(-halfHorzW, 0, halfHorzH), new Vector3(-halfHorzW, 0, -halfHorzH), 
                  Vector3.left, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Horizontal bar - left edge (bottom section)
        CheckEdge(new Vector3(-halfHorzW, 0, -halfHorzH), new Vector3(-halfVertW, 0, -halfHorzH), 
                  Vector3.back, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        // Bottom vertical bar - left edge (partial)
        CheckEdge(new Vector3(-halfVertW, 0, -halfHorzH), new Vector3(-halfVertW, 0, -halfVertH), 
                  Vector3.left, localPoint, ref minDistance, ref closestPoint, ref normal);
        
        return true;
    }
    
    private void CheckEdge(Vector3 edgeStart, Vector3 edgeEnd, Vector3 edgeNormal, 
                          Vector3 point, ref float minDistance, ref Vector3 closestPoint, ref Vector3 normal)
    {
        // Project point onto edge line
        Vector3 edgeDir = edgeEnd - edgeStart;
        float edgeLength = edgeDir.magnitude;
        
        if (edgeLength < 0.001f) return;
        
        edgeDir /= edgeLength;
        
        Vector3 toPoint = point - edgeStart;
        float projection = Vector3.Dot(toPoint, edgeDir);
        
        // Clamp to edge bounds
        projection = Mathf.Clamp(projection, 0, edgeLength);
        
        Vector3 pointOnEdge = edgeStart + edgeDir * projection;
        float distance = Vector3.Distance(point, pointOnEdge);
        
        if (distance < minDistance)
        {
            minDistance = distance;
            closestPoint = pointOnEdge;
            normal = edgeNormal;
        }
    }
}