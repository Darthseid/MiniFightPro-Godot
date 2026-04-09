using Godot;
using System;

public partial class SquadListItem : Button
{
    public int Index;
    private Timer _longPressTimer;
    private bool _longPressTriggered = false;

    [Signal] public delegate void SquadClickedEventHandler(int index);
    [Signal] public delegate void SquadDeleteRequestedEventHandler(int index);

    public override void _Ready()
    {
        _longPressTimer = new Timer
        {
            OneShot = true,
            WaitTime = GameGlobals.LongPressSeconds
        };
        AddChild(_longPressTimer);
        _longPressTimer.Timeout += OnLongPressTimeout;
    }

    public void Setup(string squadName, int index)
    {
        GetNode<Label>("%SquadName").Text = squadName;
        Index = index;
    }

    public override void _Pressed()
    {
        if (_longPressTriggered)
        {
            _longPressTriggered = false;
            return;
        }

        EmitSignal(SignalName.SquadClicked, Index);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                EmitSignal(SignalName.SquadDeleteRequested, Index);
                return;
            }

            if (mouseEvent.ButtonIndex == MouseButton.Left)
                HandlePress(mouseEvent.Pressed);
        }
        else if (@event is InputEventScreenTouch touchEvent)
            HandlePress(touchEvent.Pressed);
    }

    private void HandlePress(bool pressed)
    {
        if (pressed)
        {
            _longPressTriggered = false;
            _longPressTimer?.Start();
        }
        else
            _longPressTimer?.Stop();
    }

    private void OnLongPressTimeout()
    {
        _longPressTriggered = true;
        EmitSignal(SignalName.SquadDeleteRequested, Index);
    }
}
