using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Pillar : MonoBehaviour
{
    public enum RoomType { Pillar }
    public RoomType Type { get { return RoomType.Pillar; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Pillar;

    [Header("Pillar Dimensions")]
    [SerializeField] private float height = 2.5f; // Pillar height
    [SerializeField] private float width = 0.5f; // Pillar width (X axis)
    [SerializeField] private float depth = 0.5f; // Pillar depth (Z axis)
    
    [Header("Mesh Quality")]
    [SerializeField] private int segmentsAlongHeight = 1; // Vertical segments
    
    [Header("Material Settings")]
    [SerializeField] private Material pillarMaterial; // Material to apply to the pillar
    [SerializeField] private float uvScale = 1f; // UV scale factor (1 = 1 Unity unit = 1 texture repeat)
    
    [Header("Attachment Settings")]
    [SerializeField] private bool enableAttachment = true; // Enable auto-attachment to doors/walls/floors
    [SerializeField] private float detectionRange = 1f; // Range to detect attachment points
    [SerializeField] private bool snapToAttachment = false; // Currently snapped
    [SerializeField] private bool showConnectionPoint = false; // Show connection point gizmo
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    // Attachment references - can attach to any door, wall, or floor corner
    private MonoBehaviour attachedObject; // The object (Wall, Door, or Floor) we're attached to
    private bool attachedToLeftSide; // Which side of the object we're attached to (for walls/doors)
    private int attachedCornerIndex; // Which corner of the floor we're attached to (0-3: BL, BR, TR, TL)
    private string attachedObjectType; // Track type: "RectangularWall", "Door", "CircularWall", "RectangularFloor"

    // Previous position for tracking movement
    private Vector3 prevObjectPos;
    private Quaternion prevObjectRot;

    private bool needsRegeneration = false;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        
        if (enableAttachment)
            DetectAndAttachToObject();
        
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
        
        if (enableAttachment)
            DetectAndAttachToObject();
        
        GeneratePillar();
        ApplyMaterial();
    }

    private void Update()
    {
        // Continuously update attachment in play mode if enabled
        if (enableAttachment)
        {
            DetectAndAttachToObject();
            UpdateAttachmentFollowing();
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
        // Track object position after all other updates
        if (attachedObject != null)
        {
            prevObjectPos = attachedObject.transform.position;
            prevObjectRot = attachedObject.transform.rotation;
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

    private void UpdateAttachmentFollowing()
    {
        bool objectMoved = false;

        // Check if attached object moved
        if (attachedObject != null && snapToAttachment)
        {
            Vector3 currentPos = attachedObject.transform.position;
            Quaternion currentRot = attachedObject.transform.rotation;
            
            if (Vector3.Distance(currentPos, prevObjectPos) > 0.001f || 
                Quaternion.Angle(currentRot, prevObjectRot) > 0.1f)
            {
                objectMoved = true;
            }
        }

        // If object moved, reposition pillar
        if (objectMoved)
        {
            SnapToAttachment();
        }
    }

    private void DetectAndAttachToObject()
    {
        // Find all walls, doors, and floors in the scene
        Wall[] rectangularWalls = FindObjectsOfType<Wall>();
        Door[] Doors = FindObjectsOfType<Door>();
        CircularWall[] circularWalls = FindObjectsOfType<CircularWall>();
        RectangularFloor[] rectangularFloors = FindObjectsOfType<RectangularFloor>();
        TShapedFloor[] tShapedFloors = FindObjectsOfType<TShapedFloor>();
        CrossShapedFloor[] crossShapedFloors = FindObjectsOfType<CrossShapedFloor>();

        // Calculate current pillar connection point in world space (at bottom center)
        Vector3 connectionWorld = GetConnectionPointWorld();

        // Store previous attachment
        MonoBehaviour prevObject = attachedObject;

        // Reset attachment flags
        snapToAttachment = false;
        attachedObject = null;
        attachedObjectType = "";

        // Check for closest attachment point
        float closestDist = detectionRange;

        // Check rectangular walls
        foreach (Wall wall in rectangularWalls)
        {
            if (wall.Type == Wall.RoomType.Wall)
            {
                Vector3 snapPoint;
                bool isLeftSide;
                
                if (IsPointNearWallAttachment(wall, connectionWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(connectionWorld, snapPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        attachedObject = wall;
                        attachedToLeftSide = isLeftSide;
                        attachedObjectType = "RectangularWall";
                        snapToAttachment = true;
                    }
                }
            }
        }

        // Check rectangular doors
        foreach (Door door in Doors)
        {
            if (door.Type == Door.RoomType.Door)
            {
                Vector3 snapPoint;
                bool isLeftSide;
                
                if (door.IsPointNearAttachment(connectionWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(connectionWorld, snapPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        attachedObject = door;
                        attachedToLeftSide = isLeftSide;
                        attachedObjectType = "Door";
                        snapToAttachment = true;
                    }
                }
            }
        }

        // Check circular walls
        foreach (CircularWall wall in circularWalls)
        {
            if (wall.Type == CircularWall.RoomType.Wall)
            {
                Vector3 snapPoint;
                bool isLeftSide;
                
                if (IsPointNearCircularWallAttachment(wall, connectionWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(connectionWorld, snapPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        attachedObject = wall;
                        attachedToLeftSide = isLeftSide;
                        attachedObjectType = "CircularWall";
                        snapToAttachment = true;
                    }
                }
            }
        }

        // Check rectangular floors (NEW)
        foreach (RectangularFloor floor in rectangularFloors)
        {
            if (floor.Type == RectangularFloor.RoomType.Floor)
            {
                Vector3 snapPoint;
                int cornerIndex;
                
                if (IsPointNearFloorCorner(floor, connectionWorld, out snapPoint, out cornerIndex))
                {
                    float dist = Vector3.Distance(connectionWorld, snapPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        attachedObject = floor;
                        attachedCornerIndex = cornerIndex;
                        attachedObjectType = "RectangularFloor";
                        snapToAttachment = true;
                    }
                }
            }
        }

        // Check T-shaped floors
        foreach (TShapedFloor floor in tShapedFloors)
        {
            if (floor.Type == TShapedFloor.RoomType.Floor)
            {
                Vector3 snapPoint;
                int cornerIndex;

                if (IsPointNearTShapedFloorCorner(floor, connectionWorld, out snapPoint, out cornerIndex))
                {
                    float dist = Vector3.Distance(connectionWorld, snapPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        attachedObject = floor;
                        attachedCornerIndex = cornerIndex;
                        attachedObjectType = "TShapedFloor";
                        snapToAttachment = true;
                    }
                }
            }
        }

        // Check cross-shaped floors
        foreach (CrossShapedFloor floor in crossShapedFloors)
        {
            if (floor.Type == CrossShapedFloor.RoomType.Floor)
            {
                Vector3 snapPoint;
                int cornerIndex;

                if (IsPointNearCrossShapedFloorCorner(floor, connectionWorld, out snapPoint, out cornerIndex))
                {
                    float dist = Vector3.Distance(connectionWorld, snapPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        attachedObject = floor;
                        attachedCornerIndex = cornerIndex;
                        attachedObjectType = "CrossShapedFloor";
                        snapToAttachment = true;
                    }
                }
            }
        }

        // Trigger snap if attachment changed
        if (snapToAttachment && attachedObject != prevObject)
        {
            SnapToAttachment();
        }
    }

    // NEW: Check if point is near a floor corner
    private bool IsPointNearFloorCorner(RectangularFloor floor, Vector3 point, out Vector3 snapPoint, out int cornerIndex)
    {
        snapPoint = Vector3.zero;
        cornerIndex = -1;

        float halfW = floor.GetWidth() / 2f;
        float halfH = floor.GetHeight() / 2f;

        // Define the 4 corners in local space (same order as floor vertices)
        Vector3[] localCorners = new Vector3[]
        {
            new Vector3(-halfW, 0, -halfH), // 0: Bottom-left
            new Vector3(halfW, 0, -halfH),  // 1: Bottom-right
            new Vector3(halfW, 0, halfH),   // 2: Top-right
            new Vector3(-halfW, 0, halfH)   // 3: Top-left
        };

        // Convert to world space
        Transform floorTransform = floor.transform;
        Vector3[] worldCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            worldCorners[i] = floorTransform.TransformPoint(localCorners[i]);
        }

        // Find the closest corner
        float closestDist = detectionRange;
        int closestCorner = -1;

        for (int i = 0; i < 4; i++)
        {
            float dist = Vector3.Distance(point, worldCorners[i]);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestCorner = i;
                snapPoint = worldCorners[i];
            }
        }

        if (closestCorner != -1)
        {
            cornerIndex = closestCorner;
            return true;
        }

        return false;
    }

    private bool IsPointNearWallAttachment(Wall wall, Vector3 point, out Vector3 snapPoint, out bool isLeftSide)
    {
        Vector3 leftPoint = wall.GetLeftConnectionPointWorld();
        Vector3 rightPoint = wall.GetRightConnectionPointWorld();

        float distLeft = Vector3.Distance(point, leftPoint);
        float distRight = Vector3.Distance(point, rightPoint);

        if (distLeft < distRight && distLeft < detectionRange)
        {
            snapPoint = leftPoint;
            isLeftSide = true;
            return true;
        }
        else if (distRight < detectionRange)
        {
            snapPoint = rightPoint;
            isLeftSide = false;
            return true;
        }

        snapPoint = Vector3.zero;
        isLeftSide = false;
        return false;
    }

    public bool IsPointNearCircularWallAttachment(CircularWall wall, Vector3 point, out Vector3 snapPoint, out bool isLeftSide)
    {
        Vector3 leftPoint = wall.GetLeftConnectionPointWorld();
        Vector3 rightPoint = wall.GetRightConnectionPointWorld();

        float distLeft = Vector3.Distance(point, leftPoint);
        float distRight = Vector3.Distance(point, rightPoint);

        if (distLeft < distRight && distLeft < detectionRange)
        {
            snapPoint = leftPoint;
            isLeftSide = true;
            return true;
        }
        else if (distRight < detectionRange)
        {
            snapPoint = rightPoint;
            isLeftSide = false;
            return true;
        }

        snapPoint = Vector3.zero;
        isLeftSide = false;
        return false;
    }

    private void SnapToAttachment()
    {
        if (!snapToAttachment || attachedObject == null) return;

        Vector3 targetPoint = Vector3.zero;
        
        switch (attachedObjectType)
        {
            case "RectangularWall":
                Wall rectWall = (Wall)attachedObject;
                targetPoint = attachedToLeftSide ? 
                    rectWall.GetLeftConnectionPointWorld() : 
                    rectWall.GetRightConnectionPointWorld();
                break;

            case "Door":
                Door door = (Door)attachedObject;
                targetPoint = attachedToLeftSide ? 
                    door.GetLeftAttachmentPointWorld() : 
                    door.GetRightAttachmentPointWorld();
                break;

            case "CircularWall":
                CircularWall circWall = (CircularWall)attachedObject;
                targetPoint = attachedToLeftSide ? 
                    circWall.GetLeftConnectionPointWorld() : 
                    circWall.GetRightConnectionPointWorld();
                break;

            case "RectangularFloor":
                RectangularFloor floor = (RectangularFloor)attachedObject;
                targetPoint = GetFloorCornerWorld(floor, attachedCornerIndex);
                break;
            case "TShapedFloor":
                TShapedFloor tFloor = (TShapedFloor)attachedObject;
                targetPoint = GetTShapedFloorCornerWorld(tFloor, attachedCornerIndex);
                break;

            case "CrossShapedFloor":
                CrossShapedFloor cFloor = (CrossShapedFloor)attachedObject;
                targetPoint = GetCrossShapedFloorCornerWorld(cFloor, attachedCornerIndex);
                break;
        }

        // Calculate the offset from transform.position to connection point
        Vector3 connectionLocal = GetConnectionPointLocal();
        
        // Position the pillar so that its connection point aligns with the target
        transform.position = targetPoint - transform.TransformVector(connectionLocal);
    }

    // NEW: Get floor corner in world space
    private Vector3 GetFloorCornerWorld(RectangularFloor floor, int cornerIndex)
    {
        float halfW = floor.GetWidth() / 2f;
        float halfH = floor.GetHeight() / 2f;

        Vector3 localCorner = Vector3.zero;
        switch (cornerIndex)
        {
            case 0: // Bottom-left
                localCorner = new Vector3(-halfW, 0, -halfH);
                break;
            case 1: // Bottom-right
                localCorner = new Vector3(halfW, 0, -halfH);
                break;
            case 2: // Top-right
                localCorner = new Vector3(halfW, 0, halfH);
                break;
            case 3: // Top-left
                localCorner = new Vector3(-halfW, 0, halfH);
                break;
        }

        return floor.transform.TransformPoint(localCorner);
    }
    private bool IsPointNearTShapedFloorCorner(TShapedFloor floor, Vector3 point, out Vector3 snapPoint, out int cornerIndex)
    {
        Vector3[] worldCorners = GetTShapedFloorCorners(floor);
        return FindClosestCorner(worldCorners, point, out snapPoint, out cornerIndex);
    }

    private bool IsPointNearCrossShapedFloorCorner(CrossShapedFloor floor, Vector3 point, out Vector3 snapPoint, out int cornerIndex)
    {
        Vector3[] worldCorners = GetCrossShapedFloorCorners(floor);
        return FindClosestCorner(worldCorners, point, out snapPoint, out cornerIndex);
    }

    // Shared logic: given an array of world-space corners, find the closest one within detectionRange
    private bool FindClosestCorner(Vector3[] worldCorners, Vector3 point, out Vector3 snapPoint, out int cornerIndex)
    {
        snapPoint = Vector3.zero;
        cornerIndex = -1;
        float closestDist = detectionRange;

        for (int i = 0; i < worldCorners.Length; i++)
        {
            float dist = Vector3.Distance(point, worldCorners[i]);
            if (dist < closestDist)
            {
                closestDist = dist;
                cornerIndex = i;
                snapPoint = worldCorners[i];
            }
        }

        return cornerIndex != -1;
    }

    private Vector3[] GetTShapedFloorCorners(TShapedFloor floor)
    {
        float halfTopW  = floor.GetTopWidth() / 2f;
        float halfStemW = floor.GetStemWidth() / 2f;
        float topY      = floor.GetStemHeight() / 2f;
        float bottomY   = -floor.GetStemHeight() / 2f;
        float topBarBottom = topY - floor.GetTopHeight();

        // 8 corners matching the T-shape vertex layout
        Vector3[] local = new Vector3[]
        {
            new Vector3(-halfStemW, 0, bottomY),      // 0: bottom-left of stem
            new Vector3( halfStemW, 0, bottomY),      // 1: bottom-right of stem
            new Vector3( halfStemW, 0, topBarBottom), // 2: inner-right (stem meets bar)
            new Vector3( halfTopW,  0, topBarBottom), // 3: outer-right of bar bottom
            new Vector3( halfTopW,  0, topY),         // 4: top-right
            new Vector3(-halfTopW,  0, topY),         // 5: top-left
            new Vector3(-halfTopW,  0, topBarBottom), // 6: outer-left of bar bottom
            new Vector3(-halfStemW, 0, topBarBottom)  // 7: inner-left (stem meets bar)
        };

        Vector3[] world = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            world[i] = floor.transform.TransformPoint(local[i]);
        return world;
    }

    private Vector3 GetTShapedFloorCornerWorld(TShapedFloor floor, int cornerIndex)
    {
        Vector3[] corners = GetTShapedFloorCorners(floor);
        return (cornerIndex >= 0 && cornerIndex < corners.Length) ? corners[cornerIndex] : Vector3.zero;
    }

    private Vector3[] GetCrossShapedFloorCorners(CrossShapedFloor floor)
    {
        float halfHW = floor.GetHorizontalWidth() / 2f;
        float halfHH = floor.GetHorizontalHeight() / 2f;
        float halfVW = floor.GetVerticalWidth() / 2f;
        float halfVH = floor.GetVerticalHeight() / 2f;

        // 12 corners matching the cross-shape vertex layout
        Vector3[] local = new Vector3[]
        {
            new Vector3(-halfVW, 0, -halfVH), // 0
            new Vector3( halfVW, 0, -halfVH), // 1
            new Vector3( halfVW, 0, -halfHH), // 2
            new Vector3( halfHW, 0, -halfHH), // 3
            new Vector3( halfHW, 0,  halfHH), // 4
            new Vector3( halfVW, 0,  halfHH), // 5
            new Vector3( halfVW, 0,  halfVH), // 6
            new Vector3(-halfVW, 0,  halfVH), // 7
            new Vector3(-halfVW, 0,  halfHH), // 8
            new Vector3(-halfHW, 0,  halfHH), // 9
            new Vector3(-halfHW, 0, -halfHH), // 10
            new Vector3(-halfVW, 0, -halfHH)  // 11
        };

        Vector3[] world = new Vector3[local.Length];
        for (int i = 0; i < local.Length; i++)
            world[i] = floor.transform.TransformPoint(local[i]);
        return world;
    }

    private Vector3 GetCrossShapedFloorCornerWorld(CrossShapedFloor floor, int cornerIndex)
    {
        Vector3[] corners = GetCrossShapedFloorCorners(floor);
        return (cornerIndex >= 0 && cornerIndex < corners.Length) ? corners[cornerIndex] : Vector3.zero;
    }


    private Vector3 GetConnectionPointLocal()
    {
        // Connection point is at the bottom center of the pillar (local space)
        return new Vector3(0, 0, 0);
    }

    public Vector3 GetConnectionPointWorld()
    {
        // Connection point is at the bottom center of the pillar (world space)
        return transform.TransformPoint(GetConnectionPointLocal());
    }

    private void GeneratePillar()
    {
        // Validate inputs
        width = Mathf.Max(0.01f, width);
        depth = Mathf.Max(0.01f, depth);
        height = Mathf.Max(0.01f, height);
        segmentsAlongHeight = Mathf.Max(1, segmentsAlongHeight);

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Pillar Mesh";
        }

        mesh.Clear();

        // Calculate vertex and triangle counts
        int vertsPerLevel = 8; // 4 faces × 2 vertices per face
        int totalLevels = segmentsAlongHeight + 1;
        int totalVerts = vertsPerLevel * totalLevels + 8; // +8 for top/bottom caps

        int trisPerSegment = 4 * 2 * 3; // 4 faces × 2 triangles × 3 vertices
        int totalTris = segmentsAlongHeight * trisPerSegment + 4 * 3; // +12 for caps (2 caps × 2 triangles × 3 vertices)

        Vector3[] vertices = new Vector3[totalVerts];
        Vector2[] uvs = new Vector2[totalVerts];
        int[] triangles = new int[totalTris];

        float halfW = width / 2f;
        float halfD = depth / 2f;

        // Calculate total perimeter for UV unwrapping
        float frontWidth = width;
        float sideDepth = depth;
        float totalPerimeter = (frontWidth + sideDepth) * 2;

        // Generate vertices in rings from bottom to top
        int vertIndex = 0;
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float t = (float)h / segmentsAlongHeight;
            float y = height * t;
            
            // V coordinate based on height (0 to height in world units)
            float v = y;

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
        int triIndex = 0;
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

        // Bottom cap triangles (facing down - counter-clockwise from below)
        triangles[triIndex++] = capVertStart + 0;
        triangles[triIndex++] = capVertStart + 3;
        triangles[triIndex++] = capVertStart + 2;
        triangles[triIndex++] = capVertStart + 0;
        triangles[triIndex++] = capVertStart + 2;
        triangles[triIndex++] = capVertStart + 1;

        // Top cap triangles (facing up - clockwise from above)
        triangles[triIndex++] = capVertStart + 4;
        triangles[triIndex++] = capVertStart + 5;
        triangles[triIndex++] = capVertStart + 6;
        triangles[triIndex++] = capVertStart + 4;
        triangles[triIndex++] = capVertStart + 6;
        triangles[triIndex++] = capVertStart + 7;

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
        if (!enableAttachment && !showConnectionPoint) return;

        // Draw connection point (at bottom center)
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

        if (!enableAttachment) return;

        Vector3 connectionPoint = GetConnectionPointWorld();

        // Draw attachment status
        if (snapToAttachment && attachedObject != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(connectionPoint, 0.2f);
            
            Vector3 attachPoint = Vector3.zero;
            
            // Get attachment point based on object type
            switch (attachedObjectType)
            {
                case "RectangularWall":
                    Wall rectWall = (Wall)attachedObject;
                    attachPoint = attachedToLeftSide ? 
                        rectWall.GetLeftConnectionPointWorld() : 
                        rectWall.GetRightConnectionPointWorld();
                    break;

                case "Door":
                    Door door = (Door)attachedObject;
                    attachPoint = attachedToLeftSide ? 
                        door.GetLeftAttachmentPointWorld() : 
                        door.GetRightAttachmentPointWorld();
                    break;

                case "CircularWall":
                    CircularWall circWall = (CircularWall)attachedObject;
                    attachPoint = attachedToLeftSide ? 
                        circWall.GetLeftConnectionPointWorld() : 
                        circWall.GetRightConnectionPointWorld();
                    break;

                case "RectangularFloor":
                    RectangularFloor floor = (RectangularFloor)attachedObject;
                    attachPoint = GetFloorCornerWorld(floor, attachedCornerIndex);
                    
                   
                    break;
                case "TShapedFloor":
                    TShapedFloor tFloorGiz = (TShapedFloor)attachedObject;
                    attachPoint = GetTShapedFloorCornerWorld(tFloorGiz, attachedCornerIndex);
                    break;

                case "CrossShapedFloor":
                    CrossShapedFloor cFloorGiz = (CrossShapedFloor)attachedObject;
                    attachPoint = GetCrossShapedFloorCornerWorld(cFloorGiz, attachedCornerIndex);
                    break;
            }
            
            Gizmos.DrawLine(connectionPoint, attachPoint);
        }

        // Draw detection range
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(connectionPoint, detectionRange);
    }
}