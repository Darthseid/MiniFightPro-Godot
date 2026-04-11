using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public static class CombatHelpers
{
    private static void LogAbilityTrigger(string abilityType, string abilityInnate, string context)
        { GD.Print($"[Ability Triggered] {abilityType} ability '{abilityInnate}' triggered ({context})."); }



    public static bool HasFriendlyAura(
        Squad target,
        IEnumerable<Squad> friendlySquads,
        string auraInnate,
        float auraRangeInches,
        Func<Squad, Squad, float, bool> areSquadsWithinDistance,
        bool includeSelf = true)
    {
        if (target == null || friendlySquads == null || string.IsNullOrWhiteSpace(auraInnate) || areSquadsWithinDistance == null)
            return false;

        return friendlySquads.Any(source =>
            source != null
            && source.SquadAbilities.Any(ability => ability.Innate == auraInnate)
            && (includeSelf || !ReferenceEquals(source, target))
            && areSquadsWithinDistance(source, target, auraRangeInches));
    }

    public static bool HasEnemyDebuffAura(
        Squad target,
        IEnumerable<Squad> enemySquads,
        string auraInnate,
        float auraRangeInches,
        Func<Squad, Squad, float, bool> areSquadsWithinDistance)
    {
        if (target == null || enemySquads == null || string.IsNullOrWhiteSpace(auraInnate) || areSquadsWithinDistance == null)
            return false;

        return enemySquads.Any(source =>
            source != null
            && source.SquadAbilities.Any(ability => ability.Innate == auraInnate)
            && areSquadsWithinDistance(source, target, auraRangeInches));
    }

    public static Squad GetActiveSquad(int activeTeamId, Squad teamASquad, Squad teamBSquad)
        { return activeTeamId == 1 ? teamASquad : teamBSquad; }

    public static Squad GetInactiveSquad(int activeTeamId, Squad teamASquad, Squad teamBSquad)
        { return activeTeamId == 1 ? teamBSquad : teamASquad; }

    public static MoveVars GetActiveMoveVars(int activeTeamId, MoveVars teamAMove, MoveVars teamBMove)
        { return activeTeamId == 1 ? teamAMove : teamBMove; }

    public static MoveVars GetMoveVarsForTeam(int teamId, MoveVars teamAMove, MoveVars teamBMove)
        { return teamId == 1 ? teamAMove : teamBMove; }

    private static bool IsMonsterorVehicle(Squad squad)
        { return squad?.SquadType?.Any(type => type == "Monster" || type == "Vehicle") == true; }

    public static List<Weapon> AttackUnitWeapons(Squad unity)
    {
        var weaponList = unity.Composition
            .SelectMany(model => model.Tools.Select(tool => tool.DeepCopy()))
            .ToList();
        var weaponCount = weaponList.GroupBy(weapon => weapon.WeaponName)
            .ToDictionary(group => group.Key, group => group.Count());
        weaponList = weaponList.Where(weapon => weaponCount.GetValueOrDefault(weapon.WeaponName, 0) > 1).ToList();
        return weaponList.GroupBy(weapon => weapon.WeaponName).Select(group => group.First()).ToList();
    }

    public static bool CheckValidShooting(Squad shooterSquad, MoveVars shooterMove, Weapon firearm, Squad targetSquad, float currentDistance, bool hasLineOfSight = true)
    {
        var validShooting = currentDistance <= firearm.Range;
        var shotAbilities = firearm.Special;
        var fightRange = currentDistance <= 1f;
        if (firearm.IsMelee)
            return false; //This method doesn't trigger during the Melee Phase.
        if (shooterMove.Rush && shotAbilities.All(ability => ability.Innate != "RunGun"))
            validShooting = false;
        if (shooterMove.Retreat && shooterSquad.SquadAbilities.All(ability => ability.Innate != "FleeShoot"))
            validShooting = false;
        if (!IsMonsterorVehicle(shooterSquad))
        {
            if (fightRange && shotAbilities.All(ability => ability.Innate != "Handgun"))
                validShooting = false;
        }
        if (!hasLineOfSight && firearm.Special.All(ability => ability.Innate != WeaponAbilities.IndirectFire.Innate))
            validShooting = false;
        if (targetSquad.SquadAbilities.Any(ability => ability.Innate == "12 inch or bust") && currentDistance > 12f)
        {
            LogAbilityTrigger("Squad", "12 inch or bust", "invalidated shooting beyond 12 inches");
            validShooting = false;
        }
        if (fightRange && shotAbilities.Any(ability => ability.Innate == "Boom"))
        {
            LogAbilityTrigger("Weapon", "Boom", "blocked shooting in fight range");
            validShooting = false;
        }
        return validShooting;
    }

    public static DiceModifiers ObtainModifiers(
        Weapon firearm,
        Squad attackerSquad,
        MoveVars attackerMove,
        Squad defenderSquad,
        bool coverType,
        bool isFight,
        float currentDistance,
        int additionalHitModifier = 0
    )
    {
        var hitMod = 0;
        var woundMod = 0;
        var hitReroll = 0;
        var woundReroll = 0;
        var antiThreshold = 6;
        var critThreshold = 6;
        var antiAbilities = new Dictionary<string, string>
        {
            { "Mobkiller", "Infantry" },
            { "TankKiller", "Vehicle" },
            { "KaijuKiller", "Monster" },
            { "Assassinate", "Character" },
            { "Ack-Ack", "Fly" },
            { "WitchKiller", "Psychic" }
        };

        foreach (var pair in antiAbilities)
        {
            if (firearm.Special.Any(ability => ability.Innate == pair.Key) &&
                defenderSquad.SquadType.Contains(pair.Value))
            {
                var modifier = firearm.Special.FirstOrDefault(ability => ability.Innate == pair.Key)?.ResolveModifier() ?? antiThreshold;
                antiThreshold = modifier;
                LogAbilityTrigger("Weapon", pair.Key, $"set anti threshold to {antiThreshold}");
            }
        }

        if (currentDistance <= 1f && IsMonsterorVehicle(attackerSquad) && !firearm.IsMelee)
            hitMod -= 1;
        if (currentDistance > 12f && firearm.Special.Any(ability => ability.Innate == "Convert"))
        {
            critThreshold = Math.Min(critThreshold, 4);
            LogAbilityTrigger("Weapon", "Convert", $"set crit threshold to {critThreshold}");
        }
        var armorMod = firearm.ArmorPenetration;
        if (coverType && firearm.Special.All(ability => ability.Innate != "noCover"))
            armorMod += 1;
        if (defenderSquad.Defense > 3 && defenderSquad.SquadAbilities.Any(ability => ability.Innate == SquadAbilities.CoverBenefit.Innate) &&
            firearm.Special.All(ability => ability.Innate != WeaponAbilities.IgnoresCover.Innate)) //This represents being in cover.
            armorMod += 1;
        var unsteady = attackerMove.Move;
        if (!unsteady && firearm.Special.Any(ability => ability.Innate == "Hefty"))
        {
            hitMod += 1;
            LogAbilityTrigger("Weapon", "Hefty", "granted +1 hit modifier while stationary");
        }
        if (!isFight && defenderSquad.SquadAbilities.Any(ability => ability.Innate == "-1 Shoot"))
        {
            hitMod -= 1;
            LogAbilityTrigger("Squad", "-1 Shoot", "applied -1 hit modifier to attacker");
        }
        if (isFight && defenderSquad.SquadAbilities.Any(ability => ability.Innate == "-1 Fight"))
        {
            hitMod -= 1;
            LogAbilityTrigger("Squad", "-1 Fight", "applied -1 hit modifier in fight");
        }
        if (defenderSquad.SquadAbilities.Any(ability => ability.Innate == "-1 All"))
        {
            hitMod -= 1;
            LogAbilityTrigger("Squad", "-1 All", "applied -1 universal hit modifier to attacker");
        }
        if (firearm.Special.Any(ability => ability.Innate == "RRI"))
        {
            woundReroll = 2;
            LogAbilityTrigger("Weapon", "RRI", "enabled wound reroll failed");
        }
        var hasGreatCombatLeaderAura = CombatHelpers.HasFriendlyAura(
            attackerSquad,
            StepChecks.FriendlyAuraSquadProvider?.Invoke(attackerSquad),
            SquadAbilities.GreatCombatLeader.Innate,
            12f,
            StepChecks.AuraRangeCheckProvider,
            includeSelf: true);

        if (firearm.Special.Any(ability => ability.Innate == "@") || attackerSquad.SquadAbilities.Any(ability => ability.Innate == "Pow1"))
        {
            hitReroll = 2;
            LogAbilityTrigger("Weapon/Squad", "@/Pow1", "enabled hit reroll failed");
        }
        if (firearm.Special.Any(ability => ability.Innate == "#") || attackerSquad.SquadAbilities.Any(ability => ability.Innate == "Pow2") || hasGreatCombatLeaderAura)
        {
            hitReroll = Math.Max(hitReroll, 1);
            LogAbilityTrigger("Weapon/Squad", "#/Pow2/GCL", "enabled hit reroll ones");
        }
        if (firearm.Special.Any(ability => ability.Innate == "$") || attackerSquad.SquadAbilities.Any(ability => ability.Innate == "Pow3") || hasGreatCombatLeaderAura)
        {
            woundReroll = Math.Max(woundReroll, 1);
            LogAbilityTrigger("Weapon/Squad", "$/Pow3/GCL", "enabled injury reroll ones");
        }
        if (firearm.Special.Any(ability => ability.Innate == "^") || attackerSquad.SquadAbilities.Any(ability => ability.Innate == "Pow4"))
        {
            woundMod += 1;
            LogAbilityTrigger("Weapon/Squad", "^/Pow4", "granted +1 wound modifier");
        }
        if (defenderSquad.SquadAbilities.Any(ability => ability.Innate == "Pow0"))
        {
            hitReroll = 0;
            woundReroll = 0;
            LogAbilityTrigger("Squad", "Pow0", "canceled attacker rerolls");
        }
        if (firearm.Strength > defenderSquad.Hardness && defenderSquad.SquadAbilities.Any(ability => ability.Innate == "4s Please"))
        {
            woundMod -= 1;
            LogAbilityTrigger("Squad", "4s Please", "reduced wound modifier by 1");
        }
        hitMod += additionalHitModifier;

        if (attackerSquad.SquadAbilities.Any(ability => ability.Innate == "Pow-1"))
        {
            hitMod = Math.Max(hitMod, 0);
            woundMod = Math.Max(woundMod, 0);
            LogAbilityTrigger("Squad", "Pow-1", "prevented negative hit/wound modifiers");
        }       
        hitMod = Math.Clamp(hitMod, -1, 1);
        woundMod = Math.Clamp(woundMod, -1, 1);
        armorMod = Math.Clamp(armorMod, -1, 1); // All modifiers must be between -1 and 1.

        return new DiceModifiers(hitMod, woundMod, hitReroll, woundReroll, armorMod, critThreshold, antiThreshold);
    }

    public static int DamageMods(int damage, List<SquadAbility> targetSquadAbilities, List<WeaponAbility> specials, bool half)
    {
        if (targetSquadAbilities.Any(ability => ability.Innate == "BrainBlock") &&
            specials.Any(ability => ability.Innate == "Psi"))
            LogAbilityTrigger("Squad", "BrainBlock", "applies psychic damage resistance instead of negating damage");

        var newDamage = damage;
        var fusionModifier = specials.FirstOrDefault(ability => ability.Innate == "Fusion")?.ResolveModifier() ?? 0;
        if (half && fusionModifier > 0)
        {
            newDamage += fusionModifier;
            LogAbilityTrigger("Weapon", "Fusion", $"increased damage by {fusionModifier}");
        }
        if (targetSquadAbilities.Any(ability => ability.Innate == "Less1"))
        {
            newDamage = Math.Max(newDamage - 1, 1);
            LogAbilityTrigger("Squad", "Less1", "reduced incoming damage by 1");
        }
        if (targetSquadAbilities.Any(ability => ability.Innate == "Less2"))
        {
            newDamage = (newDamage + 1) / 2; //This rounds up when halving, so 3 damage would be reduced to 2 instead of 1.
            LogAbilityTrigger("Squad", "Less2", "halved incoming damage");
        }
        if (targetSquadAbilities.Any(ability => ability.Innate == "Less3"))
        {
            newDamage = 1;
            LogAbilityTrigger("Squad", "Less3", "set incoming damage to 1");
        }
        return newDamage;
    }
}
