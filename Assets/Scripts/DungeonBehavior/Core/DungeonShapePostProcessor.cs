using System.Collections.Generic;
using UnityEngine;

public class DungeonShapePostProcessConfig
{
    public int MaxInset = 2; // Profundidad máxima de recorte desde cada borde
    public float ErodeChance = 0.12f; // Probabilidad de eliminar una celda interna
    public int MaxAttempts = 3; // Reintentos si la forma queda desconectada
    public int MinEntrancePadding = 1; // Tiles protegidos alrededor de la entrada a corredor
    public int RectCuts = 3; // Cantidad de cortes rectangulares al azar
    public Vector2Int RectSizeMin = new Vector2Int(2, 2);
    public Vector2Int RectSizeMax = new Vector2Int(5, 6);
    public int MinGapBetweenCuts = 1; // Separación mínima entre cortes para evitar bolsillos
    public int MinWallThickness = 1; // Espesor mínimo que debe quedar tras un corte
    public int MinEdgeBuffer = 0; // Distancia mínima desde el borde para iniciar cortes
}

/// <summary>
/// Ajusta la forma de las salas tras la generación inicial manteniendo accesos y conectividad.
/// Aplica recortes ligeros (jitter/erosión) y valida que no se pierdan accesos desde corredores.
/// </summary>
public class DungeonShapePostProcessor
{
    private readonly DungeonShapePostProcessConfig config;
    private static readonly Vector2Int[] Neigh =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    public DungeonShapePostProcessor(DungeonShapePostProcessConfig customConfig = null)
    {
        config = customConfig ?? new DungeonShapePostProcessConfig();
    }

    public void Process(DungeonGrid grid, List<RoomNode> rooms, int corridorWidth)
    {
        if (grid == null || rooms == null) return;

        foreach (var room in rooms)
        {
            TryProcessRoom(grid, room, corridorWidth);
        }
    }

    private void TryProcessRoom(DungeonGrid grid, RoomNode room, int corridorWidth)
    {
        // Snapshot original state to restore if connectivity fails
        var original = SnapshotRoom(grid, room);

        for (int attempt = 0; attempt < config.MaxAttempts; attempt++)
        {
            RestoreRoom(grid, room, original);

            HashSet<Vector2Int> protectedCells = FindProtectedCells(grid, room, corridorWidth);

            ApplyRectCuts(grid, room, protectedCells);

            RemoveThinStrips(grid, room, protectedCells);

            FillInternalVoids(grid, room);

            if (ValidateConnectivity(grid, room, protectedCells))
            {
                return; // success
            }
        }

        // If all attempts fail, restore original shape
        RestoreRoom(grid, room, original);
    }

