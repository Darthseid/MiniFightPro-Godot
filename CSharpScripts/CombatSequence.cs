using Godot;
using System.Linq;
using System.Threading.Tasks;

public sealed class CombatSequence
{
    private readonly Battle _battle;

    public CombatSequence(Battle battle)
    {
        _battle = battle;
    }

    public void BeginTurn()
    {
        _battle.ActiveTeamId = _battle.CurrentTurn == 1
            ? _battle.StartingTeamId
            : (_battle.StartingTeamId == 1 ? 2 : 1);

        _battle.ResetMoveVarsForActiveTeam();
        _battle.ActiveSquadChargedThisTurn = false;
        _battle.ActiveSquadMovedAfterShootingThisTurn = false;
        _battle.SyncGlobalTurnRound();
        _battle.AnnounceTurnStart();
        _ = RunTurnAsync();
    }

    private async Task RunTurnAsync()
    {
        await _battle.DelaySecondsAsync(1f);
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Command);

        var activePlayer = _battle.ActiveTeamId == 1 ? _battle.TeamAPlayer : _battle.TeamBPlayer;
        var inactivePlayer = _battle.ActiveTeamId == 1 ? _battle.TeamBPlayer : _battle.TeamAPlayer;

        await StepChecks.RoundStartChecks(activePlayer, inactivePlayer, _battle.Hud);
        await StepChecks.CommandPhaseChecks(activePlayer, inactivePlayer, null, null, _battle.Hud);
        _battle.Hud?.ShowToast("Press ➡️ for Movement");
        await _battle.WaitForPhaseAdvanceAsync();

