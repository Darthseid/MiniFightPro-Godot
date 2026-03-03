using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Array = Godot.Collections.Array;
using Dictionary = Godot.Collections.Dictionary;

public partial class DuelComparison : Control
{
    private OptionButton _squadASelect = null!;
    private OptionButton _squadBSelect = null!;
    private SpinBox _rangeInput = null!;
    private SpinBox _trialsInput = null!;
    private Button _runButton = null!;
    private Button _backButton = null!;
    private Label _progressLabel = null!;
    private RichTextLabel _resultLabel = null!;
    private Control _histogramSlideshow = null!;
    private AnimatedSprite2D _loadingGif = null!;

    private readonly List<Squad> _squads = new();

    public override void _Ready()
    {
        var data = GameData.Instance;
        data.LoadWeaponsFromFile();
        data.LoadModelsFromFile();
        data.LoadSquadsFromFile();
        data.SyncModelsWithWeapons();
        data.SyncSquadsWithModels();
        data.SyncPlayersWithSquads();

        _squadASelect = GetNode<OptionButton>("%SquadASelect");
        _squadBSelect = GetNode<OptionButton>("%SquadBSelect");
        _rangeInput = GetNode<SpinBox>("%RangeInput");
        _trialsInput = GetNode<SpinBox>("%TrialsInput");
        _runButton = GetNode<Button>("%BtnRun");
        _backButton = GetNode<Button>("%BtnBack");
        _progressLabel = GetNode<Label>("%ProgressLabel");
        _resultLabel = GetNode<RichTextLabel>("%ResultLabel");
        _histogramSlideshow = GetNode<Control>("%HistogramSlideshow");
        _loadingGif = GetNode<AnimatedSprite2D>("%LoadingGif");
        _loadingGif.Visible = false;
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
            FirstAttacker = FirstAttackerMode.Random
        };

        var trials = Math.Clamp((int)_trialsInput.Value, 1, 100000);
        var seed = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        _runButton.Disabled = true;
        _resultLabel.Text = string.Empty;
        _histogramSlideshow.Call("clear_slides");
        _loadingGif.Visible = true;
        _loadingGif.Play();

