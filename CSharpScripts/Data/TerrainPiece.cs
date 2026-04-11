using Godot;

public partial class TerrainPiece : Area2D
{
    [Export] public NodePath SpritePath = "Sprite2D";
    [Export] public float RadiusInches = 4f;

    public TerrainFeature Data { get; private set; }
    public bool IsLocked { get; private set; }

    private Sprite2D _sprite;
    private bool _isDragging;
    private Vector2 _dragOffset;

    public override void _Ready()
    {
        InputPickable = true;
        _sprite = GetNodeOrNull<Sprite2D>(SpritePath);
        UpdateVisualScale();
    }

    public void Bind(TerrainFeature data)
    {
        Data = data;
        GlobalPosition = data?.Position ?? Vector2.Zero;
        if (Data != null)
            Data.Position = GlobalPosition;
    }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;
        _isDragging = false;
    }

    public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
    {
        if (IsLocked)
            return;

        switch (@event)
        {
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left:
                _isDragging = mouse.Pressed;
                _dragOffset = GlobalPosition - GetGlobalMousePosition();
                viewport.SetInputAsHandled();
                break;
            case InputEventScreenTouch touch:
                _isDragging = touch.Pressed;
                _dragOffset = GlobalPosition - ToGlobalPoint(touch.Position);
                viewport.SetInputAsHandled();
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsLocked || !_isDragging)
            return;

        switch (@event)
        {
            case InputEventMouseMotion:
                SetFromPointer(GetGlobalMousePosition());
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenDrag drag:
                SetFromPointer(ToGlobalPoint(drag.Position));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left && !mouse.Pressed:
                _isDragging = false;
                break;
            case InputEventScreenTouch touch when !touch.Pressed:
                _isDragging = false;
                break;
        }
    }

    private Vector2 ToGlobalPoint(Vector2 viewportPosition)
    {
        var inv = GetViewport().GetCanvasTransform().AffineInverse(); // Convert from viewport/screen coordinates to world coordinates, accounting for any camera zoom or offset.
        return inv * viewportPosition;
    }

    private void SetFromPointer(Vector2 pointer)
    {
        var viewport = GetViewportRect().Size;
        var radiusPx = RadiusInches * Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f); 
        var next = pointer + _dragOffset;
        next.X = Mathf.Clamp(next.X, radiusPx, viewport.X - radiusPx);
        next.Y = Mathf.Clamp(next.Y, radiusPx, viewport.Y - radiusPx);
        GlobalPosition = next;
        if (Data != null)
        {
            Data.Position = GlobalPosition;
            Data.IsPlaced = true;
        }
    }

    private void UpdateVisualScale()
    {
        if (_sprite?.Texture == null)
            return;

        var pxPerInch = Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f);
        var diameterPx = RadiusInches * 2f * pxPerInch;
        var texSize = _sprite.Texture.GetSize();
        if (texSize.X <= 0 || texSize.Y <= 0)
            return;

        var scale = diameterPx / Mathf.Max(texSize.X, texSize.Y); //Uniform scale to fit the larger dimension to the diameter.
        _sprite.Scale = new Vector2(scale, scale);
    }
}
