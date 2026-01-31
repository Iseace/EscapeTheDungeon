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
    private RectangularDoor attachedLeftDoor;
    private RectangularDoor attachedRightDoor;
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
        if ((leftDoorMoved || rightDoorMoved) && autoDimensionFromDoors && snapLeftEndToDoor && snapRightEndToDoor)
        {
            RecalculateDimensionsFromDoors();
        }
        else if (leftDoorMoved && snapLeftEndToDoor)
        {
            SnapToLeftDoor();
        }
        else if (rightDoorMoved && snapRightEndToDoor)
        {
            SnapToRightDoor();
        }
    }

    private void DetectAndAttachToDoors()
    {
        // Find all doors in the scene
        RectangularDoor[] doors = FindObjectsOfType<RectangularDoor>();

        if (doors.Length == 0)
        {
            attachedLeftDoor = null;
            attachedRightDoor = null;
            snapLeftEndToDoor = false;
            snapRightEndToDoor = false;
            return;
        }

        // Calculate current wall connection points in world space (at bottom)
        Vector3 leftConnWorld = GetLeftConnectionPointWorld();
        Vector3 rightConnWorld = GetRightConnectionPointWorld();

        // Store previous attachments
        RectangularDoor prevLeftDoor = attachedLeftDoor;
        RectangularDoor prevRightDoor = attachedRightDoor;

        // Reset attachment flags
        snapLeftEndToDoor = false;
        snapRightEndToDoor = false;
        attachedLeftDoor = null;
        attachedRightDoor = null;

        // Check left connection point for door attachment
        float closestLeftDist = doorDetectionRange;
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
                        snapLeftEndToDoor = true;
                        attachedLeftDoor = door;
                        leftAttachedToLeftSide = isLeftSide;
                    }
                }
            }
        }

        // Check right connection point for door attachment
        float closestRightDist = doorDetectionRange;
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
                        snapRightEndToDoor = true;
                        attachedRightDoor = door;
                        rightAttachedToLeftSide = isLeftSide;
                    }
                }
            }
        }

        // If attachment changed, mark for regeneration
        if ((prevLeftDoor != attachedLeftDoor) || (prevRightDoor != attachedRightDoor))
        {
            needsRegeneration = true;
        }

        // FIXED: Snap logic now works correctly
        // If both ends attached and auto dimension is enabled, recalculate dimensions
        if (snapLeftEndToDoor && snapRightEndToDoor && autoDimensionFromDoors)
        {
            RecalculateDimensionsFromDoors();
        }
        // Otherwise snap individual ends
        else
        {
            if (snapLeftEndToDoor)
                SnapToLeftDoor();

            if (snapRightEndToDoor)
                SnapToRightDoor();
        }
    }

    private void RecalculateDimensionsFromDoors()
    {
        if (attachedLeftDoor == null || attachedRightDoor == null)
            return;

        Vector3 leftPoint = leftAttachedToLeftSide ?
            attachedLeftDoor.GetLeftAttachmentPointWorld() :
            attachedLeftDoor.GetRightAttachmentPointWorld();

        Vector3 rightPoint = rightAttachedToLeftSide ?
            attachedRightDoor.GetLeftAttachmentPointWorld() :
            attachedRightDoor.GetRightAttachmentPointWorld();

        // Calculate new length
        Vector3 diff = rightPoint - leftPoint;
        float newLength = diff.magnitude;

        // Calculate new position (midpoint)
        Vector3 midpoint = (leftPoint + rightPoint) / 2f;

        // Calculate rotation to align with door connection
        Vector3 direction = (rightPoint - leftPoint).normalized;
        Quaternion newRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
        if (direction.magnitude > 0.001f)
        {
            newRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up) *
                          Quaternion.FromToRotation(Vector3.right, direction);
        }

        // Apply new transforms and dimensions
        if (newLength > 0.1f)
        {
            length = newLength;
            transform.position = midpoint;
            transform.rotation = newRotation;
            needsRegeneration = true;
        }
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
            mesh.name = "Rectangular Wall";
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
        if (!enableDoorAttachment && !showConnectionPoints) return;

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