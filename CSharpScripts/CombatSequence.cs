using Godot;
using System;
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
        _battle.OrderManager?.BeginTurn();
        _battle.SyncGlobalTurnRound();
        _battle.AnnounceTurnStart();
        _ = RunTurnAsync();
    }

    private async Task RunTurnAsync()
    {
        await _battle.DelaySecondsAsync(1f);
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Starting);

        var activeTeamIsAI = _battle.IsTeamAI(_battle.ActiveTeamId);
        await WaitForOrdersAtPhaseStartAsync(BattlePhase.Starting, activeTeamIsAI);
        var activePlayer = _battle.ActiveTeamId == 1 ? _battle.TeamAPlayer : _battle.TeamBPlayer;
        var inactivePlayer = _battle.ActiveTeamId == 1 ? _battle.TeamBPlayer : _battle.TeamAPlayer;

        _battle.ApplyTerrainCoverAtCommandPhaseStart();
        await StepChecks.RoundStartChecks(activePlayer, inactivePlayer, _battle.Hud, allowPlayerChoices: !activeTeamIsAI);
        await StepChecks.CommandPhaseChecks(activePlayer, inactivePlayer, null, null, _battle.Hud, allowPlayerChoices: !activeTeamIsAI);
        _battle.PostDamageCleanupAndVictoryCheck();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        await HandleMovementAsync(activeTeamIsAI);
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        await HandleShootingAsync(activeTeamIsAI);
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        await HandleChargeAsync(activeTeamIsAI);
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        await HandleFightAsync(activeTeamIsAI);
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        EndTurn();
    }


    private async Task WaitForOrdersAtPhaseStartAsync(BattlePhase phase, bool activeTeamIsAI)
    {
        if (activeTeamIsAI)
        {
            return;
        }

        _battle.OrderManager?.RefreshHud();
        _battle.Hud?.ShowToast($"Phase start: use Orders, then press ➡️ to continue {phase}.");
        await _battle.WaitForPhaseRushAsync();
    }

    private async Task HandleMovementAsync(bool activeTeamIsAI)
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Movement);
        _battle.OrderManager?.ResetPhaseUsage();
        _battle.OrderManager?.OpenWindow(OrderWindowType.StartOfMovementPhase, _battle.ActiveTeamId);
        await WaitForOrdersAtPhaseStartAsync(BattlePhase.Movement, activeTeamIsAI);
        _battle.OrderManager?.CloseWindow(OrderWindowType.StartOfMovementPhase);
        var activeTeamId = _battle.ActiveTeamId;
        var enemyTeamId = activeTeamId == 1 ? 2 : 1;
        await _battle.HandleTransportEmbarkDisembarkStepAsync(activeTeamId, activeTeamIsAI);
        var activeSquads = _battle.GetAliveSquadsForTeam(activeTeamId);
        var inactiveSquads = _battle.GetAliveSquadsForTeam(enemyTeamId);
        if (inactiveSquads.Count == 0)
        {
            return;
        }

        if (!activeTeamIsAI)
        {
            foreach (var squad in activeSquads)
            {
                _battle.SetActiveSquadForTeam(activeTeamId, squad);
                var enemy = inactiveSquads.FirstOrDefault();
                _battle.SetActiveSquadForTeam(enemyTeamId, enemy);

                var hasTeleport = SquadHasTeleportMove(squad);
                if (squad.Movement <= 0.01f && !hasTeleport)
                {
                    SetMoveVarsForActiveTeam(false, false, false);
                    continue;
                }

                var movementType = await ChooseMovementTypeAsync(squad, enemyTeamId, activeTeamIsAI);
                if (movementType == null)
                {
                    SetMoveVarsForActiveTeam(false, false, false);
                    continue;
                }

                var movementBonus = StepChecks.MovementPhaseChecks(squad);
                if (movementType == MovementType.Rush)
                {
                    var rushRoll = await DiceRoller.PresentAndRollAsync(
                        6,
                        1,
                        new RollContext(
                            RollPhase.Other,
                            "Rush",
                            AttackerName: squad.Name,
                            OwnerTeamId: activeTeamId));
                    movementBonus += rushRoll.Results.Sum();
                }

                var movementAllowance = Math.Max(0f, squad.Movement + movementBonus);
                var ignoreMaxDistance = hasTeleport && squad.Movement <= 0.01f;
                var moveVars = await _battle.MovingStuff(movementAllowance, ignoreMaxDistance, 1.05f, true, true, true, string.Empty, true);
                moveVars.Rush = movementType == MovementType.Rush;
                moveVars.Retreat = movementType == MovementType.Retreat;
                squad.RushedThisTurn = moveVars.Move && movementType == MovementType.Rush;
                squad.RetreatedThisTurn = moveVars.Move && movementType == MovementType.Retreat;

                if (moveVars.Retreat && squad.ShellShock && !squad.SquadType.Contains("Titanic"))
                {
                    _battle.ApplyRout(squad);
                }
                _battle.CheckVictory();
                if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
            }

            return;
        }

        var aiThreat = SimpleAIController.ComputeTeamMeleeThreatScore(activeSquads);
        var enemyThreat = SimpleAIController.ComputeTeamMeleeThreatScore(inactiveSquads);
        var aggressive = SimpleAIController.IsAggressive(aiThreat, enemyThreat);
        foreach (var squad in activeSquads)
        {
            _battle.SetActiveSquadForTeam(activeTeamId, squad);
            var closestEnemy = SimpleAIController.PickClosestEnemySquad(_battle, squad, enemyTeamId, inactiveSquads);
            if (closestEnemy == null)
            {
                continue;
            }

            _battle.SetActiveSquadForTeam(enemyTeamId, closestEnemy);
            var movers = _battle.GetActorsForSquad(squad);
            var targetActors = _battle.GetActorsForSquad(closestEnemy);

            bool moved;
            if (aggressive)
            {
                moved = await BoardGeometry.TryMoveSquadTowardTarget(movers, targetActors, _battle.Field, squad.Movement);
                SetMoveVarsForActiveTeam(moved, rush: false, retreat: false);
                squad.RushedThisTurn = false;
                squad.RetreatedThisTurn = false;
            }
            else
            {
                var shouldRetreat = !aggressive && _battle.GetAliveSquadsForTeam(activeTeamId).Count > 1;
                moved = await BoardGeometry.TryMoveSquadAwayFromTarget(movers, targetActors, _battle.Field, squad.Movement);
                SetMoveVarsForActiveTeam(moved, rush: false, retreat: shouldRetreat);
                squad.RushedThisTurn = false;
                squad.RetreatedThisTurn = moved && shouldRetreat;
                if (shouldRetreat && squad.ShellShock && !squad.SquadType.Contains("Titanic"))
                {
                    _battle.ApplyRout(squad);
                }
            }

            _battle.CheckVictory();
            if (_battle.CurrentPhase == BattlePhase.BattleOver)
            {
                return;
            }
        }
    }

    private async Task HandleShootingAsync(bool activeTeamIsAI)
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Shooting);
        var activeTeamId = _battle.ActiveTeamId;
        var defenderTeamId = activeTeamId == 1 ? 2 : 1;
        _battle.OrderManager?.ResetPhaseUsage();
        _battle.OrderManager?.OpenWindow(OrderWindowType.OpponentShootingPhaseStart, activeTeamId);
        await WaitForOrdersAtPhaseStartAsync(BattlePhase.Shooting, activeTeamIsAI);
        if (_battle.OrderManager != null)
        {
            await _battle.OrderManager.HandleMistsRedeployAtShootingStartAsync(activeTeamId);
        }
        if (!_battle.IsTeamAI(defenderTeamId))
        {
            await _battle.WaitForPhaseRushAsync();
        }
        _battle.OrderManager?.CloseWindow(OrderWindowType.OpponentShootingPhaseStart);
        var activeSquads = _battle.GetAliveSquadsForTeam(activeTeamId);
        var enemyTeamId = activeTeamId == 1 ? 2 : 1;

        foreach (var squad in activeSquads)
        {
            _battle.SetActiveSquadForTeam(activeTeamId, squad);
            SetMoveVarsForActiveTeam(squad.RushedThisTurn || squad.RetreatedThisTurn, squad.RushedThisTurn, squad.RetreatedThisTurn);
            var enemyOptions = _battle.GetAliveSquadsForTeam(enemyTeamId);
            if (enemyOptions.Count == 0) return;

            if (!_battle.SquadHasRangedWeaponThatCanShoot(squad, enemyTeamId))
            {
                continue;
            }

            if (!activeTeamIsAI)
            {
                var wantsShoot = await _battle.Hud.ConfirmActionAsync($"{squad.Name}: Shoot this phase?");
                if (!wantsShoot) continue;
            }

            var validShootingTargets = GetValidShootingTargets(squad, enemyTeamId);
            if (validShootingTargets.Count == 0)
            {
                _battle.Hud?.ShowToast($"{squad.Name}: No valid shooting targets.");
                continue;
            }

            Squad? target = activeTeamIsAI
                ? SimpleAIController.PickClosestEnemySquad(_battle, squad, enemyTeamId, validShootingTargets)
                : await _battle.PromptForEnemySquadTargetAsync(
                    $"{squad.Name}: Click enemy squad to shoot",
                    enemyTeamId,
                    validShootingTargets
                );

            if (target == null) continue;

            var hasLineOfSight = _battle.HasMajorityLineOfSight(squad, target);
            var hasIndirectOption = _battle.SquadHasIndirectFireWeaponThatCanShootTarget(squad, target);
            if (!hasLineOfSight && !hasIndirectOption)
            {
                _battle.Hud?.ShowToast("No line of sight: shooting invalid");
                GD.Print("[Terrain] No line of sight: shooting invalid");
                continue;
            }

            _battle.SetActiveSquadForTeam(enemyTeamId, target);

            _battle.OrderManager?.ResetPhaseUsage();
            _battle.OrderManager?.SetCurrentShootingDefender(target);
            _battle.OrderManager?.OpenWindow(OrderWindowType.OnTargetedByShooting, activeTeamId);
            if (!_battle.IsTeamAI(enemyTeamId))
            {
                await _battle.WaitForPhaseRushAsync();
            }
            _battle.OrderManager?.CloseWindow(OrderWindowType.OnTargetedByShooting);

            var selectedRangedProfile = await ChooseMultiProfileWeaponFingerprintAsync(squad, isMelee: false, "shoot", activeTeamIsAI);
            if (selectedRangedProfile == string.Empty)
            {
                continue;
            }

            var prevActiveTeamId = _battle.ActiveTeamId;
            _battle.ActiveTeamId = activeTeamId;
            var appliedIndirectCover = false;
            if (!hasLineOfSight && hasIndirectOption && target.SquadAbilities.All(a => a.Innate != SquadAbilities.CoverBenefitTemp.Innate))
            {
                target.SquadAbilities.Add(SquadAbilities.CoverBenefitTemp);
                appliedIndirectCover = true;
            }

            await _battle.ResolveShootingPhase(selectedRangedProfile, hasLineOfSight);

            if (appliedIndirectCover)
            {
                target.SquadAbilities.RemoveAll(a => a.IsTemporary && a.Innate == SquadAbilities.CoverBenefitTemp.Innate);
            }
            _battle.ActiveTeamId = prevActiveTeamId;
            _battle.CheckVictory();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }

        _battle.OrderManager?.ClearShootingPhaseTemporaryEffects();
    }

    private async Task HandleChargeAsync(bool activeTeamIsAI)
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Engagement);
        _battle.OrderManager?.ResetPhaseUsage();
        _battle.OrderManager?.OpenWindow(OrderWindowType.EngagementPhase, _battle.ActiveTeamId);
        _battle.OrderManager?.OpenWindow(OrderWindowType.OpponentEngagementPhaseStart, _battle.ActiveTeamId);
        await WaitForOrdersAtPhaseStartAsync(BattlePhase.Engagement, activeTeamIsAI);
        var activeTeamId = _battle.ActiveTeamId;
        var enemyTeamId = activeTeamId == 1 ? 2 : 1;
        var activeSquads = _battle.GetAliveSquadsForTeam(activeTeamId);
        var enemySquads = _battle.GetAliveSquadsForTeam(enemyTeamId);
        if (enemySquads.Count == 0)
        {
            return;
        }

        var aiAggressive = true;
        if (activeTeamIsAI)
        {
            var aiThreat = SimpleAIController.ComputeTeamMeleeThreatScore(activeSquads);
            var enemyThreat = SimpleAIController.ComputeTeamMeleeThreatScore(enemySquads);
            aiAggressive = SimpleAIController.IsAggressive(aiThreat, enemyThreat);
        }

        foreach (var squad in activeSquads)
        {
            _battle.SetActiveSquadForTeam(activeTeamId, squad);
            var enemyOptions = _battle.GetAliveSquadsForTeam(enemyTeamId);
            if (enemyOptions.Count == 0) return;

            if (activeTeamIsAI && !aiAggressive)
            {
                continue;
            }

            if (!CanSquadAttemptCharge(squad))
            {
                if (!activeTeamIsAI)
                {
                    _battle.Hud?.ShowToast($"{squad.Name} cannot charge this turn.");
                }

                continue;
            }

            if (!activeTeamIsAI)
            {
                var wantsCharge = await _battle.Hud.ConfirmActionAsync($"{squad.Name}: Charge this phase?");
                if (!wantsCharge) continue;
            }

            var validChargeTargets = enemyOptions
                .Where(target => ShapeHelpers.CanDeclareChargeTarget(squad, target))
                .ToList();
            if (validChargeTargets.Count == 0)
            {
                if (!activeTeamIsAI)
                {
                    _battle.Hud?.ShowToast($"{squad.Name}: No valid charge targets.");
                }

                continue;
            }

            Squad? target = activeTeamIsAI
                ? SimpleAIController.PickClosestEnemySquad(_battle, squad, enemyTeamId, validChargeTargets)
                : await _battle.PromptForEnemySquadTargetAsync($"{squad.Name}: Click enemy squad to charge", enemyTeamId, validChargeTargets);
            if (target == null) continue;

            if (!ShapeHelpers.CanDeclareChargeTarget(squad, target))
            {
                if (!activeTeamIsAI)
                {
                    _battle.Hud?.ShowToast("Only units with Fly can charge Aircraft.");
                }

                continue;
            }

            _battle.SetActiveSquadForTeam(enemyTeamId, target);

            var activeActors = _battle.GetActiveActors();
            var inactiveActors = _battle.GetInactiveActors();
            var distanceInches = BoardGeometry.ClosestDistanceInches(activeActors, inactiveActors);
            var moveVars = new MoveVars(squad.RushedThisTurn || squad.RetreatedThisTurn, squad.RushedThisTurn, squad.RetreatedThisTurn);
            if (squad.CannotChargeThisTurn)
            {
                if (!activeTeamIsAI)
                {
                    _battle.Hud?.ShowToast($"{squad.Name} cannot charge this turn.");
                }

                continue;
            }

            if (_battle.IsChargeBlockedByTerrain(squad, target))
            {
                if (!activeTeamIsAI)
                {
                    _battle.Hud?.ShowToast("Charge blocked by terrain");
                    AudioManager.Instance?.Play("failedcharge");
                }

                GD.Print("[Terrain] Charge blocked by terrain");
                continue;
            }

            if (!ShapeHelpers.CanCharge(squad, moveVars, distanceInches))
            {
                if (!activeTeamIsAI)
                {
                    _battle.Hud?.ShowToast("Charge not allowed (must be within 12\" and follow rules).");
                    AudioManager.Instance?.Play("failedcharge");
                }

                GD.Print($"[Rules] Charge blocked. Distance: {distanceInches:0.0}\" Squad: {squad.Name}.");
                continue;
            }

            var chargeModifier = await StepChecks.EngagementPhaseChecks(squad, target, _battle.ActiveSquadMovedAfterShootingThisTurn);
            var chargeRoll = await _battle.RollInteractiveAsync(
                2,
                6,
                "Charge Roll (2D6)",
                activeTeamId,
                attackerName: squad.Name,
                defenderName: target.Name,
                phase: RollPhase.Other);
            var chargeTotal = chargeRoll.Results.Sum() + chargeModifier;
            if (chargeTotal < distanceInches)
            {
                if (!activeTeamIsAI)
                {
                    _battle.Hud?.ShowToast($"Charge failed ({chargeTotal:0.#}\" < {distanceInches:0.#}\").");
                    AudioManager.Instance?.Play("failedcharge");
                }

                continue;
            }

            if (_battle.OrderManager != null)
            {
                await _battle.OrderManager.TryResolveOverwatchOnChargeDeclaredAsync(activeTeamId, squad, target);
            }
            var moved = await BoardGeometry.TryMoveIntoEngagement(activeActors, inactiveActors, _battle.Field);
            if (moved)
            {
                _battle.ActiveSquadChargedThisTurn = true;
                _battle.GrantTemporaryFirstStrike(squad);
            }
        }

        _battle.OrderManager?.CloseWindow(OrderWindowType.EngagementPhase);
        _battle.OrderManager?.CloseWindow(OrderWindowType.OpponentEngagementPhaseStart);
    }

    private async Task HandleFightAsync(bool activeTeamIsAI)
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Melee);
        _battle.OrderManager?.ResetPhaseUsage();
        _battle.OrderManager?.OpenWindow(OrderWindowType.StartOfMeleePhase, _battle.ActiveTeamId);
        await WaitForOrdersAtPhaseStartAsync(BattlePhase.Melee, activeTeamIsAI);
        var activeTeamId = _battle.ActiveTeamId;
        var inactiveTeamId = activeTeamId == 1 ? 2 : 1;

        var heroicEnemy = _battle.GetAliveSquadsForTeam(activeTeamId)
            .FirstOrDefault(enemy => _battle.GetAliveSquadsForTeam(inactiveTeamId)
                .Any(friend => _battle.AreSquadsWithinDistance(friend, enemy, 1f)));
        _battle.OrderManager?.ConfigureHeroicInterventionEnemy(inactiveTeamId, heroicEnemy);
        if (!_battle.IsTeamAI(inactiveTeamId) && heroicEnemy != null)
        {
            await _battle.WaitForPhaseRushAsync();
        }

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

        await ResolveFightTierAlternating(inactiveTeamId, activeTeamId, inactiveFirstStrike, activeFirstStrike, activeTeamIsAI);
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        await ResolveFightTierAlternating(inactiveTeamId, activeTeamId, inactiveNormal, activeNormal, activeTeamIsAI);
        _battle.OrderManager?.CloseWindow(OrderWindowType.StartOfMeleePhase);
        _battle.OrderManager?.EndFightPhaseCleanup();
    }

    private async Task ResolveFightTierAlternating(int firstTeamId, int secondTeamId, System.Collections.Generic.List<Squad> firstTier, System.Collections.Generic.List<Squad> secondTier, bool activeTeamIsAI)
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

                var targetSquad = await PickFightTargetAsync(actingSquad, secondTeamId, validTargets, _battle.IsTeamAI(firstTeamId));
                if (targetSquad == null)
                {
                    continue;
                }

                _battle.SetActiveSquadForTeam(secondTeamId, targetSquad);
                var selectedMeleeProfile = await ChooseMultiProfileWeaponFingerprintAsync(actingSquad, isMelee: true, "fight", _battle.IsTeamAI(firstTeamId));
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

                var targetSquad = await PickFightTargetAsync(actingSquad, firstTeamId, validTargets, _battle.IsTeamAI(secondTeamId));
                if (targetSquad == null)
                {
                    continue;
                }

                _battle.SetActiveSquadForTeam(firstTeamId, targetSquad);
                var selectedMeleeProfile = await ChooseMultiProfileWeaponFingerprintAsync(actingSquad, isMelee: true, "fight", _battle.IsTeamAI(secondTeamId));
                if (selectedMeleeProfile == string.Empty)
                {
                    continue;
                }

                var prev = _battle.ActiveTeamId;
                _battle.ActiveTeamId = secondTeamId;
                await _battle.ResolveFightPhase(selectedMeleeProfile);
                _battle.ActiveTeamId = prev;
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

    private List<Squad> GetValidShootingTargets(Squad attackerSquad, int enemyTeamId)
    {
        var enemySquads = _battle.GetAliveSquadsForTeam(enemyTeamId);
        var friendlySquads = _battle.GetAliveSquadsForTeam(_battle.ActiveTeamId);

        return enemySquads
            .Where(enemySquad =>
                friendlySquads.All(friendlySquad =>
                    !IsInFightRange(enemySquad, friendlySquad)))
            .Where(enemySquad =>
            {
                var hasLos = _battle.HasMajorityLineOfSight(attackerSquad, enemySquad);
                return hasLos || _battle.SquadHasIndirectFireWeaponThatCanShootTarget(attackerSquad, enemySquad);
            })
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

    private async Task<Squad?> PickFightTargetAsync(Squad actingSquad, int enemyTeamId, System.Collections.Generic.IReadOnlyCollection<Squad> validTargets, bool actingTeamIsAI)
    {
        if (actingTeamIsAI)
        {
            return SimpleAIController.PickClosestEnemySquad(_battle, actingSquad, enemyTeamId, validTargets);
        }

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

    private async Task<string?> ChooseMultiProfileWeaponFingerprintAsync(Squad squad, bool isMelee, string actionLabel, bool actingTeamIsAI)
    {
        var allPhaseWeapons = CombatEngine.GetEffectiveWeaponsForPhase(squad, isMelee)
            .Where(weapon => weapon != null && weapon.IsMelee == isMelee)
            .ToList();

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

        if (actingTeamIsAI)
        {
            return CombatEngine.GetWeaponFingerprint(options.First());
        }

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

    private void SetMoveVarsForActiveTeam(bool moved, bool retreat)
    {
        SetMoveVarsForActiveTeam(moved, false, retreat);
    }

    private void SetMoveVarsForActiveTeam(bool moved, bool rush, bool retreat)
    {
        var moveVars = new MoveVars(moved, rush, retreat);
        if (_battle.ActiveTeamId == 1)
        {
            _battle.TeamAMove = moveVars;
        }
        else
        {
            _battle.TeamBMove = moveVars;
        }
    }

    private async Task<MovementType?> ChooseMovementTypeAsync(Squad squad, int enemyTeamId, bool actingTeamIsAI)
    {
        if (squad == null)
        {
            return null;
        }

        if (SquadHasTeleportMove(squad) || squad.SquadType.Contains("Aircraft"))
        {
            return MovementType.Standard;
        }

        if (squad.Movement <= 0.01f)
        {
            return null;
        }

        var startsInFightRange = _battle.IsSquadInFightRange(squad, enemyTeamId);
        if (actingTeamIsAI)
        {
            return startsInFightRange ? MovementType.Retreat : MovementType.Standard;
        }

        if (startsInFightRange)
        {
            var retreatChoice = await _battle.Hud.ChooseOptionAsync(
                $"{squad.Name}: In Fight Range. Choose movement.",
                new[] { "Retreat", "Stay" });
            return retreatChoice == 0 ? MovementType.Retreat : null;
        }

        var moveChoice = await _battle.Hud.ChooseOptionAsync(
            $"{squad.Name}: Choose movement type.",
            new[] { "Standard Move", "Rush", "Skip" });
        return moveChoice switch
        {
            0 => MovementType.Standard,
            1 => MovementType.Rush,
            _ => null
        };
    }

    private static bool CanSquadAttemptCharge(Squad squad)
    {
        if (squad == null || squad.CannotChargeThisTurn || squad.RushedThisTurn || squad.RetreatedThisTurn)
        {
            return false;
        }

        if (squad.SquadType.Contains("Aircraft"))
        {
            return false;
        }

        if (squad.SquadType.Contains("Fortification") || squad.Movement <= 0.01f)
        {
            return false;
        }

        return true;
    }

    private static bool SquadHasTeleportMove(Squad squad)
    {
        return squad?.SquadAbilities?.Any(ability => ability?.Innate == "Tele") == true;
    }

    private void EndTurn()
    {
        _battle.EnterPhase(BattlePhase.EndTurn);
        _battle.ClearTemporaryAbilitiesAndTurnFlags();
        foreach (var squad in _battle.GetAliveSquadsForTeam(1).Concat(_battle.GetAliveSquadsForTeam(2)))
        {
            squad.CannotChargeThisTurn = false;
        }
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
