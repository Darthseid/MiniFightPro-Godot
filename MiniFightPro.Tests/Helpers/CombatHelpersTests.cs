using System.Collections.Generic;
using Xunit;

public class CombatHelpersTests
{
    [Fact]
    public void DamageMods_NoAbilities_BaseDamageUnchanged()
    {
        var result = CombatHelpers.DamageMods(5, new List<SquadAbility>(), new List<WeaponAbility>(), half: false);

        Assert.Equal(5, result);
    }

    [Fact]
    public void DamageMods_Less1_ReducesAndFloorsAt1()
    {
        var abilities = new List<SquadAbility> { new SquadAbility("Less1", "Less1", 0, false) };

        Assert.Equal(3, CombatHelpers.DamageMods(4, abilities, new List<WeaponAbility>(), false));
        Assert.Equal(1, CombatHelpers.DamageMods(1, abilities, new List<WeaponAbility>(), false));
    }

    [Fact]
    public void DamageMods_Less2_HalvesRoundedUp()
    {
        var abilities = new List<SquadAbility> { new SquadAbility("Less2", "Less2", 0, false) };

        Assert.Equal(3, CombatHelpers.DamageMods(5, abilities, new List<WeaponAbility>(), false));
        Assert.Equal(1, CombatHelpers.DamageMods(1, abilities, new List<WeaponAbility>(), false));
    }

    [Fact]
    public void DamageMods_Less3_SetsToOne()
    {
        var abilities = new List<SquadAbility> { new SquadAbility("Less3", "Less3", 0, false) };

        Assert.Equal(1, CombatHelpers.DamageMods(8, abilities, new List<WeaponAbility>(), false));
    }

    [Fact]
    public void DamageMods_FusionAppliedBeforeLessModifiers()
    {
        var defender = new List<SquadAbility> { new SquadAbility("Less1", "Less1", 0, false) };
        var specials = new List<WeaponAbility> { new WeaponAbility("Fusion", "Fusion 1", 1, false) };

        var result = CombatHelpers.DamageMods(2, defender, specials, half: true);

        Assert.Equal(2, result);
    }

    [Fact]
    public void DamageMods_BrainBlockAndPsi_ReducesToZero()
    {
        var defender = new List<SquadAbility> { SquadAbilities.BrainBlock };
        var specials = new List<WeaponAbility> { WeaponAbilities.Psychic };

        var result = CombatHelpers.DamageMods(9, defender, specials, half: true);

        Assert.Equal(0, result);
    }

    [Fact]
    public void DamageMods_NeverNegative()
    {
        var defender = new List<SquadAbility> { new SquadAbility("Less1", "Less1", 0, false) };

        var result = CombatHelpers.DamageMods(0, defender, new List<WeaponAbility>(), half: false);

        Assert.True(result >= 0);
    }

    [Fact]
    public void CheckValidShooting_MeleeWeapon_IsInvalid()
    {
        var shooter = TestObjectBuilder.BuildSquad();
        var target = TestObjectBuilder.BuildSquad(name: "Target");
        var weapon = TestObjectBuilder.BuildWeapon(range: 1);

        var valid = CombatHelpers.CheckValidShooting(shooter, new MoveVars(false, false, false), weapon, target, 1);

        Assert.False(valid);
    }

    [Fact]
    public void CheckValidShooting_RangedWeaponBeyondRange_IsInvalid()
    {
        var shooter = TestObjectBuilder.BuildSquad();
        var target = TestObjectBuilder.BuildSquad(name: "Target");
        var weapon = TestObjectBuilder.BuildWeapon(range: 18);

        var valid = CombatHelpers.CheckValidShooting(shooter, new MoveVars(false, false, false), weapon, target, 24);

        Assert.False(valid);
    }

    [Fact]
    public void CheckValidShooting_RushWithoutRunGun_IsInvalid()
    {
        var shooter = TestObjectBuilder.BuildSquad();
        var target = TestObjectBuilder.BuildSquad(name: "Target");
        var weapon = TestObjectBuilder.BuildWeapon(range: 24, specials: new List<WeaponAbility>());

        var valid = CombatHelpers.CheckValidShooting(shooter, new MoveVars(true, true, false), weapon, target, 10);

        Assert.False(valid);
    }

    [Fact]
    public void CheckValidShooting_BoomWithinFightRange_IsInvalid()
    {
        var shooter = TestObjectBuilder.BuildSquad();
        var target = TestObjectBuilder.BuildSquad(name: "Target");
        var boom = new WeaponAbility("Boom", "Blast", 0, false);
        var weapon = TestObjectBuilder.BuildWeapon(range: 24, specials: new List<WeaponAbility> { boom });

        var valid = CombatHelpers.CheckValidShooting(shooter, new MoveVars(false, false, false), weapon, target, 1);

        Assert.False(valid);
    }

    [Fact]
    public void CheckValidShooting_DefenderCloseUpToShootBeyond12_IsInvalid()
    {
        var shooter = TestObjectBuilder.BuildSquad();
        var target = TestObjectBuilder.BuildSquad(
            name: "Target",
            abilities: new List<SquadAbility> { SquadAbilities.CloseUpToShoot });
        var weapon = TestObjectBuilder.BuildWeapon(range: 24);

        var valid = CombatHelpers.CheckValidShooting(shooter, new MoveVars(false, false, false), weapon, target, 13);

        Assert.False(valid);
    }
}
