using Godot;
using System.Threading.Tasks;

public partial class DiceOverlay : CanvasLayer
{
    private const double RollAnimSeconds = 0.8;
    private const double RevealSeconds = 2.0;

    private Label _headerLabel;
    private GridContainer _diceGrid;
    private AudioStreamPlayer _audioPlayer;
    private PackedScene _dieWidgetScene;

    public override void _Ready()
    {
        _headerLabel = GetNode<Label>("Panel/Margin/VBox/HeaderLabel");
        _diceGrid = GetNode<GridContainer>("Panel/Margin/VBox/DiceGrid");
        _audioPlayer = GetNode<AudioStreamPlayer>("AudioStreamPlayer");
        _dieWidgetScene = GD.Load<PackedScene>("res://Scenes/UI/DieWidget.tscn");
        Visible = false;
    }

    public async Task ShowRollAsync(RollEvent rollEvent)
    {
        foreach (Node child in _diceGrid.GetChildren())
        {
            child.QueueFree();
        }

        _headerLabel.Text = $"{rollEvent.Label} - {rollEvent.Results.Length} dice";
        var widgets = new DieWidget[rollEvent.Results.Length];
        for (var i = 0; i < rollEvent.Results.Length; i++)
        {
            var widget = _dieWidgetScene.Instantiate<DieWidget>();
            _diceGrid.AddChild(widget);
            widget.StartRolling();
            widgets[i] = widget;
        }

        Visible = true;
        _audioPlayer.Play();
        await ToSignal(GetTree().CreateTimer(RollAnimSeconds), SceneTreeTimer.SignalName.Timeout);

        for (var i = 0; i < rollEvent.Results.Length; i++)
        {
            widgets[i].RevealFace(rollEvent.Results[i]);
        }

        await ToSignal(GetTree().CreateTimer(RevealSeconds), SceneTreeTimer.SignalName.Timeout);
        Visible = false;
    }
}
