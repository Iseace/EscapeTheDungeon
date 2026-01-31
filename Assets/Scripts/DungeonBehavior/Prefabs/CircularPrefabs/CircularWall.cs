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
    [SerializeField] private float height = 3f; // Wall height
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
    
    [Header("Door Attachment")]
    [SerializeField] private bool enableDoorAttachment = true; // Enable auto-attachment to doors
    [SerializeField] private float doorDetectionRange = 2f; // Range to detect doors
    [SerializeField] private bool autoDimensionFromDoors = true; // Auto-adjust dimensions when both ends attached
    [SerializeField] private bool snapLeftEndToDoor = false; // Snap left end to door
    [SerializeField] private bool snapRightEndToDoor = false; // Snap right end to door
    [SerializeField] private bool showConnectionPoints = false; // Show connection point gizmos
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    // Door attachment references
    private CircularDoor attachedLeftDoor;
    private CircularDoor attachedRightDoor;
    private bool leftAttachedToLeftSide; // Which side of the door is attached
    private bool rightAttachedToLeftSide;

    // Previous door positions for tracking movement
    private Vector3 prevLeftDoorPos;
    private Vector3 prevRightDoorPos;
    private Quaternion prevLeftDoorRot;
    private Quaternion prevRightDoorRot;

    private bool needsRegeneration = false;

    private void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        
        if (enableDoorAttachment)
            DetectAndAttachToDoors();
        
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
        
        if (enableDoorAttachment)
            DetectAndAttachToDoors();
        
        GenerateWall();
        ApplyMaterial();
    }

    private void Update()
    {
        // Continuously update attachment in play mode if enabled
        if (enableDoorAttachment)
        {
            DetectAndAttachToDoors();
            UpdateDoorFollowing();
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
        // Track door positions after all other updates
        if (attachedLeftDoor != null)
        {
            prevLeftDoorPos = attachedLeftDoor.transform.position;
            prevLeftDoorRot = attachedLeftDoor.transform.rotation;
        }
        if (attachedRightDoor != null)
        {
            prevRightDoorPos = attachedRightDoor.transform.position;
            prevRightDoorRot = attachedRightDoor.transform.rotation;
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

    private void UpdateDoorFollowing()
    {
        bool leftDoorMoved = false;
        bool rightDoorMoved = false;

        // Check if left door moved
        if (attachedLeftDoor != null && snapLeftEndToDoor)
        {
            Vector3 currentPos = attachedLeftDoor.transform.position;
            Quaternion currentRot = attachedLeftDoor.transform.rotation;
            
            if (Vector3.Distance(currentPos, prevLeftDoorPos) > 0.001f || 
                Quaternion.Angle(currentRot, prevLeftDoorRot) > 0.1f)
            {
                leftDoorMoved = true;
            }
        }

        // Check if right door moved
        if (attachedRightDoor != null && snapRightEndToDoor)
        {
            Vector3 currentPos = attachedRightDoor.transform.position;
            Quaternion currentRot = attachedRightDoor.transform.rotation;
            
            if (Vector3.Distance(currentPos, prevRightDoorPos) > 0.001f || 
                Quaternion.Angle(currentRot, prevRightDoorRot) > 0.1f)
            {
                rightDoorMoved = true;
            }
        }

        // If doors moved, recalculate wall dimensions and position
        if ((leftDoorMoved || rightDoorMoved) && autoDimensionFromDoors)
        {
            RecalculateDimensionsFromDoors();
        }
    }

    private void DetectAndAttachToDoors()
    {
        // Find all doors in the scene
        CircularDoor[] doors = FindObjectsOfType<CircularDoor>();
        
        if (doors.Length == 0)
        {
            attachedLeftDoor = null;
            attachedRightDoor = null;
            snapLeftEndToDoor = false;
            snapRightEndToDoor = false;
            return;
        }

        // Calculate current wall connection points in world space (at mid-height)
        Vector3 leftConnWorld = GetLeftConnectionPointWorld();
        Vector3 rightConnWorld = GetRightConnectionPointWorld();

        // Store previous attachments
        CircularDoor prevLeftDoor = attachedLeftDoor;
        CircularDoor prevRightDoor = attachedRightDoor;

        // Reset attachment flags
        snapLeftEndToDoor = false;
        snapRightEndToDoor = false;
        attachedLeftDoor = null;
        attachedRightDoor = null;

        // Check left connection point for door attachment
        float closestLeftDist = doorDetectionRange;
        foreach (CircularDoor door in doors)
        {
            if (door.Type == CircularDoor.RoomType.Door)
            {
                Vector3 snapPoint;
                bool isLeftSide;
                
                if (door.IsPointNearAttachment(leftConnWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(leftConnWorld, snapPoint);
                    if (dist < closestLeftDist)
                    {
                        closestLeftDist = dist;
                        snapLeftEndToDoor = true;
                        attachedLeftDoor = door;
                        leftAttachedToLeftSide = isLeftSide;
                    }
                }
            }
        }

        // Check right connection point for door attachment
        float closestRightDist = doorDetectionRange;
        foreach (CircularDoor door in doors)
        {
            if (door.Type == CircularDoor.RoomType.Door)
            {
                Vector3 snapPoint;
                bool isLeftSide;
                
                if (door.IsPointNearAttachment(rightConnWorld, out snapPoint, out isLeftSide))
                {
                    float dist = Vector3.Distance(rightConnWorld, snapPoint);
                    if (dist < closestRightDist)
                    {
                        closestRightDist = dist;
                        snapRightEndToDoor = true;
                        attachedRightDoor = door;
                        rightAttachedToLeftSide = isLeftSide;
                    }
                }
            }
        }

        // Initialize door tracking if new attachments were made
        if (attachedLeftDoor != null && attachedLeftDoor != prevLeftDoor)
        {
            prevLeftDoorPos = attachedLeftDoor.transform.position;
            prevLeftDoorRot = attachedLeftDoor.transform.rotation;
        }
        if (attachedRightDoor != null && attachedRightDoor != prevRightDoor)
        {
            prevRightDoorPos = attachedRightDoor.transform.position;
            prevRightDoorRot = attachedRightDoor.transform.rotation;
        }

        // If both ends are attached and auto-dimension is enabled, recalculate dimensions
        if (snapLeftEndToDoor && snapRightEndToDoor && autoDimensionFromDoors)
        {
            RecalculateDimensionsFromDoors();
        }
        // If only one end is attached, snap that end
        else if (snapLeftEndToDoor && !snapRightEndToDoor)
        {
            SnapToLeftDoor();
        }
        else if (!snapLeftEndToDoor && snapRightEndToDoor)
        {
            SnapToRightDoor();
        }
    }

    private void RecalculateDimensionsFromDoors()
    {
        if (attachedLeftDoor == null || attachedRightDoor == null)
            return;

        // Get the attachment points from both doors
        Vector3 leftDoorPoint = leftAttachedToLeftSide ? 
            attachedLeftDoor.GetLeftAttachmentPointWorld() : 
            attachedLeftDoor.GetRightAttachmentPointWorld();
        
        Vector3 rightDoorPoint = rightAttachedToLeftSide ? 
            attachedRightDoor.GetLeftAttachmentPointWorld() : 
            attachedRightDoor.GetRightAttachmentPointWorld();

        // Calculate the distance between the two door attachment points
        float distance3D = Vector3.Distance(leftDoorPoint, rightDoorPoint);
        
        // Project to XZ plane for arc calculation
        Vector3 leftPoint2D = new Vector3(leftDoorPoint.x, 0, leftDoorPoint.z);
        Vector3 rightPoint2D = new Vector3(rightDoorPoint.x, 0, rightDoorPoint.z);
        float distance2D = Vector3.Distance(leftPoint2D, rightPoint2D);

        // Calculate the direction from left to right door
        Vector3 direction = (rightDoorPoint - leftDoorPoint).normalized;
        Vector3 direction2D = (rightPoint2D - leftPoint2D).normalized;

        // Calculate the angle between the doors
        Vector3 leftForward = leftAttachedToLeftSide ? 
            -attachedLeftDoor.transform.right : 
            attachedLeftDoor.transform.right;
        
        Vector3 rightForward = rightAttachedToLeftSide ? 
            -attachedRightDoor.transform.right : 
            attachedRightDoor.transform.right;

        // Project to XZ plane
        leftForward = new Vector3(leftForward.x, 0, leftForward.z).normalized;
        rightForward = new Vector3(rightForward.x, 0, rightForward.z).normalized;

        // Calculate the angle between door normals
        float angleRad = Vector3.Angle(leftForward, rightForward) * Mathf.Deg2Rad;
        
        // If doors are roughly parallel, use straight wall
        if (angleRad < 0.1f)
        {
            curvatureAngle = 1f; // Minimum curvature (almost straight)
            length = distance2D;
        }
        else
        {
            // Calculate arc parameters
            // For a circular arc: length = radius * angle
            // We want to find the radius and angle that fits between the two doors
            
            // Use the distance as the chord length
            float chordLength = distance2D;
            
            // Estimate curvature angle from door orientations
            float doorAngleDiff = Vector3.SignedAngle(leftForward, rightForward, Vector3.up);
            curvatureAngle = Mathf.Abs(doorAngleDiff);
            
            // Clamp angle
            curvatureAngle = Mathf.Clamp(curvatureAngle, 1f, 360f);
            
            // Calculate arc length from chord and angle
            angleRad = curvatureAngle * Mathf.Deg2Rad;
            float radius = chordLength / (2f * Mathf.Sin(angleRad / 2f));
            length = radius * angleRad;
        }

        // Position the wall so its left connection point aligns with the left door
        // Calculate where the pivot should be
        Vector3 leftConnLocal = GetLeftConnectionPointLocal();
        Vector3 desiredPivotPos = leftDoorPoint - transform.TransformDirection(leftConnLocal);
        transform.position = desiredPivotPos;

        // Calculate rotation to align with doors
        Vector3 midPoint = (leftDoorPoint + rightDoorPoint) / 2f;
        Vector3 toMid = (midPoint - leftDoorPoint).normalized;
        float rotationAngle = Mathf.Atan2(toMid.x, toMid.z) * Mathf.Rad2Deg;
        
        // Adjust rotation based on curvature
        rotationAngle -= curvatureAngle / 2f;
        
        transform.rotation = Quaternion.Euler(0, rotationAngle, 0);

        // Mark for regeneration
        needsRegeneration = true;
    }

    private void SnapToLeftDoor()
    {
        if (attachedLeftDoor == null)
            return;

        Vector3 snapPoint = leftAttachedToLeftSide ? 
            attachedLeftDoor.GetLeftAttachmentPointWorld() : 
            attachedLeftDoor.GetRightAttachmentPointWorld();

        Vector3 leftConnWorld = GetLeftConnectionPointWorld();
        transform.position += snapPoint - leftConnWorld;
    }

    private void SnapToRightDoor()
    {
        if (attachedRightDoor == null)
            return;

        Vector3 snapPoint = rightAttachedToLeftSide ? 
            attachedRightDoor.GetLeftAttachmentPointWorld() : 
            attachedRightDoor.GetRightAttachmentPointWorld();

        Vector3 rightConnWorld = GetRightConnectionPointWorld();
        transform.position += snapPoint - rightConnWorld;
    }

    // Calculate the center point of the bottom arc (for pivot positioning)
    private Vector3 GetBottomCenterLocal()
    {
        // Calculate the geometric center of the arc at y=0
        float angleRad = curvatureAngle * Mathf.Deg2Rad;
        float baseRadius = length / angleRad;
        
        // Center is at the midpoint angle
        float centerAngle = angleRad / 2f;
        float x = Mathf.Sin(centerAngle) * baseRadius;
        float z = (1f - Mathf.Cos(centerAngle)) * baseRadius;
        
        return new Vector3(x, 0, z);
    }

    // Get left connection point in local space (at mid-height)
    private Vector3 GetLeftConnectionPointLocal()
    {
        // Left endpoint at angle 0, mid-height
        Vector3 centerOffset = GetBottomCenterLocal();
        return new Vector3(0, 0, 0) - centerOffset;
    }

    // Get right connection point in local space (at mid-height)
    private Vector3 GetRightConnectionPointLocal()
    {
        float angleRad = curvatureAngle * Mathf.Deg2Rad;
        float baseRadius = length / angleRad;
        
        // Right endpoint at final angle, mid-height
        float x = Mathf.Sin(angleRad) * baseRadius;
        float z = (1f - Mathf.Cos(angleRad)) * baseRadius;
        
        Vector3 centerOffset = GetBottomCenterLocal();
        return new Vector3(x, height / 2f, z) - centerOffset;
    }

    // Get left connection point in world space
    private Vector3 GetLeftConnectionPointWorld()
    {
        return transform.TransformPoint(GetLeftConnectionPointLocal());
    }

    // Get right connection point in world space
    private Vector3 GetRightConnectionPointWorld()
    {
        return transform.TransformPoint(GetRightConnectionPointLocal());
    }

    // Legacy methods for backward compatibility (now point to bottom of endpoints)
    private Vector3 GetLeftEndpointWorld()
    {
        Vector3 centerOffset = GetBottomCenterLocal();
        return transform.TransformPoint(new Vector3(0, 0, 0) - centerOffset);
    }

    private Vector3 GetRightEndpointWorld()
    {
        float angleRad = curvatureAngle * Mathf.Deg2Rad;
        float baseRadius = length / angleRad;
        
        float x = Mathf.Sin(angleRad) * baseRadius;
        float z = (1f - Mathf.Cos(angleRad)) * baseRadius;
        
        Vector3 centerOffset = GetBottomCenterLocal();
        Vector3 localPoint = new Vector3(x, 0, z) - centerOffset;
        return transform.TransformPoint(localPoint);
    }

    private void GenerateWall()
    {
        mesh = new Mesh();
        mesh.name = "Circular Wall";

        // Calculate base radius from length and angle
        float angleRad = curvatureAngle * Mathf.Deg2Rad;
        float baseRadius = length / angleRad;
        
        float direction = curveInward ? 1f : -1f;
        
        // Calculate the offset to center the pivot at the middle of the bottom arc
        Vector3 pivotOffset = GetBottomCenterLocal();
        
        // Calculate vertex counts
        int wallVertexCount = (segmentsAlongLength + 1) * (segmentsAlongHeight + 1) * 2; // Inner and outer surfaces
        int sideCapVertexCount = (segmentsAlongHeight + 1) * 2 * 2; // Left and right caps
        int topBottomCapVertexCount = (segmentsAlongLength + 1) * 2 * 2; // Top and bottom caps
        int totalVertexCount = wallVertexCount + sideCapVertexCount + topBottomCapVertexCount;
        
        Vector3[] vertices = new Vector3[totalVertexCount];
        Vector2[] uvs = new Vector2[totalVertexCount];
        
        // Calculate triangle counts
        int wallTriangleCount = segmentsAlongLength * segmentsAlongHeight * 12; // Outer and inner surfaces
        int sideCapTriangleCount = segmentsAlongHeight * 2 * 6; // Left and right caps
        int topBottomCapTriangleCount = segmentsAlongLength * 2 * 6; // Top and bottom caps
        int[] triangles = new int[wallTriangleCount + sideCapTriangleCount + topBottomCapTriangleCount];

        int vertIndex = 0;
        int triIndex = 0;

        // Generate vertices for outer surface (away from center)
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;
            
            for (int l = 0; l <= segmentsAlongLength; l++)
            {
                float t = l / (float)segmentsAlongLength;
                float currentAngle = t * angleRad;
                
                // Calculate position on arc at base radius
                float x = Mathf.Sin(currentAngle) * baseRadius;
                float z = (1f - Mathf.Cos(currentAngle)) * baseRadius;
                
                // Calculate normal direction (perpendicular to arc, pointing outward)
                Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));
                
                // Offset by half thickness in normal direction
                Vector3 offset = normal * (thickness / 2f) * direction;
                
                // Apply pivot offset to center the wall
                vertices[vertIndex] = new Vector3(x, yPos, z) + offset - pivotOffset;
                
                // UV mapping: U along arc length, V along height (world-space scaled)
                float arcLength = t * length; // Current position along arc
                uvs[vertIndex] = new Vector2(arcLength * uvScale, yPos * uvScale);
                vertIndex++;
            }
        }

        // Generate vertices for inner surface (toward center)
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;
            
            for (int l = 0; l <= segmentsAlongLength; l++)
            {
                float t = l / (float)segmentsAlongLength;
                float currentAngle = t * angleRad;
                
                // Calculate position on arc at base radius
                float x = Mathf.Sin(currentAngle) * baseRadius;
                float z = (1f - Mathf.Cos(currentAngle)) * baseRadius;
                
                // Calculate normal direction (perpendicular to arc, pointing outward)
                Vector3 normal = new Vector3(Mathf.Sin(currentAngle), 0, -Mathf.Cos(currentAngle));
                
                // Offset by half thickness in opposite normal direction
                Vector3 offset = normal * (thickness / 2f) * direction;
                
                // Apply pivot offset to center the wall
                vertices[vertIndex] = new Vector3(x, yPos, z) - offset - pivotOffset;
                
                // UV mapping (same as outer for consistent tiling)
                float arcLength = t * length;
                uvs[vertIndex] = new Vector2(arcLength * uvScale, yPos * uvScale);
                vertIndex++;
            }
        }

        int vertsPerRow = segmentsAlongLength + 1;

        // Generate triangles for outer surface
        for (int h = 0; h < segmentsAlongHeight; h++)
        {
            for (int l = 0; l < segmentsAlongLength; l++)
            {
                int bottomLeft = h * vertsPerRow + l;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + vertsPerRow;
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

        // Generate triangles for inner surface (reversed winding)
        int innerOffset = (segmentsAlongHeight + 1) * vertsPerRow;
        for (int h = 0; h < segmentsAlongHeight; h++)
        {
            for (int l = 0; l < segmentsAlongLength; l++)
            {
                int bottomLeft = innerOffset + h * vertsPerRow + l;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + vertsPerRow;
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

        // Store starting index for side caps
        int sideCapVertexStart = vertIndex;

        // === LEFT SIDE CAP (at angle 0) ===
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;
            
            // At angle 0, calculate normal perpendicular to the curve
            Vector3 normal = new Vector3(Mathf.Sin(0), 0, -Mathf.Cos(0)); // (0, 0, -1)
            Vector3 basePos = new Vector3(0, yPos, 0) - pivotOffset;
            
            // Outer vertex (offset in direction)
            vertices[vertIndex] = basePos + normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(0, yPos * uvScale);
            vertIndex++;
            
            // Inner vertex (offset in opposite direction)
            vertices[vertIndex] = basePos - normal * (thickness / 2f) * direction;
            uvs[vertIndex] = new Vector2(thickness * uvScale, yPos * uvScale);
            vertIndex++;
        }

        // === RIGHT SIDE CAP (at final angle) ===
        for (int h = 0; h <= segmentsAlongHeight; h++)
        {
            float yPos = (h / (float)segmentsAlongHeight) * height;
            
            // Calculate base position and normal at final angle
            float x_base = Mathf.Sin(angleRad) * baseRadius;
            float z_base = (1f - Mathf.Cos(angleRad)) * baseRadius;
            Vector3 basePos = new Vector3(x_base, yPos, z_base) - pivotOffset;
            
            // Use perpendicular normal pointing outward from the curve
            Vector3 normal = new Vector3(Mathf.Sin(angleRad), 0, -Mathf.Cos(angleRad));
            
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
        if (!enableDoorAttachment && !showConnectionPoints) return;

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

        if (!enableDoorAttachment) return;

        Vector3 leftEnd = GetLeftConnectionPointWorld();
        Vector3 rightEnd = GetRightConnectionPointWorld();

        // Draw left connection status
        if (snapLeftEndToDoor && attachedLeftDoor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(leftEnd, 0.2f);
            
            Vector3 doorPoint = leftAttachedToLeftSide ? 
                attachedLeftDoor.GetLeftAttachmentPointWorld() : 
                attachedLeftDoor.GetRightAttachmentPointWorld();
            Gizmos.DrawLine(leftEnd, doorPoint);
        }

        // Draw right connection status
        if (snapRightEndToDoor && attachedRightDoor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(rightEnd, 0.2f);
            
            Vector3 doorPoint = rightAttachedToLeftSide ? 
                attachedRightDoor.GetLeftAttachmentPointWorld() : 
                attachedRightDoor.GetRightAttachmentPointWorld();
            Gizmos.DrawLine(rightEnd, doorPoint);
        }

        // Draw detection range
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(leftEnd, doorDetectionRange);
        Gizmos.DrawWireSphere(rightEnd, doorDetectionRange);
    }
}