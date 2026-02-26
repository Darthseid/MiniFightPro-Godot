using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Weapon
{
    public string WeaponName;
    public float Range;
    public string Attacks;
    public int HitSkill;
    public int Strength;
    public int ArmorPenetration;
    public string Damage;
    public List<WeaponAbility> Special;
    public bool IsMelee => Range <= 1f;

    public Weapon(
        string weaponName,
        float range,
        string attacks,
        int hitSkill,
        int strength,
        int armorPenetration,
        string damage,
        List<WeaponAbility> special
    )
    {
        WeaponName = weaponName;
        Range = range;
        Attacks = attacks;
        HitSkill = hitSkill;
        Strength = strength;
        ArmorPenetration = armorPenetration;
        Damage = damage;
        Special = special ?? new List<WeaponAbility>();
    }

    public Weapon DeepCopy()
    {
        return new Weapon(
            WeaponName,
            Range,
            Attacks,
            HitSkill,
            Strength,
            ArmorPenetration,
            Damage,
            new List<WeaponAbility>(Special)
        );
    }
}



public class WeaponAbility
{
    public string Innate;
    public string Name;
    public int Modifier;
    public bool IsTemporary;

    [JsonConstructor]
    public WeaponAbility(string innate, string name, int modifier, bool isTemporary)
    {
        Innate = innate;
        Name = name;
        Modifier = modifier;
        IsTemporary = isTemporary;
    }
}

