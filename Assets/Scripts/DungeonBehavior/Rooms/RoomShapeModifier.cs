using System.Collections.Generic;
using UnityEngine;

public enum RoomShape
{
    Rectangular,
    LShape,
    TShape,
    UShape,
    CrossShape,
    Circular,
    WithRecesses
}

/// <summary>
/// Configuration of probabilities for room shapes
/// </summary>
[System.Serializable]
public class RoomShapeConfig
{
    [Header("Shape Probabilities")]
    [Range(0f, 1f)] public float rectangleChance = 0.25f;
    [Range(0f, 1f)] public float lShapeChance = 0.2f;
    [Range(0f, 1f)] public float tShapeChance = 0.15f;
    [Range(0f, 1f)] public float uShapeChance = 0.1f;
    [Range(0f, 1f)] public float crossShapeChance = 0.1f;
    [Range(0f, 1f)] public float circularChance = 0.05f;
    [Range(0f, 1f)] public float withRecessesChance = 0.15f;

    [Header("Shape Modifiers")]
    [Tooltip("Minimum cutout size for L/T/Cross shapes (percentage of side)")]
    [Range(0.2f, 0.5f)] public float cutoutMinSize = 0.3f;
    
    [Tooltip("Maximum cutout size for L/T/Cross shapes (percentage of side)")]
    [Range(0.3f, 0.7f)] public float cutoutMaxSize = 0.5f;
    
    [Tooltip("Number of recesses for rooms with recesses")]
    [Range(1, 6)] public int recessCount = 2;

    [Header("Advanced Settings")]
    [Tooltip("Minimum room size to apply non-rectangular shapes")]
    [Range(4, 10)] public int minRoomSizeForShapes = 6;
}

/// <summary>
/// Optimized room shape modifier using modular shape system
/// </summary>
public static class RoomShapeModifier
{
    // Cache shape instances for better performance
    private static Dictionary<RoomShape, BaseRoomShape> shapeInstances;

    private static void InitializeShapes()
    {
        if (shapeInstances != null)
            return;

        shapeInstances = new Dictionary<RoomShape, BaseRoomShape>
        {
            { RoomShape.Rectangular, new RectangularRoomShape() },
            { RoomShape.LShape, new LShapedRoom() },
            { RoomShape.TShape, new TShapedRoom() },
            { RoomShape.UShape, new UShapedRoom() },
            { RoomShape.CrossShape, new CrossShapedRoom() },
            { RoomShape.Circular, new CircularRoomShape() },
            { RoomShape.WithRecesses, new RecessesRoomShape() }
        };
    }

    public static void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        InitializeShapes();

        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;

        // Check minimum size
        if (width < config.minRoomSizeForShapes || height < config.minRoomSizeForShapes)
        {
            // Room too small, keep rectangular
            return;
        }

        // Select shape based on probabilities and compatibility
        RoomShape selectedShape = SelectCompatibleShape(width, height, config);

        // Apply the selected shape
        if (shapeInstances.TryGetValue(selectedShape, out BaseRoomShape shapeModifier))
        {
            shapeModifier.ApplyShape(room, grid, config);
        }
    }

    private static RoomShape SelectCompatibleShape(int width, int height, RoomShapeConfig config)
    {
        // Build weighted list of compatible shapes
        List<(RoomShape shape, float weight)> compatibleShapes = new List<(RoomShape, float)>();

        // Check each shape for compatibility
        AddIfCompatible(compatibleShapes, RoomShape.Rectangular, config.rectangleChance, width, height);
        AddIfCompatible(compatibleShapes, RoomShape.LShape, config.lShapeChance, width, height);
        AddIfCompatible(compatibleShapes, RoomShape.TShape, config.tShapeChance, width, height);
        AddIfCompatible(compatibleShapes, RoomShape.UShape, config.uShapeChance, width, height);
        AddIfCompatible(compatibleShapes, RoomShape.CrossShape, config.crossShapeChance, width, height);
        AddIfCompatible(compatibleShapes, RoomShape.Circular, config.circularChance, width, height);
        AddIfCompatible(compatibleShapes, RoomShape.WithRecesses, config.withRecessesChance, width, height);

        // If no compatible shapes, default to rectangular
        if (compatibleShapes.Count == 0)
        {
            return RoomShape.Rectangular;
        }

        // Select weighted random shape
        return SelectWeightedRandom(compatibleShapes);
    }

    private static void AddIfCompatible(
        List<(RoomShape shape, float weight)> list,
        RoomShape shape,
        float weight,
        int width,
        int height)
    {
        if (weight <= 0)
            return;

        if (shapeInstances.TryGetValue(shape, out BaseRoomShape shapeModifier))
        {
            if (shapeModifier.CanApply(width, height))
            {
                list.Add((shape, weight));
            }
        }
    }

    private static RoomShape SelectWeightedRandom(List<(RoomShape shape, float weight)> shapes)
    {
        // Calculate total weight
        float totalWeight = 0f;
        foreach (var (_, weight) in shapes)
        {
            totalWeight += weight;
        }

        // Select random value
        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        // Find selected shape
        foreach (var (shape, weight) in shapes)
        {
            cumulative += weight;
            if (randomValue <= cumulative)
            {
                return shape;
            }
        }

        // Fallback to last shape (shouldn't happen)
        return shapes[shapes.Count - 1].shape;
    }

    /// <summary>
    /// Gets the name of a room shape for debugging
    /// </summary>
    public static string GetShapeName(RoomShape shape)
    {
        return shape.ToString();
    }
}