using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Battle : Node2D
{
    [Export] public PackedScene BattleFieldScene = GD.Load<PackedScene>("res://Scenes/BattleField.tscn");
    [Export] public PackedScene BattleHudScene = GD.Load<PackedScene>("res://Scenes/BattleHud.tscn");
    [Export] public bool TeamAIsAI;
    [Export] public bool TeamBIsAI;

    private BattleField _battleField;
    private BattleHud _battleHud;
    private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();
    private Squad _pendingUnitOne;
    private Squad _pendingUnitTwo;
    private Player _pendingPlayerOne;
    private Player _pendingPlayerTwo;
    private Squad _teamASquad;
    private Squad _teamBSquad;
    private Player _teamAPlayer;
    private Player _teamBPlayer;
    private List<BattleModelActor> _teamAActors = new();
    private List<BattleModelActor> _teamBActors = new();
    private List<BattleModelActor> _selectedActors = new();
    private int _selectedTeamId = -1;
    private MoveVars _teamAMove = new MoveVars(false, false, false);
    private MoveVars _teamBMove = new MoveVars(false, false, false);
    private readonly Dictionary<BattleModelActor, Vector2> _movementStartPositions = new();
    private BattlePhase _currentPhase = BattlePhase.Command;
    private int _activeTeamId = 1;
    private int _startingTeamId = 1;
    private int _currentTurn = 1;
    private int _round = 1;
    private float _movementAllowanceInches;
    private bool _movementIgnoresMaxLimit;
    private float _movementEnemyBufferInches = 1.05f;
    private bool _enforceAircraftMinMove;
    private bool _movementCompletesPhase = true;
    private bool _movementUpdatesMoveVars = true;
    private bool _movementAllowsTeleport;
    private bool _awaitingMovement;
    private bool _activeSquadChargedThisTurn;
    private bool _activeSquadMovedAfterShootingThisTurn;
    private bool _isBattleEnding;
    private TaskCompletionSource<bool> _movementTcs;
    private TaskCompletionSource<bool> _phaseAdvanceTcs;
    private TaskCompletionSource<Squad?>? _enemyTargetSelectionTcs;
    private bool _awaitingEnemyTargetSelection;
    private int _enemyTargetTeamId = -1;
    private HashSet<Squad>? _enemyTargetAllowedSquads;
    private readonly AudioStream _gameOverMusic = GD.Load<AudioStream>("res://Assets/GameSounds/victory.mp3");
    private CombatSequence _sequence;
    private bool _measureModeEnabled;
    private DicePresenter _dicePresenter;
    private readonly Dictionary<Squad, Vector2> _lastKnownTransportCenters = new();
    private OrderManager? _orderManager;
    private int _player1OrderPoints;
    private int _player2OrderPoints;

    public override void _Ready()
    {
        _rng.Randomize();
        EnsureNodes();

        if ((_pendingUnitOne != null && _pendingUnitTwo != null) || (_pendingPlayerOne != null && _pendingPlayerTwo != null))
        {
            _ = InitializeBattleAsync();
        }
    }
    public void SetupSquads(Squad unitOne, Squad unitTwo)
    {
        _pendingUnitOne = unitOne;
        _pendingUnitTwo = unitTwo;

        if (IsInsideTree())
        {
            _ = InitializeBattleAsync();
        }
    }

    public void SetupPlayers(Player playerOne, Player playerTwo)
    {
        SetupPlayers(playerOne, playerTwo, TeamAIsAI, TeamBIsAI);
    }

    public void SetupPlayers(Player playerOne, Player playerTwo, bool teamAIsAI, bool teamBIsAI)
    {
        _pendingPlayerOne = playerOne;
        _pendingPlayerTwo = playerTwo;
        TeamAIsAI = teamAIsAI;
        TeamBIsAI = teamBIsAI;

        if (IsInsideTree())
        {
            _ = InitializeBattleAsync();
        }
    }
    private void EnsureNodes()
    {
        _battleField = GetNodeOrNull<BattleField>("BattleField");
        _battleHud = GetNodeOrNull<BattleHud>("HudLayer/BattleHud") ?? GetNodeOrNull<BattleHud>("BattleHud");

        if (_battleField == null && BattleFieldScene != null)
        {
            _battleField = BattleFieldScene.Instantiate<BattleField>();
            _battleField.Name = "BattleField";
            AddChild(_battleField);
        }

        if (_battleHud == null && BattleHudScene != null)
        {
            _battleHud = BattleHudScene.Instantiate<BattleHud>();
            _battleHud.Name = "BattleHud";
            var hudLayer = GetNodeOrNull<CanvasLayer>("HudLayer");
            if (hudLayer != null)
            {
                hudLayer.AddChild(_battleHud);
            }
            else
            {
                AddChild(_battleHud);
            }
        }

        if (_battleHud != null)
        {
            _battleHud.NextPhasePressed -= OnNextPhasePressed;
            _battleHud.NextPhasePressed += OnNextPhasePressed;
            _battleHud.MeasureRequested -= OnMeasureRequested;
            _battleHud.MeasureRequested += OnMeasureRequested;
        }

        if (_battleField != null)
        {
            _battleField.DragUpdated -= HandleDragUpdated;
            _battleField.DragUpdated += HandleDragUpdated;
            _battleField.DragEnded -= OnDragEnded;
            _battleField.DragEnded += OnDragEnded;
        }

        _dicePresenter = GetNodeOrNull<DicePresenter>("DicePresenter");
        if (_dicePresenter == null)
        {
            _dicePresenter = new DicePresenter { Name = "DicePresenter" };
            AddChild(_dicePresenter);
        }

        DiceRoller.Initialize(_dicePresenter);
    }

    private async Task InitializeBattleAsync()
    {
        EnsureNodes();
        if (_battleField == null || _battleHud == null)
        {
            return;
        }

        if (_pendingPlayerOne != null && _pendingPlayerTwo != null)
        {
            _teamAPlayer = _pendingPlayerOne.DeepCopy();
            _teamBPlayer = _pendingPlayerTwo.DeepCopy();
            TeamAIsAI = _teamAPlayer.IsAI;
            TeamBIsAI = _teamBPlayer.IsAI;
        }
        else
        {
            _teamAPlayer = new Player(new List<Squad> { _pendingUnitOne.DeepCopy() }, 0, new List<Order>(), false, "Player 1", new List<string>());
            _teamBPlayer = new Player(new List<Squad> { _pendingUnitTwo.DeepCopy() }, 0, new List<Order>(), false, "Player 2", new List<string>());
        }

        _teamAPlayer.OrderPoints = 0;
        _teamBPlayer.OrderPoints = 0;

        _teamASquad = _teamAPlayer.TheirSquads.FirstOrDefault();
        _teamBSquad = _teamBPlayer.TheirSquads.FirstOrDefault();

        _battleField.ClearExistingUnits();
        _teamAActors.Clear();
        _teamBActors.Clear();

        for (int i = 0; i < _teamAPlayer.TheirSquads.Count; i++)
        {
            var squad = _teamAPlayer.TheirSquads[i];
            var spawned = _battleField.SpawnSquad(squad, true, LoadSquadTexture(squad, true)).ToList();
            _teamAActors.AddRange(spawned);
            var offset = new Vector2(i * GameGlobals.Instance.FakeInchPx * 10f, 0f);
            foreach (var actor in spawned)
            {
                actor.GlobalPosition += offset;
            }
        }

        for (int i = 0; i < _teamBPlayer.TheirSquads.Count; i++)
        {
            var squad = _teamBPlayer.TheirSquads[i];
            var spawned = _battleField.SpawnSquad(squad, false, LoadSquadTexture(squad, false)).ToList();
            _teamBActors.AddRange(spawned);
            var offset = new Vector2(-i * GameGlobals.Instance.FakeInchPx * 10f, 0f);
            foreach (var actor in spawned)
            {
                actor.GlobalPosition += offset;
            }
        }

        foreach (var actor in _teamAActors.Concat(_teamBActors))
        {
            actor.Selected += HandleActorSelected;
        }


        AudioManager.Instance?.Play("startbattle");

        _startingTeamId = DetermineStartingTeamId();
        _activeTeamId = _startingTeamId;
        _currentTurn = 1;

        await RunPreGameDeploymentAsync(_activeTeamId);

        _battleHud.ShowToast($"{GetSquadName(_startingTeamId)} acts first", 2f);
        await DelaySecondsAsync(2f);
        _battleHud.ShowToast($"{GetSquadName(_startingTeamId == 1 ? 2 : 1)} acts second", 2f);
        await DelaySecondsAsync(2f);

        AudioManager.Instance?.Play("roundbell");
        _round = 1;
        if (GameGlobals.Instance != null)
        {
            GameGlobals.Instance.CurrentRound = _round;
            GameGlobals.Instance.CurrentTurn = _currentTurn;
            GameGlobals.Instance.CurrentPhase = BattlePhase.Command.ToString();
        }
        _orderManager = new OrderManager(this);
        _orderManager.InitializeBattlePoints();

        _sequence = new CombatSequence(this);
        _sequence.BeginTurn();
    }

    private Texture2D LoadSquadTexture(Squad squad, bool isTeamA)
    {
        var squadTypes = squad.SquadType ?? new List<string>();
        string texturePath;

        if (isTeamA)
        {
            texturePath = squadTypes.Contains("Aircraft") ? "res://Assets/ModelIcons/combatjet.png"
                : squadTypes.Contains("Titanic") ? "res://Assets/ModelIcons/mecha.png"
                : squadTypes.Contains("Fortification") || squadTypes.Contains("Building") ? "res://Assets/ModelIcons/fort.png"
                : squadTypes.Contains("Character") ? "res://Assets/ModelIcons/vip.png"
                : squadTypes.Contains("Mounted") ? "res://Assets/ModelIcons/biker.png"
                : squadTypes.Contains("Monster") ? "res://Assets/ModelIcons/monsterbug.png"
                : squadTypes.Contains("Vehicle") ? "res://Assets/ModelIcons/tank.png"
                : squadTypes.Contains("Infantry") ? "res://Assets/ModelIcons/gunman.png"
                : "res://Assets/ModelIcons/red-square.png";
        }
        else
        {
            texturePath = squadTypes.Contains("Aircraft") ? "res://Assets/ModelIcons/helicopter.png"
                : squadTypes.Contains("Fortification") || squadTypes.Contains("Building") ? "res://Assets/ModelIcons/fort2.png"
                : squadTypes.Contains("Character") ? "res://Assets/ModelIcons/vip2.png"
                : squadTypes.Contains("Mounted") ? "res://Assets/ModelIcons/dinorider.png"
                : squadTypes.Contains("Monster") ? "res://Assets/ModelIcons/monsterspike.png"
                : squadTypes.Contains("Vehicle") ? "res://Assets/ModelIcons/tank2.png"
                : squadTypes.Contains("Infantry") ? "res://Assets/ModelIcons/gunman2.png"
                : "res://Assets/ModelIcons/red-circle.svg";
        }

        return GD.Load<Texture2D>(texturePath);
    }

    private void HandleActorSelected(BattleModelActor actor, Vector2 pointerGlobal)
    {
        PruneDisposedActors("HandleActorSelected");

        if (_awaitingEnemyTargetSelection)
        {
            if (actor != null && actor.TeamId == _enemyTargetTeamId)
            {
                var squad = FindSquadByActor(actor, _enemyTargetTeamId);
                var squadAllowed = squad != null && (_enemyTargetAllowedSquads == null || _enemyTargetAllowedSquads.Contains(squad));
                if (squadAllowed)
                {
                    AudioManager.Instance?.Play("select");
                    _awaitingEnemyTargetSelection = false;
                    ShapeHelpers.SetSelectableVisuals(GetInactiveActors(), false);
                    _enemyTargetSelectionTcs?.TrySetResult(squad);
                    return;
                }
            }

            return;
        }

        if (_awaitingMovement)
        {
            ShapeHelpers.SetSelectableVisuals(GetActiveActors(), false);
        }
        var selection = ShapeHelpers.OnActorSelected(
            actor,
            pointerGlobal,
            _currentPhase,
            _awaitingMovement,
            _activeTeamId,
            _selectedTeamId,
            _selectedActors,
            GetActiveActors(),
            _teamAActors,
            _teamBActors,
            _battleField,
            _movementIgnoresMaxLimit ? -1f : _movementAllowanceInches,
            _movementEnemyBufferInches
        );
        _selectedTeamId = selection.SelectedTeamId;
        _selectedActors = selection.SelectedActors;

        if (actor != null && actor.TeamId == _activeTeamId && _selectedActors.Count > 0)
        {
            AudioManager.Instance?.Play("select");
        }
    }

    private void HandleDragUpdated()
    {
        PruneDisposedActors("HandleDragUpdated");
        ShapeHelpers.OnDragUpdated(_battleHud, _teamAActors, _teamBActors);
    }

    private void OnDragEnded()
    {
        FaceMovedSquadAfterDrag();

        if (_currentPhase == BattlePhase.Movement && _awaitingMovement)
        {
            _awaitingMovement = false;
            FinishMovementPhase(true);
            _movementTcs?.TrySetResult(true);
        }

        if (_selectedActors.Count > 0)
        {
            ShapeHelpers.SetSelectableVisuals(_selectedActors, false);
        }

        HandleDragUpdated();
    }

    private void FaceMovedSquadAfterDrag()
    {
        foreach (var actor in GetActiveActors())
        {
            if (actor == null || !_movementStartPositions.TryGetValue(actor, out var startPos))
            {
                continue;
            }

            var delta = actor.GlobalPosition - startPos;
            if (delta.LengthSquared() <= 0.01f)
            {
                continue;
            }

            BoardGeometry.FaceDelta(actor, delta);
        }
    }

    private async Task RunPreGameDeploymentAsync(int firstDeploymentTeamId)
    {
        if (_battleHud == null)
        {
            return;
        }

        var originalActiveTeamId = _activeTeamId;
        var secondDeploymentTeamId = firstDeploymentTeamId == 1 ? 2 : 1;
        _battleHud.ShowToast("Pre-game Deployment", 4f);
        await RunDeploymentForTeamAsync(firstDeploymentTeamId);
        _battleHud.ShowToast("Pre-game Deployment", 4f);
        await RunDeploymentForTeamAsync(secondDeploymentTeamId);

        _activeTeamId = originalActiveTeamId;
    }

    private async Task RunDeploymentForTeamAsync(int teamId)
    {
        _activeTeamId = teamId;
        EnterPhase(BattlePhase.Movement, announce: false);

        var squads = GetAliveSquadsForTeam(teamId);
        var playerName = GetSquadName(teamId);
        if (squads.Count == 0)
        {
            return;
        }

        if (IsTeamAI(teamId))
        {
            SetActiveSquadForTeam(teamId, squads.FirstOrDefault());
            _battleHud?.ShowToast($"{playerName}: AI skips deployment movement.", 2f);
            return;
        }

        for (int i = 0; i < squads.Count; i++)
        {
            var squad = squads[i];
            SetActiveSquadForTeam(teamId, squad);
            _battleHud?.ShowToast($"{playerName}: Deploy {squad.Name} ({i + 1}/{squads.Count})", 2f);

            await MovingStuff(
                movementAllowanceInches: 100f,
                ignoreMaxDistance: true,
                enemyBufferInches: 9.05f,
                enforceAircraftMinMove: false,
                completesPhase: false,
                updateMoveVars: false,
                prompt: $"{squad.Name}: Move this squad during deployment?",
                autoMove: false
            );
        }

        SetActiveSquadForTeam(teamId, squads.FirstOrDefault());
    }

    internal async Task EnterPhaseWithCadenceAsync(BattlePhase phase, string? overrideToast = null)
    {
        EnterPhase(phase, announce: false);
        var toast = overrideToast ?? $"{phase.ToString().ToUpper()} PHASE";
        _battleHud?.ShowToast(toast, 2f);
        await DelaySecondsAsync(2f);
    }


    private static bool IsActorLive(BattleModelActor actor)
    {
        return actor != null && GodotObject.IsInstanceValid(actor) && actor.IsInsideTree() && actor.BoundModel != null;
    }

    internal void PruneDisposedActors(string context)
    {
        var removedA = _teamAActors.RemoveAll(actor => !IsActorLive(actor));
        var removedB = _teamBActors.RemoveAll(actor => !IsActorLive(actor));
        _selectedActors.RemoveAll(actor => !IsActorLive(actor));

        if (removedA > 0 || removedB > 0)
        {
            GD.PushWarning($"[Battle] Removed stale actor references during {context}. TeamA removed: {removedA}, TeamB removed: {removedB}.");
        }
    }

    internal async Task DelaySecondsAsync(float seconds)
    {
        await ToSignal(GetTree().CreateTimer(seconds), Timer.SignalName.Timeout);
    }

    internal string GetSquadName(int teamId)
    {
        var player = teamId == 1 ? _teamAPlayer : _teamBPlayer;
        return player?.PlayerName ?? $"Team {teamId}";
    }

    private int DetermineStartingTeamId()
    {
        return _rng.RandiRange(0, 1) == 0 ? 1 : 2;
    }

    internal void CheckVictory()
    {
        PruneDisposedActors("CheckVictory");
        if (_isBattleEnding)
        {
            return;
        }

        _teamAPlayer.TheirSquads.RemoveAll(s => s == null || s.Composition == null || !s.Composition.Any(m => m != null && m.Health > 0));
        _teamBPlayer.TheirSquads.RemoveAll(s => s == null || s.Composition == null || !s.Composition.Any(m => m != null && m.Health > 0));

        var teamAModelCount = CountPlayerModels(_teamAPlayer);
        var teamBModelCount = CountPlayerModels(_teamBPlayer);

        if (teamAModelCount <= 0)
        {
            _ = EndBattleAsync(_teamBPlayer);
        }
        else if (teamBModelCount <= 0)
        {
            _ = EndBattleAsync(_teamAPlayer);
        }
    }


    private void DeployReplacementSquad(int teamId)
    {
        if (teamId == 1)
            _teamASquad = _teamAPlayer.TheirSquads.First();
        else
            _teamBSquad = _teamBPlayer.TheirSquads.First();

        _battleField.ClearExistingUnits();
        _teamAActors = _battleField.SpawnSquad(_teamASquad, true, LoadSquadTexture(_teamASquad, true)).ToList();
        _teamBActors = _battleField.SpawnSquad(_teamBSquad, false, LoadSquadTexture(_teamBSquad, false)).ToList();
        foreach (var actor in _teamAActors.Concat(_teamBActors))
        {
            actor.Selected += HandleActorSelected;
        }
    }

    private int CountPlayerModels(Player player)
    {
        if (player?.TheirSquads == null)
        {
            return 0;
        }

        return player.TheirSquads.Sum(squad => squad?.Composition?.Count(model => model != null && model.Health > 0) ?? 0);
    }

    internal void PostDamageCleanupAndVictoryCheck()
    {
        var allSquads = (_teamAPlayer?.TheirSquads ?? new List<Squad>())
            .Concat(_teamBPlayer?.TheirSquads ?? new List<Squad>())
            .Where(squad => squad != null)
            .ToList();

        foreach (var squad in allSquads)
        {
            var livingModels = squad.Composition?.Count(model => model != null && model.Health > 0) ?? 0;
            if (livingModels <= 0 && IsTransportSquad(squad) && squad.EmbarkedSquad != null)
            {
                TryDisembarkSquad(squad, emergency: true);
            }

            CombatEngine.RemoveDeadModels(GetActorsForSquad(squad), squad, _battleField);
        }

        CheckVictory();
    }

    internal bool SquadHasFirstStrike(Squad squad)
    {
        return squad?.SquadAbilities?.Any(ability => ability?.Innate == SquadAbilities.FirstStrike.Innate) == true;
    }

    internal void GrantTemporaryFirstStrike(Squad squad)
    {
        if (squad?.SquadAbilities == null)
        {
            return;
        }

        if (squad.SquadAbilities.Any(ability => ability?.Innate == SquadAbilities.FirstStrikeTemp.Innate && ability.IsTemporary))
        {
            return;
        }

        squad.SquadAbilities.Add(new SquadAbility(
            SquadAbilities.FirstStrikeTemp.Innate,
            SquadAbilities.FirstStrikeTemp.Name,
            SquadAbilities.FirstStrikeTemp.Modifier,
            true
        ));
    }

    internal void ClearTemporaryAbilitiesAndTurnFlags()
    {
        foreach (var squad in (_teamAPlayer?.TheirSquads ?? new List<Squad>()).Concat(_teamBPlayer?.TheirSquads ?? new List<Squad>()))
        {
            if (squad == null)
            {
                continue;
            }

            squad.SquadAbilities = StepChecks.CleanupTemporaryAbilities(squad);
        }

        _teamAMove = new MoveVars(false, false, false);
        _teamBMove = new MoveVars(false, false, false);
        _activeSquadChargedThisTurn = false;
        _activeSquadMovedAfterShootingThisTurn = false;
    }

    private async Task EndBattleAsync(Player winner)
    {
        if (_isBattleEnding)
        {
            return;
        }

        _isBattleEnding = true;
        EnterPhase(BattlePhase.BattleOver, announce: false);
        _awaitingMovement = false;
        _movementTcs?.TrySetResult(true);
        SyncGlobalTurnRound();

        _battleHud?.ShowGameOverBanner($"Game Over. {winner.PlayerName} Wins!");

        var musicManager = GetNodeOrNull<AudioStreamPlayer>("/root/Musicmanager");
        musicManager?.Stop();

        var gameOverPlayer = new AudioStreamPlayer
        {
            Bus = "Music",
            Stream = _gameOverMusic
        };
        AddChild(gameOverPlayer);
        gameOverPlayer.Play();
        await DelaySecondsAsync(14f);
        gameOverPlayer.Stop();
        gameOverPlayer.QueueFree();

        _battleHud?.HideGameOverBanner();
        musicManager?.Play();
        GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
        QueueFree();  // Ensure the banner is removed before changing scene back to main menu
    }


    internal void EnterPhase(BattlePhase phase, bool announce = true)
    {
        _currentPhase = phase;
        if (GameGlobals.Instance != null)
        {
            GameGlobals.Instance.CurrentPhase = phase.ToString();
        }

        if (announce)
        {
            _battleHud?.ShowToast($"Phase: {phase}");
        }

        var phaseSound = phase switch
        {
            BattlePhase.Command => "stratagem",
            BattlePhase.Movement => "startmovement",
            BattlePhase.Shooting => "startshooting",
            BattlePhase.Charge => "charge",
            BattlePhase.Fight => "startfight",
            BattlePhase.EndTurn => "turnover",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(phaseSound))
        {
            AudioManager.Instance?.Play(phaseSound);
        }
    }

    private List<BattleModelActor> GetVisibleActorsForTeam(int teamId)
    {
        return GetAliveSquadsForTeam(teamId)
            .SelectMany(GetActorsForSquad)
            .ToList();
    }

    internal async Task<Squad?> PromptForSquadTargetAsync(string prompt, int teamId, IReadOnlyCollection<Squad>? allowedSquads = null)
    {
        _battleHud?.ShowToast(prompt, 2.5f);
        _enemyTargetTeamId = teamId;
        _enemyTargetAllowedSquads = allowedSquads == null ? null : new HashSet<Squad>(allowedSquads.Where(s => s != null));
        _awaitingEnemyTargetSelection = true;
        _enemyTargetSelectionTcs = new TaskCompletionSource<Squad?>();

        var selectableActors = GetVisibleActorsForTeam(teamId);
        ShapeHelpers.SetSelectableVisuals(selectableActors, true);
        var selectedSquad = await _enemyTargetSelectionTcs.Task;
        ShapeHelpers.SetSelectableVisuals(selectableActors, false);

        _enemyTargetSelectionTcs = null;
        _enemyTargetTeamId = -1;
        _enemyTargetAllowedSquads = null;
        _awaitingEnemyTargetSelection = false;

        return selectedSquad;
    }

    internal Task<Squad?> PromptForEnemySquadTargetAsync(string prompt, int enemyTeamId, IReadOnlyCollection<Squad>? allowedSquads = null)
    {
        return PromptForSquadTargetAsync(prompt, enemyTeamId, allowedSquads);
    }


    internal OrderManager? OrderManager => _orderManager;

    internal Player GetPlayerByTeam(int teamId)
    {
        return teamId == 1 ? _teamAPlayer : _teamBPlayer;
    }

    internal bool SquadHasRangedWeaponThatCanShoot(Squad shooter, int enemyTeamId)
    {
        if (shooter == null)
        {
            return false;
        }

        var moveVars = CombatHelpers.GetMoveVarsForTeam(GetTeamIdForSquad(shooter), _teamAMove, _teamBMove);
        var enemySquads = GetAliveSquadsForTeam(enemyTeamId);
        foreach (var enemy in enemySquads)
        {
            var distance = BoardGeometry.ClosestDistanceInches(GetActorsForSquad(shooter), GetActorsForSquad(enemy));
            var weapons = (shooter.Composition ?? new List<Model>()).SelectMany(model => model.Tools ?? new List<Weapon>()).Where(w => !w.IsMelee);
            if (weapons.Any(weapon => CombatHelpers.CheckValidShooting(shooter, moveVars, weapon, enemy, distance)))
            {
                return true;
            }
        }

        return false;
    }

    internal async Task ResolveOverwatchAsync(int defenderTeamId, Squad defenderSquad, Squad chargingSquad)
    {
        var prevActiveTeam = _activeTeamId;
        var attackerActors = defenderTeamId == 1 ? _teamAActors : _teamBActors;
        var defenderActors = defenderTeamId == 1 ? _teamBActors : _teamAActors;
        if (attackerActors.Count == 0 || defenderActors.Count == 0)
        {
            return;
        }

        SetActiveSquadForTeam(defenderTeamId, defenderSquad);
        SetActiveSquadForTeam(defenderTeamId == 1 ? 2 : 1, chargingSquad);
        _activeTeamId = defenderTeamId;

        var attacker = GetActorsForSquad(defenderSquad).FirstOrDefault(actor => actor?.BoundModel != null && actor.BoundModel.Health > 0);
        var target = GetActorsForSquad(chargingSquad).FirstOrDefault(actor => actor?.BoundModel != null && actor.BoundModel.Health > 0);
        if (attacker != null && target != null)
        {
            await CombatEngine.ResolveBatchedAttack(attacker, target, false, _teamASquad, _teamBSquad, _teamAActors, _teamBActors, _teamAMove, _teamBMove, _battleHud, _battleField, PostDamageCleanupAndVictoryCheck, HandleExplosionProcess, null, true);
        }

        _activeTeamId = prevActiveTeam;
    }

    private Squad? FindSquadByActor(BattleModelActor actor, int teamId)
    {
        if (actor?.BoundModel == null)
        {
            return null;
        }

        var squads = GetAliveSquadsForTeam(teamId);
        return squads.FirstOrDefault(squad => squad?.Composition?.Contains(actor.BoundModel) == true);
    }

    internal void SyncGlobalTurnRound()
    {
        if (GameGlobals.Instance == null)
        {
            return;
        }

        GameGlobals.Instance.CurrentRound = _round;
        GameGlobals.Instance.CurrentTurn = _currentTurn;
        GameGlobals.Instance.CurrentPhase = _currentPhase.ToString();
    }

    internal void AnnounceTurnStart()
    {
        var playerName = GetSquadName(_activeTeamId);
        _battleHud?.ShowToast($"Round {_round} - Turn {_currentTurn}: ({playerName}).", 2f);
    }


    internal List<BattleModelActor> GetActorsForSquad(Squad squad)
    {
        PruneDisposedActors("GetActorsForSquad");
        if (squad?.Composition == null || IsSquadEmbarked(squad))
        {
            return new List<BattleModelActor>();
        }

        var models = squad.Composition;
        var actors = _teamAActors.Concat(_teamBActors)
            .Where(actor => actor?.BoundModel != null && actor.Visible && models.Contains(actor.BoundModel))
            .ToList();

        if (IsTransportSquad(squad) && squad.EmbarkedSquad != null && actors.Count > 0)
        {
            _lastKnownTransportCenters[squad] = BoardGeometry.GetActorsCenter(actors);
        }

        return actors;
    }

    internal List<BattleModelActor> GetOpposingActorsForSquad(Squad squad)
    {
        PruneDisposedActors("GetOpposingActorsForSquad");
        if (ReferenceEquals(squad, _teamASquad))
        {
            return _teamBActors;
        }

        if (ReferenceEquals(squad, _teamBSquad))
        {
            return _teamAActors;
        }

        return new List<BattleModelActor>();
    }

    internal List<BattleModelActor> GetActiveActors()
    {
        PruneDisposedActors("GetActiveActors");
        var squad = _activeTeamId == 1 ? _teamASquad : _teamBSquad;
        return GetActorsForSquad(squad);
    }

    internal List<BattleModelActor> GetInactiveActors()
    {
        PruneDisposedActors("GetInactiveActors");
        var squad = _activeTeamId == 1 ? _teamBSquad : _teamASquad;
        return GetActorsForSquad(squad);
    }


    internal void PrepareMovementStartPositions(float movementAllowanceInches, bool ignoreMaxDistance, float enemyBufferInches, bool enforceAircraftMinMove)
    {
        _movementStartPositions.Clear();
        foreach (var actor in GetActiveActors())
        {
            _movementStartPositions[actor] = actor.GlobalPosition;
        }

        _movementAllowanceInches = movementAllowanceInches;
        _movementIgnoresMaxLimit = ignoreMaxDistance;
        _movementEnemyBufferInches = enemyBufferInches;
        _enforceAircraftMinMove = enforceAircraftMinMove;

        var activeSquad = CombatHelpers.GetActiveSquad(_activeTeamId, _teamASquad, _teamBSquad);
        _movementAllowsTeleport = activeSquad?.SquadAbilities?.Any(ability => ability?.Innate == "Tele") == true;
        if (_movementAllowsTeleport)
        {
            _movementEnemyBufferInches = 0f;
        }
    }

    internal MoveVars FinishMovementPhase(bool didAttemptMove)
    {
        ShapeHelpers.SetSelectableVisuals(GetActiveActors(), false);
        var activeActors = GetActiveActors();
        var maxMoved = 0f;
        foreach (var actor in activeActors)
        {
            if (_movementStartPositions.TryGetValue(actor, out var startPos))
            {
                maxMoved = Mathf.Max(maxMoved, actor.GlobalPosition.DistanceTo(startPos));
            }
        }

        var didMove = didAttemptMove && maxMoved > 0.1f;
        var movedInches = maxMoved / Mathf.Max(0.001f, GameGlobals.Instance.FakeInchPx);

        var activeSquad = CombatHelpers.GetActiveSquad(_activeTeamId, _teamASquad, _teamBSquad);

        if (_movementAllowsTeleport && didMove)
        {
            var enemyActors = GetInactiveActors();
            var closestEnemyDistanceInches = BoardGeometry.ClosestDistanceInches(activeActors, enemyActors);
            const float minTeleportEnemyDistanceInches = 9f;
            if (enemyActors.Count > 0 && closestEnemyDistanceInches <= minTeleportEnemyDistanceInches)
            {
                foreach (var actor in activeActors)
                {
                    if (_movementStartPositions.TryGetValue(actor, out var startPos))
                    {
                        actor.GlobalPosition = startPos;
                    }
                }

                didMove = false;
                _battleHud?.ShowToast($"Teleport must end more than {minTeleportEnemyDistanceInches:0.#}\" away from enemies.");
                GD.Print($"[Rules] Blocked teleport move within {minTeleportEnemyDistanceInches}\" of enemies (closest: {closestEnemyDistanceInches:0.0}\").");
            }
        }

        if (didMove)
        {
            var moveSound = activeSquad?.SquadType.Contains("Mounted") == true ? "motorcycle" : "moved";
            AudioManager.Instance?.Play(moveSound);
        }
        if (_enforceAircraftMinMove && activeSquad?.SquadType?.Contains("Aircraft") == true)
        {
            if (maxMoved < 20f * GameGlobals.Instance.FakeInchPx)
            {
                foreach (var actor in activeActors)
                {
                    if (_movementStartPositions.TryGetValue(actor, out var startPos))
                    {
                        actor.GlobalPosition = startPos;
                    }
                }

                didMove = false;
                _battleHud?.ShowToast($"Aircraft must move at least 20\" ({maxMoved / GameGlobals.Instance.FakeInchPx:0.0}\").");
                GD.Print($"[Rules] Blocked aircraft move under 20\" (attempted {movedInches:0.0}\").");
            }
        }

        var moveVars = CombatHelpers.GetActiveMoveVars(_activeTeamId, _teamAMove, _teamBMove);
        if (_movementUpdatesMoveVars)
        {
            moveVars.Move = didMove;
        }



        return moveVars;
    }

    internal async Task<MoveVars> MovingStuff(
        float movementAllowanceInches,
        bool ignoreMaxDistance,
        float enemyBufferInches,
        bool enforceAircraftMinMove,
        bool completesPhase,
        bool updateMoveVars,
        string prompt,
        bool autoMove)
    {
        if (_battleHud == null)
        {
            return CombatHelpers.GetActiveMoveVars(_activeTeamId, _teamAMove, _teamBMove);
        }

        PrepareMovementStartPositions(movementAllowanceInches, ignoreMaxDistance, enemyBufferInches, enforceAircraftMinMove);
        _movementCompletesPhase = completesPhase;
        _movementUpdatesMoveVars = updateMoveVars;

        var wantsMove = autoMove;
        if (!autoMove)
        {
            wantsMove = await _battleHud.ConfirmActionAsync(prompt);
        }

        if (!wantsMove)
        {
            return FinishMovementPhase(false);
        }

        _awaitingMovement = true;
        ShapeHelpers.SetSelectableVisuals(GetActiveActors(), true);
        _movementTcs = new TaskCompletionSource<bool>();
        _battleHud.ShowToast("Drag your squad to move.");
        await _movementTcs.Task;

        if (_movementAllowsTeleport)
        {
            GD.Print("[Rules] Teleport movement validation applied for this squad.");
        }

        return CombatHelpers.GetActiveMoveVars(_activeTeamId, _teamAMove, _teamBMove);
    }


    internal void HandleExplosionProcess(Squad explodedSquad, Squad enemySquad, int demiseCheck)
    {
        if (explodedSquad == null || enemySquad == null || demiseCheck <= 0 ||
            explodedSquad.SquadAbilities.All(ability => ability.Innate != "Explodes"))
        {
            return;
        }

        var manyExplosions = 0;
        for (int i = 0; i < demiseCheck; i++)
        {
            if (DiceHelpers.SimpleRoll(6) == 1)
            {
                manyExplosions++;
            }
        }

        if (manyExplosions <= 0)
        {
            return;
        }

        const int safetyLimit = 10;
        var processedExplosions = 0;

        while (manyExplosions > 0 && processedExplosions < safetyLimit)
        {
            var explodeDamage = explodedSquad.SquadAbilities.FirstOrDefault(ability => ability.Innate == "Explodes")?.ResolveModifier() ?? 1;
            AudioManager.Instance?.Play("explodes");
            var blastDamage = explodeDamage * manyExplosions;

            var nearby = GetSquadsWithinRadius(explodedSquad, 6f, includeSameTeam: true);
            foreach (var nearbySquad in nearby)
            {
                CombatRolls.AllocatePure(blastDamage, nearbySquad);
                foreach (var enemyActor in GetActorsForSquad(nearbySquad))
                {
                    enemyActor.RefreshHp();
                }
            }

            var newExplosions = CombatRolls.AllocatePure(blastDamage, explodedSquad);
            foreach (var explodedActor in GetActorsForSquad(explodedSquad))
            {
                explodedActor.RefreshHp();
            }

            manyExplosions = 0;
            for (int i = 0; i < newExplosions; i++)
            {
                if (DiceHelpers.SimpleRoll(6) == 1)
                {
                    manyExplosions++;
                }
            }

            processedExplosions++;
        }

        PostDamageCleanupAndVictoryCheck();
    }

    internal async Task ResolveShootingPhase(string selectedWeaponFingerprint = null)
    {
        var attackers = GetActiveActors().ToList();
        var defenders = GetInactiveActors();
        if (attackers.Count == 0 || defenders.Count == 0)
        {
            return;
        }

        var attacker = attackers.FirstOrDefault(actor => actor != null && actor.BoundModel != null && actor.BoundModel.Health > 0);
        if (attacker == null)
        {
            return;
        }

        var target = BoardGeometry.GetClosestEnemy(attacker, defenders);
        if (target == null)
        {
            return;
        }

        BoardGeometry.FaceGroupTowardsEnemies(attackers, defenders);
        await CombatEngine.ResolveBatchedAttack(
            attacker,
            target,
            false,
            _teamASquad,
            _teamBSquad,
            attackers,
            defenders,
            _teamAMove,
            _teamBMove,
            _battleHud,
            _battleField,
            PostDamageCleanupAndVictoryCheck,
            HandleExplosionProcess,
            selectedWeaponFingerprint
        );
    }

    internal async Task ResolveFightPhase(string selectedWeaponFingerprint = null)
    {
        var attackers = GetActiveActors().ToList();
        var defenders = GetInactiveActors();
        if (attackers.Count == 0 || defenders.Count == 0)
        {
            return;
        }

        var attacker = attackers.FirstOrDefault(actor => actor != null && actor.BoundModel != null && actor.BoundModel.Health > 0);
        if (attacker == null)
        {
            return;
        }

        var target = BoardGeometry.GetClosestEnemy(attacker, defenders);
        if (target == null)
        {
            return;
        }

        BoardGeometry.FaceGroupTowardsEnemies(attackers, defenders);
        await CombatEngine.ResolveBatchedAttack(
            attacker,
            target,
            true,
            _teamASquad,
            _teamBSquad,
            attackers,
            defenders,
            _teamAMove,
            _teamBMove,
            _battleHud,
            _battleField,
            PostDamageCleanupAndVictoryCheck,
            HandleExplosionProcess,
            selectedWeaponFingerprint
        );
    }





    private void OnMeasureRequested()
    {
        _measureModeEnabled = !_measureModeEnabled;
        _battleField?.SetMeasuringMode(_measureModeEnabled);
        _battleHud?.SetMeasureButtonEnabledVisual(_measureModeEnabled);
    }

    internal List<Squad> GetSquadsWithinRadius(Squad targetSquad, float radiusInches, bool includeSameTeam)
    {
        var targetActors = GetActorsForSquad(targetSquad);
        var all = _teamAPlayer.TheirSquads.Concat(_teamBPlayer.TheirSquads)
            .Where(s => s != null && s.Composition != null && s.Composition.Count > 0)
            .ToList();

        if (!includeSameTeam)
        {
            var teamId = GetTeamIdForSquad(targetSquad);
            all = all.Where(s => GetTeamIdForSquad(s) != teamId).ToList();
        }

        return BoardGeometry.GetSquadsWithinRadius(targetSquad, targetActors, all, GetActorsForSquad, radiusInches);
    }

    internal bool IsSquadEmbarked(Squad? squad)
    {
        return squad?.TransportedBy != null;
    }

    internal bool IsTransportSquad(Squad? squad)
    {
        return squad?.SquadType?.Contains("Transport") == true;
    }

    internal bool SquadInFightRangeOfEnemy(Squad squad, int enemyTeamId)
    {
        var squadActors = GetActorsForSquad(squad);
        if (squadActors.Count == 0)
        {
            return false;
        }

        foreach (var enemy in GetAliveSquadsForTeam(enemyTeamId))
        {
            var enemyActors = GetActorsForSquad(enemy);
            if (enemyActors.Count == 0)
            {
                continue;
            }

            if (BoardGeometry.ClosestDistanceInches(squadActors, enemyActors) <= 1f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEmbarkEligiblePassenger(Squad? squad)
    {
        if (squad == null || squad.TransportedBy != null)
        {
            return false;
        }

        return squad.SquadType?.Contains("Infantry") == true || squad.SquadType?.Contains("Character") == true;
    }

    private void SetSquadActorsEmbarkedVisualState(Squad squad, bool embarked)
    {
        foreach (var actor in _teamAActors.Concat(_teamBActors).Where(a => a?.BoundModel != null && squad.Composition.Contains(a.BoundModel)))
        {
            actor.Visible = !embarked;
            var clickArea = actor.GetNodeOrNull<Area2D>("ClickArea");
            if (clickArea != null)
            {
                clickArea.InputPickable = !embarked;
            }

            var collision = actor.GetNodeOrNull<CollisionShape2D>("ClickArea/CollisionShape2D");
            if (collision != null)
            {
                collision.Disabled = embarked;
            }
        }
    }

    private bool TryEmbarkSquad(Squad transport, Squad passenger)
    {
        if (!IsTransportSquad(transport) || transport.EmbarkedSquad != null || !IsEmbarkEligiblePassenger(passenger))
        {
            return false;
        }

        var transportTeamId = GetTeamIdForSquad(transport);
        var enemyTeamId = transportTeamId == 1 ? 2 : 1;
        if (SquadInFightRangeOfEnemy(transport, enemyTeamId) || SquadInFightRangeOfEnemy(passenger, enemyTeamId))
        {
            return false;
        }

        var transportActors = GetActorsForSquad(transport);
        var passengerActors = GetActorsForSquad(passenger);
        if (transportActors.Count == 0 || passengerActors.Count == 0)
        {
            return false;
        }

        if (BoardGeometry.ClosestDistanceInches(transportActors, passengerActors) > 3f)
        {
            return false;
        }

        transport.EmbarkedSquad = passenger;
        passenger.TransportedBy = transport;
        SetSquadActorsEmbarkedVisualState(passenger, true);
        return true;
    }

    private bool TryDisembarkSquad(Squad transport, bool emergency)
    {
        var passenger = transport?.EmbarkedSquad;
        if (transport == null || passenger == null)
        {
            return false;
        }

        var transportTeamId = GetTeamIdForSquad(transport);
        var enemyTeamId = transportTeamId == 1 ? 2 : 1;
        var transportActors = _teamAActors.Concat(_teamBActors)
            .Where(actor => actor?.BoundModel != null && transport.Composition.Contains(actor.BoundModel))
            .ToList();
        var passengerActors = _teamAActors.Concat(_teamBActors)
            .Where(actor => actor?.BoundModel != null && passenger.Composition.Contains(actor.BoundModel))
            .ToList();
        var enemyActors = GetAliveSquadsForTeam(enemyTeamId).SelectMany(GetActorsForSquad).ToList();

        if (passengerActors.Count == 0)
        {
            return false;
        }

        SetSquadActorsEmbarkedVisualState(passenger, false);

        bool placed;
        if (transportActors.Count > 0)
        {
            placed = BoardGeometry.PlacePassengerSquadAroundTransport(
                transportActors,
                passengerActors,
                emergency ? 6f : 3f,
                enemyActors,
                avoidFightRange: true
            );

            if (!placed)
            {
                placed = BoardGeometry.PlacePassengerSquadAroundTransport(
                    transportActors,
                    passengerActors,
                    emergency ? 6f : 3f,
                    enemyActors,
                    avoidFightRange: false
                );
            }

            if (placed)
            {
                _lastKnownTransportCenters[transport] = BoardGeometry.GetActorsCenter(transportActors);
            }
        }
        else if (emergency && _lastKnownTransportCenters.TryGetValue(transport, out var lastCenter))
        {
            placed = BoardGeometry.PlacePassengerSquadAroundPoint(
                lastCenter,
                passengerActors,
                6f,
                enemyActors,
                avoidFightRange: true
            );

            if (!placed)
            {
                placed = BoardGeometry.PlacePassengerSquadAroundPoint(
                    lastCenter,
                    passengerActors,
                    6f,
                    enemyActors,
                    avoidFightRange: false
                );
            }
        }
        else
        {
            return false;
        }

        transport.EmbarkedSquad = null;
        passenger.TransportedBy = null;
        _lastKnownTransportCenters.Remove(transport);

        if (emergency)
        {
            passenger.ShellShock = true;
            ApplyRout(passenger, runCleanup: false);
        }

        return true;
    }

    internal async Task HandleTransportEmbarkDisembarkStepAsync(int activeTeamId, bool activeTeamIsAI)
    {
        if (activeTeamIsAI)
        {
            return;
        }

        var enemyTeamId = activeTeamId == 1 ? 2 : 1;
        var activeSquads = GetAliveSquadsForTeam(activeTeamId);
        var transports = activeSquads.Where(IsTransportSquad).ToList();

        foreach (var transport in transports)
        {
            if (transport.EmbarkedSquad == null)
            {
                continue;
            }

            var wantsDisembark = await _battleHud.ConfirmActionAsync($"{transport.Name}: Disembark {transport.EmbarkedSquad.Name}?");
            if (!wantsDisembark)
            {
                continue;
            }

            if (!TryDisembarkSquad(transport, emergency: false))
            {
                _battleHud?.ShowToast($"{transport.Name}: could not disembark now.");
            }
        }

        foreach (var transport in transports)
        {
            if (transport.EmbarkedSquad != null)
            {
                continue;
            }

            var candidates = activeSquads
                .Where(s => !ReferenceEquals(s, transport) && IsEmbarkEligiblePassenger(s))
                .Where(s => BoardGeometry.ClosestDistanceInches(GetActorsForSquad(transport), GetActorsForSquad(s)) <= 3f)
                .ToList();

            if (candidates.Count == 0)
            {
                continue;
            }

            var wantsEmbark = await _battleHud.ConfirmActionAsync($"{transport.Name}: Embark a nearby squad?");
            if (!wantsEmbark)
            {
                continue;
            }

            if (SquadInFightRangeOfEnemy(transport, enemyTeamId))
            {
                _battleHud?.ShowToast($"{transport.Name} cannot embark while in fight range.");
                continue;
            }

            var passenger = await PromptForSquadTargetAsync(
                $"{transport.Name}: Click friendly squad to embark",
                activeTeamId,
                candidates
            );
            if (passenger == null)
            {
                continue;
            }
            if (SquadInFightRangeOfEnemy(passenger, enemyTeamId))
            {
                _battleHud?.ShowToast($"{passenger.Name} cannot embark while in fight range.");
                continue;
            }

            if (!TryEmbarkSquad(transport, passenger))
            {
                _battleHud?.ShowToast($"{passenger.Name} could not embark {transport.Name}.");
                continue;
            }

            _battleHud?.ShowToast($"{passenger.Name} embarked {transport.Name}.");
        }
    }

    private void OnNextPhasePressed()
    {
        _phaseAdvanceTcs?.TrySetResult(true);
    }

    internal async Task WaitForPhaseAdvanceAsync()
    {
        _phaseAdvanceTcs = new TaskCompletionSource<bool>();
        await _phaseAdvanceTcs.Task;
        _phaseAdvanceTcs = null;
    }

    internal List<Squad> GetAliveSquadsForTeam(int teamId)
    {
        var player = teamId == 1 ? _teamAPlayer : _teamBPlayer;
        return player?.TheirSquads?
            .Where(s => s != null && s.Composition != null && s.Composition.Count > 0 && !IsSquadEmbarked(s))
            .ToList() ?? new List<Squad>();
    }

    internal void SetActiveSquadForTeam(int teamId, Squad? squad)
    {
        if (teamId == 1)
        {
            _teamASquad = squad;
        }
        else
        {
            _teamBSquad = squad;
        }
    }

    internal bool IsTeamAI(int teamId)
    {
        return teamId == 1 ? TeamAIsAI : TeamBIsAI;
    }

    internal BattleHud Hud => _battleHud;
    internal BattleField Field => _battleField;
    internal int ActiveTeamId { get => _activeTeamId; set => _activeTeamId = value; }
    internal int StartingTeamId => _startingTeamId;
    internal int CurrentTurn { get => _currentTurn; set => _currentTurn = value; }
    internal int Round { get => _round; set => _round = value; }
    internal BattlePhase CurrentPhase { get => _currentPhase; set => _currentPhase = value; }
    internal Squad TeamASquad => _teamASquad;
    internal Squad TeamBSquad => _teamBSquad;
    internal Player TeamAPlayer => _teamAPlayer;
    internal Player TeamBPlayer => _teamBPlayer;
    internal int Player1OrderPoints { get => _player1OrderPoints; set { _player1OrderPoints = value; if (_teamAPlayer != null) _teamAPlayer.OrderPoints = value; } }
    internal int Player2OrderPoints { get => _player2OrderPoints; set { _player2OrderPoints = value; if (_teamBPlayer != null) _teamBPlayer.OrderPoints = value; } }
    internal MoveVars TeamAMove { get => _teamAMove; set => _teamAMove = value; }
    internal MoveVars TeamBMove { get => _teamBMove; set => _teamBMove = value; }
    internal bool ActiveSquadChargedThisTurn { get => _activeSquadChargedThisTurn; set => _activeSquadChargedThisTurn = value; }
    internal bool ActiveSquadMovedAfterShootingThisTurn { get => _activeSquadMovedAfterShootingThisTurn; set => _activeSquadMovedAfterShootingThisTurn = value; }
    internal bool IsBattleEnding => _isBattleEnding;
    internal List<BattleModelActor> TeamAActors => _teamAActors;
    internal List<BattleModelActor> TeamBActors => _teamBActors;

    internal void ResetMoveVarsForActiveTeam()
    {
        if (_activeTeamId == 1)
        {
            _teamAMove = new MoveVars(false, false, false);
        }
        else
        {
            _teamBMove = new MoveVars(false, false, false);
        }
    }

    internal void ApplyRout(Squad squad, bool runCleanup = true)
    {
        var actors = GetActorsForSquad(squad);
        var toRemove = actors
            .Where(actor => actor?.BoundModel != null && DiceHelpers.SimpleRoll(6) < 3)
            .ToList();

        foreach (var actor in toRemove)
        {
            squad.Composition.Remove(actor.BoundModel);
            actors.Remove(actor);
            _battleField?.UnregisterActor(actor);
            actor.QueueFree();
            AudioManager.Instance?.Play("punch");
        }

        if (runCleanup)
        {
            PostDamageCleanupAndVictoryCheck();
        }
    }

    internal int GetTeamIdForSquad(Squad squad)
    {
        if (_teamAPlayer?.TheirSquads?.Contains(squad) == true) return 1;
        if (_teamBPlayer?.TheirSquads?.Contains(squad) == true) return 2;
        return ReferenceEquals(squad, _teamASquad) ? 1 : 2;
    }

    internal void EndTurnAndQueueNext()
    {
        _sequence?.BeginTurn();
    }

}
