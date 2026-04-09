using Godot;
using System;

public partial class WeaponListItem : Button
{
    public int Index; // Which weapon in the list does this button represent?
    private const float LongPressSeconds = 0.65f; //Change this to be a global value.
    private Timer _longPressTimer;
    private bool _longPressTriggered = false;

    [Signal] public delegate void WeaponClickedEventHandler(int index);
    [Signal] public delegate void WeaponDeleteRequestedEventHandler(int index);

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

    public void Setup(string weaponName, int index)
    {
        GetNode<Label>("%WeaponNames").Text = weaponName;
        Index = index;
    }

    public override void _Pressed()
    {
        if (_longPressTriggered)
        {
            _longPressTriggered = false;
            return;
        }     
        EmitSignal(SignalName.WeaponClicked, Index);  // When clicked, shout "I was clicked!" and pass my index ID
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Right && mouseEvent.Pressed)
            {
                EmitSignal(SignalName.WeaponDeleteRequested, Index);
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
        EmitSignal(SignalName.WeaponDeleteRequested, Index);
    }
}
