using Godot;
using System;
using System.Collections.Generic;

public partial class StartGame : Control
{
    private OptionButton _unit1Dropdown;
    private OptionButton _unit2Dropdown;
    private Button _beginBattleButton;
    private Label _statusLabel;
    private TextureRect _proBanner;
    private readonly string _proBannerUrl = "https://play.google.com/store/apps/details?id=org.mozilla.firefox&hl=en_US&pli=1";
    private readonly List<Squad> _selectableSquads = new List<Squad>();

    public override void _Ready()
    {
        var data = GameData.Instance;
        data.LoadWeaponsFromFile();
        data.LoadModelsFromFile();
        data.LoadSquadsFromFile();
        data.SyncModelsWithWeapons();
        data.SyncSquadsWithModels();

        _unit1Dropdown = GetNode<OptionButton>("%Unit1Dropdown");
        _unit2Dropdown = GetNode<OptionButton>("%Unit2Dropdown");
        _beginBattleButton = GetNode<Button>("%BtnBeginBattle");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _proBanner = GetNode<TextureRect>("%ProBanner");

        PopulateSquads();
        _beginBattleButton.Pressed += OnBeginBattlePressed;
        _proBanner.GuiInput += OnProBannerInput;
    }

    private void PopulateSquads()
    {
        _unit1Dropdown.Clear();
        _unit2Dropdown.Clear();
        var data = GameData.Instance;
        if (data.SquadList.Count == 0)
        {
            _statusLabel.Text = "No units available. Please create units first.";
            _beginBattleButton.Disabled = true;
            return;
        }

        RefreshSelectableSquads(data);

        if (_selectableSquads.Count == 0)
        {
            _statusLabel.Text = "No valid units available. Please add models to squads first.";
            _beginBattleButton.Disabled = true;
            return;
        }

        for (int i = 0; i < _selectableSquads.Count; i++)
        {
            _unit1Dropdown.AddItem(_selectableSquads[i].Name, i);
            _unit2Dropdown.AddItem(_selectableSquads[i].Name, i);
        }

        _statusLabel.Text = "";
        _beginBattleButton.Disabled = false;
    }

    private void OnBeginBattlePressed()
    {
        var unit1Index = _unit1Dropdown.Selected;
        var unit2Index = _unit2Dropdown.Selected;
        if (unit1Index < 0 || unit2Index < 0)
        {
            _statusLabel.Text = "Please select two valid units.";
            return;
        }

        var data = GameData.Instance;
        RefreshSelectableSquads(data);

        if (unit1Index >= _selectableSquads.Count || unit2Index >= _selectableSquads.Count)
        {
            _statusLabel.Text = "Please select two valid units.";
            return;
        }

        var unit1 = _selectableSquads[unit1Index].DeepCopy();
        var unit2 = _selectableSquads[unit2Index].DeepCopy();

        const string battleScenePath = "res://Scenes/Battle.tscn";
        if (!ResourceLoader.Exists(battleScenePath))
        {
            _statusLabel.Text = $"Battle scene not available yet. Selected: {unit1.Name} vs {unit2.Name}.";
            return;
        }

        var battleScene = GD.Load<PackedScene>(battleScenePath);
        if (battleScene == null)
        {
            _statusLabel.Text = "Battle scene failed to load.";
            return;
        }

        var battleRoot = battleScene.Instantiate<Battle>();
        if (battleRoot == null)
        {
            _statusLabel.Text = "Battle scene missing Battle script.";
            return;
        }

        battleRoot.SetupSquads(unit1, unit2);
        GetTree().Root.AddChild(battleRoot);
        QueueFree();
    }

    private void RefreshSelectableSquads(GameData data)
    {
        _selectableSquads.Clear();
        foreach (var squad in data.SquadList)
        {
            if (squad.Composition != null && squad.Composition.Count > 0)
            {
                _selectableSquads.Add(squad);
            }
        }
    }

    private void OnProBannerInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            OS.ShellOpen(_proBannerUrl);
        }
    }
}
