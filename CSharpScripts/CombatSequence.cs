using Godot;
using System.Collections.Generic;
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
        _battle.PostDamageCleanupAndVictoryCheck();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
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

            var moveVars = await _battle.MovingStuff(squad.Movement, false, 1.05f, true, true, true, string.Empty, true);
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

            var validShootingTargets = GetValidShootingTargets(enemyTeamId);
            if (validShootingTargets.Count == 0)
            {
                _battle.Hud?.ShowToast($"{squad.Name}: No valid shooting targets (enemy engaged in fight range).");
                continue;
            }

            var target = await _battle.PromptForEnemySquadTargetAsync(
                $"{squad.Name}: Click enemy squad to shoot",
                enemyTeamId,
                validShootingTargets
            );
            if (target == null) continue;

            _battle.SetActiveSquadForTeam(enemyTeamId, target);

            var selectedRangedProfile = await ChooseMultiProfileWeaponFingerprintAsync(squad, isMelee: false, "shoot");
            if (selectedRangedProfile == string.Empty)
            {
                continue;
            }

            await _battle.ResolveShootingPhase(selectedRangedProfile);
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

            var activeActors = _battle.GetActiveActors();
            var inactiveActors = _battle.GetInactiveActors();
            var distanceInches = BoardGeometry.ClosestDistanceInches(activeActors, inactiveActors);
            var moveVars = CombatHelpers.GetMoveVarsForTeam(_battle.ActiveTeamId, _battle.TeamAMove, _battle.TeamBMove);
            if (!ShapeHelpers.CanCharge(squad, moveVars, distanceInches))
            {
                _battle.Hud?.ShowToast("Charge not allowed (must be within 12\" and follow rules).");
                AudioManager.Instance?.Play("failedcharge");
                GD.Print($"[Rules] Charge blocked. Distance: {distanceInches:0.0}\" Squad: {squad.Name}.");
                continue;
            }

            var moved = await BoardGeometry.TryMoveIntoEngagement(activeActors, inactiveActors, _battle.Field);
            if (moved)
            {
                _battle.ActiveSquadChargedThisTurn = true;
                _battle.GrantTemporaryFirstStrike(squad);
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

        var activeFirstStrike = activeRucks.Where(_battle.SquadHasFirstStrike).ToList();
        var activeNormal = activeRucks.Where(s => !_battle.SquadHasFirstStrike(s)).ToList();
        var inactiveFirstStrike = inactiveRucks.Where(_battle.SquadHasFirstStrike).ToList();
        var inactiveNormal = inactiveRucks.Where(s => !_battle.SquadHasFirstStrike(s)).ToList();

        await ResolveFightTierAlternating(inactiveTeamId, activeTeamId, inactiveFirstStrike, activeFirstStrike);
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        await ResolveFightTierAlternating(inactiveTeamId, activeTeamId, inactiveNormal, activeNormal);
    }

    private async Task ResolveFightTierAlternating(int firstTeamId, int secondTeamId, System.Collections.Generic.List<Squad> firstTier, System.Collections.Generic.List<Squad> secondTier)
    {
        int rounds = System.Math.Max(firstTier.Count, secondTier.Count);
        for (int i = 0; i < rounds; i++)
        {
            if (i < firstTier.Count)
            {
                var actingSquad = firstTier[i];
                _battle.SetActiveSquadForTeam(firstTeamId, actingSquad);

                var validTargets = GetFightTargetsInRange(actingSquad, secondTeamId);
                if (validTargets.Count == 0)
                {
                    continue;
                }

                var targetSquad = await PickFightTargetAsync(actingSquad, secondTeamId, validTargets);
                if (targetSquad == null)
                {
                    continue;
                }

                _battle.SetActiveSquadForTeam(secondTeamId, targetSquad);
                var selectedMeleeProfile = await ChooseMultiProfileWeaponFingerprintAsync(actingSquad, isMelee: true, "fight");
                if (selectedMeleeProfile == string.Empty)
                {
                    continue;
                }

                var prev = _battle.ActiveTeamId;
                _battle.ActiveTeamId = firstTeamId;
                await _battle.ResolveFightPhase(selectedMeleeProfile);
                _battle.ActiveTeamId = prev;
                _battle.PostDamageCleanupAndVictoryCheck();
                if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
            }

            if (i < secondTier.Count)
            {
                var actingSquad = secondTier[i];
                _battle.SetActiveSquadForTeam(secondTeamId, actingSquad);

                var validTargets = GetFightTargetsInRange(actingSquad, firstTeamId);
                if (validTargets.Count == 0)
                {
                    continue;
                }

                var targetSquad = await PickFightTargetAsync(actingSquad, firstTeamId, validTargets);
                if (targetSquad == null)
                {
                    continue;
                }

                _battle.SetActiveSquadForTeam(firstTeamId, targetSquad);
                var selectedMeleeProfile = await ChooseMultiProfileWeaponFingerprintAsync(actingSquad, isMelee: true, "fight");
                if (selectedMeleeProfile == string.Empty)
                {
                    continue;
                }

                await _battle.ResolveFightPhase(selectedMeleeProfile);
                _battle.PostDamageCleanupAndVictoryCheck();
                if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
            }
        }
    }

    private System.Collections.Generic.List<Squad> GetFightTargetsInRange(Squad attackerSquad, int enemyTeamId)
    {
        var attackerActors = _battle.GetActorsForSquad(attackerSquad);
        return _battle.GetAliveSquadsForTeam(enemyTeamId)
            .Where(enemySquad => BoardGeometry.ClosestDistanceInches(attackerActors, _battle.GetActorsForSquad(enemySquad)) <= 1f)
            .ToList();
    }

    private List<Squad> GetValidShootingTargets(int enemyTeamId)
    {
        var enemySquads = _battle.GetAliveSquadsForTeam(enemyTeamId);
        var friendlySquads = _battle.GetAliveSquadsForTeam(_battle.ActiveTeamId);

        return enemySquads
            .Where(enemySquad =>
                friendlySquads.All(friendlySquad =>
                    !IsInFightRange(enemySquad, friendlySquad)))
            .ToList();
    }

    private bool IsInFightRange(Squad firstSquad, Squad secondSquad)
    {
        if (ReferenceEquals(firstSquad, secondSquad))
        {
            return false;
        }

        var firstActors = _battle.GetActorsForSquad(firstSquad);
        var secondActors = _battle.GetActorsForSquad(secondSquad);
        return BoardGeometry.ClosestDistanceInches(firstActors, secondActors) <= 1f;
    }

    private async Task<Squad?> PickFightTargetAsync(Squad actingSquad, int enemyTeamId, System.Collections.Generic.IReadOnlyCollection<Squad> validTargets)
    {
        if (validTargets.Count == 1)
        {
            return validTargets.First();
        }

        return await _battle.PromptForEnemySquadTargetAsync(
            $"{actingSquad.Name}: Click enemy squad to fight",
            enemyTeamId,
            validTargets
        );
    }

    private async Task<string?> ChooseMultiProfileWeaponFingerprintAsync(Squad squad, bool isMelee, string actionLabel)
    {
        var allPhaseWeapons = squad?.Composition
            ?.Where(model => model != null && model.Health > 0)
            .SelectMany(model => model.Tools)
            .Where(weapon => weapon != null && weapon.IsMelee == isMelee)
            .ToList() ?? new List<Weapon>();

        if (allPhaseWeapons.Count == 0)
        {
            return null;
        }

        var multiProfileGroups = allPhaseWeapons
            .Where(weapon => weapon.Special?.Any(ability => ability?.Innate == "MultiProfile") == true)
            .GroupBy(CombatEngine.GetWeaponFingerprint)
            .ToList();

        if (multiProfileGroups.Count <= 1)
        {
            return null;
        }

        var options = multiProfileGroups
            .Select(group => group.First())
            .OrderBy(weapon => weapon.WeaponName)
            .ThenBy(weapon => weapon.Range)
            .ToList();

        var optionLabels = options
            .Select(weapon => $"{weapon.WeaponName} (R:{weapon.Range:0.#} A:{weapon.Attacks} HS:{weapon.HitSkill}+ S:{weapon.Strength} AP:{weapon.ArmorPenetration} D:{weapon.Damage})")
            .ToList();

        var selectedIndex = await _battle.Hud.ChooseOptionAsync(
            $"{squad.Name}: choose Multi-profile weapon to {actionLabel}",
            optionLabels
        );

        if (selectedIndex < 0 || selectedIndex >= options.Count)
        {
            return string.Empty;
        }

        return CombatEngine.GetWeaponFingerprint(options[selectedIndex]);
    }

    private void EndTurn()
    {
        _battle.EnterPhase(BattlePhase.EndTurn);
        _battle.ClearTemporaryAbilitiesAndTurnFlags();
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
