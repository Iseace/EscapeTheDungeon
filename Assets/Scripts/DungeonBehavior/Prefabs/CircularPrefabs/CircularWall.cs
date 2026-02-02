using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class CircularWall : MonoBehaviour
{
    public enum RoomType { Wall }
    public RoomType Type { get { return RoomType.Wall; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Wall;

    [Header("Wall Dimensions")]
    [SerializeField] private float height = 2.5f; // Wall height
    [SerializeField] private float length = 10f; // Wall length (arc length)
    [SerializeField] private float thickness = 0.2f; // Wall thickness
    
    [Header("Curvature Settings")]
    [SerializeField] [Range(0f, 360f)] private float curvatureAngle = 90f; // Degrees of curvature (min 1)
    [SerializeField] private bool curveInward = true; // Curve direction
    
    [Header("Mesh Quality")]
    [SerializeField] private int segmentsAlongLength = 20; // Segments for smoothness
    [SerializeField] private int segmentsAlongHeight = 1; // Vertical segments

    [Header("Material Settings")]
    [SerializeField] private Material wallMaterial; // Material to apply to the wall
    [SerializeField] private float uvScale = 1f; // UV scale factor (1 = 1 Unity unit = 1 texture repeat)
    
    [Header("Attachment Settings")]
    [SerializeField] private bool enableAttachment = true; // Enable auto-attachment to doors and pillars
    [SerializeField] private float detectionRange = 1f; // Range to detect doors and pillars
    [SerializeField] private bool autoDimensionFromAttachments = true; // Auto-adjust dimensions when both ends attached
    [SerializeField] private bool snapLeftEnd = false; // Snap left end to door/pillar
    [SerializeField] private bool snapRightEnd = false; // Snap right end to door/pillar
    [SerializeField] private bool showConnectionPoints = false; // Show connection point gizmos
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    // Attachment references - can be either Door or Pillar
    private MonoBehaviour attachedLeftObject;  // Door or Pillar
    private MonoBehaviour attachedRightObject; // Door or Pillar
    private bool leftAttachedToLeftSide; // Which side of the door is attached (for doors)
    private bool rightAttachedToLeftSide;
    private string leftObjectType; // "Door" or "Pillar"
    private string rightObjectType; // "Door" or "Pillar"

    // Previous object positions for tracking movement
    private Vector3 prevLeftObjectPos;
    private Vector3 prevRightObjectPos;
    private Quaternion prevLeftObjectRot;
    private Quaternion prevRightObjectRot;

    private bool needsRegeneration = false;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        
        if (enableAttachment)
            DetectAndAttachToObjects();
        
        GenerateWall();
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
        
        // Enforce minimum angle
        if (curvatureAngle < 1f)
            curvatureAngle = 1f;
        
        if (enableAttachment)
            DetectAndAttachToObjects();
        
        GenerateWall();
        ApplyMaterial();
    }

    private void Update()
    {
        // Continuously update attachment in play mode if enabled
        if (enableAttachment)
        {
            DetectAndAttachToObjects();
            UpdateAttachmentFollowing();
        }

        // Regenerate wall if needed
        if (needsRegeneration)
        {
            GenerateWall();
            needsRegeneration = false;
        }
    }

    private void LateUpdate()
    {
        // Track object positions after all other updates
        if (attachedLeftObject != null)
        {
            prevLeftObjectPos = attachedLeftObject.transform.position;
            prevLeftObjectRot = attachedLeftObject.transform.rotation;
        }
        if (attachedRightObject != null)
        {
            prevRightObjectPos = attachedRightObject.transform.position;
            prevRightObjectRot = attachedRightObject.transform.rotation;
        }
    }

    private void OnDestroy()
    {
        // Cleanup is handled automatically
    }

    private void ApplyMaterial()
    {
        // Apply material if one is assigned
        if (wallMaterial != null && meshRenderer != null)
        {
            meshRenderer.sharedMaterial = wallMaterial;
        }
    }

    private void UpdateAttachmentFollowing()
    {
        bool leftObjectMoved = false;
        bool rightObjectMoved = false;

        // Check if left object moved
        if (attachedLeftObject != null && snapLeftEnd)
        {
            Vector3 currentPos = attachedLeftObject.transform.position;
            Quaternion currentRot = attachedLeftObject.transform.rotation;
            
            if (Vector3.Distance(currentPos, prevLeftObjectPos) > 0.001f || 
                Quaternion.Angle(currentRot, prevLeftObjectRot) > 0.1f)
            {
                leftObjectMoved = true;
            }
        }

        // Check if right object moved
        if (attachedRightObject != null && snapRightEnd)
        {
            Vector3 currentPos = attachedRightObject.transform.position;
            Quaternion currentRot = attachedRightObject.transform.rotation;
            
            if (Vector3.Distance(currentPos, prevRightObjectPos) > 0.001f || 
                Quaternion.Angle(currentRot, prevRightObjectRot) > 0.1f)
            {
                rightObjectMoved = true;
            }
        }

        // If objects moved, recalculate wall dimensions and position
        if ((leftObjectMoved || rightObjectMoved) && autoDimensionFromAttachments && snapLeftEnd && snapRightEnd)
        {
            RecalculateDimensionsFromAttachments();
        }
        else if (leftObjectMoved && snapLeftEnd)
        {
            SnapToLeftObject();
        }
        else if (rightObjectMoved && snapRightEnd)
        {
            SnapToRightObject();
        }
    }

    private void DetectAndAttachToObjects()
    {
        // Find all doors and pillars in the scene
        Door[] doors = FindObjectsOfType<Door>();
        Pillar[] pillars = FindObjectsOfType<Pillar>();

        // Calculate current wall connection points in world space (at mid-height)
        Vector3 leftConnWorld = GetLeftConnectionPointWorld();
        Vector3 rightConnWorld = GetRightConnectionPointWorld();

        // Store previous attachments
        MonoBehaviour prevLeftObject = attachedLeftObject;
        MonoBehaviour prevRightObject = attachedRightObject;

        // Reset attachment flags
        snapLeftEnd = false;
        snapRightEnd = false;
        attachedLeftObject = null;
        attachedRightObject = null;
        leftObjectType = "";
        rightObjectType = "";

        // Check left connection point for attachment
        float closestLeftDist = detectionRange;
        
        // Check doors
        foreach (Door door in doors)
        {
            if (door.Type == Door.RoomType.Door)
            {
                Vector3 snapPoint;
                bool isLeftSide;

                if (door.IsPointNearAttachment(leftConnWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(leftConnWorld, snapPoint);
                    if (dist < closestLeftDist)
                    {
                        closestLeftDist = dist;
                        snapLeftEnd = true;
                        attachedLeftObject = door;
                        leftAttachedToLeftSide = isLeftSide;
                        leftObjectType = "Door";
                    }
                }
            }
        }

        // Check pillars
        foreach (Pillar pillar in pillars)
        {
            if (pillar.Type == Pillar.RoomType.Pillar)
            {
                Vector3 pillarConnection = pillar.GetConnectionPointWorld();
                float dist = Vector3.Distance(leftConnWorld, pillarConnection);
                
                if (dist < closestLeftDist)
                {
                    closestLeftDist = dist;
                    snapLeftEnd = true;
                    attachedLeftObject = pillar;
                    leftObjectType = "Pillar";
                }
            }
        }

        // Check right connection point for attachment
        float closestRightDist = detectionRange;
        
        // Check doors
        foreach (Door door in doors)
        {
            if (door.Type == Door.RoomType.Door)
            {
                Vector3 snapPoint;
                bool isLeftSide;

                if (door.IsPointNearAttachment(rightConnWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(rightConnWorld, snapPoint);
                    if (dist < closestRightDist)
                    {
                        closestRightDist = dist;
                        snapRightEnd = true;
                        attachedRightObject = door;
                        rightAttachedToLeftSide = isLeftSide;
                        rightObjectType = "Door";
                    }
                }
            }
        }

        // Check pillars
        foreach (Pillar pillar in pillars)
        {
            if (pillar.Type == Pillar.RoomType.Pillar)
            {
                Vector3 pillarConnection = pillar.GetConnectionPointWorld();
                float dist = Vector3.Distance(rightConnWorld, pillarConnection);
                
                if (dist < closestRightDist)
                {
                    closestRightDist = dist;
                    snapRightEnd = true;
                    attachedRightObject = pillar;
                    rightObjectType = "Pillar";
                }
            }
        }

        // Initialize object tracking if new attachments were made
        if (attachedLeftObject != null && attachedLeftObject != prevLeftObject)
        {
            prevLeftObjectPos = attachedLeftObject.transform.position;
            prevLeftObjectRot = attachedLeftObject.transform.rotation;
        }
        if (attachedRightObject != null && attachedRightObject != prevRightObject)
        {
            prevRightObjectPos = attachedRightObject.transform.position;
            prevRightObjectRot = attachedRightObject.transform.rotation;
        }

        // If both ends are attached and auto-dimension is enabled, recalculate dimensions
        if (snapLeftEnd && snapRightEnd && autoDimensionFromAttachments)
        {
            RecalculateDimensionsFromAttachments();
        }
        // If only one end is attached, snap that end
        else if (snapLeftEnd && !snapRightEnd)
        {
            SnapToLeftObject();
        }
        else if (!snapLeftEnd && snapRightEnd)
        {
            SnapToRightObject();
        }
    }

    private void RecalculateDimensionsFromAttachments()
    {
        if (attachedLeftObject == null || attachedRightObject == null)
            return;

        // Get attachment points from both objects
        Vector3 leftPoint = GetAttachmentPoint(attachedLeftObject, leftObjectType, leftAttachedToLeftSide);
        Vector3 rightPoint = GetAttachmentPoint(attachedRightObject, rightObjectType, rightAttachedToLeftSide);

        // Calculate distance and midpoint
        float distance = Vector3.Distance(leftPoint, rightPoint);
        Vector3 midpoint = (leftPoint + rightPoint) / 2f;

        // Update wall position
        transform.position = midpoint;

        // Calculate direction and rotation
        Vector3 direction = (rightPoint - leftPoint).normalized;
        if (direction.magnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        // Update length to match distance
        length = distance;

        // Regenerate wall with new dimensions
        needsRegeneration = true;
    }

    private void SnapToLeftObject()
    {
        if (attachedLeftObject == null)
            return;

        Vector3 attachPoint = GetAttachmentPoint(attachedLeftObject, leftObjectType, leftAttachedToLeftSide);
        Vector3 leftConnectionLocal = GetLeftConnectionPointLocal();
        
        // Move wall so left connection point aligns with attachment point
        Vector3 offset = attachPoint - transform.TransformPoint(leftConnectionLocal);
        transform.position += offset;
    }

    private void SnapToRightObject()
    {
        if (attachedRightObject == null)
            return;

        Vector3 attachPoint = GetAttachmentPoint(attachedRightObject, rightObjectType, rightAttachedToLeftSide);
        Vector3 rightConnectionLocal = GetRightConnectionPointLocal();
        
        // Move wall so right connection point aligns with attachment point
        Vector3 offset = attachPoint - transform.TransformPoint(rightConnectionLocal);
        transform.position += offset;
    }

    private Vector3 GetAttachmentPoint(MonoBehaviour obj, string objType, bool isLeftSide)
    {
        if (objType == "Door")
        {
            Door door = (Door)obj;
            return isLeftSide ? door.GetLeftAttachmentPointWorld() : door.GetRightAttachmentPointWorld();
        }
        else if (objType == "Pillar")
        {
            Pillar pillar = (Pillar)obj;
            return pillar.GetConnectionPointWorld();
        }
        return Vector3.zero;
    }

    private Vector3 GetLeftConnectionPointLocal()
    {
        // Calculate the left end position in local space (at bottom, y=0)
        float angleRad = curvatureAngle * Mathf.Deg2Rad;
        float baseRadius = length / angleRad;
        
        // Left end is at angle = 0
        float x = 0;
        float z = 0;
        
        Vector3 pivotOffset = new Vector3(0, 0, baseRadius);
        return new Vector3(x, 0, z) - pivotOffset;
    }

    private Vector3 GetRightConnectionPointLocal()
    {
        // Calculate the right end position in local space (at bottom, y=0)
        float angleRad = curvatureAngle * Mathf.Deg2Rad;
        float baseRadius = length / angleRad;
        
        // Right end is at angle = curvatureAngle
        float x = Mathf.Sin(angleRad) * baseRadius;
        float z = (1f - Mathf.Cos(angleRad)) * baseRadius;
        
        Vector3 pivotOffset = new Vector3(0, 0, baseRadius);
        return new Vector3(x, 0, z) - pivotOffset;
    }

    public Vector3 GetLeftConnectionPointWorld()
    {
        return transform.TransformPoint(GetLeftConnectionPointLocal());
    }

    public Vector3 GetRightConnectionPointWorld()
    {
        return transform.TransformPoint(GetRightConnectionPointLocal());
    }

    private void GenerateWall()
    {
        if (meshFilter == null) return;

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "CircularWall";
        }
        mesh.Clear();

        // Enforce minimum angle
        curvatureAngle = Mathf.Max(1f, curvatureAngle);

        // Clamp values for safety
        length = Mathf.Max(0.1f, length);
        height = Mathf.Max(0.1f, height);
        thickness = Mathf.Max(0.01f, thickness);
        segmentsAlongLength = Mathf.Max(1, segmentsAlongLength);
        segmentsAlongHeight = Mathf.Max(1, segmentsAlongHeight);

        // Calculate curvature
        float angleRad = curvatureAngle * Mathf.Deg2Rad;
        float baseRadius = length / angleRad; // R = arc_length / angle_in_radians

        // Curve direction
        float direction = curveInward ? 1f : -1f;

        // Calculate pivot offset to position the arc centered at the GameObject's origin
        Vector3 pivotOffset = new Vector3(0, 0, baseRadius);

        // Calculate vertex and triangle counts
        int verticesPerRing = (segmentsAlongLength + 1) * 2; // outer + inner per segment
        int totalVertices = verticesPerRing * (segmentsAlongHeight + 1);

        // Add side cap vertices
        int sideCapVertices = (segmentsAlongHeight + 1) * 2 * 2; // left and right caps

        // Add top and bottom cap vertices
        int topBottomCapVertices = (segmentsAlongLength + 1) * 2 * 2; // top and bottom caps

        totalVertices += sideCapVertices + topBottomCapVertices;

        // Triangle counts
        int outerTriangles = segmentsAlongLength * segmentsAlongHeight * 6;
        int innerTriangles = segmentsAlongLength * segmentsAlongHeight * 6;
        int sideCapTriangles = segmentsAlongHeight * 2 * 6; // left and right caps
        int topBottomCapTriangles = segmentsAlongLength * 2 * 6; // top and bottom caps
        int totalTriangles = outerTriangles + innerTriangles + sideCapTriangles + topBottomCapTriangles;

        Vector3[] vertices = new Vector3[totalVertices];
        Vector2[] uvs = new Vector2[totalVertices];
        int[] triangles = new int[totalTriangles];

        int vertIndex = 0;
        int triIndex = 0;

        // === OUTER SURFACE (convex side) ===
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;

            for (int l = 0; l <= segmentsAlongLength; l++)
            {
                float t = l / (float)segmentsAlongLength;
                float currentAngle = t * angleRad;

                // Calculate position on the base arc
                float x_base = Mathf.Sin(currentAngle) * baseRadius;
                float z_base = (1f - Mathf.Cos(currentAngle)) * baseRadius;
                Vector3 basePos = new Vector3(x_base, yPos, z_base) - pivotOffset;

                // Calculate normal direction (perpendicular to the curve)
                Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));

                // Outer vertex (push outward)
                vertices[vertIndex] = basePos + normal * (thickness / 2f) * direction;
                float arcLength = t * length;
                uvs[vertIndex] = new Vector2(arcLength * uvScale, yPos * uvScale);
                vertIndex++;
            }
        }

        // Generate triangles for outer surface
        for (int h = 0; h < segmentsAlongHeight; h++)
        {
            for (int l = 0; l < segmentsAlongLength; l++)
            {
                int bottomLeft = h * (segmentsAlongLength + 1) + l;
                int bottomRight = bottomLeft + 1;
                int topLeft = (h + 1) * (segmentsAlongLength + 1) + l;
                int topRight = topLeft + 1;

                // First triangle
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomRight;

                // Second triangle
                triangles[triIndex++] = bottomRight;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = topRight;
            }
        }

        // === INNER SURFACE (concave side) ===
        int innerStartIndex = vertIndex;
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;

            for (int l = 0; l <= segmentsAlongLength; l++)
            {
                float t = l / (float)segmentsAlongLength;
                float currentAngle = t * angleRad;

                float x_base = Mathf.Sin(currentAngle) * baseRadius;
                float z_base = (1f - Mathf.Cos(currentAngle)) * baseRadius;
                Vector3 basePos = new Vector3(x_base, yPos, z_base) - pivotOffset;

                Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));

                // Inner vertex (push inward)
                vertices[vertIndex] = basePos - normal * (thickness / 2f) * direction;
                float arcLength = t * length;
                uvs[vertIndex] = new Vector2(arcLength * uvScale, yPos * uvScale);
                vertIndex++;
            }
        }

        // Generate triangles for inner surface (reversed winding)
        for (int h = 0; h < segmentsAlongHeight; h++)
        {
            for (int l = 0; l < segmentsAlongLength; l++)
            {
                int bottomLeft = innerStartIndex + h * (segmentsAlongLength + 1) + l;
                int bottomRight = bottomLeft + 1;
                int topLeft = innerStartIndex + (h + 1) * (segmentsAlongLength + 1) + l;
                int topRight = topLeft + 1;

                // First triangle (reversed)
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = bottomRight;
                triangles[triIndex++] = topLeft;

                // Second triangle (reversed)
                triangles[triIndex++] = bottomRight;
                triangles[triIndex++] = topRight;
                triangles[triIndex++] = topLeft;
            }
        }

        // === SIDE CAPS (left and right ends) ===
        int sideCapVertexStart = vertIndex;

        // LEFT side cap (at angle = 0, start of arc)
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;
            float currentAngle = 0f;
            
            float x_base = Mathf.Sin(currentAngle) * baseRadius;
            float z_base = (1f - Mathf.Cos(currentAngle)) * baseRadius;
            Vector3 basePos = new Vector3(x_base, yPos, z_base) - pivotOffset;
            
            Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));
            
            // Outer vertex
            vertices[vertIndex] = basePos + normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(0, yPos * uvScale);
            vertIndex++;
            
            // Inner vertex
            vertices[vertIndex] = basePos - normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(thickness * uvScale, yPos * uvScale);
            vertIndex++;
        }

        // RIGHT side cap (at angle = curvatureAngle, end of arc)
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;
            float currentAngle = angleRad;
            
            float x_base = Mathf.Sin(currentAngle) * baseRadius;
            float z_base = (1f - Mathf.Cos(currentAngle)) * baseRadius;
            Vector3 basePos = new Vector3(x_base, yPos, z_base) - pivotOffset;
            
            Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));
            
            // Outer vertex
            vertices[vertIndex] = basePos + normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(0, yPos * uvScale);
            vertIndex++;
            
            // Inner vertex
            vertices[vertIndex] = basePos - normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(thickness * uvScale, yPos * uvScale);
            vertIndex++;
        }

        // Generate triangles for left side cap
        for (int h = 0; h < segmentsAlongHeight; h++)
        {
            int bottomOuter = sideCapVertexStart + h * 2;
            int bottomInner = bottomOuter + 1;
            int topOuter = bottomOuter + 2;
            int topInner = bottomInner + 2;

            // First triangle
            triangles[triIndex++] = bottomOuter;
            triangles[triIndex++] = bottomInner;
            triangles[triIndex++] = topOuter;

            // Second triangle
            triangles[triIndex++] = topOuter;
            triangles[triIndex++] = bottomInner;
            triangles[triIndex++] = topInner;
        }

        // Generate triangles for right side cap (reversed winding)
        int rightCapStart = sideCapVertexStart + (segmentsAlongHeight + 1) * 2;
        for (int h = 0; h < segmentsAlongHeight; h++)
        {
            int bottomOuter = rightCapStart + h * 2;
            int bottomInner = bottomOuter + 1;
            int topOuter = bottomOuter + 2;
            int topInner = bottomInner + 2;

            // First triangle (reversed)
            triangles[triIndex++] = bottomOuter;
            triangles[triIndex++] = topOuter;
            triangles[triIndex++] = bottomInner;

            // Second triangle (reversed)
            triangles[triIndex++] = topOuter;
            triangles[triIndex++] = topInner;
            triangles[triIndex++] = bottomInner;
        }

        // Store starting index for top/bottom caps
        int topBottomCapVertexStart = vertIndex;

        // === BOTTOM CAP (at y = 0) ===
        for (int l = 0; l <= segmentsAlongLength; l++)
        {
            float t = l / (float)segmentsAlongLength;
            float currentAngle = t * angleRad;
            
            float x_base = Mathf.Sin(currentAngle) * baseRadius;
            float z_base = (1f - Mathf.Cos(currentAngle)) * baseRadius;
            Vector3 basePos = new Vector3(x_base, 0, z_base) - pivotOffset;
            
            Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));
            
            // Outer vertex
            vertices[vertIndex] = basePos + normal * (thickness / 2f) * direction;
            float arcLength = t * length;
            uvs[vertIndex] = new Vector2(arcLength * uvScale, 0);
            vertIndex++;
            
            // Inner vertex
            vertices[vertIndex] = basePos - normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(arcLength * uvScale, thickness * uvScale);
            vertIndex++;
        }

        // === TOP CAP (at y = height) ===
        for (int l = 0; l <= segmentsAlongLength; l++)
        {
            float t = l / (float)segmentsAlongLength;
            float currentAngle = t * angleRad;
            
            float x_base = Mathf.Sin(currentAngle) * baseRadius;
            float z_base = (1f - Mathf.Cos(currentAngle)) * baseRadius;
            Vector3 basePos = new Vector3(x_base, height, z_base) - pivotOffset;
            
            Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));
            
            // Outer vertex
            vertices[vertIndex] = basePos + normal * (thickness / 2f) * direction;
            float arcLength = t * length;
            uvs[vertIndex] = new Vector2(arcLength * uvScale, 0);
            vertIndex++;
            
            // Inner vertex
            vertices[vertIndex] = basePos - normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(arcLength * uvScale, thickness * uvScale);
            vertIndex++;
        }

        // Generate triangles for bottom cap (reversed winding for downward normal)
        for (int l = 0; l < segmentsAlongLength; l++)
        {
            int leftOuter = topBottomCapVertexStart + l * 2;
            int leftInner = leftOuter + 1;
            int rightOuter = leftOuter + 2;
            int rightInner = leftInner + 2;

            // First triangle (reversed)
            triangles[triIndex++] = leftOuter;
            triangles[triIndex++] = rightOuter;
            triangles[triIndex++] = leftInner;

            // Second triangle (reversed)
            triangles[triIndex++] = rightOuter;
            triangles[triIndex++] = rightInner;
            triangles[triIndex++] = leftInner;
        }

        // Generate triangles for top cap (normal winding for upward normal)
        int topCapStart = topBottomCapVertexStart + (segmentsAlongLength + 1) * 2;
        for (int l = 0; l < segmentsAlongLength; l++)
        {
            int leftOuter = topCapStart + l * 2;
            int leftInner = leftOuter + 1;
            int rightOuter = leftOuter + 2;
            int rightInner = leftInner + 2;

            // First triangle
            triangles[triIndex++] = leftOuter;
            triangles[triIndex++] = leftInner;
            triangles[triIndex++] = rightOuter;

            // Second triangle
            triangles[triIndex++] = rightOuter;
            triangles[triIndex++] = leftInner;
            triangles[triIndex++] = rightInner;
        }

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

    // Visualize attachment status and connection points in editor
    private void OnDrawGizmos()
    {
        if (!enableAttachment && !showConnectionPoints) return;

        // Draw connection points (at mid-height)
        if (showConnectionPoints)
        {
            Vector3 leftConn = GetLeftConnectionPointWorld();
            Vector3 rightConn = GetRightConnectionPointWorld();

            // Left connection point
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftConn, 0.15f);
            Gizmos.DrawRay(leftConn, Vector3.up * 0.3f);
            Gizmos.DrawRay(leftConn, Vector3.down * 0.3f);

            // Right connection point
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(rightConn, 0.15f);
            Gizmos.DrawRay(rightConn, Vector3.up * 0.3f);
            Gizmos.DrawRay(rightConn, Vector3.down * 0.3f);

            // Draw pivot point
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }

        if (!enableAttachment) return;

        Vector3 leftEnd = GetLeftConnectionPointWorld();
        Vector3 rightEnd = GetRightConnectionPointWorld();

        // Draw left connection status
        if (snapLeftEnd && attachedLeftObject != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(leftEnd, 0.2f);
            
            Vector3 objectPoint = GetAttachmentPoint(attachedLeftObject, leftObjectType, leftAttachedToLeftSide);
            Gizmos.DrawLine(leftEnd, objectPoint);
        }

        // Draw right connection status
        if (snapRightEnd && attachedRightObject != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(rightEnd, 0.2f);
            
            Vector3 objectPoint = GetAttachmentPoint(attachedRightObject, rightObjectType, rightAttachedToLeftSide);
            Gizmos.DrawLine(rightEnd, objectPoint);
        }

        // Draw detection range
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(leftEnd, detectionRange);
        Gizmos.DrawWireSphere(rightEnd, detectionRange);
    }
}