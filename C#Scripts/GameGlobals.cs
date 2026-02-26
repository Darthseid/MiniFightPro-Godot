using Godot;

public partial class GameGlobals : Node
{
    public static GameGlobals Instance { get; private set; } = null!;

    // ---- CONSTANT-LIKE VALUES ----
    public float FakeInchPx { get; private set; }

    // ---- RUNTIME GLOBALS ----
	public int CurrentRound { get; set; }
    public int CurrentTurn { get; set; } = 1;
    public string CurrentPhase { get; set; } = "Command";

    public override void _Ready()
    {
        Instance = this;
        RecalculateFakeInch(); // This will be 13.22 pixels on a default go dot playtest app. Using my laptop. 
    }

    public void RecalculateFakeInch()
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        FakeInchPx = Mathf.Sqrt(size.X * size.X + size.Y * size.Y) / 100f;
    }
}
