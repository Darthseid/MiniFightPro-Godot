using Godot;
using System;
using System.Collections.Generic;

public partial class DieWidget : Control
{
    private static readonly Dictionary<int, Texture2D> FaceTextures = new()
    {
        { 1, GD.Load<Texture2D>("res://Assets/GamePics/dice1.png") },
        { 2, GD.Load<Texture2D>("res://Assets/GamePics/Dice2.png") },
        { 3, GD.Load<Texture2D>("res://Assets/GamePics/Dice3.png") },
        { 4, GD.Load<Texture2D>("res://Assets/GamePics/Dice4.png") },
        { 5, GD.Load<Texture2D>("res://Assets/GamePics/Dice5.png") },
        { 6, GD.Load<Texture2D>("res://Assets/GamePics/Dice6.png") }
    };

    private AnimatedSprite2D _rollingSprite;
    private TextureRect _faceTexture;
    private Button _hitArea;
    private Panel _highlight;
    private int _index;

    public event Action<int>? DieClicked;

    public override void _Ready()
    {
        _rollingSprite = GetNode<AnimatedSprite2D>("RollingSprite");
        _faceTexture = GetNode<TextureRect>("FaceTexture");
        _hitArea = GetNode<Button>("HitArea");
        _highlight = GetNode<Panel>("Highlight");
        _hitArea.Pressed += () => DieClicked?.Invoke(_index);
        SetInteractable(false, false);
    }

    public void SetIndex(int index)
        { _index = index; }

    public void SetInteractable(bool enabled, bool highlighted)
    {
        _hitArea.Disabled = !enabled;
        _highlight.Visible = highlighted;
    }

    public void StartRolling()
    {
        _faceTexture.Visible = false;
        _rollingSprite.Visible = true;
        _rollingSprite.Play();
    }

    public void RevealFace(int value)
    {
        _rollingSprite.Stop();
        _rollingSprite.Visible = false;
        _faceTexture.Texture = FaceTextures.GetValueOrDefault(value, FaceTextures[1]);
        _faceTexture.Visible = true;
    }
}
