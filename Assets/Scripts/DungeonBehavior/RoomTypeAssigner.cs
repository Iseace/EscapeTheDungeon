using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RoomTypeAssigner
{
    private RoomTypeConfiguration config;
    private HashSet<RoomType> usedUniqueTypes;

    public RoomTypeAssigner(RoomTypeConfiguration configuration)
    {
        config = configuration;
        usedUniqueTypes = new HashSet<RoomType>();
    }

    public void AssignRoomTypes(List<RoomNode> rooms)
    {
        if (rooms.Count == 0 || config == null) return;

        usedUniqueTypes.Clear();

        // 1. Asignar habitación de inicio (primera habitación)
        AssignSpecificRoomType(rooms[0], RoomType.Start);

        // 2. Asignar habitación del boss (última o más lejana)
        RoomNode bossRoom = GetFurthestRoom(rooms, rooms[0]);
        AssignSpecificRoomType(bossRoom, RoomType.Boss);

        // 3. Asignar tipos especiales a otras habitaciones
        List<RoomNode> remainingRooms = rooms.Where(r =>
            r.RoomType != RoomType.Start && r.RoomType != RoomType.Boss).ToList();

        foreach (var room in remainingRooms)
        {
            AssignRandomRoomType(room);
        }
    }

    private void AssignSpecificRoomType(RoomNode room, RoomType type)
    {
        RoomTypeData data = config.GetRoomTypeData(type);
        room.SetRoomType(type, data);

        if (data != null && data.isUnique)
        {
            usedUniqueTypes.Add(type);
        }
    }

    private void AssignRandomRoomType(RoomNode room)
    {
        // Filtrar tipos disponibles
        List<RoomTypeData> availableTypes = config.roomTypes
            .Where(rt => !rt.isUnique || !usedUniqueTypes.Contains(rt.roomType))
            .Where(rt => rt.roomType != RoomType.Start && rt.roomType != RoomType.Boss)
            .ToList();

        if (availableTypes.Count == 0)
        {
            room.SetRoomType(RoomType.Normal);
            return;
        }

        // Selección ponderada basada en spawnWeight
        float totalWeight = availableTypes.Sum(rt => rt.spawnWeight);
        float randomValue = Random.Range(0f, totalWeight);
        float currentWeight = 0f;

        foreach (var typeData in availableTypes)
        {
            currentWeight += typeData.spawnWeight;
            if (randomValue <= currentWeight)
            {
                room.SetRoomType(typeData.roomType, typeData);

                if (typeData.isUnique)
                {
                    usedUniqueTypes.Add(typeData.roomType);
                }
                return;
            }
        }

        // Fallback
        room.SetRoomType(RoomType.Normal);
    }

    private RoomNode GetFurthestRoom(List<RoomNode> rooms, RoomNode fromRoom)
    {
        RoomNode furthest = rooms[0];
        float maxDistance = 0f;

        Vector2Int startPos = fromRoom.GetCenterPosition();

        foreach (var room in rooms)
        {
            if (room == fromRoom) continue;

            Vector2Int roomPos = room.GetCenterPosition();
            float distance = Vector2Int.Distance(startPos, roomPos);

            if (distance > maxDistance)
            {
                maxDistance = distance;
                furthest = room;
            }
        }

        return furthest;
    }

    public RoomNode GetRoomByType(List<RoomNode> rooms, RoomType type)
    {
        return rooms.FirstOrDefault(r => r.RoomType == type);
    }

    public List<RoomNode> GetRoomsByType(List<RoomNode> rooms, RoomType type)
    {
        return rooms.Where(r => r.RoomType == type).ToList();
    }
}