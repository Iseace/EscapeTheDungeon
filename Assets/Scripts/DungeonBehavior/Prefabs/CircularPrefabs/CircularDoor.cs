using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class CircularDoor : MonoBehaviour
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
    
    [Header("Floor Border Attachment")]
    [SerializeField] private bool enableFloorAttachment = true; // Enable auto-attachment to floor borders
    [SerializeField] private float floorDetectionRange = 1f; // Range to detect floor borders
    [SerializeField] private bool snapToFloorBorder = false; // Currently snapped to floor
    [SerializeField] private bool showFloorConnectionPoint = true; // Show floor connection gizmo
    
    private LineRenderer lineRenderer;
    private List<Vector3> borderPoints = new List<Vector3>();

    // Attachment point properties - now at bottom corners
    public Vector3 LeftAttachmentPoint { get; private set; }
    public Vector3 RightAttachmentPoint { get; private set; }
    public Vector3 LeftAttachmentNormal { get; private set; }
    public Vector3 RightAttachmentNormal { get; private set; }

    // Floor attachment properties
    private Circularfloor attachedFloor;
    private Vector3 floorSnapPoint; // Point on floor border where door is snapped
    private Vector3 floorSnapNormal; // Normal at the snap point
    private Vector3 prevFloorPosition;
    private Quaternion prevFloorRotation;
    private float prevFloorWidth;
    private float prevFloorHeight;
    private float prevFloorCornerRadius;

    private void OnEnable()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false; // Use local space so border moves with object
        GenerateDoorBorder();
        UpdateAttachmentPoints();
        
        if (enableFloorAttachment)
            DetectAndAttachToFloor();
    }

    private void OnValidate()
    {
        // Update border in editor when values change
        if (!Application.isPlaying && GetComponent<LineRenderer>() != null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false; // Ensure local space
            GenerateDoorBorder();
            UpdateAttachmentPoints();
            
            if (enableFloorAttachment)
                DetectAndAttachToFloor();
        }
    }

    private void Update()
    {
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
            prevFloorWidth = GetFloorWidth(attachedFloor);
            prevFloorHeight = GetFloorHeight(attachedFloor);
            prevFloorCornerRadius = GetFloorCornerRadius(attachedFloor);
        }
    }

    private void GenerateDoorBorder()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.useWorldSpace = false; // Ensure local space

        borderPoints.Clear();

        // Door frame centered at the bottom middle (pivot at bottom-center)
        // Calculate half width to center the door
        float halfWidth = doorWidth / 2f;

        // Bottom-left corner (-halfWidth, 0, 0)
        Vector3 bottomLeft = new Vector3(-halfWidth, 0, 0);

        // Bottom-right corner (halfWidth, 0, 0)
        Vector3 bottomRight = new Vector3(halfWidth, 0, 0);

        // Top-left corner (-halfWidth, doorHeight, 0)
        Vector3 topLeft = new Vector3(-halfWidth, doorHeight, 0);

        // Top-right corner (halfWidth, doorHeight, 0)
        Vector3 topRight = new Vector3(halfWidth, doorHeight, 0);

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

        float distToLeft = Vector3.Distance(worldPoint, leftWorld);
        float distToRight = Vector3.Distance(worldPoint, rightWorld);

        // Check if point is within attachment range of either side
        if (distToLeft < attachmentRange)
        {
            snapPoint = leftWorld;
            isLeftSide = true;
            return true;
        }
        else if (distToRight < attachmentRange)
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
        if (!enableFloorAttachment)
        {
            snapToFloorBorder = false;
            attachedFloor = null;
            return;
        }

        // Get the door's floor connection point (bottom center)
        Vector3 doorFloorPoint = GetFloorConnectionPointWorld();

        // Find all Circularfloor objects in the scene
        Circularfloor[] floors = FindObjectsOfType<Circularfloor>();
        Circularfloor nearestFloor = null;
        float minDist = float.MaxValue;
        Vector3 nearestSnapPoint = Vector3.zero;
        Vector3 nearestBorderNormal = Vector3.zero;

        foreach (Circularfloor floor in floors)
        {
            Vector3 closestPoint;
            Vector3 borderNormal;
            float dist = GetClosestPointOnFloorBorder(floor, doorFloorPoint, out closestPoint, out borderNormal);

            if (dist < minDist && dist < floorDetectionRange)
            {
                minDist = dist;
                nearestFloor = floor;
                nearestSnapPoint = closestPoint;
                nearestBorderNormal = borderNormal;
            }
        }

        // Update attachment status
        if (nearestFloor != null)
        {
            attachedFloor = nearestFloor;
            floorSnapPoint = nearestSnapPoint;
            floorSnapNormal = nearestBorderNormal;
            snapToFloorBorder = true;
            SnapToFloorBorder();
        }
        else
        {
            snapToFloorBorder = false;
            attachedFloor = null;
        }
    }

    private float GetClosestPointOnFloorBorder(Circularfloor floor, Vector3 worldPoint, out Vector3 closestPoint, out Vector3 borderNormal)
    {
        // Convert world point to floor's local space
        Vector3 localPoint = floor.transform.InverseTransformPoint(worldPoint);
        float x = localPoint.x;
        float z = localPoint.z;

        // Get floor dimensions (width and height in floor's local space)
        float floorWidth = GetFloorWidth(floor);
        float floorHeight = GetFloorHeight(floor);
        float cornerRadius = GetFloorCornerRadius(floor);

        float halfW = floorWidth / 2f;
        float halfH = floorHeight / 2f;

        // Default values
        Vector3 closestLocal = Vector3.zero;
        Vector3 normalLocal = Vector3.forward;

        // Determine which edge or corner is closest
        float distToLeft = Mathf.Abs(x + halfW);
        float distToRight = Mathf.Abs(x - halfW);
        float distToBottom = Mathf.Abs(z + halfH);
        float distToTop = Mathf.Abs(z - halfH);

        float minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);

        // Check if in a corner region
        bool inCornerRegion = false;
        Vector3 cornerCenter = Vector3.zero;

        // Check each corner
        Vector3[] corners = new Vector3[]
        {
            new Vector3(-halfW + cornerRadius, 0, -halfH + cornerRadius), // Bottom-left
            new Vector3(halfW - cornerRadius, 0, -halfH + cornerRadius),  // Bottom-right
            new Vector3(halfW - cornerRadius, 0, halfH - cornerRadius),   // Top-right
            new Vector3(-halfW + cornerRadius, 0, halfH - cornerRadius)   // Top-left
        };

        foreach (Vector3 corner in corners)
        {
            float distToCorner = Vector3.Distance(new Vector3(x, 0, z), corner);
            if (distToCorner < cornerRadius &&
                (Mathf.Abs(x - corner.x) < cornerRadius || Mathf.Abs(z - corner.z) < cornerRadius))
            {
                inCornerRegion = true;
                cornerCenter = corner;
                break;
            }
        }

        // Clamp x and z for edge calculation
        float clampedX = Mathf.Clamp(x, -halfW + cornerRadius, halfW - cornerRadius);
        float clampedZ = Mathf.Clamp(z, -halfH + cornerRadius, halfH - cornerRadius);

        // Recalculate distances with clamped values for areas outside corners
        if (!inCornerRegion)
        {
            distToLeft = Mathf.Abs(clampedX + halfW);
            distToRight = Mathf.Abs(clampedX - halfW);
            distToBottom = Mathf.Abs(clampedZ + halfH);
            distToTop = Mathf.Abs(clampedZ - halfH);
            minDist = Mathf.Min(distToLeft, distToRight, distToBottom, distToTop);

            // Further adjust for corner transition zones
            if ((x < -halfW + cornerRadius || x > halfW - cornerRadius) &&
                (z < -halfH + cornerRadius || z > halfH - cornerRadius))
            {
                inCornerRegion = true;
                cornerCenter = new Vector3(
                    x < 0 ? -halfW + cornerRadius : halfW - cornerRadius,
                    0,
                    z < 0 ? -halfH + cornerRadius : halfH - cornerRadius
                );
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

        // Return distance
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

    private void UpdateFloorFollowing()
    {
        if (attachedFloor == null || !snapToFloorBorder)
            return;

        // Check if floor has moved, rotated, or dimensions changed
        bool floorChanged = false;

        Vector3 currentPos = attachedFloor.transform.position;
        Quaternion currentRot = attachedFloor.transform.rotation;
        float currentWidth = GetFloorWidth(attachedFloor);
        float currentHeight = GetFloorHeight(attachedFloor);
        float currentRadius = GetFloorCornerRadius(attachedFloor);

        if (Vector3.Distance(currentPos, prevFloorPosition) > 0.001f ||
            Quaternion.Angle(currentRot, prevFloorRotation) > 0.1f ||
            Mathf.Abs(currentWidth - prevFloorWidth) > 0.001f ||
            Mathf.Abs(currentHeight - prevFloorHeight) > 0.001f ||
            Mathf.Abs(currentRadius - prevFloorCornerRadius) > 0.001f)
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

    // Helper methods to get floor properties using reflection (since fields are private)
    private float GetFloorWidth(Circularfloor floor)
    {
        var field = floor.GetType().GetField("width", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (float)field.GetValue(floor) : 10f;
    }

    private float GetFloorHeight(Circularfloor floor)
    {
        var field = floor.GetType().GetField("height", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null ? (float)field.GetValue(floor) : 10f;
    }

    private float GetFloorCornerRadius(Circularfloor floor)
    {
        return floor.cornerRadius; // This is public
    }

    public void SetDoorDimensions(float newWidth, float newHeight)
    {
        doorWidth = newWidth;
        doorHeight = newHeight;
        GenerateDoorBorder();
        UpdateAttachmentPoints();
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
}