        await HandleMovementAsync();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        _battle.Hud?.ShowToast("Press ➡️ for Shooting");
        await _battle.WaitForPhaseAdvanceAsync();
        await HandleShootingAsync();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        _battle.Hud?.ShowToast("Press ➡️ for Charge");
        await _battle.WaitForPhaseAdvanceAsync();
        await HandleChargeAsync();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        _battle.Hud?.ShowToast("Press ➡️ for Fight");
        await _battle.WaitForPhaseAdvanceAsync();
        await HandleFightAsync();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        EndTurn();
    }

    private async Task HandleMovementAsync()
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Movement);
        var activeSquads = _battle.GetAliveSquadsForTeam(_battle.ActiveTeamId);
        var inactiveSquads = _battle.GetAliveSquadsForTeam(_battle.ActiveTeamId == 1 ? 2 : 1);
        if (inactiveSquads.Count == 0) return;

        foreach (var squad in activeSquads)
        {
            _battle.SetActiveSquadForTeam(_battle.ActiveTeamId, squad);
            var enemy = inactiveSquads.FirstOrDefault();
            if (_battle.ActiveTeamId == 1) _battle.SetActiveSquadForTeam(2, enemy);
            else _battle.SetActiveSquadForTeam(1, enemy);

            var wantsMove = await _battle.Hud.ConfirmActionAsync($"{squad.Name}: Move/Advance this phase?");
            if (!wantsMove) continue;

            var moveVars = await _battle.MovingStuff(squad.Movement, false, 1.05f, false, true, true, string.Empty, true);
            if (moveVars.Retreat && squad.ShellShock && !squad.SquadType.Contains("Titanic"))
            {
                _battle.ApplyRout(squad);
            }
            _battle.CheckVictory();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }
    }

    private async Task HandleShootingAsync()
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Shooting);
        var activeSquads = _battle.GetAliveSquadsForTeam(_battle.ActiveTeamId);
        var enemyTeamId = _battle.ActiveTeamId == 1 ? 2 : 1;

        foreach (var squad in activeSquads)
        {
            _battle.SetActiveSquadForTeam(_battle.ActiveTeamId, squad);
            var enemyOptions = _battle.GetAliveSquadsForTeam(enemyTeamId);
            if (enemyOptions.Count == 0) return;

            var wantsShoot = await _battle.Hud.ConfirmActionAsync($"{squad.Name}: Shoot this phase?");
            if (!wantsShoot) continue;

            var target = await _battle.PromptForEnemySquadTargetAsync($"{squad.Name}: Click enemy squad to shoot", enemyTeamId);
            if (target == null) continue;

            _battle.SetActiveSquadForTeam(enemyTeamId, target);

            await _battle.ResolveShootingPhase();
            _battle.CheckVictory();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }
    }

    private async Task HandleChargeAsync()
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Charge);
        var activeSquads = _battle.GetAliveSquadsForTeam(_battle.ActiveTeamId);
        var enemyTeamId = _battle.ActiveTeamId == 1 ? 2 : 1;

        foreach (var squad in activeSquads)
        {
            _battle.SetActiveSquadForTeam(_battle.ActiveTeamId, squad);
            var enemyOptions = _battle.GetAliveSquadsForTeam(enemyTeamId);
            if (enemyOptions.Count == 0) return;

            var wantsCharge = await _battle.Hud.ConfirmActionAsync($"{squad.Name}: Charge this phase?");
            if (!wantsCharge) continue;

            var target = await _battle.PromptForEnemySquadTargetAsync($"{squad.Name}: Click enemy squad to charge", enemyTeamId);
            if (target == null) continue;

            _battle.SetActiveSquadForTeam(enemyTeamId, target);

            var moved = await BoardGeometry.TryMoveIntoEngagement(_battle.GetActiveActors(), _battle.GetInactiveActors(), _battle.Field);
            if (moved)
            {
                _battle.ActiveSquadChargedThisTurn = true;
            }
        }
    }

    private async Task HandleFightAsync()
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Fight);
        var activeTeamId = _battle.ActiveTeamId;
        var inactiveTeamId = activeTeamId == 1 ? 2 : 1;

        var activeRucks = _battle.GetAliveSquadsForTeam(activeTeamId)
            .Where(s => BoardGeometry.ClosestDistanceInches(_battle.GetActorsForSquad(s), _battle.GetAliveSquadsForTeam(inactiveTeamId).SelectMany(es => _battle.GetActorsForSquad(es)).ToList()) <= 1f)
            .ToList();
        var inactiveRucks = _battle.GetAliveSquadsForTeam(inactiveTeamId)
            .Where(s => BoardGeometry.ClosestDistanceInches(_battle.GetActorsForSquad(s), _battle.GetAliveSquadsForTeam(activeTeamId).SelectMany(es => _battle.GetActorsForSquad(es)).ToList()) <= 1f)
            .ToList();

        int rounds = System.Math.Max(activeRucks.Count, inactiveRucks.Count);
        for (int i = 0; i < rounds; i++)
        {
            if (i < inactiveRucks.Count)
            {
                _battle.SetActiveSquadForTeam(inactiveTeamId, inactiveRucks[i]);
                _battle.SetActiveSquadForTeam(activeTeamId, activeRucks.FirstOrDefault() ?? activeRucks.LastOrDefault());
                var prev = _battle.ActiveTeamId;
                _battle.ActiveTeamId = inactiveTeamId;
                await _battle.ResolveFightPhase();
                _battle.ActiveTeamId = prev;
                _battle.CheckVictory();
                if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
            }

            if (i < activeRucks.Count)
            {
                _battle.SetActiveSquadForTeam(activeTeamId, activeRucks[i]);
                _battle.SetActiveSquadForTeam(inactiveTeamId, inactiveRucks.FirstOrDefault() ?? inactiveRucks.LastOrDefault());
                await _battle.ResolveFightPhase();
                _battle.CheckVictory();
                if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
            }
        }
    }

    private void EndTurn()
    {
        _battle.EnterPhase(BattlePhase.EndTurn);
        var nextTurn = _battle.CurrentTurn + 1;
        while (nextTurn > 2)
        {
            _battle.Round++;
            nextTurn -= 2;
        }

        _battle.CurrentTurn = nextTurn;
        _battle.SyncGlobalTurnRound();
        BeginTurn();
    }
}
