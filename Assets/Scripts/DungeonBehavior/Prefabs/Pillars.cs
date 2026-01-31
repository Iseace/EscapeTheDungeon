using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Pillar : MonoBehaviour
{
    public enum RoomType { Pillar }
    public RoomType Type { get { return RoomType.Pillar; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Pillar;

    [Header("Pillar Dimensions")]
    [SerializeField] private float height = 3f; // Pillar height
    [SerializeField] private float width = 0.3f; // Pillar width (X axis)
    [SerializeField] private float depth = 0.3f; // Pillar depth (Z axis)
    
    [Header("Mesh Quality")]
    [SerializeField] private int segmentsAlongHeight = 1; // Vertical segments
    
    [Header("Material Settings")]
    [SerializeField] private Material pillarMaterial; // Material to apply to the pillar
    [SerializeField] private float uvScale = 1f; // UV scale factor (1 = 1 Unity unit = 1 texture repeat)
    
    [Header("Door Attachment")]
    [SerializeField] private bool enableDoorAttachment = true; // Enable auto-attachment to doors
    [SerializeField] private float doorDetectionRange = 2f; // Range to detect doors
    [SerializeField] private bool snapToDoor = false; // Currently snapped to door
    [SerializeField] private bool showConnectionPoint = false; // Show connection point gizmo
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    // Door attachment references
    private CircularDoor attachedDoor;
    private bool attachedToLeftSide; // Which side of the door is attached

    // Previous door position for tracking movement
    private Vector3 prevDoorPos;
    private Quaternion prevDoorRot;

    private bool needsRegeneration = false;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        
        if (enableDoorAttachment)
            DetectAndAttachToDoor();
        
        GeneratePillar();
        ApplyMaterial();
    }

    private void OnValidate()
    {
        // Update mesh in editor when values change
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        
        if (meshCollider == null)
            meshCollider = GetComponent<MeshCollider>();
        
        if (enableDoorAttachment)
            DetectAndAttachToDoor();
        
        GeneratePillar();
        ApplyMaterial();
    }

    private void Update()
    {
        // Continuously update attachment in play mode if enabled
        if (enableDoorAttachment)
        {
            DetectAndAttachToDoor();
            UpdateDoorFollowing();
        }

        // Regenerate pillar if needed
        if (needsRegeneration)
        {
            GeneratePillar();
            needsRegeneration = false;
        }
    }

    private void LateUpdate()
    {
        // Track door position after all other updates
        if (attachedDoor != null)
        {
            prevDoorPos = attachedDoor.transform.position;
            prevDoorRot = attachedDoor.transform.rotation;
        }
    }

    private void ApplyMaterial()
    {
        // Apply material if one is assigned
        if (pillarMaterial != null && meshRenderer != null)
        {
            meshRenderer.sharedMaterial = pillarMaterial;
        }
    }

    private void UpdateDoorFollowing()
    {
        bool doorMoved = false;

        // Check if door moved
        if (attachedDoor != null && snapToDoor)
        {
            Vector3 currentPos = attachedDoor.transform.position;
            Quaternion currentRot = attachedDoor.transform.rotation;
            
            if (Vector3.Distance(currentPos, prevDoorPos) > 0.001f || 
                Quaternion.Angle(currentRot, prevDoorRot) > 0.1f)
            {
                doorMoved = true;
            }
        }

        // If door moved, reposition pillar
        if (doorMoved)
        {
            SnapToDoor();
        }
    }

    private void DetectAndAttachToDoor()
    {
        // Find all doors in the scene
        CircularDoor[] doors = FindObjectsOfType<CircularDoor>();
        
        if (doors.Length == 0)
        {
            attachedDoor = null;
            snapToDoor = false;
            return;
        }

        // Calculate current pillar connection point in world space (at mid-height)
        Vector3 connectionWorld = GetConnectionPointWorld();

        // Store previous attachment
        CircularDoor prevDoor = attachedDoor;

        // Reset attachment flags
        snapToDoor = false;
        attachedDoor = null;

        // Check connection point for door attachment
        float closestDist = doorDetectionRange;
        foreach (CircularDoor door in doors)
        {
            if (door.Type == CircularDoor.RoomType.Door)
            {
                Vector3 snapPoint;
                bool isLeftSide;
                
                if (door.IsPointNearAttachment(connectionWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(connectionWorld, snapPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        attachedDoor = door;
                        attachedToLeftSide = isLeftSide;
                        snapToDoor = true;
                    }
                }
            }
        }

        // If attached to a door, snap to it
        if (snapToDoor && attachedDoor != null)
        {
            SnapToDoor();
            
            // Initialize tracking variables on new attachment
            if (prevDoor != attachedDoor)
            {
                prevDoorPos = attachedDoor.transform.position;
                prevDoorRot = attachedDoor.transform.rotation;
            }
        }
    }

    private void SnapToDoor()
    {
        if (attachedDoor == null || !snapToDoor)
            return;

        // Get the door's attachment point (at mid-height of door)
        Vector3 doorAttachPoint = attachedToLeftSide ? 
            attachedDoor.GetLeftAttachmentPointWorld() : 
            attachedDoor.GetRightAttachmentPointWorld();

        Vector3 doorNormal = attachedToLeftSide ? 
            attachedDoor.GetLeftAttachmentNormalWorld() : 
            attachedDoor.GetRightAttachmentNormalWorld();

        // Position pillar so its bottom aligns with the door's bottom (y=0 in door's local space)
        // Get the door's bottom position in world space
        Vector3 doorBottomWorld = attachedDoor.transform.position;
        
        // Set pillar position to door's bottom, keeping X and Z from attachment point
        transform.position = new Vector3(doorAttachPoint.x, doorBottomWorld.y, doorAttachPoint.z);

        // Optionally rotate pillar to align with door
        // For now, keep pillar upright but you could add rotation logic here
    }

    // Get connection point in world space (at mid-height of pillar)
    private Vector3 GetConnectionPointWorld()
    {
        // Connection point is at the center of the pillar at mid-height
        Vector3 localConnectionPoint = new Vector3(0, height / 2f, 0);
        return transform.TransformPoint(localConnectionPoint);
    }

    private void GeneratePillar()
    {
        // Create or get mesh
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Pillar Mesh";
        }
        else
        {
            mesh.Clear();
        }

        // Calculate mesh data
        // A pillar is a simple rectangular prism (box)
        
        int vertsPerHeight = segmentsAlongHeight + 1;
        int sideVerts = 8 * vertsPerHeight; // 4 faces, 2 verts per face corner, per height segment
        int capVerts = 8; // 4 verts for bottom cap + 4 verts for top cap
        int totalVerts = sideVerts + capVerts;
        
        int sideTris = 4 * segmentsAlongHeight * 6; // 4 faces, 2 triangles per segment, 3 indices per triangle
        int capTris = 4 * 3; // 2 caps, 2 triangles each, 3 indices per triangle
        int totalTriangles = sideTris + capTris;

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        int[] triangles = new int[totalTriangles];

        int vertIndex = 0;
        int triIndex = 0;

        float halfW = width / 2f;
        float halfD = depth / 2f;

        // Calculate perimeter for UV mapping
        float perimeter = 2f * (width + depth);
        float frontWidth = width;
        float sideDepth = depth;

        // Generate vertices for each height level
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float y = (h / (float)segmentsAlongHeight) * height;
            float v = y * uvScale; // UV V based on actual height

            // Track U coordinate around the perimeter
            float uOffset = 0f;

            // Front face (Z+) - width units
            vertices[vertIndex] = new Vector3(-halfW, y, halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);
            uOffset += frontWidth;
            vertices[vertIndex] = new Vector3(halfW, y, halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);

            // Right face (X+) - depth units
            vertices[vertIndex] = new Vector3(halfW, y, halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);
            uOffset += sideDepth;
            vertices[vertIndex] = new Vector3(halfW, y, -halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);

            // Back face (Z-) - width units
            vertices[vertIndex] = new Vector3(halfW, y, -halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);
            uOffset += frontWidth;
            vertices[vertIndex] = new Vector3(-halfW, y, -halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);

            // Left face (X-) - depth units
            vertices[vertIndex] = new Vector3(-halfW, y, -halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);
            uOffset += sideDepth;
            vertices[vertIndex] = new Vector3(-halfW, y, halfD);
            uvs[vertIndex++] = new Vector2(uOffset * uvScale, v);
        }

        // Generate triangles for the 4 vertical faces
        for (int h = 0; h < segmentsAlongHeight; h++)
        {
            int baseIndex = h * 8; // 8 vertices per height level

            // For each of the 4 faces
            for (int face = 0; face < 4; face++)
            {
                int faceBase = baseIndex + face * 2;
                
                int v0 = faceBase;
                int v1 = faceBase + 1;
                int v2 = faceBase + 8; // Same position on next level
                int v3 = faceBase + 9;

                // First triangle (reversed winding for outward normals)
                triangles[triIndex++] = v0;
                triangles[triIndex++] = v1;
                triangles[triIndex++] = v2;

                // Second triangle (reversed winding for outward normals)
                triangles[triIndex++] = v1;
                triangles[triIndex++] = v3;
                triangles[triIndex++] = v2;
            }
        }

        // Add top and bottom caps
        int capVertStart = vertIndex;

        // Bottom cap (Y = 0) - UVs based on XZ position
        vertices[vertIndex] = new Vector3(-halfW, 0, halfD);
        uvs[vertIndex++] = new Vector2(0, depth) * uvScale;
        vertices[vertIndex] = new Vector3(halfW, 0, halfD);
        uvs[vertIndex++] = new Vector2(width, depth) * uvScale;
        vertices[vertIndex] = new Vector3(halfW, 0, -halfD);
        uvs[vertIndex++] = new Vector2(width, 0) * uvScale;
        vertices[vertIndex] = new Vector3(-halfW, 0, -halfD);
        uvs[vertIndex++] = new Vector2(0, 0) * uvScale;

        // Top cap (Y = height) - UVs based on XZ position
        vertices[vertIndex] = new Vector3(-halfW, height, halfD);
        uvs[vertIndex++] = new Vector2(0, depth) * uvScale;
        vertices[vertIndex] = new Vector3(halfW, height, halfD);
        uvs[vertIndex++] = new Vector2(width, depth) * uvScale;
        vertices[vertIndex] = new Vector3(halfW, height, -halfD);
        uvs[vertIndex++] = new Vector2(width, 0) * uvScale;
        vertices[vertIndex] = new Vector3(-halfW, height, -halfD);
        uvs[vertIndex++] = new Vector2(0, 0) * uvScale;

        // Bottom cap triangles (facing down - clockwise from below)
        triangles[triIndex++] = capVertStart + 0;
        triangles[triIndex++] = capVertStart + 1;
        triangles[triIndex++] = capVertStart + 2;
        triangles[triIndex++] = capVertStart + 0;
        triangles[triIndex++] = capVertStart + 2;
        triangles[triIndex++] = capVertStart + 3;

        // Top cap triangles (facing up - counter-clockwise from above)
        triangles[triIndex++] = capVertStart + 4;
        triangles[triIndex++] = capVertStart + 6;
        triangles[triIndex++] = capVertStart + 5;
        triangles[triIndex++] = capVertStart + 4;
        triangles[triIndex++] = capVertStart + 7;
        triangles[triIndex++] = capVertStart + 6;

        // Assign mesh data
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.sharedMesh = mesh;
        
        // Update MeshCollider
        if (meshCollider != null)
        {
            meshCollider.convex = false;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
        }
    }

    public void SetDimensions(float newHeight, float newWidth, float newDepth)
    {
        height = newHeight;
        width = newWidth;
        depth = newDepth;
        GeneratePillar();
    }

    public void SetHeight(float newHeight)
    {
        height = newHeight;
        GeneratePillar();
    }

    public void SetWidth(float newWidth)
    {
        width = newWidth;
        GeneratePillar();
    }

    public void SetDepth(float newDepth)
    {
        depth = newDepth;
        GeneratePillar();
    }

    // Visualize attachment status and connection point in editor
    private void OnDrawGizmos()
    {
        if (!enableDoorAttachment && !showConnectionPoint) return;

        // Draw connection point (at mid-height)
        if (showConnectionPoint)
        {
            Vector3 conn = GetConnectionPointWorld();

            // Connection point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(conn, 0.15f);
            Gizmos.DrawRay(conn, Vector3.up * 0.3f);
            Gizmos.DrawRay(conn, Vector3.down * 0.3f);

            // Draw pivot point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }

        if (!enableDoorAttachment) return;

        Vector3 connectionPoint = GetConnectionPointWorld();

        // Draw connection status
        if (snapToDoor && attachedDoor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(connectionPoint, 0.2f);
            
            Vector3 doorPoint = attachedToLeftSide ? 
                attachedDoor.GetLeftAttachmentPointWorld() : 
                attachedDoor.GetRightAttachmentPointWorld();
            Gizmos.DrawLine(connectionPoint, doorPoint);
        }

        // Draw detection range
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(connectionPoint, doorDetectionRange);
    }
}