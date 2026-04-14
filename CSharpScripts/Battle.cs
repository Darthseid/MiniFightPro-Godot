using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Battle : Node2D
{
    [Export] public PackedScene BattleFieldScene = GD.Load<PackedScene>("res://Scenes/BattleField.tscn");
    [Export] public PackedScene BattleHudScene = GD.Load<PackedScene>("res://Scenes/BattleHud.tscn");

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
    private BattlePhase _currentPhase = BattlePhase.TerrainSetup;
    private int _activeTeamId = 1;
    private int _startingTeamId = 1;
    private int _currentTurn = 1;
    private int _round = 1;
    private bool _activeSquadChargedThisTurn;
    private bool _activeSquadMovedAfterShootingThisTurn;
    private bool _isBattleEnding;
    private TaskCompletionSource<bool> _phaseRushTcs;
    private TaskCompletionSource<Squad?>? _enemyTargetSelectionTcs;
    private bool _awaitingEnemyTargetSelection;
    private int _enemyTargetTeamId = -1;
    private HashSet<Squad>? _enemyTargetAllowedSquads;
    private readonly AudioStream _gameOverMusic = GD.Load<AudioStream>("res://Assets/GameSounds/victory.mp3");
    private CombatSequence _sequence;
    private bool _measureModeEnabled;
    private DicePresenter _dicePresenter;
    private OrderManager? _orderManager;
    private int _player1OrderPoints;
    private int _player2OrderPoints;
    private int _terrainCount;
    private MovementManager _movementManager;
    private TerrainManager _terrainManager = new();
    private CombatManager _combatManager;
    private TransportController _transportController;
    public List<TerrainFeature> ActiveTerrain => _terrainManager.ActiveTerrain;

    public override void _Ready()
    {
        CombatRolls.DamageResistanceModifierProvider = GetDamageResistanceAuraModifier;
        StepChecks.FriendlyAuraSquadProvider = GetFriendlyAuraSquads;
        StepChecks.EnemyAuraSquadProvider = GetEnemyAuraSquads;
        StepChecks.AuraRangeCheckProvider = AreSquadsWithinDistance;
        _rng.Randomize();
        EnsureNodes();
        if ((_pendingUnitOne != null && _pendingUnitTwo != null) || (_pendingPlayerOne != null && _pendingPlayerTwo != null))
            _ = InitializeBattleAsync();
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(CombatRolls.DamageResistanceModifierProvider, GetDamageResistanceAuraModifier))
            CombatRolls.DamageResistanceModifierProvider = null;
        if (ReferenceEquals(StepChecks.FriendlyAuraSquadProvider, GetFriendlyAuraSquads))
            StepChecks.FriendlyAuraSquadProvider = null;
        if (ReferenceEquals(StepChecks.EnemyAuraSquadProvider, GetEnemyAuraSquads))
            StepChecks.EnemyAuraSquadProvider = null;
        if (ReferenceEquals(StepChecks.AuraRangeCheckProvider, AreSquadsWithinDistance))
            StepChecks.AuraRangeCheckProvider = null;
    }

    public void SetupSquads(Squad unitOne, Squad unitTwo)
    {
        _pendingUnitOne = unitOne;
        _pendingUnitTwo = unitTwo;

        if (IsInsideTree())
            _ = InitializeBattleAsync();
    }

    public void SetupPlayers(Player playerOne, Player playerTwo)
    { SetupPlayers(playerOne, playerTwo, 0); }
    public void SetupPlayers(Player playerOne, Player playerTwo, int terrainCount)
    {
        _pendingPlayerOne = playerOne;
        _pendingPlayerTwo = playerTwo;
        _terrainCount = Math.Max(0, terrainCount);

        if (IsInsideTree())
            _ = InitializeBattleAsync();
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
        _dicePresenter.ActivePlayerTeamId = _activeTeamId;

        _movementManager ??= new MovementManager(
            () => CombatHelpers.GetActiveMoveVars(_activeTeamId, _teamAMove, _teamBMove),
            moved =>
            {
                var moveVars = CombatHelpers.GetActiveMoveVars(_activeTeamId, _teamAMove, _teamBMove);
                moveVars.Move = moved;
            });

        _combatManager ??= new CombatManager(
            _battleHud,
            _battleField,
            GetActiveActors,
            GetInactiveActors,
            () => _teamASquad,
            () => _teamBSquad,
            () => _teamAMove,
            () => _teamBMove,
            GetSquadsWithinRadius,
            GetActorsForSquad,
            PostDamageCleanupAndVictoryCheck,
            CanActorSeeTargetSquad);

        _transportController ??= new TransportController(
            _battleField,
            GetAliveSquadsForTeam,
            GetActorsForSquad,
            GetTeamIdForSquad,
            SquadInFightRangeOfEnemy,
            async (prompt, teamId, allowed) => await PromptForSquadTargetAsync(prompt, teamId, allowed),
            async prompt => await _battleHud.ConfirmActionAsync(prompt),
            message => _battleHud?.ShowToast(message),
            ApplyRout,
            SetActiveSquadForTeam,
            async (teamId, squad) => await MovingStuff(99f, true, 0f, false, false, false, $"{squad.Name}: place strategic reserve squad", false));
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
        }
        else
        {
            _teamAPlayer = new Player(new List<Squad> { _pendingUnitOne.DeepCopy() }, 0, new List<Order>(), false, "Player 1", new List<string>());
            _teamBPlayer = new Player(new List<Squad> { _pendingUnitTwo.DeepCopy() }, 0, new List<Order>(), false, "Player 2", new List<string>());
        }

        _teamAPlayer.OrderPoints = 0;
        _teamBPlayer.OrderPoints = 0;
        _teamAPlayer.HasStrandedMiracle = _teamAPlayer.HasStrandedMiracle || (_teamAPlayer.PlayerAbilities?.Contains(PlayerAbilities.StrandedMiracle) == true);
        _teamBPlayer.HasStrandedMiracle = _teamBPlayer.HasStrandedMiracle || (_teamBPlayer.PlayerAbilities?.Contains(PlayerAbilities.StrandedMiracle) == true);
        _teamAPlayer.FateSixPool = _teamAPlayer.HasStrandedMiracle ? 3 : 0;
        _teamBPlayer.FateSixPool = _teamBPlayer.HasStrandedMiracle ? 3 : 0;

        _teamASquad = _teamAPlayer.TheirSquads.FirstOrDefault();
        _teamBSquad = _teamBPlayer.TheirSquads.FirstOrDefault();

        _battleField.ClearExistingUnits();
        _teamAActors.Clear();
        _teamBActors.Clear();

        for (int i = 0; i < _teamAPlayer.TheirSquads.Count; i++)
        {
            var squad = _teamAPlayer.TheirSquads[i];
            var spawned = _battleField.SpawnSquad(squad, true).ToList();
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
            var spawned = _battleField.SpawnSquad(squad, false).ToList();
            _teamBActors.AddRange(spawned);
            var offset = new Vector2(-i * GameGlobals.Instance.FakeInchPx * 10f, 0f);
            foreach (var actor in spawned)
                actor.GlobalPosition += offset;
        }

        foreach (var actor in _teamAActors.Concat(_teamBActors))
            actor.Selected += HandleActorSelected;

        LogFreeHealthcareAuraDebugMatrix();
        AudioManager.Instance?.Play("startbattle");

        _startingTeamId = DetermineStartingTeamId();
        _activeTeamId = _startingTeamId;
        _currentTurn = 1;

        await RunTerrainSetupAsync();
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
            GameGlobals.Instance.CurrentPhase = BattlePhase.Starting.ToString();
        }
        _orderManager = new OrderManager(this);
        _orderManager.InitializeBattlePoints();

        EnterPhase(BattlePhase.NormalPlay, announce: false);
        _sequence = new CombatSequence(this);
        _sequence.BeginTurn();
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

        if (_movementManager.IsAwaitingMovement)
            ShapeHelpers.SetSelectableVisuals(GetActiveActors(), false);
        var selection = ShapeHelpers.OnActorSelected(
            actor,
            pointerGlobal,
            _currentPhase,
            _movementManager.IsAwaitingMovement,
            _activeTeamId,
            _selectedTeamId,
            _selectedActors,
            GetActiveActors(),
            _teamAActors,
            _teamBActors,
            _battleField,
            _movementManager.MovementIgnoresMaxLimit ? -1f : _movementManager.MovementAllowanceInches,
            _movementManager.MovementEnemyBufferInches
        );
        _selectedTeamId = selection.SelectedTeamId;
        _selectedActors = selection.SelectedActors;

        if (actor != null && actor.TeamId == _activeTeamId && _selectedActors.Count > 0)
            AudioManager.Instance?.Play("select");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_currentPhase != BattlePhase.TerrainSetup || _terrainManager.IsLocked || _terrainManager.UnplacedCount <= 0)
            return;

        switch (@event)
        {
            case InputEventMouseButton mouseButton when mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed:
                PlaceNextTerrainAt(GetPointerGlobal(mouseButton.Position));
                GetViewport().SetInputAsHandled();
                break;
            case InputEventScreenTouch touch when touch.Pressed:
                PlaceNextTerrainAt(GetPointerGlobal(touch.Position));
                GetViewport().SetInputAsHandled();
                break;
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
        if ((_currentPhase == BattlePhase.Movement || _currentPhase == BattlePhase.SquadDeployment) && _movementManager.IsAwaitingMovement)
        {
            var activeSquad = CombatHelpers.GetActiveSquad(_activeTeamId, _teamASquad, _teamBSquad);
            if (IsTerrainBlockingMovement(activeSquad))
            {
                _movementManager.RevertTrackedActors(GetActiveActors());
                _battleHud?.ShowToast("Move blocked by terrain");
                GD.Print("[Terrain] Move blocked by terrain");
                FinishMovementPhase(false);
                _movementManager.ResolveAwaitingMovement(false);
            }
            else
            {
                FinishMovementPhase(true);
                if (_movementManager.RequiresMovementRetry)
                {
                    _battleHud?.ShowToast("Invalid move. Try moving this squad again.");
                    return;
                }
                _movementManager.ResolveAwaitingMovement(true);
            }
        }

        if (_selectedActors.Count > 0)
            ShapeHelpers.SetSelectableVisuals(_selectedActors, false);
        HandleDragUpdated();
    }

    private void FaceMovedSquadAfterDrag()
    {
        foreach (var actor in GetActiveActors())
        {
            if (actor == null || !_movementManager.TryGetStartPosition(actor, out var startPos))
                continue;
            var delta = actor.GlobalPosition - startPos;
            if (delta.LengthSquared() <= 0.01f) // Prevent jittery facing changes when the squad was barely moved
                continue;
            BoardGeometry.FaceDelta(actor, delta);
        }
    }

    private async Task RunPreGameDeploymentAsync(int firstDeploymentTeamId)
    {
        if (_battleHud == null)
            return;
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
            return;
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
        _orderManager?.OnPhaseStarted();
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
        if (_isBattleEnding) // Victory has already been achieved, no need to waste resources.
            return;
        _teamAPlayer.TheirSquads.RemoveAll(s => s == null || s.Composition == null || !s.Composition.Any(m => m != null && m.Health > 0));
        _teamBPlayer.TheirSquads.RemoveAll(s => s == null || s.Composition == null || !s.Composition.Any(m => m != null && m.Health > 0));

        var teamAModelCount = CountPlayerModels(_teamAPlayer);
        var teamBModelCount = CountPlayerModels(_teamBPlayer);

        if (teamAModelCount <= 0)
            _ = EndBattleAsync(_teamBPlayer);
        else if (teamBModelCount <= 0)
            _ = EndBattleAsync(_teamAPlayer);
    }

    private void DeployReplacementSquad(int teamId)
    {
        if (teamId == 1)
            _teamASquad = _teamAPlayer.TheirSquads.First();
        else
            _teamBSquad = _teamBPlayer.TheirSquads.First();
        _battleField.ClearExistingUnits();
        _teamAActors = _battleField.SpawnSquad(_teamASquad, true).ToList();
        _teamBActors = _battleField.SpawnSquad(_teamBSquad, false).ToList();
        foreach (var actor in _teamAActors.Concat(_teamBActors))
        {
            actor.Selected += HandleActorSelected;
        }
    }

    private int CountPlayerModels(Player player)
    {
        if (player?.TheirSquads == null)
            return 0;
        return player.TheirSquads.Sum(squad => squad?.Composition?.Count(model => model != null && model.Health > 0) ?? 0); // Only count living models.
    }

    internal void PostDamageCleanupAndVictoryCheck()
    {
        var allSquads = (_teamAPlayer?.TheirSquads ?? new List<Squad>())
            .Concat(_teamBPlayer?.TheirSquads ?? new List<Squad>())
            .Where(squad => squad != null)
            .ToList();
        var destroyedCount = 0;

        foreach (var squad in allSquads)
        {
            var livingModels = squad.Composition?.Count(model => model != null && model.Health > 0) ?? 0;
            if (livingModels <= 0 && IsTransportSquad(squad) && squad.EmbarkedSquad != null)
                TryDisembarkSquad(squad, emergency: true);

            CombatEngine.RemoveDeadModels(GetActorsForSquad(squad), squad, _battleField);

            var stillLivingModels = squad.Composition?.Count(model => model != null && model.Health > 0) ?? 0;
            if (stillLivingModels <= 0)
                destroyedCount++;
        }

        if (destroyedCount > 0)
        {
            if (_teamAPlayer?.HasStrandedMiracle == true)
                _teamAPlayer.FateSixPool += destroyedCount;

            if (_teamBPlayer?.HasStrandedMiracle == true)
                _teamBPlayer.FateSixPool += destroyedCount;

            _orderManager?.RefreshHud();
            _dicePresenter?.RefreshFateSixHud();
        }
        CheckVictory();
    }

    internal bool SquadHasFirstStrike(Squad squad)
        { return squad?.SquadAbilities?.Any(ability => ability?.Innate == SquadAbilities.FirstStrike.Innate || ability?.Innate == SquadAbilities.TempFirstStrike.Innate) == true; }

    internal void GrantTemporaryFirstStrike(Squad squad)
    {
        if (squad?.SquadAbilities == null)
            return;

        if (squad.SquadAbilities.Any(ability => ability?.Innate == SquadAbilities.FirstStrikeTemp.Innate && ability.IsTemporary))
            return;

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
                continue;

            squad.SquadAbilities = StepChecks.CleanupTemporaryAbilities(squad);
            squad.RushedThisTurn = false;
            squad.RetreatedThisTurn = false;
        }

        _teamAMove = new MoveVars(false, false, false);
        _teamBMove = new MoveVars(false, false, false);
        _activeSquadChargedThisTurn = false;
        _activeSquadMovedAfterShootingThisTurn = false;
    }

    private async Task EndBattleAsync(Player winner)
    {
        if (_isBattleEnding)
            return;

        _isBattleEnding = true;
        EnterPhase(BattlePhase.BattleOver, announce: false);
        _movementManager.ResolveAwaitingMovement(false);
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
            GameGlobals.Instance.CurrentPhase = phase.ToString();

        if (announce)
            _battleHud?.ShowToast($"Phase: {phase}");

        var phaseSound = phase switch
        {
            BattlePhase.Starting => "order_ping",
            BattlePhase.Movement => "startmovement",
            BattlePhase.Shooting => "startshooting",
            BattlePhase.Engagement => "charge",
            BattlePhase.Melee => "startfight",
            BattlePhase.EndTurn => "turnover",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(phaseSound))
            AudioManager.Instance?.Play(phaseSound);
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
        { return PromptForSquadTargetAsync(prompt, enemyTeamId, allowedSquads); }


    internal OrderManager? OrderManager => _orderManager;

    internal Player GetPlayerByTeam(int teamId)
        { return teamId == 1 ? _teamAPlayer : _teamBPlayer; }

    internal int GetFateSixPool(int teamId)
    {
        var player = GetPlayerByTeam(teamId);
        return player?.HasStrandedMiracle == true ? Math.Max(0, player.FateSixPool) : 0;
    }

    internal bool TryConsumeFateSix(int teamId)
    {
        var player = GetPlayerByTeam(teamId);
        if (player == null || !player.HasStrandedMiracle || player.FateSixPool <= 0)
            return false;

        player.FateSixPool--;
        _orderManager?.RefreshHud();
        _dicePresenter?.RefreshFateSixHud();
        return true;
    }

    internal bool SquadHasRangedWeaponThatCanShoot(Squad shooter, int enemyTeamId)
    {
        if (shooter == null)
            return false;

        var moveVars = CombatHelpers.GetMoveVarsForTeam(GetTeamIdForSquad(shooter), _teamAMove, _teamBMove);
        var enemySquads = GetAliveSquadsForTeam(enemyTeamId);
        var shooterActors = GetActorsForSquad(shooter);
        if (shooterActors.Count == 0)
            return false;
        foreach (var enemy in enemySquads)
        {
            var distance = BoardGeometry.ClosestDistanceInches(GetActorsForSquad(shooter), GetActorsForSquad(enemy));
            var visibleShooters = shooterActors
                .Where(actor => actor?.BoundModel != null && actor.BoundModel.Health > 0)
                .Where(actor => CanActorSeeTargetSquad(actor, shooter, enemy))
                .Select(actor => actor.BoundModel)
                .ToList();
            if (visibleShooters.Count == 0)
                continue;

            var weapons = visibleShooters
                .SelectMany(model => model.Tools ?? new List<Weapon>())
                .Where(w => !w.IsMelee);
            if (weapons.Any(weapon => CombatHelpers.CheckValidShooting(shooter, moveVars, weapon, enemy, distance, hasLineOfSight: true)))
                return true;
        }
        return false;
    }

    internal bool SquadHasIndirectFireWeaponThatCanShootTarget(Squad shooter, Squad target)
    {
        if (shooter == null || target == null)
            return false;

        var moveVars = CombatHelpers.GetMoveVarsForTeam(GetTeamIdForSquad(shooter), _teamAMove, _teamBMove);
        var distance = BoardGeometry.ClosestDistanceInches(GetActorsForSquad(shooter), GetActorsForSquad(target));
        return (shooter.Composition ?? new List<Model>())
            .SelectMany(model => model.Tools ?? new List<Weapon>())
            .Where(weapon => !weapon.IsMelee)
            .Any(weapon => weapon.Special.Any(ability => ability.Innate == WeaponAbilities.IndirectFire.Innate)
                           && CombatHelpers.CheckValidShooting(shooter, moveVars, weapon, target, distance, hasLineOfSight: false));
    }

    internal async Task ResolveReactiveFireAsync(int defenderTeamId, Squad defenderSquad, Squad chargingSquad)
    {
        var prevActiveTeam = _activeTeamId;
        var attackerActors = defenderTeamId == 1 ? _teamAActors : _teamBActors;
        var defenderActors = defenderTeamId == 1 ? _teamBActors : _teamAActors;
        if (attackerActors.Count == 0 || defenderActors.Count == 0)
            return;

        await _combatManager.ResolveReactiveFireAsync(
            defenderTeamId,
            defenderSquad,
            chargingSquad,
            teamId => ActiveTeamId = teamId,
            (teamId, squad) => SetActiveSquadForTeam(teamId, squad));

        _activeTeamId = prevActiveTeam;
    }

    internal Task<RollEvent> RollInteractiveAsync(
        int diceCount,
        int sides,
        string titleText,
        int ownerTeamId,
        string attackerName = null,
        string defenderName = null,
        string weaponName = null,
        string weaponFingerprint = null,
        RollPhase phase = RollPhase.Other)
    {
        var context = new RollContext(
            phase,
            titleText,
            attackerName,
            defenderName,
            weaponName,
            weaponFingerprint,
            false,
            ownerTeamId);
        return DiceRoller.PresentAndRollAsync(sides, diceCount, context);
    }

    private Squad? FindSquadByActor(BattleModelActor actor, int teamId)
    {
        if (actor?.BoundModel == null)
            return null;

        var squads = GetAliveSquadsForTeam(teamId);
        return squads.FirstOrDefault(squad => squad?.Composition?.Contains(actor.BoundModel) == true);
    }

    internal void SyncGlobalTurnRound()
    {
        if (GameGlobals.Instance == null)
            return;

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
            return new List<BattleModelActor>();

        var models = squad.Composition;
        var actors = _teamAActors.Concat(_teamBActors)
            .Where(actor => actor?.BoundModel != null && actor.Visible && models.Contains(actor.BoundModel))
            .ToList();

        return actors;
    }

    internal List<BattleModelActor> GetOpposingActorsForSquad(Squad squad)
    {
        PruneDisposedActors("GetOpposingActorsForSquad");
        if (ReferenceEquals(squad, _teamASquad))
            return _teamBActors;

        if (ReferenceEquals(squad, _teamBSquad))
            return _teamAActors;

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
        _movementManager.PrepareMovement(
            CombatHelpers.GetActiveSquad(_activeTeamId, _teamASquad, _teamBSquad),
            GetActiveActors(),
            GetInactiveActors(),
            movementAllowanceInches,
            ignoreMaxDistance,
            enemyBufferInches,
            enforceAircraftMinMove);
    }

    internal MoveVars FinishMovementPhase(bool didAttemptMove)
    {
        ShapeHelpers.SetSelectableVisuals(GetActiveActors(), false);
        return _movementManager.FinishMovement(
            didAttemptMove,
            GetActiveActors,
            () => CombatHelpers.GetActiveSquad(_activeTeamId, _teamASquad, _teamBSquad),
            GetInactiveActors,
            message => _battleHud?.ShowToast(message),
            GD.Print);
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
            return CombatHelpers.GetActiveMoveVars(_activeTeamId, _teamAMove, _teamBMove);

        PrepareMovementStartPositions(movementAllowanceInches, ignoreMaxDistance, enemyBufferInches, enforceAircraftMinMove);
        _movementManager.SetMovementUpdatesMoveVars(updateMoveVars);

        return await _movementManager.StartMovementAsync(
            prompt,
            autoMove,
            async () => await _battleHud.ConfirmActionAsync(prompt),
            (message, duration) => _battleHud.ShowToast(message, duration ?? 2f),
            ShapeHelpers.SetSelectableVisuals,
            () => CombatHelpers.GetActiveMoveVars(_activeTeamId, _teamAMove, _teamBMove),
            () => { });
    }


    internal void HandleExplosionProcess(Squad explodedSquad, Squad enemySquad, int demiseCheck)
    { _combatManager.HandleExplosionProcess(explodedSquad, enemySquad, demiseCheck); } //Streamline this in future.

    internal async Task ResolveShootingPhase(string selectedWeaponFingerprint = null, bool hasLineOfSight = true)
    {
        await _combatManager.ResolveShootingPhaseAsync(selectedWeaponFingerprint, hasLineOfSight);
    }

    internal async Task ResolveFightPhase(string selectedWeaponFingerprint = null)
    {
        await _combatManager.ResolveFightPhaseAsync(selectedWeaponFingerprint);
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
        { return squad?.IsEmbarked() == true; }

    internal bool IsTransportSquad(Squad? squad)
        { return squad?.IsTransport() == true; } //Streamline this in future by caching transport status on squad and updating when composition changes, if needed.

    internal bool SquadInFightRangeOfEnemy(Squad squad, int enemyTeamId)
    {
        var squadActors = GetActorsForSquad(squad);
        if (squadActors.Count == 0)
            return false;

        foreach (var enemy in GetAliveSquadsForTeam(enemyTeamId))
        {
            var enemyActors = GetActorsForSquad(enemy);
            if (enemyActors.Count == 0)
                continue;
            if (BoardGeometry.ClosestDistanceInches(squadActors, enemyActors) <= 1f)
                return true;
        }

        return false;
    }

    private static bool IsEmbarkEligiblePassenger(Squad? squad)
    {
        return squad?.IsEmbarkEligiblePassenger() == true;
    }

    private void SetSquadActorsEmbarkedVisualState(Squad squad, bool embarked)
    {
        _transportController.SetSquadActorsEmbarkedVisualState(squad, embarked);
    }

    private bool TryEmbarkSquad(Squad transport, Squad passenger)
    {
        return _transportController.TryEmbarkSquad(transport, passenger);
    }

    private bool TryDisembarkSquad(Squad transport, bool emergency)
    {
        return _transportController.TryDisembarkSquad(transport, emergency);
    }

    internal async Task HandleTransportEmbarkDisembarkStepAsync(int activeTeamId, bool activeTeamIsAI)
    {
        await _transportController.HandleTransportEmbarkDisembarkStepAsync(activeTeamId, activeTeamIsAI);
    }

    private async Task RunTerrainSetupAsync()
    {
        BuildTerrainFeatures();
        SpawnTerrainPieces();
        EnterPhase(BattlePhase.TerrainSetup, announce: false);
        _battleHud?.ShowToast($"Terrain Setup: {_terrainCount} pieces", 4f);
        _battleHud?.ShowToast("Terrain Setup: place and move terrain, then press Continue →", 5f);
        GD.Print($"[Terrain] Terrain Setup: {_terrainCount} pieces");
        await WaitForPhaseRushAsync();
        LockTerrain();
        EnterPhase(BattlePhase.Movement, announce: false);
        _battleHud?.ShowToast("Continue → Squad Deployment", 3f);
        GD.Print("[Terrain] Continue → Squad Deployment");
    }

    private void BuildTerrainFeatures()
    {
        _terrainManager.Initialize(_terrainCount);
        _terrainManager.BuildTerrainFeatures();
    }

    private void SpawnTerrainPieces()
    {
        var scene = GD.Load<PackedScene>("res://Scenes/Terrain/TerrainPiece.tscn");
        _terrainManager.SpawnTerrainPieces(_battleField, scene);
    }

    private void PlaceNextTerrainAt(Vector2 globalPos)
    {
        _terrainManager.PlaceNextTerrainAt(globalPos);
    }

    private void LockTerrain()
    {
        _terrainManager.LockTerrain();
    }

    private Vector2 GetPointerGlobal(Vector2 viewportPosition)
    {
        var inv = GetViewport().GetCanvasTransform().AffineInverse();
        return inv * viewportPosition;
    }

    internal bool IsTerrainBlockingMovement(Squad squad)
    {
        if (squad == null || squad.SquadType?.Contains("Fly") == true)
            return false;

        var segments = new List<(Vector2 start, Vector2 end)>();
        foreach (var actor in GetActorsForSquad(squad))
        {
            if (_movementManager.TryGetStartPosition(actor, out var startPos))
                segments.Add((startPos, actor.GlobalPosition));
        }
        return _terrainManager.IsTerrainBlockingMovement(segments, Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f));
    }

    internal bool IsChargeBlockedByTerrain(Squad attacker, Squad target)
    {
        if (attacker == null || target == null || attacker.SquadType?.Contains("Fly") == true)
            return false;

        var targetActors = GetActorsForSquad(target);
        if (targetActors.Count == 0)
            return false;

        var targetCenter = BoardGeometry.GetActorsCenter(targetActors);
        foreach (var actor in GetActorsForSquad(attacker))
        {
            foreach (var terrain in ActiveTerrain.Where(t => t.IsPlaced && t.BlocksMovement))
            {
                var radiusPx = terrain.Radius * Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f);
                if (BoardGeometry.SegmentIntersectsCircle(actor.GlobalPosition, targetCenter, terrain.Position, radiusPx))
                    return true;
            }
        }
        return false;
    }

    internal bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        return _terrainManager.HasLineOfSight(from, to, Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f));
    }

    internal bool HasAnyLineOfSight(Squad attacker, Squad target)
    {
        var attackerActors = GetActorsForSquad(attacker);
        var targetActors = GetActorsForSquad(target);
        if (attackerActors.Count == 0 || targetActors.Count == 0)
        {
            return false;
        }

        return attackerActors.Any(actor => CanActorSeeAnyTargetActor(actor, targetActors, attacker, target));
    }

    internal bool CanActorSeeTargetSquad(BattleModelActor attackerActor, Squad attackerSquad, Squad targetSquad)
    {
        var targetActors = GetActorsForSquad(targetSquad);
        return CanActorSeeAnyTargetActor(attackerActor, targetActors, attackerSquad, targetSquad);
    }

    private bool CanActorSeeAnyTargetActor(BattleModelActor attackerActor, List<BattleModelActor> targetActors, Squad attackerSquad, Squad targetSquad)
    {
        if (attackerActor?.BoundModel == null || attackerActor.BoundModel.Health <= 0 || targetActors == null || targetActors.Count == 0)
            return false;

        return targetActors
            .Where(targetActor => targetActor?.BoundModel != null && targetActor.BoundModel.Health > 0)
            .Any(targetActor => HasLineOfSight(attackerActor, attackerSquad, targetActor, targetSquad));
    }

    internal bool HasLineOfSight(BattleModelActor attackerActor, Squad attackerSquad, BattleModelActor targetActor, Squad targetSquad)
    {
        if (attackerActor?.BoundModel == null || targetActor?.BoundModel == null || attackerSquad == null || targetSquad == null)
            return false;

        if (!HasLineOfSight(attackerActor.GlobalPosition, targetActor.GlobalPosition))
            return false;

        var pxPerInch = Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f);
        foreach (var squad in GetAliveSquadsForTeam(1).Concat(GetAliveSquadsForTeam(2)))
        {
            if (ReferenceEquals(squad, attackerSquad) || ReferenceEquals(squad, targetSquad))
                continue;

            var blockRadiusInches = GetSquadLosBlockRadiusInches(squad);
            if (blockRadiusInches <= 0f)
                continue;

            var blockRadiusPx = blockRadiusInches * pxPerInch;
            foreach (var blocker in GetActorsForSquad(squad))
            {
                if (blocker?.BoundModel == null || blocker.BoundModel.Health <= 0)
                    continue;

                if (BoardGeometry.SegmentIntersectsCircle(attackerActor.GlobalPosition, targetActor.GlobalPosition, blocker.GlobalPosition, blockRadiusPx))
                    return false;
            }
        }

        return true;
    }

    private static float GetSquadLosBlockRadiusInches(Squad squad)
    {
        var squadType = squad?.SquadType ?? string.Empty;
        var blockRadius = squadType.Contains("Fortification", StringComparison.OrdinalIgnoreCase) ? 2.4f
            : squadType.Contains("Monster", StringComparison.OrdinalIgnoreCase) ? 2.0f
            : squadType.Contains("Vehicle", StringComparison.OrdinalIgnoreCase) ? 1.6f
            : squadType.Contains("Character", StringComparison.OrdinalIgnoreCase) ? 1.2f
            : squadType.Contains("Mounted", StringComparison.OrdinalIgnoreCase) ? 0.8f
            : squadType.Contains("Infantry", StringComparison.OrdinalIgnoreCase) ? 0.4f
            : 0.4f;

        if (squadType.Contains("Titanic", StringComparison.OrdinalIgnoreCase))
            blockRadius *= 1.5f;

        return blockRadius;
    }

    internal void ApplyTerrainCoverAtCommandPhaseStart()
    {
        _terrainManager.ApplyTerrainCoverAtCommandPhaseStart(
            GetAliveSquadsForTeam(1).Concat(GetAliveSquadsForTeam(2)),
            squad => GetActorsForSquad(squad).Select(actor => actor.GlobalPosition),
            GD.Print);
    }

    private bool IsSquadInTerrainCover(Squad squad)
    {
        return _terrainManager.IsSquadInTerrainCover(
            GetActorsForSquad(squad).Select(actor => actor.GlobalPosition),
            Mathf.Max(1f, GameGlobals.Instance?.FakeInchPx ?? 1f));
    }

    private void OnNextPhasePressed()
        { _phaseRushTcs?.TrySetResult(true); }

    internal async Task WaitForPhaseRushAsync()
    {
        _phaseRushTcs = new TaskCompletionSource<bool>();
        await _phaseRushTcs.Task;
        _phaseRushTcs = null;
    }


    internal bool IsSquadInFightRange(Squad squad, int enemyTeamId)
    {
        if (squad == null)
            return false;

        var squadActors = GetActorsForSquad(squad);
        if (squadActors.Count == 0)
            return false;
        foreach (var enemy in GetAliveSquadsForTeam(enemyTeamId))
        {
            var enemyActors = GetActorsForSquad(enemy);
            if (enemyActors.Count == 0)
                continue;
            if (BoardGeometry.ClosestDistanceInches(squadActors, enemyActors) <= 1f)           
                return true;
        }
        return false;
    }

    private IEnumerable<Squad> GetFriendlyAuraSquads(Squad targetSquad)
        { return GetAliveSquadsForTeam(GetTeamIdForSquad(targetSquad)); }

    private IEnumerable<Squad> GetEnemyAuraSquads(Squad targetSquad)
    {
        var teamId = GetTeamIdForSquad(targetSquad);
        return GetAliveSquadsForTeam(teamId == 1 ? 2 : 1);
    }

    private int GetDamageResistanceAuraModifier(Squad targetSquad)
    {
        if (targetSquad == null)
            return 0;

        var hasAura = CombatHelpers.HasFriendlyAura(
            targetSquad,
            GetFriendlyAuraSquads(targetSquad),
            SquadAbilities.FreeHealthcare.Innate,
            6f,
            AreSquadsWithinDistance,
            includeSelf: true); //Make this less bespoke.

        return hasAura ? -1 : 0;
    }

    private void LogFreeHealthcareAuraDebugMatrix()
    {
        var allSquads = GetAliveSquadsForTeam(1).Concat(GetAliveSquadsForTeam(2)).ToList();
        var auraSources = allSquads
            .Where(squad => squad.SquadAbilities.Any(ability => ability.Innate == "FHC"))
            .ToList();

        if (auraSources.Count == 0)
        {
            return;
        }

        foreach (var target in allSquads)
        {
            var effectiveResist = CombatRolls.ResolveEffectiveDamageResistance(target);
            var sourceCount = auraSources
                .Count(source => GetTeamIdForSquad(source) == GetTeamIdForSquad(target)
                                 && AreSquadsWithinDistance(source, target, 6f));
            GD.Print($"[FHC Debug] target={target.Name}, team={GetTeamIdForSquad(target)}, in-range friendly sources={sourceCount}, modifier={(sourceCount > 0 ? -1 : 0)}, effective DR={effectiveResist}");
        }
    }

    internal bool AreSquadsWithinDistance(Squad first, Squad second, float distanceInches)
    {
        if (first == null || second == null)
            return false;

        var a = GetActorsForSquad(first);
        var b = GetActorsForSquad(second);
        if (a.Count == 0 || b.Count == 0)
            return false;

        return BoardGeometry.ClosestDistanceInches(a, b) <= distanceInches;
    }

    internal bool TryMoveSquadIntoEngagement(Squad mover, Squad target)
    {
        var moverActors = GetActorsForSquad(mover);
        var targetActors = GetActorsForSquad(target);
        if (moverActors.Count == 0 || targetActors.Count == 0)
            return false;

        return BoardGeometry.TryMoveIntoEngagement(moverActors, targetActors, _battleField).GetAwaiter().GetResult();
    }

    internal void SetSquadBackupForceVisual(Squad squad, bool inReserve)
        { _transportController.SetSquadBackupForceVisual(squad, inReserve); }

    internal async Task<bool> RedeployBackupForceSquadAsync(int teamId, Squad squad)
    {
        var previousActiveSquad = teamId == 1 ? _teamASquad : _teamBSquad;
        try
        { return await _transportController.RedeployBackupForceSquadAsync(teamId, squad); }
        finally
        { SetActiveSquadForTeam(teamId, previousActiveSquad); }

    }
    internal List<Squad> GetAliveSquadsForTeam(int teamId)
    {
        var player = teamId == 1 ? _teamAPlayer : _teamBPlayer;
        return player?.TheirSquads?
            .Where(s => s != null && s.Composition != null && s.Composition.Count > 0 && !IsSquadEmbarked(s) && !s.IsInBackupForce)
            .ToList() ?? new List<Squad>();
    }

    internal void SetActiveSquadForTeam(int teamId, Squad? squad)
    {
        if (teamId == 1)
            _teamASquad = squad;
        else
            _teamBSquad = squad;
    }

    internal bool IsTeamAI(int teamId)
    {
        var player = GetPlayerByTeam(teamId);
        return player?.IsAI == true;
    }

    internal BattleHud Hud => _battleHud;
    internal BattleField Field => _battleField;
    internal int ActiveTeamId
    {
        get => _activeTeamId;
        set
        {
            _activeTeamId = value;
            if (_dicePresenter != null)
                _dicePresenter.ActivePlayerTeamId = value;
        }
    }
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
            _teamAMove = new MoveVars(false, false, false);
        else
            _teamBMove = new MoveVars(false, false, false);
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
            PostDamageCleanupAndVictoryCheck();
    }

    internal int GetTeamIdForSquad(Squad squad)
    {
        if (_teamAPlayer?.TheirSquads?.Contains(squad) == true) return 1;
        if (_teamBPlayer?.TheirSquads?.Contains(squad) == true) return 2;
        return ReferenceEquals(squad, _teamASquad) ? 1 : 2;
    }

    internal void EndTurnAndQueueNext()
        { _sequence?.BeginTurn(); }
}
