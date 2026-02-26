using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public sealed class CombatSequence
{
    private readonly Battle _battle;

    public CombatSequence(Battle battle)
    {
        _battle = battle;
    }

    private float CurrentBattleDistance
    {
        get => GameGlobals.Instance?.CurrentBattleDistance ?? 0f;
        set
        {
            if (GameGlobals.Instance != null)
            {
                GameGlobals.Instance.CurrentBattleDistance = value;
            }
        }
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
        await _battle.DelaySecondsAsync(2f);
        if (_battle.ActiveTeamId == 1)
        {
            await StepChecks.RoundStartChecks(_battle.TeamASquad, _battle.TeamBSquad, _battle.Hud);
        }

        await RunCommandPhaseAsync();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        await HandleMovementAsync();
        if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

        if (!ShouldSkipShootingPhase())
        {
            await HandleShootingAsync();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }

        if (!ShouldSkipChargePhase())
        {
            await HandleChargeAsync();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }

        if (!ShouldSkipFightPhase())
        {
            await HandleFightAsync();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }

        EndTurn();
    }

    private async Task RunCommandPhaseAsync()
    {
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Command);
        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        var enemySquad = CombatHelpers.GetInactiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (activeSquad == null || enemySquad == null) return;

        activeSquad.ShellShock = false;
        _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
        await StepChecks.CommandPhaseChecks(activeSquad, enemySquad, _battle.Hud);
        if (activeSquad.Composition.Count < activeSquad.StartingModelSize / 2f)
        {
            activeSquad.ShellShock = StepChecks.ShellShockTest(activeSquad, enemySquad.SquadAbilities);
        }
    }

    private bool ActiveSquadCanShoot()
    {
        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        var enemySquad = CombatHelpers.GetInactiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (activeSquad == null || enemySquad == null) return false;

        var moveVars = CombatHelpers.GetActiveMoveVars(_battle.ActiveTeamId, _battle.TeamAMove, _battle.TeamBMove);
        var rangedWeapons = activeSquad.Composition.SelectMany(model => model.Tools).Where(weapon => weapon != null && !weapon.IsMelee).ToList();
        return rangedWeapons.Any(weapon => CombatHelpers.CheckValidShooting(activeSquad, moveVars, weapon, enemySquad));
    }

    private bool ShouldSkipShootingPhase() => !ActiveSquadCanShoot();

    private bool ShouldSkipChargePhase()
    {
        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (activeSquad == null || _battle.ActiveSquadMovedAfterShootingThisTurn) return true;

        _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
        var moveVars = CombatHelpers.GetActiveMoveVars(_battle.ActiveTeamId, _battle.TeamAMove, _battle.TeamBMove);
        return !ShapeHelpers.CanCharge(activeSquad, moveVars);
    }

    private bool ShouldSkipFightPhase()
    {
        _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
        return CurrentBattleDistance > 1f;
    }

    private async Task HandleMovementAsync()
    {
        AudioManager.Instance?.Play("startmovement");
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Movement);
        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        var enemySquad = CombatHelpers.GetInactiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (_battle.Hud == null || activeSquad == null || enemySquad == null) return;

        var moveVars = CombatHelpers.GetActiveMoveVars(_battle.ActiveTeamId, _battle.TeamAMove, _battle.TeamBMove);
        moveVars.Advance = false;
        moveVars.Retreat = false;
        CurrentBattleDistance = BoardGeometry.ClosestDistanceInches(_battle.GetActiveActors(), _battle.GetInactiveActors());

        var isAircraft = activeSquad.SquadType.Contains("Aircraft");
        var hasTeleport = activeSquad.SquadAbilities.Any(ability => ability.Innate == "Tele");

        if (!hasTeleport && (activeSquad.SquadType.Contains("Fortification") || activeSquad.Movement <= 0f))
        {
            moveVars.Move = false;
            return;
        }

        var baseMove = activeSquad.Movement + StepChecks.MovementPhaseChecks(activeSquad);
        var movementAllowance = isAircraft || hasTeleport ? baseMove : MathF.Min(baseMove, 10f);
        var enemyBuffer = hasTeleport ? 9.05f : 1.05f;

        if (isAircraft)
        {
            _ = await _battle.MovingStuff(movementAllowance, true, enemyBuffer, true, true, true, "Move aircraft?", true);
            return;
        }

        var rushDistance = DiceHelpers.SimpleRoll(6);
        rushDistance += activeSquad.SquadAbilities.FirstOrDefault(ability => ability.Innate == "+Rush")?.Modifier ?? 0;
        rushDistance = activeSquad.SquadAbilities.FirstOrDefault(ability => ability.Innate == "Super Advance")?.Modifier ?? rushDistance;
        var inFightRange = CurrentBattleDistance <= 1f;

        if (!inFightRange)
        {
            var wantsRush = await _battle.Hud.ConfirmActionAsync($"Do you want to Rush {rushDistance}?", "Rush", "No Rush");
            if (wantsRush)
            {
                movementAllowance += rushDistance;
                moveVars.Advance = true;
            }
        }

        if (!moveVars.Advance)
        {
            var movePrompt = inFightRange ? "Do you want to retreat?" : "Do you want to move?";
            if (inFightRange && activeSquad.ShellShock)
            {
                movePrompt += " This will be a rout.";
            }

            var wantsMove = await _battle.Hud.ConfirmActionAsync(movePrompt, inFightRange ? "Retreat" : "Move", "Stay");
            if (!wantsMove)
            {
                _battle.PrepareMovementStartPositions(movementAllowance, isAircraft || hasTeleport, enemyBuffer, false);
                _battle.FinishMovementPhase(false);
                return;
            }

            if (CurrentBattleDistance <= 1f)
            {
                moveVars.Retreat = true;
            }
        }

        _ = await _battle.MovingStuff(movementAllowance, isAircraft || hasTeleport, enemyBuffer, false, true, true, string.Empty, true);

        if (moveVars.Retreat && activeSquad.ShellShock && !activeSquad.SquadType.Contains("Titanic"))
        {
            _battle.Hud?.ShowToast($"{activeSquad.Name} routed!");
            _battle.ApplyRout(activeSquad);
        }
    }

    private async Task HandleShootingAsync()
    {
        AudioManager.Instance?.Play("startshooting");
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Shooting);
        if (_battle.Hud == null) return;

        var didSelfDamage = await RunShootingPhaseChecksAsync();
        if (didSelfDamage)
        {
            CombatEngine.RemoveDeadModels(_battle.GetActiveActors(), CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad), _battle.Field);
            _battle.Hud.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
            _battle.CheckVictory();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }

        var wantsShoot = await _battle.Hud.ConfirmActionAsync("Do you want to shoot?");
        if (!wantsShoot) return;

        await _battle.ResolveShootingPhase();

        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        var enemySquad = CombatHelpers.GetInactiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (activeSquad?.SquadAbilities.Any(ability => ability.Innate == "ScareFire") == true && enemySquad != null)
        {
            enemySquad.ShellShock = StepChecks.ShellShockTest(enemySquad, activeSquad.SquadAbilities);
            GD.Print($"[Ability Triggered] Squad ability 'ScareFire' triggered ({enemySquad.Name} took an immediate shell shock test).");
            _battle.Hud?.ShowToast($"{enemySquad.Name} takes a Shell Shock test from Shoot Shell Shock.");
        }

        if (activeSquad?.SquadAbilities.Any(ability => ability.Innate == "GunRun") == true)
        {
            CurrentBattleDistance = BoardGeometry.ClosestDistanceInches(_battle.GetActiveActors(), _battle.GetInactiveActors());
            if (CurrentBattleDistance > 1f)
            {
                var moved = await TriggerSpecialMovementAsync(_battle.ActiveTeamId, activeSquad, 6f, "Gun Run triggered: make a free move.");
                if (moved)
                {
                    _battle.ActiveSquadMovedAfterShootingThisTurn = true;
                    GD.Print("[Ability Triggered] Squad ability 'GunRun' triggered (squad moved after shooting and can no longer charge this turn).");
                }
            }
        }
    }

    private async Task<bool> TriggerSpecialMovementAsync(int teamId, Squad squad, float moveInches, string toast)
    {
        if (_battle.Hud == null) return false;

        var priorTeamId = _battle.ActiveTeamId;
        var priorPhase = _battle.CurrentPhase;
        _battle.ActiveTeamId = teamId;
        _battle.EnterPhase(BattlePhase.Movement);
        _battle.Hud.ShowToast(toast);

        var moveVars = await _battle.MovingStuff(moveInches, false, 1.05f, false, false, false, string.Empty, true);

        _battle.ActiveTeamId = priorTeamId;
        _battle.EnterPhase(priorPhase, announce: false);
        return moveVars.Move;
    }

    private async Task HandleChargeAsync()
    {
        AudioManager.Instance?.Play("charge");
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Charge);
        if (_battle.Hud == null) return;

        var wantsCharge = await _battle.Hud.ConfirmActionAsync("Do you want to charge?");
        if (!wantsCharge) return;

        await ResolveChargeAsync();
    }

    private async Task HandleFightAsync()
    {
        AudioManager.Instance?.Play("startfight");
        await _battle.EnterPhaseWithCadenceAsync(BattlePhase.Fight);

        var didSelfDamage = await RunFightPhaseChecksAsync();
        if (didSelfDamage)
        {
            CombatEngine.RemoveDeadModels(_battle.GetActiveActors(), CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad), _battle.Field);
            _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
            _battle.CheckVictory();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
        }

        _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
        if (CurrentBattleDistance <= 1f)
        {
            var inactiveTeamId = _battle.ActiveTeamId == 1 ? 2 : 1;
            var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
            var inactiveSquad = CombatHelpers.GetActiveSquad(inactiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
            var activeHasHitFirst = activeSquad?.SquadAbilities.Any(ability => ability.Innate == "Hit First") == true;
            var inactiveHasHitFirst = inactiveSquad?.SquadAbilities.Any(ability => ability.Innate == "Hit First") == true;

            var activeFightsFirst = _battle.ActiveSquadChargedThisTurn || (activeHasHitFirst && !inactiveHasHitFirst);
            var firstFightTeamId = activeFightsFirst ? _battle.ActiveTeamId : inactiveTeamId;
            var secondFightTeamId = activeFightsFirst ? inactiveTeamId : _battle.ActiveTeamId;

            var secondFightSquad = CombatHelpers.GetActiveSquad(secondFightTeamId, _battle.TeamASquad, _battle.TeamBSquad);
            var cachedMeleeBatchesSecond = CombatEngine.BuildWeaponBatches(secondFightSquad, true);
            var secondHasFightAfterDeath = secondFightSquad?.SquadAbilities.Any(ability => ability.Innate == "Hit@End") == true;

            _battle.Hud?.ShowToast($"{_battle.GetSquadName(firstFightTeamId)} fights first", 2f);
            await _battle.DelaySecondsAsync(2f);

            var previousTeamId = _battle.ActiveTeamId;
            _battle.ActiveTeamId = firstFightTeamId;
            await _battle.ResolveFightPhase();
            _battle.ActiveTeamId = previousTeamId;
            _battle.CheckVictory();
            if (_battle.CurrentPhase == BattlePhase.BattleOver) return;

            _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
            if (CurrentBattleDistance <= 1f || secondHasFightAfterDeath)
            {
                _battle.Hud?.ShowToast($"{_battle.GetSquadName(secondFightTeamId)} fights second", 2f);
                await _battle.DelaySecondsAsync(2f);

                previousTeamId = _battle.ActiveTeamId;
                _battle.ActiveTeamId = secondFightTeamId;

                if (secondHasFightAfterDeath)
                {
                    await CombatEngine.ResolveBatchedAttackForTeam(
                        secondFightTeamId,
                        true,
                        cachedMeleeBatchesSecond,
                        _battle.TeamASquad,
                        _battle.TeamBSquad,
                        _battle.TeamAActors,
                        _battle.TeamBActors,
                        _battle.TeamAMove,
                        _battle.TeamBMove,
                        _battle.Hud,
                        _battle.Field,
                        _battle.CheckVictory,
                        _battle.HandleExplosionProcess
                    );
                }
                else
                {
                    await _battle.ResolveFightPhase();
                }

                _battle.ActiveTeamId = previousTeamId;
                _battle.CheckVictory();
            }
        }
    }

    private async Task<bool> RunShootingPhaseChecksAsync()
    {
        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        var enemySquad = CombatHelpers.GetInactiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (activeSquad == null || enemySquad == null) return false;

        _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
        return await StepChecks.ShootingPhaseChecks(activeSquad, enemySquad, _battle.Hud);
    }

    private async Task<bool> RunFightPhaseChecksAsync()
    {
        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (activeSquad == null) return false;
        return await StepChecks.FightPhaseChecks(activeSquad, _battle.Hud);
    }

    private async Task ResolveChargeAsync()
    {
        var activeSquad = CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        var enemySquad = CombatHelpers.GetInactiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad);
        if (activeSquad == null || enemySquad == null) return;

        _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
        var chargeBoost = await StepChecks.ChargePhaseChecks(activeSquad, enemySquad, _battle.ActiveSquadMovedAfterShootingThisTurn);
        var roll = DiceHelpers.Roll2d6() + chargeBoost;
        if (roll < CurrentBattleDistance)
        {
            _battle.Hud?.ShowToast("Charge failed.");
            AudioManager.Instance?.Play("failedcharge");
            return;
        }

        var moved = await BoardGeometry.TryMoveIntoEngagement(_battle.GetActiveActors(), _battle.GetInactiveActors(), _battle.Field);
        _battle.ActiveSquadChargedThisTurn = moved;
        if (moved)
        {
            _battle.Hud?.ShowToast("Charge successful!");
            AudioManager.Instance?.Play("goodcharge");

            if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Crush"))
            {
                var stampedeDamage = DiceHelpers.SimpleRoll(3);
                CombatRolls.AllocatePure(stampedeDamage, enemySquad);
                foreach (var enemyActor in _battle.GetInactiveActors())
                {
                    enemyActor.RefreshHp();
                }

                CombatEngine.RemoveDeadModels(_battle.GetInactiveActors(), enemySquad, _battle.Field);
                GD.Print($"[Ability Triggered] Squad ability 'Crush' triggered (dealt {stampedeDamage} pure damage after charge).");
                _battle.Hud?.ShowToast($"Stampede deals {stampedeDamage} pure damage to {enemySquad.Name}.");
                _battle.CheckVictory();
                if (_battle.CurrentPhase == BattlePhase.BattleOver) return;
            }
        }

        _battle.Hud?.UpdateDistanceAndHud(_battle.TeamAActors, _battle.TeamBActors);
    }

    private void EndTurn()
    {
        AudioManager.Instance?.Play("turnover");
        StepChecks.EndOfRoundChecks(CombatHelpers.GetActiveSquad(_battle.ActiveTeamId, _battle.TeamASquad, _battle.TeamBSquad));
        _battle.EnterPhase(BattlePhase.EndTurn);

        var nextTurn = _battle.CurrentTurn + 1;
        while (nextTurn > 2)
        {
            _battle.Round++;
            AudioManager.Instance?.Play("roundbell");
            nextTurn -= 2;
        }

        _battle.CurrentTurn = nextTurn;
        _battle.SyncGlobalTurnRound();
        BeginTurn();
    }
}
