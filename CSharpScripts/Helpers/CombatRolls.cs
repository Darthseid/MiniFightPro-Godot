using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class CombatRolls
{
    public static Func<Squad, int>? DamageResistanceModifierProvider { get; set; }

    private static void LogAbilityTrigger(string abilityType, string abilityInnate, string context)
        { GD.Print($"[Ability Triggered] {abilityType} ability '{abilityInnate}' triggered ({context})."); }


    public static (int Hits, int HardHits) HitSequence(
        int attackRolls,
        int hitSkill,
        int hitModifier,
        int reRollCheck,
        int critThreshold,
        List<WeaponAbility> abilityCheck
    )
    {
        var hits = 0;
        var hardHits = 0;
        if (hitSkill < 2) // Auto-hit threshold, skip rolling and just return max hits with no hard hits
            return (attackRolls, hardHits);

        var hitChange = Math.Clamp(hitModifier, -1, 1);
        var bonusHits = abilityCheck.FirstOrDefault(ability => ability.Innate == "Bonus Hits")?.ResolveModifier() ?? 0;
        var hardHitsTest = abilityCheck.Any(ability => ability.Innate == "HH");
        var oneReroll = abilityCheck.Any(ability => ability.Innate == "!");

        for (int i = 0; i < attackRolls; i++)
        {
            var rollChecker = reRollCheck;
            if (oneReroll)
            {
                rollChecker = 2;
                oneReroll = false;
            }

            var diceRoll = rollChecker switch
            {
                1 => DiceHelpers.ReRollOnes(),
                2 => DiceHelpers.ReRollFailed(hitSkill),
                _ => DiceHelpers.SimpleRoll(6)
            };

            var criticalHit = diceRoll == critThreshold;
            if (diceRoll == 1) // Automatic miss, no matter the modifiers or abilities
                continue;
            if (diceRoll + hitChange >= hitSkill || criticalHit) //Critical Hits always hit.
                hits++;
            if (hardHitsTest && criticalHit)
            {
                hits--;
                hardHits++;
                LogAbilityTrigger("Weapon", "HH", "converted a critical hit into a hard hit");
            }
            if (bonusHits > 0 && criticalHit)
            {
                hits += bonusHits;
                LogAbilityTrigger("Weapon", "Bonus Hits", $"added {bonusHits} bonus hit(s) on critical");
            }
        }
        return (hits, hardHits);
    }

    public static (int Injuries, int Devastating) WoundSequence(
        int hits,
        int strength,
        int hardness,
        int injuryModifier,
        int reRollCheck,
        List<WeaponAbility> abilityCheck,
        int antiLimit
    ) //This version is mainly for duel simulator. Async version is below for the main game to allow for player interaction on rerolls.
    {
        var injuries = 0;
        var devastatingCounter = 0;
        var injuryChange = Math.Clamp(injuryModifier, -1, 1);
        var devastatingInjuries = abilityCheck.Any(ability => ability.Innate == "DI");
        var oneReroll = abilityCheck.Any(ability => ability.Innate == "%");

        for (int i = 0; i < hits; i++)
        {
            var injuryThreshold = strength >= hardness * 2 ? 2 :
                strength > hardness && strength < hardness * 2 ? 3 :
                strength == hardness ? 4 :
                strength < hardness && hardness < strength * 2 ? 5 : 6; //If all of these fail, 6 is the threshold.

            var rollChecker = reRollCheck;
            if (oneReroll)
            {
                rollChecker = 2;
                oneReroll = false;
            }
            var diceRoll = rollChecker switch
            {
                1 => DiceHelpers.ReRollOnes(),
                2 => DiceHelpers.ReRollFailed(injuryThreshold),
                _ => DiceHelpers.SimpleRoll(6)
            };

            if (diceRoll == 1)
                continue;

            var criticalInjury = diceRoll >= antiLimit;
            if (diceRoll + injuryChange >= injuryThreshold || criticalInjury)
                injuries++;
            if (devastatingInjuries && criticalInjury)
            {
                devastatingCounter++;
                injuries--;
                LogAbilityTrigger("Weapon", "DI", "converted an injury into devastating injury");
            }
        }
        return (injuries, devastatingCounter);
    }

    public static int SaveSequence(int injuries, int defense, int armorPenetration, int dodge)
    {
        int modifiedSave = defense + armorPenetration; // AP is negative
        int finalSave = Math.Min(modifiedSave, dodge);
        bool usingInvuln = finalSave == dodge;
        int unsaved = 0;

        for (int i = 0; i < injuries; i++)
        {
            int roll = DiceHelpers.SimpleRoll(6);
            bool saved;
            if (usingInvuln)         
                saved = roll >= dodge;   // Dodge saves ignore AP
            else 
                saved = (roll + armorPenetration) >= defense;   // Defense saves apply AP
            if (!saved)
                unsaved++;
        }
        return unsaved;
    }


    public static async Task<(int Hits, int HardHits)> HitSequenceAsync(
        int attackRolls,
        int hitSkill,
        int hitModifier,
        int reRollCheck,
        int critThreshold,
        List<WeaponAbility> abilityCheck,
        RollContext rollContext
    )
    {
        var hits = 0;
        var hardHits = 0;
        if (hitSkill < 2)
            return (attackRolls, hardHits);

        var hitChange = Mathf.Clamp(hitModifier, -1, 1);
        var bonusHits = abilityCheck.FirstOrDefault(ability => ability.Innate == "Bonus Hits")?.ResolveModifier() ?? 0;
        var hardHitsTest = abilityCheck.Any(ability => ability.Innate == "HH");
        var oneReroll = abilityCheck.Any(ability => ability.Innate == "!");

        var initialBatch = await DiceRoller.PresentAndRollAsync(6, attackRolls, rollContext);
        var finalRolls = initialBatch.Results.ToArray();

        if (!rollContext.OnlySixesHit)
        {
            var rerollIndices = new List<int>();
            for (var i = 0; i < finalRolls.Length; i++)
            {
                if (oneReroll && i == 0)
                    continue;

                if (reRollCheck == 1 && finalRolls[i] == 1)
                    rerollIndices.Add(i);
                else if (reRollCheck == 2 && finalRolls[i] < hitSkill)
                    rerollIndices.Add(i);
            }

            if (oneReroll && finalRolls.Length > 0 && finalRolls[0] < hitSkill)
                rerollIndices.Add(0);

            if (rerollIndices.Count > 0) // Only present reroll if there are dice to reroll, otherwise it just looks weird
            {
                var rerollContext = rollContext with { Label = "Reroll (To Hit)" };
                var rerollBatch = await DiceRoller.PresentAndRollAsync(6, rerollIndices.Count, rerollContext, true);
                for (var i = 0; i < rerollIndices.Count; i++)
                    finalRolls[rerollIndices[i]] = rerollBatch.Results[i];
            }
        }

        foreach (var diceRoll in finalRolls)
        {
            if (rollContext.OnlySixesHit) //This matters for Reactive Fire.
            {
                if (diceRoll == 6)
                    hits++;
                continue;
            }
            var criticalHit = diceRoll == critThreshold;
            if (diceRoll == 1)
                continue;
            if (diceRoll + hitChange >= hitSkill || criticalHit)
                hits++;
            if (hardHitsTest && criticalHit)
            {
                hits--;
                hardHits++;
                LogAbilityTrigger("Weapon", "HH", "converted a critical hit into a hard hit");
            }
            if (bonusHits > 0 && criticalHit)
            {
                hits += bonusHits;
                LogAbilityTrigger("Weapon", "Bonus Hits", $"added {bonusHits} bonus hit(s) on critical");
            }
        }
        return (hits, hardHits);
    }

    public static async Task<(int Injuries, int Devastating)> WoundSequenceAsync(
        int hits,
        int strength,
        int hardness,
        int injuryModifier,
        int reRollCheck,
        List<WeaponAbility> abilityCheck,
        int antiLimit,
        RollContext rollContext
    )
    {
        var injuries = 0;
        var devastatingCounter = 0;
        var injuryChange = Mathf.Clamp(injuryModifier, -1, 1);
        var devastatingInjuries = abilityCheck.Any(ability => ability.Innate == "DI");
        var oneReroll = abilityCheck.Any(ability => ability.Innate == "%");

        var injuryThreshold = strength >= hardness * 2 ? 2 :
            strength > hardness && strength < hardness * 2 ? 3 :
            strength == hardness ? 4 :
            strength < hardness && hardness < strength * 2 ? 5 : 6;

        var initialBatch = await DiceRoller.PresentAndRollAsync(6, hits, rollContext);
        var finalRolls = initialBatch.Results.ToArray();

        var rerollIndices = new List<int>();
        for (var i = 0; i < finalRolls.Length; i++)
        {
            if (oneReroll && i == 0)
                continue;

            if (reRollCheck == 1 && finalRolls[i] == 1)
                rerollIndices.Add(i);
            else if (reRollCheck == 2 && finalRolls[i] < injuryThreshold)
                rerollIndices.Add(i);
        }

        if (oneReroll && finalRolls.Length > 0 && finalRolls[0] < injuryThreshold)
            rerollIndices.Add(0);

        if (rerollIndices.Count > 0)
        {
            var rerollContext = rollContext with { Label = "Reroll (To Wound)" };
            var rerollBatch = await DiceRoller.PresentAndRollAsync(6, rerollIndices.Count, rerollContext, true);
            for (var i = 0; i < rerollIndices.Count; i++)
                finalRolls[rerollIndices[i]] = rerollBatch.Results[i];
        }

        foreach (var diceRoll in finalRolls)
        {
            if (diceRoll == 1)
                continue;

            var criticalInjury = diceRoll >= antiLimit;
            if (diceRoll + injuryChange >= injuryThreshold || criticalInjury)
                injuries++;
            if (devastatingInjuries && criticalInjury)
            {
                devastatingCounter++;
                injuries--;
                LogAbilityTrigger("Weapon", "DI", "converted an injury into devastating injury");
            }
        }

        return (injuries, devastatingCounter);
    }

    public static async Task<int> SaveSequenceAsync(
        int injuries,
        int defense,
        int armorPenetration,
        int dodge,
        RollContext rollContext)
    {
        if (injuries <= 0) // No saves needed if there are no injuries
            return 0;
        int modifiedArmorSave = defense + armorPenetration;
        int finalSave = Math.Min(modifiedArmorSave, dodge);
        bool usingInvuln = finalSave == dodge;
        int unsaved = 0;
        var saveBatch = await DiceRoller.PresentAndRollAsync(6, injuries, rollContext);

        foreach (var roll in saveBatch.Results)
        {
            bool saved;
            if (usingInvuln)
                saved = roll >= dodge;
            else
                saved = (roll - armorPenetration) >= defense;
            if (!saved)
                unsaved++;
        }
        return unsaved;
    }

    public static int ResolveEffectiveDamageResistance(Squad squad, List<WeaponAbility>? weaponSpecials = null)
    {
        if (squad == null)
            return 7; // 7 is impossible to reach with D6. It is effectively no resistance.

        var resist = squad.DamageResistance;
        var specialDef = squad.SquadAbilities.FirstOrDefault(ability => ability.Innate == "Special Def");
        if (specialDef != null)
        {
            resist = specialDef.ResolveModifier();
            LogAbilityTrigger("Squad", "Special Def", $"set damage resistance threshold to {resist}");
        }

        var auraModifier = DamageResistanceModifierProvider?.Invoke(squad) ?? 0;
        if (auraModifier != 0)
        {
            resist += auraModifier;
            LogAbilityTrigger("Squad", SquadAbilities.FreeHealthcare.Innate, $"adjusted damage resistance threshold by {auraModifier} to {resist}");
        }

        var hasPsiBlock = squad.SquadAbilities.Any(ability => ability.Innate == "BrainBlock");
        var isPsychicAttack = weaponSpecials?.Any(ability => ability?.Innate == "Psi") == true;
        if (hasPsiBlock && isPsychicAttack) //TODO review psiBlock ability.
        {
            resist = 2;
            LogAbilityTrigger("Squad", "BrainBlock", "set psychic damage resistance threshold to 2");
        }
        return resist;
    }

    public static int AllocatePure(int mortalWounds, Squad squad)
    {
        if (mortalWounds <= 0 || squad == null)
            return 0;

        var remainingWounds = mortalWounds;
        var modelsKilled = 0;
        var unit = squad;
        unit.Composition.Sort((a, b) => a.Health.CompareTo(b.Health));
        var resist = ResolveEffectiveDamageResistance(unit);
        var iterator = unit.Composition.GetEnumerator();
        while (remainingWounds > 0 && iterator.MoveNext()) //Unlike normal damage, Pure damage carries over to other models in the squad.
        {
            var model = iterator.Current;
            while (remainingWounds > 0 && model.Health > 0)
            {
                var resistCheck = DiceHelpers.SimpleRoll(6);
                var woundNegated = resistCheck >= resist;
                if (!woundNegated)
                {
                    model.Health--;
                    remainingWounds--;
                }
                else
                    remainingWounds--;
                if (model.Health <= 0)
                {
                    modelsKilled++;
                    break;
                }
            }
        }
        return modelsKilled;
    }
}

