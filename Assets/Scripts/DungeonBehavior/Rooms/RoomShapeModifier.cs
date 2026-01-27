using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines different shapes that rooms can have
/// </summary>
public enum RoomShape
{
    Rectangle,      // Basic rectangular shape
    LShape,         // L-shaped
    TShape,         // T-shaped
    CrossShape,     // Cross/+ shaped
    WithRecesses    // Rectangular with recesses
}

/// <summary>
/// Configuration of probabilities for room shapes
/// </summary>
[System.Serializable]
public class RoomShapeConfig
{
    [Range(0f, 1f)] public float rectangleChance = 0.4f;
    [Range(0f, 1f)] public float lShapeChance = 0.2f;
    [Range(0f, 1f)] public float tShapeChance = 0.15f;
    [Range(0f, 1f)] public float crossShapeChance = 0.1f;
    [Range(0f, 1f)] public float withRecessesChance = 0.15f;

    [Header("Shape Modifiers")]
    [Tooltip("Minimum cutout size for L/T/Cross shapes (percentage of side)")]
    [Range(0.2f, 0.5f)] public float cutoutMinSize = 0.3f;
    
    [Tooltip("Maximum cutout size for L/T/Cross shapes (percentage of side)")]
    [Range(0.3f, 0.7f)] public float cutoutMaxSize = 0.5f;
    
    [Tooltip("Number of recesses for rooms with recesses")]
    [Range(1, 4)] public int recessCount = 2;
}

/// <summary>
/// Modifies the shape of a rectangular room to create variations
/// </summary>
public static class RoomShapeModifier
{
    /// <summary>
    /// Applies a specific shape to a room, modifying its grid
    /// </summary>
    public static void ApplyShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        // Don't modify very small rooms
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;
        
        if (width < 6 || height < 6)
        {
            return; // Keep rectangular
        }

        // Select shape based on probabilities
        RoomShape selectedShape = SelectRandomShape(config);
        
