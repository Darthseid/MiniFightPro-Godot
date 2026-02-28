using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public sealed class DuelSimulator
{
    private static readonly FieldInfo DiceRngField = typeof(DiceHelpers)
        .GetField("Rng", BindingFlags.NonPublic | BindingFlags.Static);

    private readonly MoveVars _stationaryMove = new MoveVars(move: false, advance: false, retreat: false);

    private long _trialSquadAAttacks;
    private long _trialSquadBAttacks;
    private long _trialSquadAPen;
    private long _trialSquadBPen;

    public DuelResult RunSingle(DuelConfig config, Squad a, Squad b, Random rng)
    {
        ResetTrialCounters();
        SeedDiceHelpers(rng);

        var squadA = new SimSquadState(a);
        var squadB = new SimSquadState(b);

        var aActsFirst = config.FirstAttacker switch
        {
            FirstAttackerMode.SquadA => true,
            FirstAttackerMode.SquadB => false,
            _ => rng.Next(0, 2) == 0
        };

        var firstAttackerIsA = aActsFirst;
        var noDamageRounds = 0;
        var roundsElapsed = 0;

        while (roundsElapsed < config.RoundCap)
        {
            roundsElapsed++;
            var roundDidDamage = false;

            if (aActsFirst)
            {
                ResolveRoundForActive(config, squadA, squadB, attackerIsA: true, ref roundDidDamage);
            }
            else
            {
                ResolveRoundForActive(config, squadB, squadA, attackerIsA: false, ref roundDidDamage);
            }

            if (squadA.IsDestroyed || squadB.IsDestroyed)
            {
                break;
            }

            if (!roundDidDamage)
            {
                noDamageRounds++;
                if (noDamageRounds >= config.NoDamageRoundLimit)
                {
                    break;
                }
            }
            else
            {
                noDamageRounds = 0;
            }

            aActsFirst = !aActsFirst;
        }

        var aDestroyed = squadA.IsDestroyed;
        var bDestroyed = squadB.IsDestroyed;

        var result = new DuelResult
        {
            RoundsElapsed = roundsElapsed,
            HitRoundCap = roundsElapsed >= config.RoundCap && !aDestroyed && !bDestroyed,
            IsDraw = (aDestroyed && bDestroyed) || (!aDestroyed && !bDestroyed),
            SquadAWon = !aDestroyed && bDestroyed,
            WinnerRemainingHealth = !aDestroyed && bDestroyed ? squadA.TotalRemainingHealth : (!bDestroyed && aDestroyed ? squadB.TotalRemainingHealth : 0),
            SquadAAttacks = _trialSquadAAttacks,
            SquadBAttacks = _trialSquadBAttacks,
            SquadAPenetratingInjuries = _trialSquadAPen,
            SquadBPenetratingInjuries = _trialSquadBPen
        };

        result.FirstAttackerWon = firstAttackerIsA ? result.SquadAWon : (!result.IsDraw && !result.SquadAWon);
        return result;
    }

    public DuelBatchResult RunBatch(DuelConfig config, Squad a, Squad b, int trials, int? seed)
    {
        var safeTrials = Math.Clamp(trials, 1, 100000);
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        var aWins = 0;
        var bWins = 0;
        var draws = 0;
        var firstAttackerWins = 0;
        long resolvedRoundSum = 0;
        long resolvedCount = 0;
        long winnerHpSum = 0;
        long totalAAttacks = 0;
        long totalBAttacks = 0;
        long totalAPen = 0;
        long totalBPen = 0;

        for (var i = 0; i < safeTrials; i++)
        {
            var single = RunSingle(config, a, b, rng);

            if (single.IsDraw) draws++;
            else if (single.SquadAWon) aWins++;
            else bWins++;

            if (single.FirstAttackerWon) firstAttackerWins++;

            if (!single.HitRoundCap)
            {
                resolvedRoundSum += single.RoundsElapsed;
                resolvedCount++;
            }

            winnerHpSum += single.WinnerRemainingHealth;
            totalAAttacks += single.SquadAAttacks;
            totalBAttacks += single.SquadBAttacks;
            totalAPen += single.SquadAPenetratingInjuries;
            totalBPen += single.SquadBPenetratingInjuries;
        }

        return new DuelBatchResult
        {
            Trials = safeTrials,
            SquadAWins = aWins,
            SquadBWins = bWins,
            Draws = draws,
            AverageRoundsToResolve = resolvedCount > 0 ? (double)resolvedRoundSum / resolvedCount : 0,
            AverageWinnerHealthRemaining = safeTrials > 0 ? (double)winnerHpSum / safeTrials : 0,
            FirstAttackerWinPercent = safeTrials > 0 ? (double)firstAttackerWins * 100d / safeTrials : 0,
            SquadAPenetratingInjuryRate = totalAAttacks > 0 ? (double)totalAPen / totalAAttacks : 0,
            SquadBPenetratingInjuryRate = totalBAttacks > 0 ? (double)totalBPen / totalBAttacks : 0
        };
    }

    private void ResolveRoundForActive(DuelConfig config, SimSquadState active, SimSquadState inactive, bool attackerIsA, ref bool roundDidDamage)
    {
        ResolveAttackStep(config, active, inactive, isMelee: false, attackerIsA, ref roundDidDamage);
        if (active.IsDestroyed || inactive.IsDestroyed) return;

        var activeFirst = active.HasFirstStrike;
        var inactiveFirst = inactive.HasFirstStrike;

        if (activeFirst && !inactiveFirst)
        {
            ResolveAttackStep(config, active, inactive, isMelee: true, attackerIsA, ref roundDidDamage);
            if (active.IsDestroyed || inactive.IsDestroyed) return;
            ResolveAttackStep(config, inactive, active, isMelee: true, !attackerIsA, ref roundDidDamage);
        }
        else
        {
            ResolveAttackStep(config, inactive, active, isMelee: true, !attackerIsA, ref roundDidDamage);
            if (active.IsDestroyed || inactive.IsDestroyed) return;
            ResolveAttackStep(config, active, inactive, isMelee: true, attackerIsA, ref roundDidDamage);
        }
    }

    private void ResolveAttackStep(DuelConfig config, SimSquadState attacker, SimSquadState defender, bool isMelee, bool attackerIsA, ref bool roundDidDamage, bool allowRetaliation = true)
    {
        if (attacker.IsDestroyed || defender.IsDestroyed)
        {
            return;
        }

        var weaponBatches = CombatEngine.BuildWeaponBatches(attacker.WorkingSquad, isMelee);
        foreach (var batch in weaponBatches)
        {
            var weapon = batch.Weapon;
            if (weapon == null) continue;

            if (!isMelee)
            {
                if (!CombatHelpers.CheckValidShooting(attacker.WorkingSquad, _stationaryMove, weapon, defender.WorkingSquad, config.RangeInches))
                {
                    continue;
                }

                if (config.RangeInches < 1f)
                {
                    var isPistol = weapon.Special.Any(a => a?.Innate == "Handgun");
                    if (!isPistol && !attacker.IsVehicleOrMonster)
                    {
                        continue;
                    }
                }
            }

            var attacks = batch.TotalAttacks;
            if (attacks <= 0) continue;

            var modifiers = CombatHelpers.ObtainModifiers(
                weapon,
                attacker.WorkingSquad,
                _stationaryMove,
                defender.WorkingSquad,
                coverType: false,
                isFight: isMelee,
                currentDistance: config.RangeInches
            );

            var (hits, hardHits) = CombatRolls.HitSequence(attacks, weapon.HitSkill, modifiers.HitMod, modifiers.HitReroll, modifiers.CritThreshold, weapon.Special);
            hits += hardHits;

            var (injuries, devastating) = CombatRolls.WoundSequence(
                hits,
                weapon.Strength,
                defender.WorkingSquad.Hardness,
                modifiers.WoundMod,
                modifiers.WoundReroll,
                weapon.Special,
                modifiers.AntiThreshold
            );
            injuries += devastating;

            var unsaved = CombatRolls.SaveSequence(injuries, defender.WorkingSquad.Defense, modifiers.DefenseMod, defender.WorkingSquad.Dodge);
            var defenderAliveBefore = defender.WorkingSquad.Composition.Count(model => model.Health > 0);
            if (attackerIsA)
            {
                _trialSquadAAttacks += attacks;
                _trialSquadAPen += unsaved;
            }
            else
            {
                _trialSquadBAttacks += attacks;
                _trialSquadBPen += unsaved;
            }

            ApplyUnsavedInjuries(attacker, defender, weapon, unsaved, config.RangeInches, ref roundDidDamage);
            ConsumeOneShotWeapons(attacker.WorkingSquad, weapon);
            ResolvePerilous(attacker, weapon, ref roundDidDamage);

            var defenderAliveAfter = defender.WorkingSquad.Composition.Count(model => model.Health > 0);
            if (allowRetaliation && isMelee && defender.HasFightOnDeath && defenderAliveAfter < defenderAliveBefore && !defender.IsDestroyed)
            {
                ResolveAttackStep(config, defender, attacker, isMelee: true, !attackerIsA, ref roundDidDamage, allowRetaliation: false);
            }

            if (attacker.IsDestroyed || defender.IsDestroyed)
            {
                break;
            }
        }
    }

    private static void ApplyUnsavedInjuries(SimSquadState attacker, SimSquadState defender, Weapon weapon, int unsaved, float range, ref bool roundDidDamage)
    {
        for (var i = 0; i < unsaved; i++)
        {
            if (defender.WorkingSquad.Composition.Count == 0)
            {
                break;
            }

            var target = defender.WorkingSquad.Composition
                .Where(m => m.Health > 0)
                .OrderBy(m => m.Health)
                .FirstOrDefault();

            if (target == null)
            {
                break;
            }

            var baseDamage = CombatHelpers.DamageParser(weapon.Damage);
            var finalDamage = CombatHelpers.DamageMods(baseDamage, defender.WorkingSquad.SquadAbilities, weapon.Special, range <= weapon.Range / 2f);

            var resist = defender.WorkingSquad.DamageResistance;
            var specialDef = defender.WorkingSquad.SquadAbilities.FirstOrDefault(a => a?.Innate == "Special Def");
            if (specialDef != null)
            {
                resist = specialDef.Modifier;
            }

            for (var p = 0; p < finalDamage && target.Health > 0; p++)
            {
                var negated = DiceHelpers.SimpleRoll(6) >= resist;
                if (!negated)
                {
                    target.Health -= 1;
                    roundDidDamage = true;
                }
            }
        }

        var deadBefore = defender.WorkingSquad.Composition.Count(m => m.Health <= 0);
        defender.RemoveDeadAndHandleSecondLife();
        if (deadBefore > 0)
        {
            HandleExplosions(defender, attacker, deadBefore, range, ref roundDidDamage);
        }
    }

    private static void HandleExplosions(SimSquadState exploded, SimSquadState other, int deadCount, float range, ref bool roundDidDamage)
    {
        if (!exploded.WorkingSquad.SquadAbilities.Any(a => a?.Innate == "Explodes"))
        {
            return;
        }

        var explodeDamage = exploded.WorkingSquad.SquadAbilities.FirstOrDefault(a => a?.Innate == "Explodes")?.Modifier ?? 1;
        var triggers = 0;
        for (var i = 0; i < deadCount; i++)
        {
            if (DiceHelpers.SimpleRoll(6) == 1)
            {
                triggers++;
            }
        }

        if (triggers <= 0)
        {
            return;
        }

        var blast = explodeDamage * triggers;
        CombatRolls.AllocatePure(blast, exploded.WorkingSquad);
        exploded.RemoveDeadAndHandleSecondLife();
        roundDidDamage = true;

        if (range <= 6f)
        {
            CombatRolls.AllocatePure(blast, other.WorkingSquad);
            other.RemoveDeadAndHandleSecondLife();
            roundDidDamage = true;
        }
    }

    private static void ConsumeOneShotWeapons(Squad attackerSquad, Weapon weapon)
    {
        if (weapon.Special == null || weapon.Special.All(a => a?.Innate != "1 Shot"))
        {
            return;
        }

        var fingerprint = CombatEngine.GetWeaponFingerprint(weapon);
        foreach (var model in attackerSquad.Composition.Where(m => m.Health > 0))
        {
            foreach (var tool in model.Tools)
            {
                if (CombatEngine.GetWeaponFingerprint(tool) == fingerprint)
                {
                    tool.Attacks = "0";
                }
            }
        }
    }

    private static void ResolvePerilous(SimSquadState attacker, Weapon weapon, ref bool roundDidDamage)
    {
        if (weapon.Special == null || weapon.Special.All(a => a?.Innate != "Self-Inflict"))
        {
            return;
        }

        var fingerprint = CombatEngine.GetWeaponFingerprint(weapon);
        foreach (var model in attacker.WorkingSquad.Composition.Where(m => m.Health > 0))
        {
            var hasWeapon = model.Tools.Any(tool => CombatEngine.GetWeaponFingerprint(tool) == fingerprint);
            if (!hasWeapon)
            {
                continue;
            }

            if (DiceHelpers.SimpleRoll(6) != 1)
            {
                continue;
            }

            if (attacker.IsInfantry)
            {
                model.Health = 0;
            }
            else
            {
                model.Health = Math.Max(0, model.Health - 3);
            }

            roundDidDamage = true;
        }

        attacker.RemoveDeadAndHandleSecondLife();
    }

    private static void SeedDiceHelpers(Random rng)
    {
        if (DiceRngField?.GetValue(null) is RandomNumberGenerator godotRng)
        {
            godotRng.Seed = (ulong)rng.NextInt64(1, long.MaxValue);
        }
    }

    private void ResetTrialCounters()
    {
        _trialSquadAAttacks = 0;
        _trialSquadBAttacks = 0;
        _trialSquadAPen = 0;
        _trialSquadBPen = 0;
    }
}
