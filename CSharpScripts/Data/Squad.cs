using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

public class Squad
{
    public static readonly IReadOnlyList<string> KnownSquadTypes = new List<string>
    {
        "Aircraft",
        "Titanic",
        "Fortification",
        "Mounted",
        "Monster",
        "Character",
        "Vehicle",
        "Infantry",
        "Fly",
        "Psychic",
        "Transport"
    };

    public string Name;
    public float Movement;
    public int Hardness;
    public int Defense;
    public int Dodge;
    public int DamageResistance;
    public int Bravery;
    public List<string> SquadType;
    public bool ShellShock;
    public List<Model> Composition;
    public int StartingModelSize;
    public List<SquadAbility> SquadAbilities;
    public Squad EmbarkedSquad;
    public Squad TransportedBy;
    public bool IsInStrategicReserve;
    public bool CannotChargeThisTurn;
	
	public Squad() { }

    public Squad(
        string name,
        float movement,
        int hardness,
        int defense,
        int dodge,
        int damageResistance,
        int bravery,
        List<string> squadType,
        bool shellShock,
        List<Model> composition,
        int startingModelSize,
        List<SquadAbility> squadAbilities
    )
    {
        Name = name;
        Movement = movement;
        Hardness = hardness;
        Defense = defense;
        Dodge = dodge;
        DamageResistance = damageResistance;
        Bravery = bravery;
        SquadType = squadType ?? new List<string>();
        ShellShock = shellShock;
        Composition = composition ?? new List<Model>();
        StartingModelSize = startingModelSize;
        SquadAbilities = squadAbilities ?? new List<SquadAbility>();
        EmbarkedSquad = null;
        TransportedBy = null;
        IsInStrategicReserve = false;
        CannotChargeThisTurn = false;
    }

    public Squad DeepCopy()
    {
        var copy = new Squad(
            Name,
            Movement,
            Hardness,
            Defense,
            Dodge,
            DamageResistance,
            Bravery,
            SquadType.ToList(),
            ShellShock,
            Composition.Select(model => model.DeepCopy()).ToList(),
            Composition.Count,
            SquadAbilities.ToList()
        );

        copy.EmbarkedSquad = EmbarkedSquad?.DeepCopy();
        if (copy.EmbarkedSquad != null)
        {
            copy.EmbarkedSquad.TransportedBy = copy;
        }

        copy.IsInStrategicReserve = IsInStrategicReserve;
        copy.CannotChargeThisTurn = CannotChargeThisTurn;

        return copy;
    }
}

public class SquadAbility
{
    public string Innate;
    public string Name;
    public int Modifier;
    public bool IsTemporary;
    public bool IsVariableGenerated;
    public string ModifierExpression;

	 [JsonConstructor]
    public SquadAbility(string innate, string name, int modifier, bool isTemporary, bool isVariableGenerated = false, string modifierExpression = "")
    {
        Innate = innate;
        Name = name;
        Modifier = modifier;
        IsTemporary = isTemporary;
        IsVariableGenerated = isVariableGenerated;
        ModifierExpression = modifierExpression ?? string.Empty;
    }

    public int ResolveModifier()
    {
        return string.IsNullOrWhiteSpace(ModifierExpression)
            ? Modifier
            : CombatHelpers.DamageParser(ModifierExpression);
    }
}