        // Apply selected shape
        switch (selectedShape)
        {
            case RoomShape.LShape:
                ApplyLShape(room, grid, config);
                break;
            case RoomShape.TShape:
                ApplyTShape(room, grid, config);
                break;
            case RoomShape.CrossShape:
                ApplyCrossShape(room, grid, config);
                break;
            case RoomShape.WithRecesses:
                ApplyRecesses(room, grid, config);
                break;
            case RoomShape.Rectangle:
            default:
                // Already rectangular, do nothing
                break;
        }
    }

    private static RoomShape SelectRandomShape(RoomShapeConfig config)
    {
        float total = config.rectangleChance + config.lShapeChance + config.tShapeChance + 
                      config.crossShapeChance + config.withRecessesChance;
        
        float randomValue = Random.Range(0f, total);
        float cumulative = 0f;
        
        cumulative += config.rectangleChance;
        if (randomValue < cumulative) return RoomShape.Rectangle;
        
        cumulative += config.lShapeChance;
        if (randomValue < cumulative) return RoomShape.LShape;
        
        cumulative += config.tShapeChance;
        if (randomValue < cumulative) return RoomShape.TShape;
        
        cumulative += config.crossShapeChance;
        if (randomValue < cumulative) return RoomShape.CrossShape;
        
        return RoomShape.WithRecesses;
    }

    /// <summary>
    /// Creates an L-shape by cutting a corner
    /// </summary>
    private static void ApplyLShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;
        
        // Select which corner to cut (0-3: BL, BR, TL, TR)
        int cornerToCut = Random.Range(0, 4);
        
        // Cutout size
        int cutWidth = Mathf.RoundToInt(width * Random.Range(config.cutoutMinSize, config.cutoutMaxSize));
        int cutHeight = Mathf.RoundToInt(height * Random.Range(config.cutoutMinSize, config.cutoutMaxSize));
        
        switch (cornerToCut)
        {
            case 0: // Bottom-Left
                RemoveCells(grid, 
                    room.BottomLeftAreaCorner.x, 
                    room.BottomLeftAreaCorner.y,
                    room.BottomLeftAreaCorner.x + cutWidth,
                    room.BottomLeftAreaCorner.y + cutHeight);
                break;
            case 1: // Bottom-Right
                RemoveCells(grid, 
                    room.TopRightAreaCorner.x - cutWidth, 
                    room.BottomLeftAreaCorner.y,
                    room.TopRightAreaCorner.x,
                    room.BottomLeftAreaCorner.y + cutHeight);
                break;
            case 2: // Top-Left
                RemoveCells(grid, 
                    room.BottomLeftAreaCorner.x, 
                    room.TopRightAreaCorner.y - cutHeight,
                    room.BottomLeftAreaCorner.x + cutWidth,
                    room.TopRightAreaCorner.y);
                break;
            case 3: // Top-Right
                RemoveCells(grid, 
                    room.TopRightAreaCorner.x - cutWidth, 
                    room.TopRightAreaCorner.y - cutHeight,
                    room.TopRightAreaCorner.x,
                    room.TopRightAreaCorner.y);
                break;
        }
    }

    /// <summary>
    /// Creates a T-shape by cutting two opposite corners
    /// </summary>
    private static void ApplyTShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;
        
        // Decide if T is horizontal or vertical
        bool horizontal = Random.value > 0.5f;
        
        int cutSize = Mathf.RoundToInt((horizontal ? height : width) * Random.Range(config.cutoutMinSize, config.cutoutMaxSize));
        
        if (horizontal)
        {
            // Horizontal T: cut top-left and top-right corners OR bottom-left and bottom-right
            if (Random.value > 0.5f)
            {
                // Cut top
                RemoveCells(grid, 
                    room.BottomLeftAreaCorner.x, 
                    room.TopRightAreaCorner.y - cutSize,
                    room.BottomLeftAreaCorner.x + width / 3,
                    room.TopRightAreaCorner.y);
                    
                RemoveCells(grid, 
                    room.TopRightAreaCorner.x - width / 3, 
                    room.TopRightAreaCorner.y - cutSize,
                    room.TopRightAreaCorner.x,
                    room.TopRightAreaCorner.y);
            }
            else
            {
                // Cut bottom
                RemoveCells(grid, 
                    room.BottomLeftAreaCorner.x, 
                    room.BottomLeftAreaCorner.y,
                    room.BottomLeftAreaCorner.x + width / 3,
                    room.BottomLeftAreaCorner.y + cutSize);
                    
                RemoveCells(grid, 
                    room.TopRightAreaCorner.x - width / 3, 
                    room.BottomLeftAreaCorner.y,
                    room.TopRightAreaCorner.x,
                    room.BottomLeftAreaCorner.y + cutSize);
            }
        }
        else
        {
            // Vertical T: cut left or right corners
            if (Random.value > 0.5f)
            {
                // Cut left
                RemoveCells(grid, 
                    room.BottomLeftAreaCorner.x, 
                    room.BottomLeftAreaCorner.y,
                    room.BottomLeftAreaCorner.x + cutSize,
                    room.BottomLeftAreaCorner.y + height / 3);
                    
                RemoveCells(grid, 
                    room.BottomLeftAreaCorner.x, 
                    room.TopRightAreaCorner.y - height / 3,
                    room.BottomLeftAreaCorner.x + cutSize,
                    room.TopRightAreaCorner.y);
            }
            else
            {
                // Cut right
                RemoveCells(grid, 
                    room.TopRightAreaCorner.x - cutSize, 
                    room.BottomLeftAreaCorner.y,
                    room.TopRightAreaCorner.x,
                    room.BottomLeftAreaCorner.y + height / 3);
                    
                RemoveCells(grid, 
                    room.TopRightAreaCorner.x - cutSize, 
                    room.TopRightAreaCorner.y - height / 3,
                    room.TopRightAreaCorner.x,
                    room.TopRightAreaCorner.y);
            }
        }
    }

    /// <summary>
    /// Creates a cross shape by cutting all 4 corners
    /// </summary>
    private static void ApplyCrossShape(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;
        
        int cutWidth = Mathf.RoundToInt(width * Random.Range(config.cutoutMinSize * 0.7f, config.cutoutMaxSize * 0.7f));
        int cutHeight = Mathf.RoundToInt(height * Random.Range(config.cutoutMinSize * 0.7f, config.cutoutMaxSize * 0.7f));
        
        // Cut all 4 corners
        // Bottom-Left
        RemoveCells(grid, 
            room.BottomLeftAreaCorner.x, 
            room.BottomLeftAreaCorner.y,
            room.BottomLeftAreaCorner.x + cutWidth,
            room.BottomLeftAreaCorner.y + cutHeight);
            
        // Bottom-Right
        RemoveCells(grid, 
            room.TopRightAreaCorner.x - cutWidth, 
            room.BottomLeftAreaCorner.y,
            room.TopRightAreaCorner.x,
            room.BottomLeftAreaCorner.y + cutHeight);
            
        // Top-Left
        RemoveCells(grid, 
            room.BottomLeftAreaCorner.x, 
            room.TopRightAreaCorner.y - cutHeight,
            room.BottomLeftAreaCorner.x + cutWidth,
            room.TopRightAreaCorner.y);
            
        // Top-Right
        RemoveCells(grid, 
            room.TopRightAreaCorner.x - cutWidth, 
            room.TopRightAreaCorner.y - cutHeight,
            room.TopRightAreaCorner.x,
            room.TopRightAreaCorner.y);
    }

    /// <summary>
    /// Creates random recesses on room edges
    /// </summary>
    private static void ApplyRecesses(RoomNode room, DungeonGrid grid, RoomShapeConfig config)
    {
        int width = room.TopRightAreaCorner.x - room.BottomLeftAreaCorner.x;
        int height = room.TopRightAreaCorner.y - room.BottomLeftAreaCorner.y;
        
        for (int i = 0; i < config.recessCount; i++)
        {
            // Select random side (0: bottom, 1: top, 2: left, 3: right)
            int side = Random.Range(0, 4);
            
            int recessDepth = Random.Range(2, 4);
            int recessLength = Random.Range(3, Mathf.Max(4, (side < 2 ? width : height) / 3));
            
            switch (side)
            {
                case 0: // Bottom
                    {
                        int startX = Random.Range(room.BottomLeftAreaCorner.x + 2, room.TopRightAreaCorner.x - recessLength - 2);
                        RemoveCells(grid, startX, room.BottomLeftAreaCorner.y, startX + recessLength, room.BottomLeftAreaCorner.y + recessDepth);
                    }
                    break;
                case 1: // Top
                    {
                        int startX = Random.Range(room.BottomLeftAreaCorner.x + 2, room.TopRightAreaCorner.x - recessLength - 2);
                        RemoveCells(grid, startX, room.TopRightAreaCorner.y - recessDepth, startX + recessLength, room.TopRightAreaCorner.y);
                    }
                    break;
                case 2: // Left
                    {
                        int startY = Random.Range(room.BottomLeftAreaCorner.y + 2, room.TopRightAreaCorner.y - recessLength - 2);
                        RemoveCells(grid, room.BottomLeftAreaCorner.x, startY, room.BottomLeftAreaCorner.x + recessDepth, startY + recessLength);
                    }
                    break;
                case 3: // Right
                    {
                        int startY = Random.Range(room.BottomLeftAreaCorner.y + 2, room.TopRightAreaCorner.y - recessLength - 2);
                        RemoveCells(grid, room.TopRightAreaCorner.x - recessDepth, startY, room.TopRightAreaCorner.x, startY + recessLength);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Removes cells from grid in specified area (converts Floor to Empty)
    /// </summary>
    private static void RemoveCells(DungeonGrid grid, int x1, int y1, int x2, int y2)
    {
        for (int x = x1; x < x2; x++)
        {
            for (int y = y1; y < y2; y++)
            {
                GridCell cell = grid.GetCell(new Vector2Int(x, y));
                if (cell != null && cell.Type == CellType.Floor)
                {
                    grid.SetCellType(new Vector2Int(x, y), CellType.Empty);
                }
            }
        }
    }
}