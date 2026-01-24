using UnityEngine;

public class RoomNode : Node
{
    public int Width { get => (int)(TopRightAreaCorner.x - BottomLeftAreaCorner.x); }
    public int Length { get => (int)(TopRightAreaCorner.y - BottomLeftAreaCorner.y); }

    // Nuevas propiedades para el sistema de tipos
    public RoomType RoomType { get; set; }
    public RoomTypeData RoomTypeData { get; set; }
    public string RoomID { get; private set; }

    public RoomNode(Vector2Int bottomLeftAreaCorner, Vector2Int topRightAreaCorner, Node parentNode, int index) : base(parentNode)
    {
        this.BottomLeftAreaCorner = bottomLeftAreaCorner;
        this.TopRightAreaCorner = topRightAreaCorner;
        this.BottomRightAreaCorner = new Vector2Int(topRightAreaCorner.x, bottomLeftAreaCorner.y);
        this.TopLeftAreaCorner = new Vector2Int(bottomLeftAreaCorner.x, TopRightAreaCorner.y);
        this.TreeLayerIndex = index;

        // Inicializar con tipo Normal por defecto
        SetRoomType(RoomType.Normal);
    }

    public void SetRoomType(RoomType type, RoomTypeData data = null)
    {
        RoomType = type;
        RoomTypeData = data;
        RoomID = $"{type}_{GetHashCode()}_{Random.Range(1000, 9999)}";
    }

    public Vector2Int GetCenterPosition()
    {
        return new Vector2Int(
            (BottomLeftAreaCorner.x + TopRightAreaCorner.x) / 2,
            (BottomLeftAreaCorner.y + TopRightAreaCorner.y) / 2
        );
    }

    public bool ContainsPosition(Vector2Int position)
    {
        return position.x >= BottomLeftAreaCorner.x && position.x < TopRightAreaCorner.x &&
               position.y >= BottomLeftAreaCorner.y && position.y < TopRightAreaCorner.y;
    }
}