public static class WeaponAbilities
{
    public static readonly WeaponAbility BonusHits1 = new WeaponAbility("Bonus Hits", "Bonus Hits 1", 1, false);
    public static readonly WeaponAbility BonusHits1Temp = new WeaponAbility("Bonus Hits", "TBonus Hits", 1, true);
    public static readonly WeaponAbility BonusHits2 = new WeaponAbility("Bonus Hits", "Bonus Hits 2", 2, false);
    public static readonly WeaponAbility BonusHits2Temp = new WeaponAbility("Bonus Hits", "Temp Bonus Hits 2", 2, true);
    public static readonly WeaponAbility BonusHits3 = new WeaponAbility("Bonus Hits", "Bonus Hits 3", 3, false);
    public static readonly WeaponAbility BonusHits3Temp = new WeaponAbility("Bonus Hits", "Temp Bonus Hits 3", 3, true);
    public static readonly WeaponAbility Precision = new WeaponAbility("rareFirst", "Precision", 0, false);
    public static readonly WeaponAbility TempPrecision = new WeaponAbility("rareFirst", "Temp Precision", 0, true);
    public static readonly WeaponAbility HardHits = new WeaponAbility("HH", "Hard Hits", 0, false);
    public static readonly WeaponAbility HardHitsTemp = new WeaponAbility("HH", "Temp Hard Hits", 0, true);
    public static readonly WeaponAbility Pike = new WeaponAbility("HH", "Pike", 0, false);
    public static readonly WeaponAbility PikeTemp = new WeaponAbility("HH", "Temp Pike", 0, true);
    public static readonly WeaponAbility Conversion = new WeaponAbility("Convert", "Conversion", 0, false);
    public static readonly WeaponAbility ConversionTemp = new WeaponAbility("Convert", "Temp Conversion", 0, true);
    public static readonly WeaponAbility DevastatingInjuries = new WeaponAbility("DI", "Devastating Injuries", 0, false);
    public static readonly WeaponAbility DevastatingInjuriesTemp = new WeaponAbility("DI", "Temp Devastating Injuries", 0, true);
    public static readonly WeaponAbility Perilous = new WeaponAbility("Self-Inflict", "Perilous", 0, false);
    public static readonly WeaponAbility Hefty = new WeaponAbility("Hefty", "Hefty", 0, false);
    public static readonly WeaponAbility HeftyTemp = new WeaponAbility("Hefty", "Temp Hefty", 0, true);
    public static readonly WeaponAbility IgnoresCover = new WeaponAbility("noCover", "Ignores Cover", 0, false);
    public static readonly WeaponAbility IgnoresCoverTemp = new WeaponAbility("Temp noCover", "Temp Ignores Cover", 0, true);
    public static readonly WeaponAbility Fusion1 = new WeaponAbility("Fusion", "Fusion 1", 1, false);
    public static readonly WeaponAbility Fusion1Temp = new WeaponAbility("Fusion", "Temp Fusion 1", 1, true);
    public static readonly WeaponAbility Fusion2 = new WeaponAbility("Fusion", "Fusion 2", 2, false);
    public static readonly WeaponAbility Fusion2Temp = new WeaponAbility("Fusion", "Temp Fusion 2", 2, true);
    public static readonly WeaponAbility Fusion3 = new WeaponAbility("Fusion", "Fusion 3", 3, false);
    public static readonly WeaponAbility Fusion3Temp = new WeaponAbility("Fusion", "Temp Fusion 3", 3, true);
    public static readonly WeaponAbility OneShot = new WeaponAbility("1 Shot", "One Shot", 0, false);
    public static readonly WeaponAbility OneShotTemp = new WeaponAbility("1 Shot", "Temp One Shot", 0, true);
    public static readonly WeaponAbility Pistol = new WeaponAbility("Handgun", "Pistol", 0, false);
    public static readonly WeaponAbility PistolTemp = new WeaponAbility("Handgun", "Temp Pistol", 0, true);
    public static readonly WeaponAbility Rapid1 = new WeaponAbility("Dakka", "Rapid Fire 1", 1, false);
    public static readonly WeaponAbility Rapid1Temp = new WeaponAbility("Dakka", "Temp Rapid Fire 1", 1, true);
    public static readonly WeaponAbility Rapid2 = new WeaponAbility("Dakka", "Rapid Fire 2", 2, false);
    public static readonly WeaponAbility Rapid2Temp = new WeaponAbility("Dakka", "Temp Rapid Fire 2", 2, true);
    public static readonly WeaponAbility Rapid3 = new WeaponAbility("Dakka", "Rapid Fire 3", 3, false);
    public static readonly WeaponAbility Rapid3Temp = new WeaponAbility("Dakka", "Temp Rapid Fire 3", 3, true);
    public static readonly WeaponAbility Skirmish = new WeaponAbility("RunGun", "Skirmish", 0, false);
    public static readonly WeaponAbility SkirmishTemp = new WeaponAbility("RunGun", "Skirmish", 0, true);
    public static readonly WeaponAbility AntiInfantry2 = new WeaponAbility("Mobkiller", "Anti-Infantry 2", 2, false);
    public static readonly WeaponAbility AntiInfantry3 = new WeaponAbility("Mobkiller", "Anti-Infantry 3", 3, false);
    public static readonly WeaponAbility AntiInfantry4 = new WeaponAbility("Mobkiller", "Anti-Infantry 4", 4, false);
    public static readonly WeaponAbility AntiMonster = new WeaponAbility("KaijuKiller", "Anti-Monster 2", 2, false);
    public static readonly WeaponAbility AntiVehicle = new WeaponAbility("TankKiller", "Anti-Vehicle 2", 2, false);
    public static readonly WeaponAbility AntiFly = new WeaponAbility("Ack-Ack", "Anti-Fly 2", 2, false);
    public static readonly WeaponAbility AntiCharacter = new WeaponAbility("Assassinate", "Anti-Character 2", 2, false);
    public static readonly WeaponAbility AntiPsi = new WeaponAbility("WitchKiller", "Anti-Psychic 2", 2, false);
    public static readonly WeaponAbility Blast = new WeaponAbility("Boom", "Blast", 0, false);
    public static readonly WeaponAbility BlastTemp = new WeaponAbility("Temp Boom", "Temp Blast", 0, true);
    public static readonly WeaponAbility InjuryReroll = new WeaponAbility("RRI", "Reroll Injuries", 0, false);
    public static readonly WeaponAbility TempInjuryReroll = new WeaponAbility("Temp RRI", "Temp Reroll Injuries", 0, true);
    public static readonly WeaponAbility Psychic = new WeaponAbility("Psi", "Psychic", 0, false);
    public static readonly WeaponAbility PsychicTemp = new WeaponAbility("Temp Psi", "Temp Psychic", 0, true);
    public static readonly WeaponAbility OneHitReroll = new WeaponAbility("!", "One Hit Reroll", 0, false);
    public static readonly WeaponAbility OneHitRerollTemp = new WeaponAbility("Temp !", "Temp One Hit Reroll", 0, true);
    public static readonly WeaponAbility RerollHits = new WeaponAbility("@", "All hits are rerolled", 0, false);
    public static readonly WeaponAbility RerollHitsTemp = new WeaponAbility("Temp @", "Temp All hits are rerolled", 0, true);
    public static readonly WeaponAbility RerollHitOnes = new WeaponAbility("#", "Hit rolls of 1 are rerolled.", 0, false);
    public static readonly WeaponAbility RerollHitOnesTemp = new WeaponAbility("Temp #", "Temp Hit rolls of 1 are rerolled.", 0, true);
    public static readonly WeaponAbility RerollInjuryOnes = new WeaponAbility("$", "Injury rolls of 1 are rerolled", 0, false);
    public static readonly WeaponAbility RerollInjuryOnesTemp = new WeaponAbility("Temp $", "Temp Injury rolls of 1 are rerolled", 0, true);
    public static readonly WeaponAbility OneWoundReroll = new WeaponAbility("%", "One Injury Reroll", 0, false);
    public static readonly WeaponAbility OneWoundRerollTemp = new WeaponAbility("Temp %", "Temp One Injury Reroll", 0, true);
    public static readonly WeaponAbility PlusOneInjuries = new WeaponAbility("^", "+1 to Injury Rolls", 0, false);
    public static readonly WeaponAbility PlusOneInjuriesTemp = new WeaponAbility("Temp ^", "Temp +1 to Injury Rolls", 0, true);

    public static readonly IReadOnlyList<WeaponAbility> All = new List<WeaponAbility>
    {
        BonusHits1,
        BonusHits2,
        BonusHits3,
        Precision,
        HardHits,
        Pike,
        Conversion,
        DevastatingInjuries,
        Perilous,
        Hefty,
        IgnoresCover,
        Fusion1,
        Fusion2,
        Fusion3,
        OneShot,
        Pistol,
        Rapid1,
        Rapid2,
        Rapid3,
        Skirmish,
        AntiInfantry2,
        AntiInfantry3,
        AntiInfantry4,
        AntiMonster,
        AntiVehicle,
        AntiFly,
        AntiCharacter,
        AntiPsi,
        Blast,
        InjuryReroll,
        Psychic,
        OneHitReroll,
        RerollHits,
        RerollHitOnes,
        RerollInjuryOnes,
        OneWoundReroll,
        PlusOneInjuries
    };
}