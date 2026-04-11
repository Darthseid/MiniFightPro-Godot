using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class TransportController
{
    private readonly Func<int, List<Squad>> _getAliveSquadsForTeam;
    private readonly Func<Squad, List<BattleModelActor>> _getActorsForSquad;
    private readonly Func<Squad, int> _getTeamIdForSquad;
    private readonly Func<Squad, int, bool> _squadInFightRangeOfEnemy;
    private readonly Func<string, int, IReadOnlyCollection<Squad>, Task<Squad?>> _promptForSquadTargetAsync;
    private readonly Func<string, Task<bool>> _confirmAsync;
    private readonly Action<string> _showToast;
    private readonly Action<Squad, bool> _applyRout;
    private readonly Action<int, Squad?> _setActiveSquadForTeam;
    private readonly Func<int, Squad, Task<MoveVars>> _redeployMoveAsync;
    private readonly BattleField _battleField;
    private readonly Dictionary<Squad, Vector2> _lastKnownTransportCenters = new();

    public TransportController(
        BattleField battleField,
        Func<int, List<Squad>> getAliveSquadsForTeam,
        Func<Squad, List<BattleModelActor>> getActorsForSquad,
        Func<Squad, int> getTeamIdForSquad,
        Func<Squad, int, bool> squadInFightRangeOfEnemy,
        Func<string, int, IReadOnlyCollection<Squad>, Task<Squad?>> promptForSquadTargetAsync,
        Func<string, Task<bool>> confirmAsync,
        Action<string> showToast,
        Action<Squad, bool> applyRout,
        Action<int, Squad?> setActiveSquadForTeam,
        Func<int, Squad, Task<MoveVars>> redeployMoveAsync)
    {
        _battleField = battleField;
        _getAliveSquadsForTeam = getAliveSquadsForTeam;
        _getActorsForSquad = getActorsForSquad;
        _getTeamIdForSquad = getTeamIdForSquad;
        _squadInFightRangeOfEnemy = squadInFightRangeOfEnemy;
        _promptForSquadTargetAsync = promptForSquadTargetAsync;
        _confirmAsync = confirmAsync;
        _showToast = showToast;
        _applyRout = applyRout;
        _setActiveSquadForTeam = setActiveSquadForTeam;
        _redeployMoveAsync = redeployMoveAsync;
    }

    public async Task HandleTransportEmbarkDisembarkStepAsync(int activeTeamId, bool activeTeamIsAI)
    {
        if (activeTeamIsAI)
            return;

        var enemyTeamId = activeTeamId == 1 ? 2 : 1;
        var activeSquads = _getAliveSquadsForTeam(activeTeamId);
        var transports = activeSquads.Where(s => s.IsTransport()).ToList();

        foreach (var transport in transports.Where(t => t.EmbarkedSquad != null))
        {
            var wantsDisembark = await _confirmAsync($"{transport.Name}: Disembark {transport.EmbarkedSquad.Name}?");
            if (!wantsDisembark)
                continue;

            if (!TryDisembarkSquad(transport, false))
                _showToast($"{transport.Name}: could not disembark now.");
        }

        foreach (var transport in transports.Where(t => t.EmbarkedSquad == null))
        {
            var candidates = activeSquads
                .Where(s => !ReferenceEquals(s, transport) && s.IsEmbarkEligiblePassenger())
                .Where(s => BoardGeometry.ClosestDistanceInches(_getActorsForSquad(transport), _getActorsForSquad(s)) <= 3f)
                .ToList();
            if (candidates.Count == 0)
                continue;

            var wantsEmbark = await _confirmAsync($"{transport.Name}: Embark a nearby squad?");
            if (!wantsEmbark)
                continue;

            if (_squadInFightRangeOfEnemy(transport, enemyTeamId))
            {
                _showToast($"{transport.Name} cannot embark while in fight range.");
                continue;
            }

            var passenger = await _promptForSquadTargetAsync($"{transport.Name}: Click friendly squad to embark", activeTeamId, candidates);
            if (passenger == null)
                continue;

            if (_squadInFightRangeOfEnemy(passenger, enemyTeamId))
            {
                _showToast($"{passenger.Name} cannot embark while in fight range.");
                continue;
            }

            if (!TryEmbarkSquad(transport, passenger))
            {
                _showToast($"{passenger.Name} could not embark {transport.Name}.");
                continue;
            }

            _showToast($"{passenger.Name} embarked {transport.Name}.");
        }
    }

    public async Task<bool> RedeployBackupForceSquadAsync(int teamId, Squad squad)
    {
        var enemyTeamId = teamId == 1 ? 2 : 1;
        var enemyActors = _getAliveSquadsForTeam(enemyTeamId).SelectMany(_getActorsForSquad).ToList();
        _setActiveSquadForTeam(teamId, squad);
        SetSquadBackupForceVisual(squad, false);

        var squadActors = _getActorsForSquad(squad);
        if (squadActors.Count > 0)
        {
            var viewportCenter = _battleField.ToGlobal(_battleField.GetViewportRect().Size * 0.5f);
            var spacing = Mathf.Max(24f, GameGlobals.Instance?.FakeInchPx ?? 24f);
            for (int i = 0; i < squadActors.Count; i++)
            {
                var offset = new Vector2((i % 5 - 2) * spacing, (i / 5) * spacing * 0.8f);
                squadActors[i].GlobalPosition = viewportCenter + offset;
                squadActors[i].Visible = true;
            }
        }

        while (true)
        {
            await _redeployMoveAsync(teamId, squad);
            var placedActors = _getActorsForSquad(squad);
            var closestEnemyDistanceInches = BoardGeometry.ClosestDistanceInches(placedActors, enemyActors);
            if (enemyActors.Count == 0 || closestEnemyDistanceInches > 9f)
                return true;

            _showToast("Reserve redeploy must end more than 9\" away from enemies.");
            var reattempt = await _confirmAsync("Invalid placement. Reattempt redeploy placement?");
            if (!reattempt)
            {
                SetSquadBackupForceVisual(squad, true);
                return false;
            }
            SetSquadBackupForceVisual(squad, false);
        }
    }

    public bool TryEmbarkSquad(Squad transport, Squad passenger)
    {
        if (!transport.IsTransport() || transport.EmbarkedSquad != null || !passenger.IsEmbarkEligiblePassenger())
            return false;

        var transportTeamId = _getTeamIdForSquad(transport);
        var enemyTeamId = transportTeamId == 1 ? 2 : 1;
        if (_squadInFightRangeOfEnemy(transport, enemyTeamId) || _squadInFightRangeOfEnemy(passenger, enemyTeamId))
            return false;

        var transportActors = _getActorsForSquad(transport);
        var passengerActors = _getActorsForSquad(passenger);
        if (transportActors.Count == 0 || passengerActors.Count == 0)
            return false;

        if (BoardGeometry.ClosestDistanceInches(transportActors, passengerActors) > 3f)
            return false;

        transport.EmbarkedSquad = passenger;
        passenger.TransportedBy = transport;
        SetSquadActorsEmbarkedVisualState(passenger, true);
        return true;
    }

    public bool TryDisembarkSquad(Squad transport, bool emergency)
    {
        var passenger = transport?.EmbarkedSquad;
        if (transport == null || passenger == null)
            return false;

        var transportTeamId = _getTeamIdForSquad(transport);
        var enemyTeamId = transportTeamId == 1 ? 2 : 1;
        var transportActors = _getActorsForSquad(transport);
        var passengerActors = _getActorsForSquad(passenger);
        var enemyActors = _getAliveSquadsForTeam(enemyTeamId).SelectMany(_getActorsForSquad).ToList();
        if (passengerActors.Count == 0)
            return false;

        SetSquadActorsEmbarkedVisualState(passenger, false);
        bool placed;
        if (transportActors.Count > 0)
        {
            placed = BoardGeometry.PlacePassengerSquadAroundTransport(transportActors, passengerActors, emergency ? 6f : 3f, enemyActors, true)
                     || BoardGeometry.PlacePassengerSquadAroundTransport(transportActors, passengerActors, emergency ? 6f : 3f, enemyActors, false);
            if (placed)
                _lastKnownTransportCenters[transport] = BoardGeometry.GetActorsCenter(transportActors);
        }
        else if (emergency && _lastKnownTransportCenters.TryGetValue(transport, out var lastCenter))
        {
            placed = BoardGeometry.PlacePassengerSquadAroundPoint(lastCenter, passengerActors, 6f, enemyActors, true)
                     || BoardGeometry.PlacePassengerSquadAroundPoint(lastCenter, passengerActors, 6f, enemyActors, false);
        }
        else
            return false;

        transport.EmbarkedSquad = null;
        passenger.TransportedBy = null;
        _lastKnownTransportCenters.Remove(transport);

        if (emergency)
        {
            passenger.ShellShock = true;
            AudioManager.Instance?.Play("failedbravery"); //Consider changing this sound effect to a terrified scream.
            _showToast($"{passenger.Name} became shell-shocked.");
            _applyRout(passenger, false);
        }
        return true;
    }

    public void SetSquadActorsEmbarkedVisualState(Squad squad, bool embarked)
    {
        foreach (var actor in _getActorsForSquad(squad))
        {
            actor.Visible = !embarked;
            var clickArea = actor.GetNodeOrNull<Area2D>("ClickArea");
            if (clickArea != null)
                clickArea.InputPickable = !embarked;

            var collision = actor.GetNodeOrNull<CollisionShape2D>("ClickArea/CollisionShape2D");
            if (collision != null)
                collision.Disabled = embarked;
        }
    }

    public void SetSquadBackupForceVisual(Squad squad, bool inReserve)
    {
        foreach (var actor in _getActorsForSquad(squad))
            actor.Visible = !inReserve;
    }
}
