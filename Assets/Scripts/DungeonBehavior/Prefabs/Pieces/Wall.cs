using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class Wall : MonoBehaviour
{
    public enum RoomType { Wall }
    public RoomType Type { get { return RoomType.Wall; } }

    [Header("Type")]
    [SerializeField] public RoomType roomType = RoomType.Wall;

    [Header("Wall Dimensions")]
    [SerializeField] private float height = 3f; // Wall height
    [SerializeField] private float length = 10f; // Wall length
    [SerializeField] private float thickness = 0.2f; // Wall thickness

    [Header("Material Settings")]
    [SerializeField] private Material wallMaterial; // Material to apply to the wall
    [SerializeField] private float uvScale = 1f; // UV scale factor (1 = 1 Unity unit = 1 texture repeat)

    [Header("Attachment Settings")]
    [SerializeField] private bool enableAttachment = true; // Enable auto-attachment to doors and pillars
    [SerializeField] private float detectionRange = 2f; // Range to detect doors and pillars
    [SerializeField] private bool autoDimensionFromAttachments = true; // Auto-adjust dimensions when both ends attached
    [SerializeField] private bool snapLeftEnd = false; // Snap left end to door/pillar
    [SerializeField] private bool snapRightEnd = false; // Snap right end to door/pillar
    [SerializeField] private bool showConnectionPoints = false; // Show connection point gizmos

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private Mesh mesh;

    // Attachment references - can be either Door or Pillar
    private MonoBehaviour attachedLeftObject;  // RectangularDoor or Pillar
    private MonoBehaviour attachedRightObject; // RectangularDoor or Pillar
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
        RectangularDoor[] doors = FindObjectsOfType<RectangularDoor>();
        Pillar[] pillars = FindObjectsOfType<Pillar>();

        // Calculate current wall connection points in world space (at bottom)
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
        foreach (RectangularDoor door in doors)
        {
            if (door.Type == RectangularDoor.RoomType.Door)
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
        foreach (RectangularDoor door in doors)
        {
            if (door.Type == RectangularDoor.RoomType.Door)
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

        // If both ends are now attached and auto-dimensioning is enabled, recalculate
        if (snapLeftEnd && snapRightEnd && autoDimensionFromAttachments)
        {
            RecalculateDimensionsFromAttachments();
        }
        else if (snapLeftEnd && (attachedLeftObject != prevLeftObject))
        {
            SnapToLeftObject();
        }
        else if (snapRightEnd && (attachedRightObject != prevRightObject))
        {
            SnapToRightObject();
        }
    }

    private void RecalculateDimensionsFromAttachments()
    {
        if (attachedLeftObject == null || attachedRightObject == null)
            return;

        // Get attachment points based on object type
        Vector3 leftPoint = GetAttachmentPoint(attachedLeftObject, leftObjectType, leftAttachedToLeftSide);
        Vector3 rightPoint = GetAttachmentPoint(attachedRightObject, rightObjectType, rightAttachedToLeftSide);

        // Calculate distance and direction between attachment points
        Vector3 direction = rightPoint - leftPoint;
        float distance = direction.magnitude;

        // Update wall length
        length = distance;

        // Calculate midpoint for wall position
        Vector3 midpoint = (leftPoint + rightPoint) / 2f;
        transform.position = midpoint;

        // Calculate rotation to align wall with attachment points
        if (direction.magnitude > 0.001f)
        {
            Vector3 forwardDir = direction.normalized;
            transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            transform.rotation = Quaternion.FromToRotation(Vector3.right, forwardDir) * transform.rotation;
        }

        // Regenerate wall with new dimensions
        needsRegeneration = true;
    }

    private void SnapToLeftObject()
    {
        if (attachedLeftObject == null)
            return;

        Vector3 snapPoint = GetAttachmentPoint(attachedLeftObject, leftObjectType, leftAttachedToLeftSide);
        Vector3 leftConnWorld = GetLeftConnectionPointWorld();
        transform.position += snapPoint - leftConnWorld;
    }

    private void SnapToRightObject()
    {
        if (attachedRightObject == null)
            return;

        Vector3 snapPoint = GetAttachmentPoint(attachedRightObject, rightObjectType, rightAttachedToLeftSide);
        Vector3 rightConnWorld = GetRightConnectionPointWorld();
        transform.position += snapPoint - rightConnWorld;
    }

    // Helper method to get attachment point based on object type
    private Vector3 GetAttachmentPoint(MonoBehaviour obj, string objectType, bool isLeftSide)
    {
        if (objectType == "Door")
        {
            RectangularDoor door = (RectangularDoor)obj;
            return isLeftSide ? door.GetLeftAttachmentPointWorld() : door.GetRightAttachmentPointWorld();
        }
        else if (objectType == "Pillar")
        {
            Pillar pillar = (Pillar)obj;
            return pillar.GetConnectionPointWorld();
        }
        
        return Vector3.zero;
    }

    // Method to check if a point is near a wall attachment point (for pillar detection)
    public bool IsPointNearWallAttachment(Vector3 worldPoint, out Vector3 snapPoint, out bool isLeftSide)
    {
        Vector3 leftWorld = GetLeftConnectionPointWorld();
        Vector3 rightWorld = GetRightConnectionPointWorld();

        float leftDist = Vector3.Distance(worldPoint, leftWorld);
        float rightDist = Vector3.Distance(worldPoint, rightWorld);

        if (leftDist < detectionRange && leftDist <= rightDist)
        {
            snapPoint = leftWorld;
            isLeftSide = true;
            return true;
        }
        else if (rightDist < detectionRange)
        {
            snapPoint = rightWorld;
            isLeftSide = false;
            return true;
        }

        snapPoint = Vector3.zero;
        isLeftSide = false;
        return false;
    }

    public Vector3 GetLeftConnectionPointWorld()
    {
        // Left end of wall at bottom
        Vector3 localPoint = new Vector3(-length / 2f, 0, 0);
        return transform.TransformPoint(localPoint);
    }

    public Vector3 GetRightConnectionPointWorld()
    {
        // Right end of wall at bottom
        Vector3 localPoint = new Vector3(length / 2f, 0, 0);
        return transform.TransformPoint(localPoint);
    }

    private void GenerateWall()
    {
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Wall";
        }

        mesh.Clear();

        // Validate dimensions
        length = Mathf.Max(0.1f, length);
        height = Mathf.Max(0.1f, height);
        thickness = Mathf.Max(0.01f, thickness);

        // Create rectangular wall mesh
        // Wall extends along X axis, centered at origin, with thickness along Z axis

        float halfLength = length / 2f;
        float halfThickness = thickness / 2f;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // Front face (positive Z) - FIXED WINDING ORDER
        vertices.Add(new Vector3(-halfLength, 0, halfThickness));           // 0: Bottom-left
        vertices.Add(new Vector3(halfLength, 0, halfThickness));            // 1: Bottom-right
        vertices.Add(new Vector3(halfLength, height, halfThickness));       // 2: Top-right
        vertices.Add(new Vector3(-halfLength, height, halfThickness));      // 3: Top-left

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(length * uvScale, 0));
        uvs.Add(new Vector2(length * uvScale, height * uvScale));
        uvs.Add(new Vector2(0, height * uvScale));

        triangles.AddRange(new int[] { 0, 1, 2, 0, 2, 3 }); // CORRECTED

        // Back face (negative Z) - FIXED WINDING ORDER
        vertices.Add(new Vector3(-halfLength, 0, -halfThickness));          // 4: Bottom-left
        vertices.Add(new Vector3(halfLength, 0, -halfThickness));           // 5: Bottom-right
        vertices.Add(new Vector3(halfLength, height, -halfThickness));      // 6: Top-right
        vertices.Add(new Vector3(-halfLength, height, -halfThickness));     // 7: Top-left

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(length * uvScale, 0));
        uvs.Add(new Vector2(length * uvScale, height * uvScale));
        uvs.Add(new Vector2(0, height * uvScale));

        triangles.AddRange(new int[] { 4, 6, 5, 4, 7, 6 }); // CORRECTED

        // Left cap (negative X) - FIXED WINDING ORDER
        vertices.Add(new Vector3(-halfLength, 0, -halfThickness));          // 8
        vertices.Add(new Vector3(-halfLength, 0, halfThickness));           // 9
        vertices.Add(new Vector3(-halfLength, height, halfThickness));      // 10
        vertices.Add(new Vector3(-halfLength, height, -halfThickness));     // 11

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(thickness * uvScale, 0));
        uvs.Add(new Vector2(thickness * uvScale, height * uvScale));
        uvs.Add(new Vector2(0, height * uvScale));

        triangles.AddRange(new int[] { 8, 9, 10, 8, 10, 11 }); // CORRECTED

        // Right cap (positive X) - FIXED WINDING ORDER
        vertices.Add(new Vector3(halfLength, 0, halfThickness));            // 12
        vertices.Add(new Vector3(halfLength, 0, -halfThickness));           // 13
        vertices.Add(new Vector3(halfLength, height, -halfThickness));      // 14
        vertices.Add(new Vector3(halfLength, height, halfThickness));       // 15

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(thickness * uvScale, 0));
        uvs.Add(new Vector2(thickness * uvScale, height * uvScale));
        uvs.Add(new Vector2(0, height * uvScale));

        triangles.AddRange(new int[] { 12, 13, 14, 12, 14, 15 }); // CORRECTED

        // Bottom cap (y = 0) - FIXED WINDING ORDER
        vertices.Add(new Vector3(-halfLength, 0, halfThickness));           // 16
        vertices.Add(new Vector3(halfLength, 0, halfThickness));            // 17
        vertices.Add(new Vector3(halfLength, 0, -halfThickness));           // 18
        vertices.Add(new Vector3(-halfLength, 0, -halfThickness));          // 19

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(length * uvScale, 0));
        uvs.Add(new Vector2(length * uvScale, thickness * uvScale));
        uvs.Add(new Vector2(0, thickness * uvScale));

        triangles.AddRange(new int[] { 16, 19, 18, 16, 18, 17 }); // CORRECTED

        // Top cap (y = height) - FIXED WINDING ORDER
        vertices.Add(new Vector3(-halfLength, height, halfThickness));      // 20
        vertices.Add(new Vector3(halfLength, height, halfThickness));       // 21
        vertices.Add(new Vector3(halfLength, height, -halfThickness));      // 22
        vertices.Add(new Vector3(-halfLength, height, -halfThickness));     // 23

        uvs.Add(new Vector2(0, 0));
        uvs.Add(new Vector2(length * uvScale, 0));
        uvs.Add(new Vector2(length * uvScale, thickness * uvScale));
        uvs.Add(new Vector2(0, thickness * uvScale));

        triangles.AddRange(new int[] { 20, 21, 22, 20, 22, 23 }); // CORRECTED

        // Assign mesh data
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
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

        // Draw connection points (at bottom)
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

            Vector3 attachPoint = GetAttachmentPoint(attachedLeftObject, leftObjectType, leftAttachedToLeftSide);
            Gizmos.DrawLine(leftEnd, attachPoint);
            
            // Draw label for object type
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(leftEnd + Vector3.up * 0.5f, "Left: " + leftObjectType);
            #endif
        }

        // Draw right connection status
        if (snapRightEnd && attachedRightObject != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(rightEnd, 0.2f);

            Vector3 attachPoint = GetAttachmentPoint(attachedRightObject, rightObjectType, rightAttachedToLeftSide);
            Gizmos.DrawLine(rightEnd, attachPoint);
            
            // Draw label for object type
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(rightEnd + Vector3.up * 0.5f, "Right: " + rightObjectType);
            #endif
        }

        // Draw detection range
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(leftEnd, detectionRange);
        Gizmos.DrawWireSphere(rightEnd, detectionRange);
    }
}