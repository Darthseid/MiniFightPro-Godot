using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class MovementManager
{
    private readonly Func<MoveVars> _getActiveMoveVars;
    private readonly Action<bool> _setActiveMoveFlag;

    private readonly Dictionary<BattleModelActor, Vector2> _movementStartPositions = new();
    private float _movementAllowanceInches;
    private bool _movementIgnoresMaxLimit;
    private float _movementEnemyBufferInches = 1.05f;
    private bool _enforceAircraftMinMove;
    private bool _movementUpdatesMoveVars = true;
    private bool _movementAllowsTeleport;
    private bool _requiresMovementRetry;
    private bool _awaitingMovement;
    private TaskCompletionSource<bool>? _movementTcs;

    public MovementManager(Func<MoveVars> getActiveMoveVars, Action<bool> setActiveMoveFlag)
    {
        _getActiveMoveVars = getActiveMoveVars;
        _setActiveMoveFlag = setActiveMoveFlag;
    }

    public float MovementAllowanceInches => _movementAllowanceInches;
    public float MovementEnemyBufferInches => _movementEnemyBufferInches;
    public bool MovementIgnoresMaxLimit => _movementIgnoresMaxLimit;
    public bool MovementAllowsTeleport => _movementAllowsTeleport;
    public bool RequiresMovementRetry => _requiresMovementRetry;
    public bool IsAwaitingMovement => _awaitingMovement;

    public void SetMovementUpdatesMoveVars(bool updates)
    {
        _movementUpdatesMoveVars = updates;
    }

    public void PrepareMovement(Squad activeSquad, IEnumerable<BattleModelActor> activeActors, IEnumerable<BattleModelActor> inactiveActors, float movementAllowanceInches, bool ignoreMaxDistance, float enemyBufferInches, bool enforceAircraftMinMove)
    {
        _movementStartPositions.Clear();
        foreach (var actor in activeActors)
        {
            _movementStartPositions[actor] = actor.GlobalPosition;
        }

        _movementAllowanceInches = movementAllowanceInches;
        _movementIgnoresMaxLimit = ignoreMaxDistance;
        _movementEnemyBufferInches = enemyBufferInches;
        _enforceAircraftMinMove = enforceAircraftMinMove;
        _requiresMovementRetry = false;

        _movementAllowsTeleport = activeSquad?.SquadAbilities?.Any(ability => ability?.Innate == "Tele") == true;
        if (_movementAllowsTeleport)
        {
            _movementEnemyBufferInches = 0f;
        }
    }

    public async Task<MoveVars> StartMovementAsync(string prompt, bool autoMove, Func<Task<bool>> confirmAsync, Action<string, float?> showToast, Action<IEnumerable<BattleModelActor>, bool> setSelectable, Func<MoveVars> getActiveMoveVars, Action playMoveSound)
    {
        var wantsMove = autoMove;
        if (!autoMove)
        {
            wantsMove = await confirmAsync();
        }

        if (!wantsMove)
        {
            return _getActiveMoveVars();
        }

        _awaitingMovement = true;
        setSelectable(_movementStartPositions.Keys, true);
        _movementTcs = new TaskCompletionSource<bool>();
        showToast("Drag your squad to move.", null);
        await _movementTcs.Task;

        if (_movementAllowsTeleport)
        {
            GD.Print("[Rules] Teleport movement validation applied for this squad.");
        }

        return getActiveMoveVars();
    }

    public MoveVars FinishMovement(bool didAttemptMove, Func<IEnumerable<BattleModelActor>> getActiveActors, Func<Squad> getActiveSquad, Func<IEnumerable<BattleModelActor>> getInactiveActors, Action<string> showToast, Action<string> log)
    {
        var activeActors = getActiveActors().ToList();
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
        var activeSquad = getActiveSquad();

        if (_movementAllowsTeleport && didMove)
        {
            var enemyActors = getInactiveActors().ToList();
            var closestEnemyDistanceInches = BoardGeometry.ClosestDistanceInches(activeActors, enemyActors);
            const float minTeleportEnemyDistanceInches = 9f;
            if (enemyActors.Count > 0 && closestEnemyDistanceInches <= minTeleportEnemyDistanceInches)
            {
                RevertTrackedActors(activeActors);
                didMove = false;
                showToast($"Teleport must end more than {minTeleportEnemyDistanceInches:0.#}\" away from enemies.");
                log($"[Rules] Blocked teleport move within {minTeleportEnemyDistanceInches}\" of enemies (closest: {closestEnemyDistanceInches:0.0}\").");
            }
        }

        if (didMove)
        {
            var moveSound = activeSquad?.SquadType.Contains("Mounted") == true ? "motorcycle" : "moved";
            AudioManager.Instance?.Play(moveSound);
        }

        if (_enforceAircraftMinMove && activeSquad?.SquadType?.Contains("Aircraft") == true && maxMoved < 20f * GameGlobals.Instance.FakeInchPx)
        {
            RevertTrackedActors(activeActors);
            didMove = false;
            _requiresMovementRetry = true;
            showToast($"Aircraft must move at least 20\" ({movedInches:0.0}\").");
            log($"[Rules] Blocked aircraft move under 20\" (attempted {movedInches:0.0}\").");
        }

        if (_movementUpdatesMoveVars)
        {
            _setActiveMoveFlag(didMove);
        }

        return _getActiveMoveVars();
    }

    public bool TryGetStartPosition(BattleModelActor actor, out Vector2 startPosition)
    {
        return _movementStartPositions.TryGetValue(actor, out startPosition);
    }

    public void ResolveAwaitingMovement(bool moved)
    {
        _awaitingMovement = false;
        _movementTcs?.TrySetResult(moved);
    }

    public void RevertTrackedActors(IEnumerable<BattleModelActor> actors)
    {
        foreach (var actor in actors)
        {
            if (_movementStartPositions.TryGetValue(actor, out var startPos))
            {
                actor.GlobalPosition = startPos;
            }
        }
    }
}