public static class SquadAbilities
{
    public static readonly SquadAbility MinusHitRanged = new SquadAbility("-1 Shoot", "-1 to Hit Ranged", 1, false);
    public static readonly SquadAbility MinusHitRangedTemp = new SquadAbility("Temp -1 Shoot", "Temp minus Hit Ranged", 1, true);
    public static readonly SquadAbility MinusHitBrawl = new SquadAbility("-1 Fight", "-1 to Hit Melee", 0, false);
    public static readonly SquadAbility TempMinusHitBrawl = new SquadAbility("-1 Fight", "Temp minus Hit Brawl", 0, true);
    public static readonly SquadAbility DeathExplode1 = new SquadAbility("Explodes", "Explode on Death 1", 1, false);
    public static readonly SquadAbility MinusHit = new SquadAbility("-1 All", "-1 to Hit", 0, false);
    public static readonly SquadAbility DemonicGrief = new SquadAbility("Bad Juju", "Demonic Grief", 2, false);
    public static readonly SquadAbility MinusHitTemp = new SquadAbility("-1 All", "Temp minus Hit", 0, true);
    public static readonly SquadAbility CloseUpToShoot = new SquadAbility("12 inch or bust", "Camouflaged", 0, false);
    public static readonly SquadAbility CloseUpToShootTemp = new SquadAbility("12 inch or bust", "Temp close Up to Shoot", 0, true);
    public static readonly SquadAbility FirstStrike = new SquadAbility("Hit First", "First Strike", 0, false);
    public static readonly SquadAbility FirstStrikeTemp = new SquadAbility("Hit First", "Temp First Strike", 0, true);
    public static readonly SquadAbility ShootRetreat = new SquadAbility("FleeShoot", "Shoot After Retreat", 0, false);
    public static readonly SquadAbility TempShootRetreat = new SquadAbility("FleeShoot", "Shoot After Retreat", 0, true);
    public static readonly SquadAbility ChargeAfterRush = new SquadAbility("DashBash", "Charge After Rush", 0, false);
    public static readonly SquadAbility TempChargeAfterRush = new SquadAbility("DashBash", "Temp Charge After Rush", 0, true);
    public static readonly SquadAbility FightAfterMeleeDeath = new SquadAbility("Hit@End", "Fight After Melee Death", 2, false);
    public static readonly SquadAbility TempFightAfterMeleeDeath = new SquadAbility("Hit@End", "Temp Fight After Melee Death", 2, true);
    public static readonly SquadAbility SelfRessurection = new SquadAbility("2nd Life", "Ressurect", 1, false);
    public static readonly SquadAbility SelfRessurectionTemp = new SquadAbility("2nd Life", "Temp Ressurect", 1, true);
    public static readonly SquadAbility PsiDefense = new SquadAbility("BrainBlock", "Psionic Defense", 0, false);
    public static readonly SquadAbility PsiDefenseTemp = new SquadAbility("BrainBlock", "Temp Psionic Defense", 0, true);
    public static readonly SquadAbility PureDefense = new SquadAbility("Special Def", "Pure Damage defense", 2, false);
    public static readonly SquadAbility PureDefenseTemp = new SquadAbility("Special Def", "Temp pure Damage defense", 2, true);
    public static readonly SquadAbility Teleport = new SquadAbility("Tele", "Teleport", 2, false);
    public static readonly SquadAbility TeleportTemp = new SquadAbility("Tele", "Temp Teleport", 2, true);
    public static readonly SquadAbility ShootShellshock = new SquadAbility("ScareFire", "Shoot shellShock", 2, false);
    public static readonly SquadAbility ShootShellshockTemp = new SquadAbility("ScareFire", "Temp Shoot shellShock", 2, true);
    public static readonly SquadAbility OfficerOrder = new SquadAbility("OO", "Officer Order", 0, false);
    public static readonly SquadAbility Reanimator = new SquadAbility("Zombie", "Regeneration", 0, false);
    public static readonly SquadAbility ReanimatorTemp = new SquadAbility("Zombie", "Temp Reanimator", 0, true);
    public static readonly SquadAbility AdvBoost1 = new SquadAbility("+Rush", "+1 Rush Boost", 1, false);
    public static readonly SquadAbility AdvBoost1Temp = new SquadAbility("+Rush", "Temp Rush Boost 1", 0, true);
    public static readonly SquadAbility AdvBoost6 = new SquadAbility("Super Advance", "Turbo Rush", 6, false);
    public static readonly SquadAbility AdvBoost6Temp = new SquadAbility("Super Advance", "Turbo Rush", 6, true);
    public static readonly SquadAbility Stampede = new SquadAbility("Crush", "Stampede", 0, false);
    public static readonly SquadAbility StampedeTemp = new SquadAbility("Crush", "Stampede", 0, true);
    public static readonly SquadAbility PlusOneToCharge = new SquadAbility("+Charge", "+1 Charge Bonus", 1, false);
    public static readonly SquadAbility PlusOneToChargeTemp = new SquadAbility("+Charge", "Temp Charge Bonus 1", 1, true);
    public static readonly SquadAbility Satanic = new SquadAbility("Satan", "Satanic", 1, false);
    public static readonly SquadAbility SatanicTemp = new SquadAbility("Temp Satan", "Temp Satanic", 1, true);
    public static readonly SquadAbility ReRollBravery = new SquadAbility("TryAgain", "Reroll Bravery", 1, false);
    public static readonly SquadAbility ReRollBraveryTemp = new SquadAbility("TryAgain", "Temp reroll Bravery", 1, true);
    public static readonly SquadAbility Infect = new SquadAbility("AIDS", "Infect", 1, false);
    public static readonly SquadAbility InfectTemp = new SquadAbility("AIDS", "Temp Infect", 1, true);
    public static readonly SquadAbility ResistFirstDamage = new SquadAbility("stopfirsthit", "Resist First Damage", 1, false);
    public static readonly SquadAbility ResistFirstDamageTemp = new SquadAbility("stopfirsthit", "Temp Resist First Damage", 1, true);
    public static readonly SquadAbility WeakenStrongAttack = new SquadAbility("4s Please", "Weaken Strong Attack", 1, false);
    public static readonly SquadAbility WeakenStrongAttackTemp = new SquadAbility("4s Please", "Temp Weaken Strong Attack", 1, true);
    public static readonly SquadAbility ReduceDamageBy1 = new SquadAbility("Less1", "-1 Damage", 1, false);
    public static readonly SquadAbility ReduceDamageBy1Temp = new SquadAbility("Less1", "Temp -1 Damage", 1, true);
    public static readonly SquadAbility ReduceDamageByHalf = new SquadAbility("Less2", "÷2 Damage", 1, false);
    public static readonly SquadAbility ReduceDamageByHalfTemp = new SquadAbility("Less2", "Temp ÷2 Damage", 1, true);
    public static readonly SquadAbility ReduceDamageTo1 = new SquadAbility("Less3", "Only 1 Damage", 1, false);
    public static readonly SquadAbility ReduceDamageTo1Temp = new SquadAbility("Less3", "Temp Only 1 Damage", 1, true);
    public static readonly SquadAbility berserking = new SquadAbility("Rampage", "Berserk Army", 0, false);
    public static readonly SquadAbility warriorBless = new SquadAbility("Angry God", "Blessed Warrior", 0, false);
    public static readonly SquadAbility hiveMind = new SquadAbility("Hive", "Hive Mind", 0, false);
    public static readonly SquadAbility MoveAfterShooting = new SquadAbility("GunRun", "Move After Shooting", 1, false);
    public static readonly SquadAbility MoveAfterShootingTemp = new SquadAbility("Temp GunRun", "Temp Move After Shooting", 1, true);
    public static readonly SquadAbility MoveBack = new SquadAbility("Run Away", "Move after enemy.", 1, false);
    public static readonly SquadAbility MoveBackTemp = new SquadAbility("Temp Run Away", "Move after enemy.", 1, true);
    public static readonly SquadAbility SquadrerollHits = new SquadAbility("Pow1", "Hits are rerolled", 0, false);
    public static readonly SquadAbility SquadrerollHitsTemp = new SquadAbility("Pow1", "Temp All hits are rerolled", 0, true);
    public static readonly SquadAbility SquadrerollHitOnes = new SquadAbility("Pow2", "Hit rolls 1 rerolled.", 0, false);
    public static readonly SquadAbility SquadrerollHitOnesTemp = new SquadAbility("Pow2", "Temp Hit rolls of 1 are rerolled.", 0, true);
    public static readonly SquadAbility SquadrerollInjuryOnes = new SquadAbility("Pow3", "Injury rolls 1 rerolled", 0, false);
    public static readonly SquadAbility SquadrerollInjuryOnesTemp = new SquadAbility("Pow3", "Temp Injury rolls of 1 are rerolled", 0, true);
    public static readonly SquadAbility SquadplusOneInjuries = new SquadAbility("Pow4", "+1 Injury Rolls", 0, false);
    public static readonly SquadAbility SquadplusOneInjuriesTemp = new SquadAbility("Pow4", "Temp +1 to Injury Rolls", 0, true);
    public static readonly SquadAbility SquadrerollInjuries = new SquadAbility("Pow5", "Injuries rerolled", 0, false);
    public static readonly SquadAbility SquadrerollInjuriesTemp = new SquadAbility("Pow5", "Temp All Injury rolls rerolled", 0, true);
    public static readonly SquadAbility StopRerolls = new SquadAbility("Pow0", "No rerolls", 0, false);
    public static readonly SquadAbility StopRerollsTemp = new SquadAbility("Pow0", "Temp No rerolls against this Squad", 0, true);
    public static readonly SquadAbility NoModifiers = new SquadAbility("Pow-1", "Ignore negative Modifiers", 0, false);
    public static readonly SquadAbility NoModifiersTemp = new SquadAbility("Pow-1", "Temp Ignore negative Modifiers", 0, true);
    public static readonly SquadAbility MartialStance = new SquadAbility("martialStance", "Martial Stances", 1, false);
     public static readonly SquadAbility SubRoutine = new SquadAbility("SubRoutine", "Subroutines", 1, false);
    public static readonly SquadAbility FiringDeck = new SquadAbility("Firing Deck", "Firing Deck", 0, false);
    public static readonly SquadAbility CoverBenefit = new SquadAbility("CoverBenefit", "Cover Benefit", 1, false);
    public static readonly SquadAbility CoverBenefitTemp = new SquadAbility("CoverBenefit", "Temp Cover Benefit", 1, true);
    public static readonly SquadAbility SixPlusDodge = new SquadAbility("SixPlusDodge", "Six Plus Dodge", 1, false);
    public static readonly SquadAbility SixPlusDodgeTemp = new SquadAbility("SixPlusDodge", "Temp Six Plus Dodge", 1, true);
    public static readonly SquadAbility FreeHealthcare = new SquadAbility("FHC", "Free Healthcare", 1, false);
    public static readonly SquadAbility TempFirstStrike = new SquadAbility("TempFirstStrike", "Temp First Strike", 0, true);

