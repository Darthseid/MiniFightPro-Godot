using System.Collections.Generic;
using Xunit;

public class StepChecksTests
{
    [Fact]
    public void CleanupTemporaryAbilities_RemovesTemporarySquadAbilities()
    {
        var squad = TestObjectBuilder.BuildSquad(
            abilities: new List<SquadAbility>
            {
                new SquadAbility("perm", "Permanent", 0, false),
                new SquadAbility("temp", "Temporary", 0, true)
            });

        var cleaned = StepChecks.CleanupTemporaryAbilities(squad);

        Assert.Single(cleaned);
        Assert.Equal("Permanent", cleaned[0].Name);
    }

    [Fact]
    public void CleanupTemporaryAbilities_RemovesTemporaryWeaponAbilities()
    {
        var weapon = TestObjectBuilder.BuildWeapon(
            specials: new List<WeaponAbility>
            {
                new WeaponAbility("perm", "Permanent", 0, false),
                new WeaponAbility("temp", "Temporary", 0, true)
            });
        var model = TestObjectBuilder.BuildModel(tools: new List<Weapon> { weapon });
        var squad = TestObjectBuilder.BuildSquad(
            composition: new List<Model> { model },
            abilities: new List<SquadAbility> { new SquadAbility("perm", "Permanent", 0, false) });

        _ = StepChecks.CleanupTemporaryAbilities(squad);

        Assert.Single(weapon.Special);
        Assert.Equal("Permanent", weapon.Special[0].Name);
    }

    [Fact]
    public void CleanupTemporaryAbilities_NullSquadAbilities_ReturnsEmptyList()
    {
        var squad = TestObjectBuilder.BuildSquad();
        squad.SquadAbilities = null!;

        var cleaned = StepChecks.CleanupTemporaryAbilities(squad);

        Assert.Empty(cleaned);
    }

    [Fact]
    public void CleanupTemporaryAbilities_NullTools_DoesNotThrow()
    {
        var model = TestObjectBuilder.BuildModel();
        model.Tools = null!;
        var squad = TestObjectBuilder.BuildSquad(
            composition: new List<Model> { model },
            abilities: new List<SquadAbility> { new SquadAbility("perm", "Permanent", 0, false) });

        var exception = Record.Exception(() => StepChecks.CleanupTemporaryAbilities(squad));

        Assert.Null(exception);
    }
}
