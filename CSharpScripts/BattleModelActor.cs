using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class BattleModelActor : Node2D
{
    public event Action<BattleModelActor, Vector2>? Selected;

    private Sprite2D _miniSprite = null!;
    private Area2D _clickArea = null!;
    private Label _hpLabel = null!;
    private CollisionShape2D _hitShape = null!;

    private Model theModel = null!;
    private int _teamId;

    private bool _flashLock; // prevents overlapping purple flashes
    private bool _selectionLock;
    private float _baseSizePx;

    public int TeamId => _teamId;
    public Model BoundModel => theModel;
    public float BaseSizePx => _baseSizePx;

    public override void _Ready()
    {
        _miniSprite = GetNode<Sprite2D>("MiniSprite");
        _clickArea = GetNode<Area2D>("ClickArea");
        _hpLabel = GetNode<Label>("HpLabel");
        _hitShape = _clickArea.GetNode<CollisionShape2D>("CollisionShape2D");
        _hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _clickArea.InputEvent += OnClickAreaInputEvent;
    }

    public void Bind(Model model, int teamId)
    {
        theModel = model;
        _teamId = teamId;
        RefreshHp();
    }

    public void SetTexture(Texture2D? texture)
    {
        if (_miniSprite == null) return;
        _miniSprite.Texture = texture;
        _miniSprite.Visible = texture != null;
    }

    public void SetBaseSize(float baseSizePx)
    {
        if (_miniSprite?.Texture == null) return;
        _baseSizePx = baseSizePx;
        _miniSprite.Centered = true;
        _miniSprite.Position = Vector2.Zero;
        if (_clickArea != null)
        {
            _clickArea.Position = Vector2.Zero;
            _clickArea.InputPickable = true;
        }
        if (_hitShape != null)
        {
            _hitShape.Position = Vector2.Zero;
            _hitShape.Disabled = false;
        }
        Vector2 tex = _miniSprite.Texture.GetSize();
        float native = Mathf.Max(tex.X, tex.Y);
        if (native <= 0.01f) native = GameGlobals.Instance.FakeInchPx * 4.9f;

        float s = baseSizePx / native;
        _miniSprite.Scale = new Vector2(s, s);

        float spriteW = tex.X * _miniSprite.Scale.X;     // --- PLACE HP LABEL RIGHT BELOW THE SPRITE --
        float spriteH = tex.Y * _miniSprite.Scale.Y;

        float margin = Mathf.Max(2f, baseSizePx * 0.06f);

        _hpLabel.Size = new Vector2(baseSizePx, Mathf.Max(GameGlobals.Instance.FakeInchPx, baseSizePx * 0.30f));
        _hpLabel.HorizontalAlignment = HorizontalAlignment.Center;  // Make label width match the token size (looks clean)
 
        float labelX = -_hpLabel.Size.X / 2f;
        float labelY = (spriteH / 2f) + margin; // Control.Position is TOP-LEFT, so we center it under the sprite

        _hpLabel.Position = new Vector2(labelX, labelY);     
        _hpLabel.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(Mathf.Clamp(baseSizePx * 0.22f, 10f, 22f)));  // Font size scales with unit size

        if (_hitShape.Shape is CircleShape2D circle)
            circle.Radius = baseSizePx * 0.333f;  // Hitbox
        else if (_hitShape.Shape is RectangleShape2D rect)
            rect.Size = new Vector2(baseSizePx, baseSizePx);
    }

    public void ApplyDamage(int damage)
    {
        if (theModel == null || damage <= 0)
            return;

        theModel.Health = Mathf.Max(0, theModel.Health - damage);
        RefreshHp();
    }


    public void RefreshHp()
    {
        if (theModel == null || _hpLabel == null) return;

        _hpLabel.Text = theModel.Health.ToString();
        if (!_flashLock && !_selectionLock)         // Only change to HP color if we aren't currently flashing purple
            _hpLabel.Modulate = GetHpColor(theModel.Health, theModel.StartingHealth, theModel.Bracketed);
    }

    public async Task FlashSelectable(float seconds = 0.15f)
    {
        if (_hpLabel == null || theModel == null) return;
        if (_flashLock) return;

        _flashLock = true;
        _hpLabel.Modulate = new Color(0.6f, 0.2f, 0.8f); // purple

        await ToSignal(GetTree().CreateTimer(seconds), "timeout");

        if (!_selectionLock)
            _hpLabel.Modulate = GetHpColor(theModel.Health, theModel.StartingHealth, theModel.Bracketed);
        else
            _hpLabel.Modulate = new Color(0.6f, 0.2f, 0.8f);
        _flashLock = false;
    }

    public void SetSelectableVisual(bool selected)
    {
        if (_hpLabel == null || theModel == null) return;

        _selectionLock = selected;
        if (selected)
            _hpLabel.Modulate = new Color(0.6f, 0.2f, 0.8f);
        else
            _hpLabel.Modulate = GetHpColor(theModel.Health, theModel.StartingHealth, theModel.Bracketed);
    }

    private void OnClickAreaInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            Selected?.Invoke(this, GetPointerGlobal(mb.Position));

        if (@event is InputEventScreenTouch st && st.Pressed)
            Selected?.Invoke(this, GetPointerGlobal(st.Position));
    }

    private Vector2 GetPointerGlobal(Vector2 viewportPosition)
    {
        var inv = GetViewport().GetCanvasTransform().AffineInverse();
        return inv * viewportPosition;
    }

    private static Color GetHpColor(int Health, int StartingHealth, int bracketHp)
    {
        float frac = Health / (float)StartingHealth;

        if (frac >= 0.66f) return Colors.Green;
        if (frac < 0.66f && Health > bracketHp) return Colors.Yellow;
        return Colors.Red; // Red if in bracket or below
    }
}