    public static readonly IReadOnlyList<SquadAbility> VariableBaseAbilities = new List<SquadAbility>
    {
        DeathExplode1,
        PsiDefense,
        PureDefense,
        AdvBoost1,
        PlusOneToCharge,
    };


    private static string GetVariableBaseDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var trimmed = name.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && int.TryParse(parts[^1], out _))
        {
            return string.Join(" ", parts, 0, parts.Length - 1);
        }

        return trimmed;
    }

    public static SquadAbility CreateVariableAbility(SquadAbility baseAbility, string modifierInput)
    {
        var parsedModifier = CombatHelpers.DamageParser(modifierInput);
        return new SquadAbility(
            baseAbility.Innate,
            $"{GetVariableBaseDisplayName(baseAbility.Name)} {modifierInput}",
            parsedModifier,
            false,
            true,
            modifierInput
        );
    }

    public static readonly IReadOnlyList<SquadAbility> All = new List<SquadAbility>
    {
        MinusHitRanged,
        MinusHitBrawl,
        DeathExplode1,
        MinusHit,
        CloseUpToShoot,
        FirstStrike,
        ShootRetreat,
        ChargeAfterRush,
        FightAfterMeleeDeath,
        SelfRessurection,
        PsiDefense,
        PureDefense,
        Teleport,
        ShootShellshock,
        Reanimator,
        AdvBoost1,
        AdvBoost6,
        Stampede,
        PlusOneToCharge,
        Satanic,
        ReRollBravery,
        Infect,
        ResistFirstDamage,
        WeakenStrongAttack,
        ReduceDamageBy1,
        ReduceDamageByHalf,
        ReduceDamageTo1,
        MoveAfterShooting,
        MoveBack,
        SquadrerollHits,
        SquadrerollHitOnes,
        SquadrerollInjuryOnes,
        SquadplusOneInjuries,
        SquadrerollInjuries,
        StopRerolls,
        NoModifiers,
        FiringDeck,
        CoverBenefit,
        SixPlusDodge,
        FreeHealthcare,
    };
}
