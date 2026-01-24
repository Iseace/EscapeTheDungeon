using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class DungeonCreator : MonoBehaviour
{
    [HideInInspector] //Hides the checkbox in the inspector
    public bool useRandomSeed = true;

    [Header("Seed Settings")]
    public int seed = 0;

    [SerializeField] private int lastUsedSeed; // Stores the seed used for current dungeon

    [Header("Dungeon Settings")]
    public int dungeonWidth;
    public int dungeonLength;
    public int roomWidthMin;
    public int roomLengthMin;
    public int wallHeight = 3;
    public int corridorWidth;
    public int maxIterations; // How many times to try dividing space
    [Range(0.0f, 0.3f)] //(Just in the Inspector) Adjusts how close to the corner the room's bottom left corner can be
    public float roomBottomCornerModifier = 0.1f;
    [Range(0.7f, 1.0f)] //(Just in the Inspector) Adjusts how close to the corner the room's top right corner can be
    public float roomTopCornerMidifier = 0.9f;
    [Range(0, 2)] //(Just in the Inspector) Adjusts how far from the dividing walls the rooms can be placed
    public int roomOffset = 1;

    [Header("Textures")]
    public Material material; // Material for floor
    public GameObject wallPrefab; // Prefab used to create walls

    List<Vector3Int> possibleDoorVerticalPosition;
    List<Vector3Int> possibleDoorHorizontalPosition;
    List<Vector3Int> possibleWallHorizontalPosition;
    List<Vector3Int> possibleWallVerticalPosition;

    void Start()
    {
        CreateDungeon();
    }

    public void CreateDungeon()
    {
        if (useRandomSeed)
        {
            seed = Random.Range(0, int.MaxValue);
        }

        lastUsedSeed = seed;
        Random.InitState(seed);

        // Clean up old dungeon
        DestroyAllChildren();
        DugeonGenerator generator = new DugeonGenerator(dungeonWidth, dungeonLength);
        var listOfRooms = generator.CalculateDungeon(
            maxIterations,
            roomWidthMin,
            roomLengthMin,
            roomBottomCornerModifier,
            roomTopCornerMidifier,
            roomOffset,
            corridorWidth
        );
        GameObject wallParent = new GameObject("WallParent");
        wallParent.transform.parent = transform;
        possibleDoorVerticalPosition = new List<Vector3Int>();
        possibleDoorHorizontalPosition = new List<Vector3Int>();
        possibleWallHorizontalPosition = new List<Vector3Int>();
        possibleWallVerticalPosition = new List<Vector3Int>();


        for (int i = 0; i < listOfRooms.Count; i++) // Create floor mesh for each room
        {
            CreateMesh(listOfRooms[i].BottomLeftAreaCorner, listOfRooms[i].TopRightAreaCorner);
        }
        CreateWalls(wallParent);
    }

    public void CreateDungeonRandom()
    {
        useRandomSeed = true;
        CreateDungeon();
    }

    public void CreateDungeonWithSeed(int specificSeed)
    {
        useRandomSeed = false;
        seed = specificSeed;
        CreateDungeon();
    }

    private void CreateWalls(GameObject wallParent)
    {
        foreach (var wallPosition in possibleWallHorizontalPosition)
        {
            if (!possibleDoorHorizontalPosition.Contains(wallPosition))
            {
                CreateWall(wallParent, wallPosition, 0f);
            }
        }

        foreach (var wallPosition in possibleWallVerticalPosition)
        {
            if (!possibleDoorVerticalPosition.Contains(wallPosition))
            {
                CreateWall(wallParent, wallPosition, 0f);
            }
        }
    }


    private void CreateWall(GameObject wallParent, Vector3Int wallPosition, float yRotation)
    {
        Quaternion rotation = Quaternion.Euler(0, yRotation, 0);
        GameObject wall = Instantiate(wallPrefab, wallPosition, rotation, wallParent.transform);

        Vector3 scale = wall.transform.localScale;
        scale.y = wallHeight;
        wall.transform.localScale = scale;
    }

    private void CreateMesh(Vector2 bottomLeftCorner, Vector2 topRightCorner)
    {
        Vector3 bottomLeftV = new Vector3(bottomLeftCorner.x, 0, bottomLeftCorner.y);
        Vector3 bottomRightV = new Vector3(topRightCorner.x, 0, bottomLeftCorner.y);
        Vector3 topLeftV = new Vector3(bottomLeftCorner.x, 0, topRightCorner.y);
        Vector3 topRightV = new Vector3(topRightCorner.x, 0, topRightCorner.y);

        Vector3[] vertices = new Vector3[]  // Create vertices array for mesh
        {
            topLeftV,
            topRightV,
            bottomLeftV,
            bottomRightV
        };

        Vector2[] uvs = new Vector2[vertices.Length]; // Create UV coordinates for texture mapping
        for (int i = 0; i < uvs.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        int[] triangles = new int[] // Define triangles
        {
            0,
            1,
            2,
            2,
            1,
            3
        };

        Mesh mesh = new Mesh(); // Build the mesh
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        GameObject dungeonFloor = new GameObject("Mesh" + bottomLeftCorner, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));

        dungeonFloor.transform.position = Vector3.zero;
        dungeonFloor.transform.localScale = Vector3.one;
        dungeonFloor.GetComponent<MeshFilter>().mesh = mesh;
        dungeonFloor.GetComponent<MeshRenderer>().material = material;

        MeshCollider meshCollider = dungeonFloor.GetComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        dungeonFloor.transform.parent = transform;

        for (int row = (int)bottomLeftV.x; row <= (int)bottomRightV.x; row++)
        {
            var wallPosition = new Vector3(row, 0, bottomLeftV.z);
            AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int row = (int)topLeftV.x; row <= (int)topRightCorner.x; row++)
        {
            var wallPosition = new Vector3(row, 0, topRightV.z);
            AddWallPositionToList(wallPosition, possibleWallHorizontalPosition, possibleDoorHorizontalPosition);
        }
        for (int col = (int)bottomLeftV.z; col <= (int)topLeftV.z; col++)
        {
            var wallPosition = new Vector3(bottomLeftV.x, 0, col);
            AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
        for (int col = (int)bottomRightV.z; col <= (int)topRightV.z; col++)
        {
            var wallPosition = new Vector3(bottomRightV.x, 0, col);
            AddWallPositionToList(wallPosition, possibleWallVerticalPosition, possibleDoorVerticalPosition);
        }
    }

    private void AddWallPositionToList(Vector3 wallPosition, List<Vector3Int> wallList, List<Vector3Int> doorList)
    {
        Vector3Int point = Vector3Int.CeilToInt(wallPosition);
        if (wallList.Contains(point)) //Mark possible door position
        {
            doorList.Add(point);
            wallList.Remove(point);
        }
        else
        {
            wallList.Add(point);
        }
    }

    public void DestroyAllChildren() // Remove all child objects
    {
        while (transform.childCount != 0)
        {
            foreach (Transform item in transform)
            {
                DestroyImmediate(item.gameObject);
            }
        }
    }
}