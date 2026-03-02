using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class StartGame : Control
{
    private OptionButton _unit1Dropdown;
    private OptionButton _unit2Dropdown;
    private SpinBox _terrainCountInput;
    private Button _beginBattleButton;
    private Label _statusLabel;
    private TextureRect _proBanner;
    private readonly string _proBannerUrl = "https://play.google.com/store/apps/details?id=org.mozilla.firefox&hl=en_US&pli=1";
    private readonly List<Player> _selectablePlayers = new List<Player>();
    private bool _teamAIsAI; // TODO: wire to duel setup toggles when UI is available.
    private bool _teamBIsAI; // TODO: wire to duel setup toggles when UI is available.

    public override void _Ready()
    {
        var data = GameData.Instance;
        data.LoadWeaponsFromFile();
        data.LoadModelsFromFile();
        data.LoadSquadsFromFile();
        data.LoadPlayersFromFile();
        data.SyncModelsWithWeapons();
        data.SyncSquadsWithModels();
        data.SyncPlayersWithSquads();

        _unit1Dropdown = GetNode<OptionButton>("%Unit1Dropdown");
        _unit2Dropdown = GetNode<OptionButton>("%Unit2Dropdown");
        _terrainCountInput = GetNode<SpinBox>("%TerrainCountInput");
        _beginBattleButton = GetNode<Button>("%BtnBeginBattle");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _proBanner = GetNode<TextureRect>("%ProBanner");

        _teamAIsAI = false;
        _teamBIsAI = false;
        PopulatePlayers();
        _beginBattleButton.Pressed += OnBeginBattlePressed;
        _proBanner.GuiInput += OnProBannerInput;
    }

    private void PopulatePlayers()
    {
        _unit1Dropdown.Clear();
        _unit2Dropdown.Clear();
        var data = GameData.Instance;
        if (data.PlayerList.Count == 0)
        {
            _statusLabel.Text = "No players available. Please create players first.";
            _beginBattleButton.Disabled = true;
            return;
        }

        RefreshSelectablePlayers(data);

        if (_selectablePlayers.Count < 2)
        {
            _statusLabel.Text = "Need at least 2 players with squads.";
            _beginBattleButton.Disabled = true;
            return;
        }

        for (int i = 0; i < _selectablePlayers.Count; i++)
        {
            _unit1Dropdown.AddItem(_selectablePlayers[i].PlayerName, i);
            _unit2Dropdown.AddItem(_selectablePlayers[i].PlayerName, i);
        }

        _statusLabel.Text = "";
        _beginBattleButton.Disabled = false;
    }

    private void OnBeginBattlePressed()
    {
        var unit1Index = _unit1Dropdown.Selected;
        var unit2Index = _unit2Dropdown.Selected;
        if (unit1Index < 0 || unit2Index < 0 || unit1Index == unit2Index)
        {
            _statusLabel.Text = "Please select two different valid players.";
            return;
        }

        var data = GameData.Instance;
        RefreshSelectablePlayers(data);

        if (unit1Index >= _selectablePlayers.Count || unit2Index >= _selectablePlayers.Count)
        {
            _statusLabel.Text = "Please select two valid players.";
            return;
        }

        var player1 = _selectablePlayers[unit1Index].DeepCopy();
        var player2 = _selectablePlayers[unit2Index].DeepCopy();

        const string battleScenePath = "res://Scenes/Battle.tscn";
        if (!ResourceLoader.Exists(battleScenePath))
        {
            _statusLabel.Text = $"Battle scene not available yet. Selected: {player1.PlayerName} vs {player2.PlayerName}.";
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

        var terrainCount = (int)Mathf.Clamp((float)_terrainCountInput.Value, 0f, 12f);
        battleRoot.SetupPlayers(player1, player2, _teamAIsAI, _teamBIsAI, terrainCount);
        GetTree().Root.AddChild(battleRoot);
        QueueFree();
    }

    private void RefreshSelectablePlayers(GameData data)
    {
        _selectablePlayers.Clear();
        foreach (var player in data.PlayerList)
        {
            if (player.TheirSquads != null && player.TheirSquads.Count > 0)
            {
                _selectablePlayers.Add(player);
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
