using Godot;
using System;

public partial class ModelListItem : Button
{
    public int Index;
    private const float LongPressSeconds = 0.65f;
    private Timer _longPressTimer;
    private bool _longPressTriggered = false;

    [Signal] public delegate void ModelClickedEventHandler(int index);
    [Signal] public delegate void ModelDeleteRequestedEventHandler(int index);

    public override void _Ready()
    {
        _longPressTimer = new Timer
        {
            OneShot = true,
            WaitTime = LongPressSeconds
        };
        AddChild(_longPressTimer);
        _longPressTimer.Timeout += OnLongPressTimeout;
    }

    public void Setup(string modelName, int index)
    {
        GetNode<Label>("%ModelName").Text = modelName;
        Index = index;
    }

    public override void _Pressed()
    {
        if (_longPressTriggered)
        {
            _longPressTriggered = false;
            return;
        }

        EmitSignal(SignalName.ModelClicked, Index);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                EmitSignal(SignalName.ModelDeleteRequested, Index);
                return;
            }

            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                HandlePress(mouseEvent.Pressed);
            }
        }
        else if (@event is InputEventScreenTouch touchEvent)
        {
            HandlePress(touchEvent.Pressed);
        }
    }

    private void HandlePress(bool pressed)
    {
        if (pressed)
        {
            _longPressTriggered = false;
            _longPressTimer?.Start();
        }
        else
        {
            _longPressTimer?.Stop();
        }
    }

    private void OnLongPressTimeout()
    {
        _longPressTriggered = true;
        EmitSignal(SignalName.ModelDeleteRequested, Index);
    }
}
