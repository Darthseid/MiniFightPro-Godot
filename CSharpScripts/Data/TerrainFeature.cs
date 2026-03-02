using Godot;

public class TerrainFeature
{
    public string TerrainId;
    public Vector2 Position;
    public float Radius;
    public bool IsPlaced;
    public bool BlocksMovement;
    public bool BlocksLineOfSight;
    public bool ProvidesCover;

    public TerrainFeature()
    {
        TerrainId = System.Guid.NewGuid().ToString();
        Position = Vector2.Zero;
        Radius = 4f;
        IsPlaced = false;
        BlocksMovement = true;
        BlocksLineOfSight = true;
        ProvidesCover = true;
    }
}
