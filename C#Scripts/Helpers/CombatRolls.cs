using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class CombatRolls
{
    private static void LogAbilityTrigger(string abilityType, string abilityInnate, string context)
    {
        GD.Print($"[Ability Triggered] {abilityType} ability '{abilityInnate}' triggered ({context}).");
    }

    private static float CurrentBattleDistance
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
        if (hitSkill < 2)
        {
            return (attackRolls, hardHits);
        }

        var hitChange = Mathf.Clamp(hitModifier, -1, 1);
        var bonusHits = abilityCheck.FirstOrDefault(ability => ability.Innate == "Bonus Hits")?.Modifier ?? 0;
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
            if (diceRoll == 1)
            {
                continue;
            }
            if (diceRoll + hitChange >= hitSkill || criticalHit)
            {
                hits++;
            }
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
    )
    {
        var injuries = 0;
        var devastatingCounter = 0;
        var injuryChange = Mathf.Clamp(injuryModifier, -1, 1);
        var devastatingInjuries = abilityCheck.Any(ability => ability.Innate == "DI");
        var oneReroll = abilityCheck.Any(ability => ability.Innate == "%");

        for (int i = 0; i < hits; i++)
        {
            var injuryThreshold = strength >= hardness * 2 ? 2 :
                strength > hardness && strength < hardness * 2 ? 3 :
                strength == hardness ? 4 :
                strength < hardness && hardness < strength * 2 ? 5 : 6;

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
            {
                continue;
            }

            var criticalInjury = diceRoll >= antiLimit;
            if (diceRoll + injuryChange >= injuryThreshold || criticalInjury)
            {
                injuries++;
            }
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
        var reducedSave = defense - armorPenetration;
        var finalSave = dodge < reducedSave ? dodge : reducedSave;
        var unsavedInjuries = 0;
        for (int i = 0; i < injuries; i++)
        {
            var diceRoll = DiceHelpers.SimpleRoll(6);
            if (diceRoll + armorPenetration < finalSave)
            {
                unsavedInjuries++;
            }
        }
        return unsavedInjuries;
    }

    public static int AllocatePure(int mortalWounds, Squad squad)
    {
        if (mortalWounds <= 0 || squad == null)
        {
            return 0;
        }

        var remainingWounds = mortalWounds;
        var modelsKilled = 0;
        var unit = squad;
        unit.Composition.Sort((a, b) => a.Health.CompareTo(b.Health));

        var resist = unit.DamageResistance;
        var specialDef = unit.SquadAbilities.FirstOrDefault(ability => ability.Innate == "Special Def");
        if (specialDef != null)
        {
            resist = specialDef.Modifier;
            LogAbilityTrigger("Squad", "Special Def", $"set damage resistance threshold to {resist}");
        }

        var iterator = unit.Composition.GetEnumerator();
        while (remainingWounds > 0 && iterator.MoveNext())
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
                {
                    remainingWounds--;
                }
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