    private Dictionary<Vector2Int, CellType> SnapshotRoom(DungeonGrid grid, RoomNode room)
    {
        var snap = new Dictionary<Vector2Int, CellType>();
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                var cell = grid.GetCell(pos);
                if (cell != null)
                {
                    snap[pos] = cell.Type;
                }
            }
        }
        return snap;
    }

    private void RestoreRoom(DungeonGrid grid, RoomNode room, Dictionary<Vector2Int, CellType> snap)
    {
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                if (snap.TryGetValue(pos, out var type))
                {
                    grid.SetCellType(pos, type, room);
                }
            }
        }
    }

    private HashSet<Vector2Int> FindProtectedCells(DungeonGrid grid, RoomNode room, int corridorWidth)
    {
        HashSet<Vector2Int> protectedCells = new HashSet<Vector2Int>();
        int padding = Mathf.Max(1, config.MinEntrancePadding);
        int halfCorridor = Mathf.Max(1, corridorWidth / 2);

        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                var cell = grid.GetCell(pos);
                if (cell == null || cell.ParentRoom != room) continue;

                bool touchesCorridor = false;
                foreach (var dir in Neigh)
                {
                    var neighbor = grid.GetCell(pos + dir);
                    if (neighbor != null && neighbor.Type == CellType.Corridor)
                    {
                        touchesCorridor = true;
                        break;
                    }
                }

                if (touchesCorridor)
                {
                    // Protege el acceso y una franja según el ancho de corredor y padding
                    for (int dx = -halfCorridor; dx <= halfCorridor; dx++)
                    {
                        for (int dy = -padding; dy <= padding; dy++)
                        {
                            var p = new Vector2Int(pos.x + dx, pos.y + dy);
                            if (IsInsideRoom(p, room))
                                protectedCells.Add(p);
                        }
                    }
                }
            }
        }

        return protectedCells;
    }

    private void ApplyEdgeInset(DungeonGrid grid, RoomNode room, HashSet<Vector2Int> protectedCells)
    {
        int maxInset = Mathf.Max(0, config.MaxInset);
        if (maxInset == 0) return;

        // Bottom to top rows
        for (int y = 0; y < room.Length; y++)
        {
            int inset = Random.Range(0, maxInset + 1);
            for (int x = 0; x < inset; x++)
            {
                Vector2Int pos = new Vector2Int(room.BottomLeftAreaCorner.x + x, room.BottomLeftAreaCorner.y + y);
                TryCarve(grid, room, pos, protectedCells);
            }

            inset = Random.Range(0, maxInset + 1);
            for (int x = 0; x < inset; x++)
            {
                Vector2Int pos = new Vector2Int(room.TopRightAreaCorner.x - 1 - x, room.BottomLeftAreaCorner.y + y);
                TryCarve(grid, room, pos, protectedCells);
            }
        }

        // Left to right columns
        for (int x = 0; x < room.Width; x++)
        {
            int inset = Random.Range(0, maxInset + 1);
            for (int y = 0; y < inset; y++)
            {
                Vector2Int pos = new Vector2Int(room.BottomLeftAreaCorner.x + x, room.BottomLeftAreaCorner.y + y);
                TryCarve(grid, room, pos, protectedCells);
            }

            inset = Random.Range(0, maxInset + 1);
            for (int y = 0; y < inset; y++)
            {
                Vector2Int pos = new Vector2Int(room.BottomLeftAreaCorner.x + x, room.TopRightAreaCorner.y - 1 - y);
                TryCarve(grid, room, pos, protectedCells);
            }
        }
    }

    private void ApplyErosion(DungeonGrid grid, RoomNode room, HashSet<Vector2Int> protectedCells)
    {
        float chance = Mathf.Clamp01(config.ErodeChance);
        if (chance <= 0f) return;

        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                if (protectedCells.Contains(pos)) continue;

                // Evita erosionar demasiado el borde inmediato para no abrir paredes grandes
                bool isEdge = x == room.BottomLeftAreaCorner.x || x == room.TopRightAreaCorner.x - 1 ||
                              y == room.BottomLeftAreaCorner.y || y == room.TopRightAreaCorner.y - 1;
                if (isEdge && Random.value > chance * 0.5f) continue;

                if (Random.value < chance)
                {
                    TryCarve(grid, room, pos, protectedCells);
                }
            }
        }
    }

    private void TryCarve(DungeonGrid grid, RoomNode room, Vector2Int pos, HashSet<Vector2Int> protectedCells)
    {
        if (!IsInsideRoom(pos, room)) return;
        if (protectedCells.Contains(pos)) return;

        var cell = grid.GetCell(pos);
        if (cell == null || cell.ParentRoom != room) return;

        grid.SetCellType(pos, CellType.Empty, null);
    }

    private void RemoveThinStrips(DungeonGrid grid, RoomNode room, HashSet<Vector2Int> protectedCells)
    {
        // Elimina tiras de 1 tile de grosor (picos) tras los cortes, respetando celdas protegidas.
        List<Vector2Int> toClear = new List<Vector2Int>();

        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                if (protectedCells.Contains(pos)) continue;
                var cell = grid.GetCell(pos);
                if (cell == null || cell.Type != CellType.Floor) continue;

                int neighbors = 0;
                foreach (var d in Neigh)
                {
                    var n = grid.GetCell(pos + d);
                    if (n != null && n.Type == CellType.Floor)
                        neighbors++;
                }

                // Celdas con 0-1 vecinos son puntas; con 2 en linea recta tambien son tiras.
                if (neighbors <= 1)
                {
                    toClear.Add(pos);
                }
                else if (neighbors == 2)
                {
                    bool horiz = IsFloor(grid, pos + Vector2Int.left) && IsFloor(grid, pos + Vector2Int.right);
                    bool vert = IsFloor(grid, pos + Vector2Int.up) && IsFloor(grid, pos + Vector2Int.down);
                    if (horiz || vert)
                    {
                        toClear.Add(pos);
                    }
                }
            }
        }

        foreach (var p in toClear)
        {
            grid.SetCellType(p, CellType.Empty, null);
        }
    }

    private void ApplyRectCuts(DungeonGrid grid, RoomNode room, HashSet<Vector2Int> protectedCells)
    {
        int cuts = Mathf.Clamp(config.RectCuts, 0, 3);
        if (cuts == 0) return;

        int attemptsPerCut = 25;
        int gap = Mathf.Max(0, config.MinGapBetweenCuts);
        int minThickness = Mathf.Max(1, config.MinWallThickness);
        int edgeBuffer = Mathf.Max(0, config.MinEdgeBuffer);
        List<RectInt> placed = new List<RectInt>();

        for (int i = 0; i < cuts; i++)
        {
            bool carved = false;
            for (int attempt = 0; attempt < attemptsPerCut && !carved; attempt++)
            {
                int w = Random.Range(config.RectSizeMin.x, config.RectSizeMax.x + 1);
                int h = Random.Range(config.RectSizeMin.y, config.RectSizeMax.y + 1);

                // Prefer pegar el corte a un borde para "morder" la sala.
                int choice = Random.Range(0, 4);
                int xStart = room.BottomLeftAreaCorner.x;
                int yStart = room.BottomLeftAreaCorner.y;

                switch (choice)
                {
                    case 0: // borde izquierdo
                        xStart = room.BottomLeftAreaCorner.x + edgeBuffer;
                        yStart = Random.Range(room.BottomLeftAreaCorner.y, room.TopRightAreaCorner.y - h);
                        break;
                    case 1: // borde derecho
                        xStart = room.TopRightAreaCorner.x - w - edgeBuffer;
                        yStart = Random.Range(room.BottomLeftAreaCorner.y, room.TopRightAreaCorner.y - h);
                        break;
                    case 2: // borde inferior
                        xStart = Random.Range(room.BottomLeftAreaCorner.x + edgeBuffer, room.TopRightAreaCorner.x - w - edgeBuffer);
                        yStart = room.BottomLeftAreaCorner.y + edgeBuffer;
                        break;
                    default: // borde superior
                        xStart = Random.Range(room.BottomLeftAreaCorner.x + edgeBuffer, room.TopRightAreaCorner.x - w - edgeBuffer);
                        yStart = room.TopRightAreaCorner.y - h - edgeBuffer;
                        break;
                }

                // Si los rangos de random son inválidos, descartar
                if (choice == 2 || choice == 3)
                {
                    if (room.BottomLeftAreaCorner.x + edgeBuffer > room.TopRightAreaCorner.x - w - edgeBuffer) continue;
                }
                if (choice == 0 || choice == 1)
                {
                    if (room.BottomLeftAreaCorner.y > room.TopRightAreaCorner.y - h) continue;
                }

                // Asegura que cabe
                if (xStart < room.BottomLeftAreaCorner.x || yStart < room.BottomLeftAreaCorner.y) continue;
                if (xStart + w > room.TopRightAreaCorner.x || yStart + h > room.TopRightAreaCorner.y) continue;

                // Evita que un corte deje la sala sin espesor mínimo
                int remainingWLeft = (xStart - room.BottomLeftAreaCorner.x);
                int remainingWRight = (room.TopRightAreaCorner.x - (xStart + w));
                int remainingHBottom = (yStart - room.BottomLeftAreaCorner.y);
                int remainingHTop = (room.TopRightAreaCorner.y - (yStart + h));
                if (remainingWLeft < minThickness && remainingWRight < minThickness) continue;
                if (remainingHBottom < minThickness && remainingHTop < minThickness) continue;

                // Evita cortes demasiado cercanos entre sí (gap)
                RectInt candidate = new RectInt(xStart, yStart, w, h);
                bool tooClose = false;
                foreach (var r in placed)
                {
                    // Expande el existente por gap y verifica intersección
                    RectInt expanded = new RectInt(r.xMin - gap, r.yMin - gap, r.width + gap * 2, r.height + gap * 2);
                    if (expanded.Overlaps(candidate))
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                bool touchesProtected = false;
                for (int x = xStart; x < xStart + w && !touchesProtected; x++)
                {
                    for (int y = yStart; y < yStart + h; y++)
                    {
                        if (protectedCells.Contains(new Vector2Int(x, y)))
                        {
                            touchesProtected = true;
                            break;
                        }
                    }
                }
                if (touchesProtected) continue;

                // Carve rectangle
                for (int x = xStart; x < xStart + w; x++)
                {
                    for (int y = yStart; y < yStart + h; y++)
                    {
                        grid.SetCellType(new Vector2Int(x, y), CellType.Empty, null);
                    }
                }

                placed.Add(candidate);
                carved = true;
            }
        }
    }

    private bool ValidateConnectivity(DungeonGrid grid, RoomNode room, HashSet<Vector2Int> protectedCells)
    {
        // Pick a seed: prefer a protected cell, else any floor in room
        Vector2Int? seed = null;
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x && seed == null; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                var cell = grid.GetCell(pos);
                if (cell != null && cell.Type == CellType.Floor)
                {
                    seed = pos;
                    if (protectedCells.Contains(pos)) break;
                }
            }
        }

        if (seed == null) return false;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(seed.Value);
        visited.Add(seed.Value);

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in Neigh)
            {
                var np = p + d;
                if (!IsInsideRoom(np, room)) continue;
                if (visited.Contains(np)) continue;
                var cell = grid.GetCell(np);
                if (cell != null && cell.Type == CellType.Floor)
                {
                    visited.Add(np);
                    q.Enqueue(np);
                }
            }
        }

        // Ensure all floor tiles in the room remain reachable
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                var cell = grid.GetCell(pos);
                if (cell != null && cell.Type == CellType.Floor && !visited.Contains(pos))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsFloor(DungeonGrid grid, Vector2Int pos)
    {
        var c = grid.GetCell(pos);
        return c != null && c.Type == CellType.Floor;
    }

    private void FillInternalVoids(DungeonGrid grid, RoomNode room)
    {
        // Marca espacios vacíos conectados al exterior de la sala y rellena huecos encerrados.
        int minX = room.BottomLeftAreaCorner.x - 1;
        int maxX = room.TopRightAreaCorner.x;
        int minY = room.BottomLeftAreaCorner.y - 1;
        int maxY = room.TopRightAreaCorner.y;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> q = new Queue<Vector2Int>();

        bool InBounds(Vector2Int p) => p.x >= minX && p.x <= maxX && p.y >= minY && p.y <= maxY;

        void EnqueueIfOutside(Vector2Int p)
        {
            if (!InBounds(p)) return;
            if (visited.Contains(p)) return;
            var cell = grid.GetCell(p);
            // Consideramos "aire" todo lo que no sea Floor; atravesamos corredores y vacío exterior.
            if (cell == null || cell.Type != CellType.Floor)
            {
                visited.Add(p);
                q.Enqueue(p);
            }
        }

        // Semillas: perímetro del bounding box ampliado
        for (int x = minX; x <= maxX; x++)
        {
            EnqueueIfOutside(new Vector2Int(x, minY));
            EnqueueIfOutside(new Vector2Int(x, maxY));
        }
        for (int y = minY; y <= maxY; y++)
        {
            EnqueueIfOutside(new Vector2Int(minX, y));
            EnqueueIfOutside(new Vector2Int(maxX, y));
        }

        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in Neigh)
            {
                var np = p + d;
                if (!InBounds(np)) continue;
                if (visited.Contains(np)) continue;
                var cell = grid.GetCell(np);
                if (cell == null || cell.Type != CellType.Floor)
                {
                    visited.Add(np);
                    q.Enqueue(np);
                }
            }
        }

        // Rellena huecos internos: celdas no Floor dentro de la sala que no fueron alcanzadas.
        for (int x = room.BottomLeftAreaCorner.x; x < room.TopRightAreaCorner.x; x++)
        {
            for (int y = room.BottomLeftAreaCorner.y; y < room.TopRightAreaCorner.y; y++)
            {
                var pos = new Vector2Int(x, y);
                if (visited.Contains(pos)) continue;
                var cell = grid.GetCell(pos);
                if (cell == null || cell.Type != CellType.Floor)
                {
                    grid.SetCellType(pos, CellType.Floor, room);
                }
            }
        }
    }

    private bool IsInsideRoom(Vector2Int pos, RoomNode room)
    {
        return pos.x >= room.BottomLeftAreaCorner.x && pos.x < room.TopRightAreaCorner.x &&
               pos.y >= room.BottomLeftAreaCorner.y && pos.y < room.TopRightAreaCorner.y;
    }
}
