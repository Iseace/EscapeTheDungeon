using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Door : MonoBehaviour
{
    public enum RoomType { Door, Wall }
    public RoomType Type { get { return roomType; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Door;

    [Header("Door Dimensions")]
    [SerializeField] private float doorWidth = 2f; // Door opening width
    [SerializeField] private float doorHeight = 2.5f; // Door opening height

    [Header("Border Settings")]
    [SerializeField] private bool showBorder = false; // Toggle border visibility
    [SerializeField] private float borderThickness = 0.05f; // Line renderer thickness
    [SerializeField] private Color borderColor = Color.yellow; // Border color

    [Header("Attachment Settings")]
    [SerializeField] private bool showAttachmentPoints = false; // Show gizmos for attachment points
    [SerializeField] private float attachmentRange = 1f; // Range to detect walls for attachment

    [Header("Wall Mode Settings")]
    [SerializeField] private float wallThickness = 0.2f; // Wall thickness when in Wall mode
    [SerializeField] private Material wallMaterial; // Material applied when acting as a wall
    [SerializeField] private float uvScale = 1f; // UV scale for wall mesh tiling

    [Header("Floor Border Attachment")]
    [SerializeField] private bool enableFloorAttachment = true; // Enable auto-attachment to floor borders
    [SerializeField] private float floorDetectionRange = 1f; // Range to detect floor borders
    [SerializeField] private bool snapToFloorBorder = false; // Currently snapped to floor
    [SerializeField] private bool showFloorConnectionPoint = true; // Show floor connection gizmo

    private LineRenderer lineRenderer;
    private List<Vector3> borderPoints = new List<Vector3>();

    // Wall mode state
    private Mesh wallMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private RoomType prevRoomType = RoomType.Door;

    // Attachment point properties - now at bottom corners
    public Vector3 LeftAttachmentPoint { get; private set; }
    public Vector3 RightAttachmentPoint { get; private set; }
    public Vector3 LeftAttachmentNormal { get; private set; }
    public Vector3 RightAttachmentNormal { get; private set; }

    // Floor attachment properties - now supports all floor types
    private MonoBehaviour attachedFloor; // Can be any floor type
    private Vector3 floorSnapPoint; // Point on floor border where door is snapped
    private Vector3 floorSnapNormal; // Normal at the snap point
    private Vector3 prevFloorPosition;
    private Quaternion prevFloorRotation;
    
    // Store floor dimensions based on type
    private Dictionary<string, float> prevFloorDimensions = new Dictionary<string, float>();

    private void OnEnable()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        RefreshForCurrentType();

        if (enableFloorAttachment)
            DetectAndAttachToFloor();
    }

    private void OnValidate()
    {
        // Update in editor when values change
        if (!Application.isPlaying && GetComponent<LineRenderer>() != null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();

            RefreshForCurrentType();

            if (enableFloorAttachment)
                DetectAndAttachToFloor();
        }
    }

    private void Update()
    {
        // Detect runtime type change (e.g. flipped in Inspector during play)
        if (roomType != prevRoomType)
            RefreshForCurrentType();

        // Continuously update attachment in play mode if enabled
        if (enableFloorAttachment)
        {
            DetectAndAttachToFloor();
            UpdateFloorFollowing();
        }
    }

    private void LateUpdate()
    {
        // Track floor state after all other updates
        if (attachedFloor != null)
        {
            prevFloorPosition = attachedFloor.transform.position;
            prevFloorRotation = attachedFloor.transform.rotation;
            StorePreviousFloorDimensions();
        }
    }

    // Routes setup to the correct mode. Called on enable, validate, and whenever roomType changes.
    private void RefreshForCurrentType()
    {
        UpdateAttachmentPoints(); // attachment points are identical in both modes

        if (roomType == RoomType.Wall)
        {
            // --- Wall mode ---
            if (lineRenderer != null)
                lineRenderer.enabled = false;   // hide door border
            GenerateWallMesh();                  // build solid mesh + apply material
        }
        else
        {
            // --- Door mode ---
            ClearWallMesh();                     // remove solid mesh / material
            GenerateDoorBorder();                // rebuild line-renderer border
        }

        prevRoomType = roomType;
    }

    // Generates the exact same 6-face solid mesh that Wall.cs generates,
    // using doorWidth as length, doorHeight as height, and wallThickness as thickness.
    private void GenerateWallMesh()
    {
        if (meshFilter == null || meshRenderer == null) return;

        if (wallMesh == null)
        {
            wallMesh = new Mesh();
            wallMesh.name = "DoorAsWall";
        }
        wallMesh.Clear();

        float length    = Mathf.Max(0.1f, doorWidth);
        float height    = Mathf.Max(0.1f, doorHeight);
        float thickness = Mathf.Max(0.01f, wallThickness);

        float halfLength    = length / 2f;
        float halfThickness = thickness / 2f;

        List<Vector3> vertices  = new List<Vector3>();
        List<Vector2> uvs       = new List<Vector2>();
        List<int>     triangles = new List<int>();

        // Front face (positive Z)
        vertices.Add(new Vector3(-halfLength, 0,      halfThickness));
        vertices.Add(new Vector3( halfLength, 0,      halfThickness));
        vertices.Add(new Vector3( halfLength, height, halfThickness));
        vertices.Add(new Vector3(-halfLength, height, halfThickness));
        uvs.Add(new Vector2(0,                  0));
        uvs.Add(new Vector2(length * uvScale,   0));
        uvs.Add(new Vector2(length * uvScale,   height * uvScale));
        uvs.Add(new Vector2(0,                  height * uvScale));
        triangles.AddRange(new int[] { 0, 1, 2, 0, 2, 3 });

        // Back face (negative Z)
        vertices.Add(new Vector3(-halfLength, 0,      -halfThickness));
        vertices.Add(new Vector3( halfLength, 0,      -halfThickness));
        vertices.Add(new Vector3( halfLength, height, -halfThickness));
        vertices.Add(new Vector3(-halfLength, height, -halfThickness));
        uvs.Add(new Vector2(0,                  0));
        uvs.Add(new Vector2(length * uvScale,   0));
        uvs.Add(new Vector2(length * uvScale,   height * uvScale));
        uvs.Add(new Vector2(0,                  height * uvScale));
        triangles.AddRange(new int[] { 4, 6, 5, 4, 7, 6 });

        // Left cap (negative X)
        vertices.Add(new Vector3(-halfLength, 0,      -halfThickness));
        vertices.Add(new Vector3(-halfLength, 0,       halfThickness));
        vertices.Add(new Vector3(-halfLength, height,  halfThickness));
        vertices.Add(new Vector3(-halfLength, height, -halfThickness));
        uvs.Add(new Vector2(0,                    0));
        uvs.Add(new Vector2(thickness * uvScale, 0));
        uvs.Add(new Vector2(thickness * uvScale, height * uvScale));
        uvs.Add(new Vector2(0,                    height * uvScale));
        triangles.AddRange(new int[] { 8, 9, 10, 8, 10, 11 });

        // Right cap (positive X)
        vertices.Add(new Vector3(halfLength, 0,      -halfThickness));
        vertices.Add(new Vector3(halfLength, 0,       halfThickness));
        vertices.Add(new Vector3(halfLength, height,  halfThickness));
        vertices.Add(new Vector3(halfLength, height, -halfThickness));
        uvs.Add(new Vector2(0,                    0));
        uvs.Add(new Vector2(thickness * uvScale, 0));
        uvs.Add(new Vector2(thickness * uvScale, height * uvScale));
        uvs.Add(new Vector2(0,                    height * uvScale));
        triangles.AddRange(new int[] { 12, 14, 13, 12, 15, 14 });

        // Bottom cap (y = 0)
        vertices.Add(new Vector3(-halfLength, 0, -halfThickness));
        vertices.Add(new Vector3( halfLength, 0, -halfThickness));
        vertices.Add(new Vector3( halfLength, 0,  halfThickness));
        vertices.Add(new Vector3(-halfLength, 0,  halfThickness));
        uvs.Add(new Vector2(0,                  0));
        uvs.Add(new Vector2(length * uvScale,   0));
        uvs.Add(new Vector2(length * uvScale,   thickness * uvScale));
        uvs.Add(new Vector2(0,                  thickness * uvScale));
        triangles.AddRange(new int[] { 16, 17, 18, 16, 18, 19 });

        // Top cap (y = height)
        vertices.Add(new Vector3(-halfLength, height, -halfThickness));
        vertices.Add(new Vector3( halfLength, height, -halfThickness));
        vertices.Add(new Vector3( halfLength, height,  halfThickness));
        vertices.Add(new Vector3(-halfLength, height,  halfThickness));
        uvs.Add(new Vector2(0,                  0));
        uvs.Add(new Vector2(length * uvScale,   0));
        uvs.Add(new Vector2(length * uvScale,   thickness * uvScale));
        uvs.Add(new Vector2(0,                  thickness * uvScale));
        triangles.AddRange(new int[] { 20, 22, 21, 20, 23, 22 });

        wallMesh.vertices  = vertices.ToArray();
        wallMesh.uv        = uvs.ToArray();
        wallMesh.triangles = triangles.ToArray();
        wallMesh.RecalculateNormals();
        wallMesh.RecalculateBounds();

        meshFilter.sharedMesh = wallMesh;
        if (meshCollider != null)
        {
            meshCollider.convex = false;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = wallMesh;
        }

        if (wallMaterial != null)
            meshRenderer.sharedMaterial = wallMaterial;
    }

    private void ClearWallMesh()
    {
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            meshFilter.sharedMesh = null;
        }
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
        }
        if (meshRenderer != null && wallMaterial != null)
        {
            meshRenderer.sharedMaterial = null;
        }
    }

    private void GenerateDoorBorder()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false;

        borderPoints.Clear();

        // Door frame centered at the bottom middle (pivot at bottom-center)
        float halfWidth = doorWidth / 2f;

        Vector3 bottomLeft  = new Vector3(-halfWidth, 0,          0);
        Vector3 bottomRight = new Vector3( halfWidth, 0,          0);
        Vector3 topLeft     = new Vector3(-halfWidth, doorHeight, 0);
        Vector3 topRight    = new Vector3( halfWidth, doorHeight, 0);

        // Build the border path (counterclockwise from bottom-left)
        // Bottom edge
        int bottomSegments = 5;
        for (int i = 0; i <= bottomSegments; i++)
        {
            float t = i / (float)bottomSegments;
            borderPoints.Add(Vector3.Lerp(bottomLeft, bottomRight, t));
        }

        // Right edge
        for (int i = 1; i <= 3; i++)
        {
            float t = i / 3f;
            borderPoints.Add(Vector3.Lerp(bottomRight, topRight, t));
        }

        // Top edge (reverse)
        int topSegments = 5;
        for (int i = topSegments; i >= 0; i--)
        {
            float t = i / (float)topSegments;
            borderPoints.Add(Vector3.Lerp(topLeft, topRight, t));
        }

        // Left edge (reverse)
        for (int i = 2; i >= 0; i--)
        {
            float t = i / 3f;
            borderPoints.Add(Vector3.Lerp(bottomLeft, topLeft, t));
        }

        // Close the loop
        borderPoints.Add(borderPoints[0]);

        // Apply to line renderer
        lineRenderer.positionCount = borderPoints.Count;
        lineRenderer.SetPositions(borderPoints.ToArray());
        lineRenderer.startWidth = borderThickness;
        lineRenderer.endWidth = borderThickness;
        lineRenderer.startColor = borderColor;
        lineRenderer.endColor = borderColor;
        lineRenderer.enabled = showBorder;
    }

    private void UpdateAttachmentPoints()
    {
        // Calculate attachment points at the BOTTOM CORNERS of the door (in local space)
        float halfWidth = doorWidth / 2f;

        // Left attachment point (bottom-left corner)
        LeftAttachmentPoint = new Vector3(-halfWidth, 0, 0);
        LeftAttachmentNormal = Vector3.left; // Normal pointing left

        // Right attachment point (bottom-right corner)
        RightAttachmentPoint = new Vector3(halfWidth, 0, 0);
        RightAttachmentNormal = Vector3.right; // Normal pointing right
    }

    // Get attachment point in world space
    public Vector3 GetLeftAttachmentPointWorld()
    {
        return transform.TransformPoint(LeftAttachmentPoint);
    }

    public Vector3 GetRightAttachmentPointWorld()
    {
        return transform.TransformPoint(RightAttachmentPoint);
    }

    // Get attachment normal in world space
    public Vector3 GetLeftAttachmentNormalWorld()
    {
        return transform.TransformDirection(LeftAttachmentNormal);
    }

    public Vector3 GetRightAttachmentNormalWorld()
    {
        return transform.TransformDirection(RightAttachmentNormal);
    }

    // Get floor connection point (bottom center of door)
    public Vector3 GetFloorConnectionPointWorld()
    {
        return transform.TransformPoint(Vector3.zero); // Door pivot is at bottom center
    }

    // Check if a point is close enough to attach
    public bool IsPointNearAttachment(Vector3 worldPoint, out Vector3 snapPoint, out bool isLeftSide)
    {
        Vector3 leftWorld = GetLeftAttachmentPointWorld();
        Vector3 rightWorld = GetRightAttachmentPointWorld();

        float leftDist = Vector3.Distance(worldPoint, leftWorld);
        float rightDist = Vector3.Distance(worldPoint, rightWorld);

        if (leftDist < attachmentRange && leftDist <= rightDist)
        {
            snapPoint = leftWorld;
            isLeftSide = true;
            return true;
        }
        else if (rightDist < attachmentRange)
        {
            snapPoint = rightWorld;
            isLeftSide = false;
            return true;
        }

        snapPoint = Vector3.zero;
        isLeftSide = false;
        return false;
    }

    private void DetectAndAttachToFloor()
    {
        // Find all floor types in the scene
        MonoBehaviour[] allFloors = GetAllFloors();
        
        if (allFloors.Length == 0)
        {
            attachedFloor = null;
            snapToFloorBorder = false;
            return;
        }

        Vector3 doorFloorPoint = GetFloorConnectionPointWorld();
        
        // Find closest floor border point
        float closestDist = floorDetectionRange;
        MonoBehaviour closestFloor = null;
        Vector3 closestSnapPoint = Vector3.zero;
        Vector3 closestBorderNormal = Vector3.zero;

        foreach (MonoBehaviour floor in allFloors)
        {
            Vector3 snapPoint;
            Vector3 borderNormal;
            float dist = GetClosestPointOnFloorBorder(floor, doorFloorPoint, out snapPoint, out borderNormal);
            
            if (dist < closestDist)
            {
                closestDist = dist;
                closestFloor = floor;
                closestSnapPoint = snapPoint;
                closestBorderNormal = borderNormal;
            }
        }

        // Update attachment
        if (closestFloor != null)
        {
            // Attach to this floor
            if (attachedFloor != closestFloor)
            {
                attachedFloor = closestFloor;
                prevFloorPosition = attachedFloor.transform.position;
                prevFloorRotation = attachedFloor.transform.rotation;
                StorePreviousFloorDimensions();
            }
            
            floorSnapPoint = closestSnapPoint;
            floorSnapNormal = closestBorderNormal;
            snapToFloorBorder = true;
            
            SnapToFloorBorder();
        }
        else
        {
            attachedFloor = null;
            snapToFloorBorder = false;
        }
    }

    private MonoBehaviour[] GetAllFloors()
    {
        List<MonoBehaviour> floors = new List<MonoBehaviour>();
        
        // Add all floor types
        floors.AddRange(FindObjectsOfType<RectangularFloor>());
        floors.AddRange(FindObjectsOfType<TShapedFloor>());
        floors.AddRange(FindObjectsOfType<CrossShapedFloor>());
        floors.AddRange(FindObjectsOfType<Circularfloor>());
        
        return floors.ToArray();
    }

    private float GetClosestPointOnFloorBorder(MonoBehaviour floor, Vector3 worldPoint, out Vector3 closestPoint, out Vector3 borderNormal)
    {
        // Check floor type and delegate to appropriate method
        if (floor is Circularfloor)
        {
            return GetClosestPointOnCircularFloorBorder((Circularfloor)floor, worldPoint, out closestPoint, out borderNormal);
        }
        else if (floor is RectangularFloor)
        {
            return GetClosestPointOnRectangularFloorBorder((RectangularFloor)floor, worldPoint, out closestPoint, out borderNormal);
        }
        else if (floor is TShapedFloor)
        {
            return GetClosestPointOnTShapedFloorBorder((TShapedFloor)floor, worldPoint, out closestPoint, out borderNormal);
        }
        else if (floor is CrossShapedFloor)
        {
            return GetClosestPointOnCrossShapedFloorBorder((CrossShapedFloor)floor, worldPoint, out closestPoint, out borderNormal);
        }

        closestPoint = Vector3.zero;
        borderNormal = Vector3.up;
        return float.MaxValue;
    }

    // === CIRCULAR FLOOR SUPPORT ===
    private float GetClosestPointOnCircularFloorBorder(Circularfloor floor, Vector3 worldPoint, out Vector3 closestPoint, out Vector3 borderNormal)
    {
        // Get floor dimensions using reflection (since fields are private)
        float width = GetCircularFloorWidth(floor);
        float height = GetCircularFloorHeight(floor);
        float cornerRadius = floor.cornerRadius; // This is public
        
        // Convert world point to floor's local space
        Vector3 localPoint = floor.transform.InverseTransformPoint(worldPoint);
        float x = localPoint.x;
        float z = localPoint.z;
        
        float halfW = width / 2f;
        float halfH = height / 2f;
        
        Vector3 closestLocal;
        Vector3 normalLocal;
        
        // Clamp to rectangle bounds (before corner rounding)
        float clampedX = Mathf.Clamp(x, -halfW, halfW);
        float clampedZ = Mathf.Clamp(z, -halfH, halfH);
        
        // Calculate distance to each edge
        float distToLeft = Mathf.Abs(x + halfW);
        float distToRight = Mathf.Abs(x - halfW);
        float distToBottom = Mathf.Abs(z + halfH);
        float distToTop = Mathf.Abs(z - halfH);
        
        float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);
        
        // Check if we're in a corner region
        bool inCornerRegion = false;
        Vector3 cornerCenter = Vector3.zero;
        
        if (cornerRadius > 0.01f)
        {
            // Determine which corner region we might be in
            if (distToLeft < cornerRadius && distToBottom < cornerRadius)
            {
                inCornerRegion = true;
                cornerCenter = new Vector3(-halfW + cornerRadius, 0, -halfH + cornerRadius);
            }
            else if (distToRight < cornerRadius && distToBottom < cornerRadius)
            {
                inCornerRegion = true;
                cornerCenter = new Vector3(halfW - cornerRadius, 0, -halfH + cornerRadius);
            }
            else if (distToLeft < cornerRadius && distToTop < cornerRadius)
            {
                inCornerRegion = true;
                cornerCenter = new Vector3(-halfW + cornerRadius, 0, halfH - cornerRadius);
            }
            else if (distToRight < cornerRadius && distToTop < cornerRadius)
            {
                inCornerRegion = true;
                cornerCenter = new Vector3(halfW - cornerRadius, 0, halfH - cornerRadius);
            }
        }
        
        if (inCornerRegion)
        {
            // Project to corner arc
            Vector3 toPoint = new Vector3(x, 0, z) - cornerCenter;
            toPoint.y = 0;
            float distFromCorner = toPoint.magnitude;
            
            if (distFromCorner > 0.001f)
            {
                Vector3 direction = toPoint.normalized;
                closestLocal = cornerCenter + direction * cornerRadius;
                normalLocal = direction;
            }
            else
            {
                closestLocal = cornerCenter + new Vector3(cornerRadius, 0, 0);
                normalLocal = Vector3.right;
            }
        }
        else
        {
            // Project to nearest edge
            if (minDist == distToLeft)
            {
                closestLocal = new Vector3(-halfW, 0, clampedZ);
                normalLocal = Vector3.left;
            }
            else if (minDist == distToRight)
            {
                closestLocal = new Vector3(halfW, 0, clampedZ);
                normalLocal = Vector3.right;
            }
            else if (minDist == distToBottom)
            {
                closestLocal = new Vector3(clampedX, 0, -halfH);
                normalLocal = Vector3.back;
            }
            else // top
            {
                closestLocal = new Vector3(clampedX, 0, halfH);
                normalLocal = Vector3.forward;
            }
        }
        
        // Convert back to world space
        closestPoint = floor.transform.TransformPoint(closestLocal);
        borderNormal = floor.transform.TransformDirection(normalLocal).normalized;
        
        return Vector3.Distance(worldPoint, closestPoint);
    }

    // Helper methods to get Circular floor properties using reflection
    private float GetCircularFloorWidth(Circularfloor floor)
    {
        var field = floor.GetType().GetField("width", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (float)field.GetValue(floor) : 10f;
    }

    private float GetCircularFloorHeight(Circularfloor floor)
    {
        var field = floor.GetType().GetField("height", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (float)field.GetValue(floor) : 10f;
    }

    // === RECTANGULAR FLOOR SUPPORT ===
    private float GetClosestPointOnRectangularFloorBorder(RectangularFloor floor, Vector3 worldPoint, out Vector3 closestPoint, out Vector3 borderNormal)
    {
        float width = floor.GetWidth();
        float height = floor.GetHeight();
        
        Vector3 localPoint = floor.transform.InverseTransformPoint(worldPoint);
        float x = localPoint.x;
        float z = localPoint.z;
        
        float halfW = width / 2f;
        float halfH = height / 2f;
        
        float clampedX = Mathf.Clamp(x, -halfW, halfW);
        float clampedZ = Mathf.Clamp(z, -halfH, halfH);
        
        float distToLeft = Mathf.Abs(x + halfW);
        float distToRight = Mathf.Abs(x - halfW);
        float distToBottom = Mathf.Abs(z + halfH);
        float distToTop = Mathf.Abs(z - halfH);
        
        float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);
        
        Vector3 closestLocal;
        Vector3 normalLocal;
        
        if (minDist == distToLeft)
        {
            closestLocal = new Vector3(-halfW, 0, clampedZ);
            normalLocal = Vector3.left;
        }
        else if (minDist == distToRight)
        {
            closestLocal = new Vector3(halfW, 0, clampedZ);
            normalLocal = Vector3.right;
        }
        else if (minDist == distToBottom)
        {
            closestLocal = new Vector3(clampedX, 0, -halfH);
            normalLocal = Vector3.back;
        }
        else
        {
            closestLocal = new Vector3(clampedX, 0, halfH);
            normalLocal = Vector3.forward;
        }
        
        closestPoint = floor.transform.TransformPoint(closestLocal);
        borderNormal = floor.transform.TransformDirection(normalLocal).normalized;
        
        return Vector3.Distance(worldPoint, closestPoint);
    }



    // === CROSS-SHAPED FLOOR SUPPORT ===
    private float GetClosestPointOnCrossShapedFloorBorder(CrossShapedFloor floor, Vector3 worldPoint, out Vector3 closestPoint, out Vector3 borderNormal)
    {
        float horizWidth = floor.GetHorizontalWidth();
        float horizHeight = floor.GetHorizontalHeight();
        float vertWidth = floor.GetVerticalWidth();
        float vertHeight = floor.GetVerticalHeight();
        
        Vector3 localPoint = floor.transform.InverseTransformPoint(worldPoint);
        float x = localPoint.x;
        float z = localPoint.z;
        
        float halfHorizWidth = horizWidth / 2f;
        float halfHorizHeight = horizHeight / 2f;
        float halfVertWidth = vertWidth / 2f;
        float halfVertHeight = vertHeight / 2f;
        
        bool inHorizontal = z >= -halfHorizHeight && z <= halfHorizHeight;
        bool inVertical = x >= -halfVertWidth && x <= halfVertWidth;
        
        Vector3 closestLocal = Vector3.zero;
        Vector3 normalLocal = Vector3.up;
        float minDistSq = float.MaxValue;
        
        if (inHorizontal)
        {
            float clampedX = Mathf.Clamp(x, -halfHorizWidth, halfHorizWidth);
            float clampedZ = Mathf.Clamp(z, -halfHorizHeight, halfHorizHeight);
            
            float distToLeft = Mathf.Abs(x + halfHorizWidth);
            float distToRight = Mathf.Abs(x - halfHorizWidth);
            float distToBottom = Mathf.Abs(z + halfHorizHeight);
            float distToTop = Mathf.Abs(z - halfHorizHeight);
            
            float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);
            
            Vector3 candidate = Vector3.zero;
            Vector3 candidateNormal = Vector3.up;
            
            if (minDist == distToLeft && !inVertical)
            {
                candidate = new Vector3(-halfHorizWidth, 0, clampedZ);
                candidateNormal = Vector3.left;
            }
            else if (minDist == distToRight && !inVertical)
            {
                candidate = new Vector3(halfHorizWidth, 0, clampedZ);
                candidateNormal = Vector3.right;
            }
            else if (minDist == distToBottom)
            {
                candidate = new Vector3(clampedX, 0, -halfHorizHeight);
                candidateNormal = Vector3.back;
            }
            else if (minDist == distToTop)
            {
                candidate = new Vector3(clampedX, 0, halfHorizHeight);
                candidateNormal = Vector3.forward;
            }
            
            float distSq = (new Vector3(x, 0, z) - candidate).sqrMagnitude;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closestLocal = candidate;
                normalLocal = candidateNormal;
            }
        }
        
        if (inVertical)
        {
            float clampedX = Mathf.Clamp(x, -halfVertWidth, halfVertWidth);
            float clampedZ = Mathf.Clamp(z, -halfVertHeight, halfVertHeight);
            
            float distToLeft = Mathf.Abs(x + halfVertWidth);
            float distToRight = Mathf.Abs(x - halfVertWidth);
            float distToBottom = Mathf.Abs(z + halfVertHeight);
            float distToTop = Mathf.Abs(z - halfVertHeight);
            
            float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);
            
            Vector3 candidate = Vector3.zero;
            Vector3 candidateNormal = Vector3.up;
            
            if (minDist == distToLeft)
            {
                candidate = new Vector3(-halfVertWidth, 0, clampedZ);
                candidateNormal = Vector3.left;
            }
            else if (minDist == distToRight)
            {
                candidate = new Vector3(halfVertWidth, 0, clampedZ);
                candidateNormal = Vector3.right;
            }
            else if (minDist == distToBottom && !inHorizontal)
            {
                candidate = new Vector3(clampedX, 0, -halfVertHeight);
                candidateNormal = Vector3.back;
            }
            else if (minDist == distToTop && !inHorizontal)
            {
                candidate = new Vector3(clampedX, 0, halfVertHeight);
                candidateNormal = Vector3.forward;
            }
            
            float distSq = (new Vector3(x, 0, z) - candidate).sqrMagnitude;
            if (distSq < minDistSq)
            {
                minDistSq = distSq;
                closestLocal = candidate;
                normalLocal = candidateNormal;
            }
        }
        
        closestPoint = floor.transform.TransformPoint(closestLocal);
        borderNormal = floor.transform.TransformDirection(normalLocal).normalized;
        
        return Vector3.Distance(worldPoint, closestPoint);
    }

    private void SnapToFloorBorder()
    {
        if (attachedFloor == null || !snapToFloorBorder)
            return;

        // Position the door at the snap point
        transform.position = floorSnapPoint;

        // Rotate the door to face away from the floor (perpendicular to border)
        // The door's forward should point along the border normal (outward from floor)
        Vector3 targetForward = floorSnapNormal;
        targetForward.y = 0; // Keep door upright

        if (targetForward.magnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(targetForward, Vector3.up);
        }
    }

    private void StorePreviousFloorDimensions()
    {
        prevFloorDimensions.Clear();
        
        if (attachedFloor is RectangularFloor)
        {
            RectangularFloor rf = (RectangularFloor)attachedFloor;
            prevFloorDimensions["width"] = rf.GetWidth();
            prevFloorDimensions["height"] = rf.GetHeight();
        }
        else if (attachedFloor is TShapedFloor)
        {
            TShapedFloor tf = (TShapedFloor)attachedFloor;
            prevFloorDimensions["topWidth"] = tf.GetTopWidth();
            prevFloorDimensions["topHeight"] = tf.GetTopHeight();
            prevFloorDimensions["stemWidth"] = tf.GetStemWidth();
            prevFloorDimensions["stemHeight"] = tf.GetStemHeight();
        }
        else if (attachedFloor is CrossShapedFloor)
        {
            CrossShapedFloor cf = (CrossShapedFloor)attachedFloor;
            prevFloorDimensions["horizontalWidth"] = cf.GetHorizontalWidth();
            prevFloorDimensions["horizontalHeight"] = cf.GetHorizontalHeight();
            prevFloorDimensions["verticalWidth"] = cf.GetVerticalWidth();
            prevFloorDimensions["verticalHeight"] = cf.GetVerticalHeight();
        }
        else if (attachedFloor is Circularfloor)
        {
            Circularfloor cf = (Circularfloor)attachedFloor;
            prevFloorDimensions["width"] = GetCircularFloorWidth(cf);
            prevFloorDimensions["height"] = GetCircularFloorHeight(cf);
            prevFloorDimensions["cornerRadius"] = cf.cornerRadius;
        }
    }

    private bool HasFloorDimensionsChanged()
    {
        if (attachedFloor is RectangularFloor)
        {
            RectangularFloor rf = (RectangularFloor)attachedFloor;
            return !prevFloorDimensions.ContainsKey("width") ||
                   Mathf.Abs(rf.GetWidth() - prevFloorDimensions["width"]) > 0.001f ||
                   Mathf.Abs(rf.GetHeight() - prevFloorDimensions["height"]) > 0.001f;
        }
        else if (attachedFloor is TShapedFloor)
        {
            TShapedFloor tf = (TShapedFloor)attachedFloor;
            return !prevFloorDimensions.ContainsKey("topWidth") ||
                   Mathf.Abs(tf.GetTopWidth() - prevFloorDimensions["topWidth"]) > 0.001f ||
                   Mathf.Abs(tf.GetTopHeight() - prevFloorDimensions["topHeight"]) > 0.001f ||
                   Mathf.Abs(tf.GetStemWidth() - prevFloorDimensions["stemWidth"]) > 0.001f ||
                   Mathf.Abs(tf.GetStemHeight() - prevFloorDimensions["stemHeight"]) > 0.001f;
        }
        else if (attachedFloor is CrossShapedFloor)
        {
            CrossShapedFloor cf = (CrossShapedFloor)attachedFloor;
            return !prevFloorDimensions.ContainsKey("horizontalWidth") ||
                   Mathf.Abs(cf.GetHorizontalWidth() - prevFloorDimensions["horizontalWidth"]) > 0.001f ||
                   Mathf.Abs(cf.GetHorizontalHeight() - prevFloorDimensions["horizontalHeight"]) > 0.001f ||
                   Mathf.Abs(cf.GetVerticalWidth() - prevFloorDimensions["verticalWidth"]) > 0.001f ||
                   Mathf.Abs(cf.GetVerticalHeight() - prevFloorDimensions["verticalHeight"]) > 0.001f;
        }
        else if (attachedFloor is Circularfloor)
        {
            Circularfloor cf = (Circularfloor)attachedFloor;
            return !prevFloorDimensions.ContainsKey("width") ||
                   Mathf.Abs(GetCircularFloorWidth(cf) - prevFloorDimensions["width"]) > 0.001f ||
                   Mathf.Abs(GetCircularFloorHeight(cf) - prevFloorDimensions["height"]) > 0.001f ||
                   Mathf.Abs(cf.cornerRadius - prevFloorDimensions["cornerRadius"]) > 0.001f;
        }
        
        return false;
    }

    private void UpdateFloorFollowing()
    {
        if (attachedFloor == null || !snapToFloorBorder)
            return;

        // Check if floor has moved, rotated, or dimensions changed
        bool floorChanged = false;

        Vector3 currentPos = attachedFloor.transform.position;
        Quaternion currentRot = attachedFloor.transform.rotation;

        if (Vector3.Distance(currentPos, prevFloorPosition) > 0.001f ||
            Quaternion.Angle(currentRot, prevFloorRotation) > 0.1f ||
            HasFloorDimensionsChanged())
        {
            floorChanged = true;
        }

        // If floor changed, recalculate snap point and reposition door
        if (floorChanged)
        {
            // Get the closest point on the new floor border
            Vector3 doorFloorPoint = GetFloorConnectionPointWorld();
            Vector3 newSnapPoint;
            Vector3 newBorderNormal;
            GetClosestPointOnFloorBorder(attachedFloor, doorFloorPoint, out newSnapPoint, out newBorderNormal);

            floorSnapPoint = newSnapPoint;
            floorSnapNormal = newBorderNormal;

            SnapToFloorBorder();
        }
    }

    public void SetDoorDimensions(float newWidth, float newHeight)
    {
        doorWidth = newWidth;
        doorHeight = newHeight;
        RefreshForCurrentType();
    }

    public void SetBorderVisibility(bool visible)
    {
        showBorder = visible;
        if (lineRenderer != null)
            lineRenderer.enabled = showBorder;
    }

    public void SetBorderColor(Color newColor)
    {
        borderColor = newColor;
        if (lineRenderer != null)
        {
            lineRenderer.startColor = borderColor;
            lineRenderer.endColor = borderColor;
        }
    }

    // Visualize attachment points in editor
    private void OnDrawGizmos()
    {
        if (!showAttachmentPoints && !showFloorConnectionPoint && !enableFloorAttachment)
            return;

        // Update attachment points for gizmo display
        UpdateAttachmentPoints();

        // Draw wall attachment points (now at bottom corners)
        if (showAttachmentPoints)
        {
            // Draw left attachment point (bottom-left corner)
            Gizmos.color = Color.cyan;
            Vector3 leftWorld = transform.TransformPoint(LeftAttachmentPoint);
            Gizmos.DrawWireSphere(leftWorld, 0.1f);
            Gizmos.DrawRay(leftWorld, transform.TransformDirection(LeftAttachmentNormal) * 0.3f);

            // Draw right attachment point (bottom-right corner)
            Gizmos.color = Color.magenta;
            Vector3 rightWorld = transform.TransformPoint(RightAttachmentPoint);
            Gizmos.DrawWireSphere(rightWorld, 0.1f);
            Gizmos.DrawRay(rightWorld, transform.TransformDirection(RightAttachmentNormal) * 0.3f);

            // Draw attachment range
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(leftWorld, attachmentRange);
            Gizmos.DrawWireSphere(rightWorld, attachmentRange);
        }

        // Draw floor connection point
        if (showFloorConnectionPoint)
        {
            Vector3 floorPoint = GetFloorConnectionPointWorld();
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(floorPoint, 0.12f);
            Gizmos.DrawRay(floorPoint, Vector3.down * 0.2f);

            // Draw floor detection range
            if (enableFloorAttachment)
            {
                Gizmos.color = new Color(0, 0, 1, 0.2f);
                Gizmos.DrawWireSphere(floorPoint, floorDetectionRange);
            }
        }

        // Draw floor attachment status
        if (enableFloorAttachment && snapToFloorBorder && attachedFloor != null)
        {
            Vector3 floorPoint = GetFloorConnectionPointWorld();

            // Draw connection line to snap point
            Gizmos.color = Color.green;
            Gizmos.DrawLine(floorPoint, floorSnapPoint);
            Gizmos.DrawWireSphere(floorSnapPoint, 0.15f);

            // Draw border normal
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(floorSnapPoint, floorSnapNormal * 0.5f);
        }
    }

    // === T-SHAPED FLOOR SUPPORT ===
    private float GetClosestPointOnTShapedFloorBorder(TShapedFloor floor, Vector3 worldPoint, out Vector3 closestPoint, out Vector3 borderNormal)
    {
        // Convert world point to floor's local space
        Vector3 localPoint = floor.transform.InverseTransformPoint(worldPoint);

        float topWidth = floor.GetTopWidth();
        float topHeight = floor.GetTopHeight();
        float stemWidth = floor.GetStemWidth();
        float stemHeight = floor.GetStemHeight();

        float halfTopW = topWidth / 2f;
        float halfStemW = stemWidth / 2f;
        float topY = stemHeight / 2f;
        float bottomY = -stemHeight / 2f;
        float topBarBottom = topY - topHeight;

        // Project onto floor plane (y=0)
        float x = localPoint.x;
        float z = localPoint.z;

        // Find closest point on the T-shape border
        List<EdgeSegment> edges = new List<EdgeSegment>();

        // Define all edge segments of the T-shape
        // Bottom of stem
        edges.Add(new EdgeSegment(
            new Vector3(-halfStemW, 0, bottomY),
            new Vector3(halfStemW, 0, bottomY),
            Vector3.back));

        // Right side of stem (bottom part)
        edges.Add(new EdgeSegment(
            new Vector3(halfStemW, 0, bottomY),
            new Vector3(halfStemW, 0, topBarBottom),
            Vector3.right));

        // Right transition from stem to top bar
        edges.Add(new EdgeSegment(
            new Vector3(halfStemW, 0, topBarBottom),
            new Vector3(halfTopW, 0, topBarBottom),
            Vector3.back));

        // Right side of top bar
        edges.Add(new EdgeSegment(
            new Vector3(halfTopW, 0, topBarBottom),
            new Vector3(halfTopW, 0, topY),
            Vector3.right));

        // Top of top bar
        edges.Add(new EdgeSegment(
            new Vector3(halfTopW, 0, topY),
            new Vector3(-halfTopW, 0, topY),
            Vector3.forward));

        // Left side of top bar
        edges.Add(new EdgeSegment(
            new Vector3(-halfTopW, 0, topY),
            new Vector3(-halfTopW, 0, topBarBottom),
            Vector3.left));

        // Left transition from top bar to stem
        edges.Add(new EdgeSegment(
            new Vector3(-halfTopW, 0, topBarBottom),
            new Vector3(-halfStemW, 0, topBarBottom),
            Vector3.back));

        // Left side of stem (bottom part)
        edges.Add(new EdgeSegment(
            new Vector3(-halfStemW, 0, topBarBottom),
            new Vector3(-halfStemW, 0, bottomY),
            Vector3.left));

        // Find the closest edge
        float minDistance = float.MaxValue;
        Vector3 bestPoint = Vector3.zero;
        Vector3 bestNormal = Vector3.up;

        Vector3 localPoint2D = new Vector3(x, 0, z);

        foreach (EdgeSegment edge in edges)
        {
            Vector3 pointOnEdge = ClosestPointOnLineSegment(edge.start, edge.end, localPoint2D);
            float distance = Vector3.Distance(localPoint2D, pointOnEdge);

            if (distance < minDistance)
            {
                minDistance = distance;
                bestPoint = pointOnEdge;
                bestNormal = edge.normal;
            }
        }

        // Convert back to world space
        closestPoint = floor.transform.TransformPoint(bestPoint);
        borderNormal = floor.transform.TransformDirection(bestNormal).normalized;

        return Vector3.Distance(worldPoint, closestPoint);
    }

    // Helper class for edge segments
    private class EdgeSegment
    {
        public Vector3 start;
        public Vector3 end;
        public Vector3 normal;

        public EdgeSegment(Vector3 s, Vector3 e, Vector3 n)
        {
            start = s;
            end = e;
            normal = n;
        }
    }

    // Helper function to find closest point on a line segment
    private Vector3 ClosestPointOnLineSegment(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        float lineLength = lineDirection.magnitude;
        lineDirection.Normalize();

        float projectionLength = Vector3.Dot(point - lineStart, lineDirection);
        projectionLength = Mathf.Clamp(projectionLength, 0f, lineLength);

        return lineStart + lineDirection * projectionLength;
    }
}