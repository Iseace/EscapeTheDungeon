using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public enum RoomType
{
    Start,
    Boss,
    Normal,
    Shop,
    Trap,
    Puzzle
}

[Serializable]
public class RoomTypeData
{
    public RoomType roomType;
    public string roomName;
    public GameObject roomPrefab; // Optional Prefab for the rooms
    public Color debugColor = Color.white;
    public List<GameObject> possibleObjectPrefabs = new List<GameObject>(); // List of spawnable items
    public int minObjects = 0;
    public int maxObjects = 5;
    public bool isUnique = false; // Only one of this type of room

    [Header("Spawn Weight")]
    [Range(0f, 100f)]
    public float spawnWeight = 10f; // Probabilities of the room spawning
}

[CreateAssetMenu(fileName = "RoomTypeConfig", menuName = "Room Type Configuration")]
public class RoomTypeConfiguration : ScriptableObject
{
    public List<RoomTypeData> roomTypes = new List<RoomTypeData>();

    public RoomTypeData GetRoomTypeData(RoomType type)
    {
        return roomTypes.Find(r => r.roomType == type);
    }
}