        try
        {
            var rng = new Random(seed);
            var simulator = new DuelSimulator();

            var aWins = 0;
            var bWins = 0;
            var draws = 0;
            var firstAttackerWins = 0;
            long resolvedRounds = 0;
            long resolvedCount = 0;
            long winnerAHp = 0;
            long winnerBHp = 0;
            long aAttacks = 0;
            long bAttacks = 0;
            long aPen = 0;
            long bPen = 0;
            long aDamage = 0;
            long bDamage = 0;

            var roundsPerTrial = new Array();
            var winnerHpWhenAWin = new Array();
            var winnerHpWhenBWin = new Array();
            var aPenRatesPerTrial = new Array();
            var bPenRatesPerTrial = new Array();
            var aDamagePerTrial = new Array();
            var bDamagePerTrial = new Array();

            const int updateChunk = 250;
            for (var i = 0; i < trials; i++)
            {
                var result = simulator.RunSingle(config, squadA, squadB, rng);
                if (result.IsDraw) draws++;
                else if (result.SquadAWon)
                {
                    aWins++;
                    winnerAHp += result.WinnerRemainingHealth;
                    winnerHpWhenAWin.Add(result.WinnerRemainingHealth);
                }
                else
                {
                    bWins++;
                    winnerBHp += result.WinnerRemainingHealth;
                    winnerHpWhenBWin.Add(result.WinnerRemainingHealth);
                }

                if (result.FirstAttackerWon) firstAttackerWins++;
                if (!result.HitRoundCap)
                {
                    resolvedRounds += result.RoundsElapsed;
                    resolvedCount++;
                }

                aAttacks += result.SquadAAttacks;
                bAttacks += result.SquadBAttacks;
                aPen += result.SquadAPenetratingInjuries;
                bPen += result.SquadBPenetratingInjuries;
                aDamage += result.SquadADamageDealt;
                bDamage += result.SquadBDamageDealt;

                roundsPerTrial.Add(result.RoundsElapsed);
                aPenRatesPerTrial.Add(result.SquadAAttacks > 0 ? (double)result.SquadAPenetratingInjuries / result.SquadAAttacks : 0d);
                bPenRatesPerTrial.Add(result.SquadBAttacks > 0 ? (double)result.SquadBPenetratingInjuries / result.SquadBAttacks : 0d);
                aDamagePerTrial.Add(result.SquadADamageDealt);
                bDamagePerTrial.Add(result.SquadBDamageDealt);

                if ((i + 1) % updateChunk == 0 || i == trials - 1)
                {
                    _progressLabel.Text = $"Running: {i + 1}/{trials}";
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }

            var avgRounds = resolvedCount > 0 ? (double)resolvedRounds / resolvedCount : 0;
            var avgWinnerHpA = aWins > 0 ? (double)winnerAHp / aWins : 0;
            var avgWinnerHpB = bWins > 0 ? (double)winnerBHp / bWins : 0;
            var firstWinPct = trials > 0 ? (double)firstAttackerWins * 100d / trials : 0;
            var aPenRate = aAttacks > 0 ? (double)aPen / aAttacks : 0;
            var bPenRate = bAttacks > 0 ? (double)bPen / bAttacks : 0;
            var avgADamage = trials > 0 ? (double)aDamage / trials : 0;
            var avgBDamage = trials > 0 ? (double)bDamage / trials : 0;

            var sb = new StringBuilder();
            sb.AppendLine($"Trials: {trials}");
            sb.AppendLine($"Squad A wins: {aWins}");
            sb.AppendLine($"Squad B wins: {bWins}");
            sb.AppendLine($"Draws: {draws}");
            sb.AppendLine($"Avg rounds (excluding cap hits): {avgRounds:0.00}");
            sb.AppendLine($"Avg winner HP remaining when Squad A wins: {avgWinnerHpA:0.00}");
            sb.AppendLine($"Avg winner HP remaining when Squad B wins: {avgWinnerHpB:0.00}");
            sb.AppendLine($"First attacker win %: {firstWinPct:0.00}%");
            sb.AppendLine($"Squad A penetrating injury rate: {aPenRate:0.0000}");
            sb.AppendLine($"Squad B penetrating injury rate: {bPenRate:0.0000}");
            sb.AppendLine($"Avg damage per trial (Squad A): {avgADamage:0.00}");
            sb.AppendLine($"Avg damage per trial (Squad B): {avgBDamage:0.00}");

            var slides = new Array
            {
                BuildSlide($"Rounds per Trial (avg {avgRounds:0.00})", "Rounds", roundsPerTrial, "Rounds", new Array(), string.Empty),
                BuildSlide("Winner HP Remaining", "HP", winnerHpWhenAWin, "Squad A wins", winnerHpWhenBWin, "Squad B wins"),
                BuildSlide("Penetrating Injury Rate per Trial", "Penetrating injuries / attacks", aPenRatesPerTrial, "Squad A", bPenRatesPerTrial, "Squad B"),
                BuildSlide("Damage per Trial", "Damage", aDamagePerTrial, "Squad A", bDamagePerTrial, "Squad B")
            };

            _resultLabel.Text = sb.ToString();
            _histogramSlideshow.Call("set_slides", slides);
            _progressLabel.Text = "Done.";
        }
        finally
        {
            _loadingGif.Stop();
            _loadingGif.Visible = false;
            _runButton.Disabled = false;
        }
    }

    private static Dictionary BuildSlide(string title, string xLabel, Array seriesAValues, string seriesALabel, Array seriesBValues, string seriesBLabel)
    {
        return new Dictionary
        {
            { "title", title },
            { "x_label", xLabel },
            { "series_a_values", seriesAValues },
            { "series_a_label", seriesALabel },
            { "series_a_color", Colors.SkyBlue },
            { "series_b_values", seriesBValues },
            { "series_b_label", seriesBLabel },
            { "series_b_color", Colors.IndianRed },
            { "bins", 12 }
        };
    }
}
