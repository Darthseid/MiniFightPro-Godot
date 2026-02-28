using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

public partial class DuelComparison : Control
{
    private OptionButton _squadASelect = null!;
    private OptionButton _squadBSelect = null!;
    private OptionButton _firstAttackerSelect = null!;
    private SpinBox _rangeInput = null!;
    private SpinBox _trialsInput = null!;
    private CheckBox _useSeed = null!;
    private SpinBox _seedInput = null!;
    private Button _runButton = null!;
    private Button _backButton = null!;
    private Label _progressLabel = null!;
    private RichTextLabel _resultLabel = null!;

    private readonly List<Squad> _squads = new();

    public override void _Ready()
    {
        var data = GameData.Instance;
        data.LoadWeaponsFromFile();
        data.LoadModelsFromFile();
        data.LoadSquadsFromFile();
        data.SyncModelsWithWeapons();
        data.SyncSquadsWithModels();

        _squadASelect = GetNode<OptionButton>("%SquadASelect");
        _squadBSelect = GetNode<OptionButton>("%SquadBSelect");
        _firstAttackerSelect = GetNode<OptionButton>("%FirstAttackerSelect");
        _rangeInput = GetNode<SpinBox>("%RangeInput");
        _trialsInput = GetNode<SpinBox>("%TrialsInput");
        _useSeed = GetNode<CheckBox>("%UseSeedCheck");
        _seedInput = GetNode<SpinBox>("%SeedInput");
        _runButton = GetNode<Button>("%BtnRun");
        _backButton = GetNode<Button>("%BtnBack");
        _progressLabel = GetNode<Label>("%ProgressLabel");
        _resultLabel = GetNode<RichTextLabel>("%ResultLabel");

        _firstAttackerSelect.AddItem("Squad A attacks first", (int)FirstAttackerMode.SquadA);
        _firstAttackerSelect.AddItem("Squad B attacks first", (int)FirstAttackerMode.SquadB);
        _firstAttackerSelect.AddItem("Random per trial", (int)FirstAttackerMode.Random);
        _firstAttackerSelect.Select(0);

        _seedInput.Editable = false;
        _useSeed.Toggled += toggled => _seedInput.Editable = toggled;

        PopulateSquadSelectors(data.SquadList);

        _runButton.Pressed += async () => await RunSimulationAsync();
        _backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
    }

    private void PopulateSquadSelectors(List<Squad> squads)
    {
        _squads.Clear();
        _squadASelect.Clear();
        _squadBSelect.Clear();

        for (var i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            if (squad?.Composition == null || squad.Composition.Count == 0)
            {
                continue;
            }

            var idx = _squads.Count;
            _squads.Add(squad);
            _squadASelect.AddItem(squad.Name, idx);
            _squadBSelect.AddItem(squad.Name, idx);
        }

        if (_squadASelect.ItemCount > 0)
        {
            _squadASelect.Select(0);
        }

        if (_squadBSelect.ItemCount > 1)
        {
            _squadBSelect.Select(1);
        }
    }

    private async Task RunSimulationAsync()
    {
        if (_squadASelect.Selected < 0 || _squadBSelect.Selected < 0 || _squadASelect.Selected == _squadBSelect.Selected)
        {
            _progressLabel.Text = "Pick two different squads.";
            return;
        }

        var squadA = _squads[_squadASelect.GetSelectedId()];
        var squadB = _squads[_squadBSelect.GetSelectedId()];

        var config = new DuelConfig
        {
            RangeInches = (float)_rangeInput.Value,
            RoundCap = 20,
            NoDamageRoundLimit = 3,
            FirstAttacker = (FirstAttackerMode)_firstAttackerSelect.GetSelectedId()
        };

        var trials = Math.Clamp((int)_trialsInput.Value, 1, 100000);
        int? seed = _useSeed.ButtonPressed ? (int)_seedInput.Value : null;

        _runButton.Disabled = true;
        _resultLabel.Text = string.Empty;

        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var simulator = new DuelSimulator();

        var aWins = 0;
        var bWins = 0;
        var draws = 0;
        var firstAttackerWins = 0;
        long resolvedRounds = 0;
        long resolvedCount = 0;
        long winnerHp = 0;
        long aAttacks = 0;
        long bAttacks = 0;
        long aPen = 0;
        long bPen = 0;

        const int updateChunk = 250;
        for (var i = 0; i < trials; i++)
        {
            var result = simulator.RunSingle(config, squadA, squadB, rng);
            if (result.IsDraw) draws++;
            else if (result.SquadAWon) aWins++;
            else bWins++;

            if (result.FirstAttackerWon) firstAttackerWins++;
            if (!result.HitRoundCap)
            {
                resolvedRounds += result.RoundsElapsed;
                resolvedCount++;
            }

            winnerHp += result.WinnerRemainingHealth;
            aAttacks += result.SquadAAttacks;
            bAttacks += result.SquadBAttacks;
            aPen += result.SquadAPenetratingInjuries;
            bPen += result.SquadBPenetratingInjuries;

            if ((i + 1) % updateChunk == 0 || i == trials - 1)
            {
                _progressLabel.Text = $"Running: {i + 1}/{trials}";
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        var avgRounds = resolvedCount > 0 ? (double)resolvedRounds / resolvedCount : 0;
        var avgWinnerHp = trials > 0 ? (double)winnerHp / trials : 0;
        var firstWinPct = trials > 0 ? (double)firstAttackerWins * 100d / trials : 0;
        var aPenRate = aAttacks > 0 ? (double)aPen / aAttacks : 0;
        var bPenRate = bAttacks > 0 ? (double)bPen / bAttacks : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"Trials: {trials}");
        sb.AppendLine($"Squad A wins: {aWins}");
        sb.AppendLine($"Squad B wins: {bWins}");
        sb.AppendLine($"Draws: {draws}");
        sb.AppendLine($"Avg rounds (excluding cap hits): {avgRounds:0.00}");
        sb.AppendLine($"Avg winner HP remaining: {avgWinnerHp:0.00}");
        sb.AppendLine($"First attacker win %: {firstWinPct:0.00}%");
        sb.AppendLine($"Squad A penetrating injury rate: {aPenRate:0.0000}");
        sb.AppendLine($"Squad B penetrating injury rate: {bPenRate:0.0000}");

        _resultLabel.Text = sb.ToString();
        _progressLabel.Text = "Done.";
        _runButton.Disabled = false;
    }
}
