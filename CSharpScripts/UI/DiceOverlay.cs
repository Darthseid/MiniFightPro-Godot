using Godot;
using System;
using System.Threading.Tasks;

public partial class DiceOverlay : CanvasLayer
{
    private const double RollAnimSeconds = 0.8;

    private Label _headerLabel;
    private GridContainer _diceGrid;
    private AudioStreamPlayer _audioPlayer;
    private PackedScene _dieWidgetScene;
    private Button _nextButton;
    private Button _commandRerollButton;
    private Label _player1FateSixLabel;
    private Label _player2FateSixLabel;

    private DieWidget[] _widgets = Array.Empty<DieWidget>();
    private RollEvent? _currentRoll;

    public event Action? NextPressed;
    public event Action? CommandRerollPressed;
    public event Action<int>? DieClicked;

    public override void _Ready()
    {
        _headerLabel = GetNode<Label>("Panel/Margin/VBox/HeaderLabel");
        _diceGrid = GetNode<GridContainer>("Panel/Margin/VBox/DiceGrid");
        _audioPlayer = GetNode<AudioStreamPlayer>("AudioStreamPlayer");
        _nextButton = GetNode<Button>("Panel/Margin/VBox/Actions/BtnNextRoll");
        _commandRerollButton = GetNode<Button>("Panel/Margin/VBox/Actions/BtnCommandReroll");
        _player1FateSixLabel = GetNode<Label>("Panel/Margin/VBox/Actions/Player1FateSixes");
        _player2FateSixLabel = GetNode<Label>("Panel/Margin/VBox/Actions/Player2FateSixes");
        _dieWidgetScene = GD.Load<PackedScene>("res://Scenes/UI/DieWidget.tscn");
        _nextButton.Pressed += () => NextPressed?.Invoke();
        _commandRerollButton.Pressed += () => CommandRerollPressed?.Invoke();
        Visible = false;
    }

    public async Task ShowRollAsync(RollEvent rollEvent)
    {
        _currentRoll = rollEvent;
        foreach (Node child in _diceGrid.GetChildren())
            child.QueueFree();

        _headerLabel.Text = $"{rollEvent.Label} - {rollEvent.Results.Count} dice";
        _widgets = new DieWidget[rollEvent.Results.Count];
        for (var i = 0; i < rollEvent.Results.Count; i++)
        {
            var widget = _dieWidgetScene.Instantiate<DieWidget>();
            widget.SetIndex(i);
            var idx = i;
            widget.DieClicked += _ => DieClicked?.Invoke(idx);
            _diceGrid.AddChild(widget);
            widget.StartRolling();
            _widgets[i] = widget;
        }

        Visible = true;
        _audioPlayer.Play();
        await ToSignal(GetTree().CreateTimer(RollAnimSeconds), SceneTreeTimer.SignalName.Timeout);

        for (var i = 0; i < rollEvent.Results.Count; i++)
            _widgets[i].RevealFace(rollEvent.Results[i]);
    }

    public void SetFateSixes(int team1Pool, bool team1HasAbility, int team2Pool, bool team2HasAbility)
    {
        _player1FateSixLabel.Visible = team1HasAbility;
        _player2FateSixLabel.Visible = team2HasAbility;
        _player1FateSixLabel.Text = $"P1 Psychic Sixes: {team1Pool}";
        _player2FateSixLabel.Text = $"P2 Psychic Sixes: {team2Pool}";
    }

    public void SetButtonsState(bool canRush, bool canReroll)
    {
        _nextButton.Disabled = !canRush;
        _commandRerollButton.Disabled = !canReroll;

        for (var i = 0; i < _widgets.Length; i++)
            _widgets[i].SetInteractable(canRush, false);
    }


    public void SetNormalSelectionState(bool isActive)
    {
        for (var i = 0; i < _widgets.Length; i++)
            _widgets[i].SetInteractable(isActive, false);
    }

    public void SetRerollSelectionState(bool isActive)
    {
        if (_currentRoll == null)
            return;

        for (var i = 0; i < _widgets.Length; i++)
        {
            var canPick = isActive && !_currentRoll.RerolledFlags[i] && !_currentRoll.FateReplacedFlags[i];
            _widgets[i].SetInteractable(canPick, canPick);
        }
    }

    public void UpdateDieFace(int index)
    {
        if (_currentRoll == null || index < 0 || index >= _widgets.Length)
            return;

        _widgets[index].RevealFace(_currentRoll.Results[index]);
        _widgets[index].SetInteractable(false, false);
    }

    public void HideOverlay() //Refactor this out.
    {
        Visible = false;
    }
}
