using System.Collections.Generic;
using Xunit;

public class DeepCopyTests
{
    [Fact]
    public void PlayerDeepCopy_ClonesSquadsOrdersAndAbilities()
    {
        var weapon = TestObjectBuilder.BuildWeapon();
        var model = TestObjectBuilder.BuildModel(tools: new List<Weapon> { weapon });
        var squad = TestObjectBuilder.BuildSquad(composition: new List<Model> { model });
        var order = new Order("epic_challenge", 1, "Epic Challenge", "Melee", false, "desc", OrderWindowType.StartOfMeleePhase, OrderTargetSide.Friendly, OrderTargetType.Character, true);
        var player = new Player(new List<Squad> { squad }, 2, new List<Order> { order }, false, "P1", new List<string> { PlayerAbilities.Grim });

        var copy = player.DeepCopy();

        Assert.NotSame(player.TheirSquads, copy.TheirSquads);
        Assert.NotSame(player.TheirSquads[0], copy.TheirSquads[0]);
        Assert.NotSame(player.Orders, copy.Orders);
        Assert.NotSame(player.Orders[0], copy.Orders[0]);
        Assert.Equal(player.Orders[0].OrderId, copy.Orders[0].OrderId);
        Assert.NotSame(player.PlayerAbilities, copy.PlayerAbilities);
        Assert.Equal(player.PlayerAbilities, copy.PlayerAbilities);
    }

    [Fact]
    public void PlayerConstructor_SetsHasStrandedMiracle_WhenAbilityPresent()
    {
        var player = new Player(new List<Squad>(), 1, new List<Order>(), false, "p", new List<string> { PlayerAbilities.StrandedMiracle });

        Assert.True(player.HasStrandedMiracle);
    }

    [Fact]
    public void ModelDeepCopy_RegeneratesModelId_AndCopiesCoreFields()
    {
        var weapon = TestObjectBuilder.BuildWeapon(name: "Blade");
        var model = TestObjectBuilder.BuildModel(name: "Hero", health: 5, tools: new List<Weapon> { weapon });

        var copy = model.DeepCopy();

        Assert.NotEqual(model.ModelId, copy.ModelId);
        Assert.Equal(model.Name, copy.Name);
        Assert.Equal(model.StartingHealth, copy.StartingHealth);
        Assert.Equal(model.Health, copy.Health);
        Assert.Equal(model.Bracketed, copy.Bracketed);
    }

    [Fact]
    public void ModelDeepCopy_CopiesToolsAsNewListAndWeaponInstances()
    {
        var weapon = TestObjectBuilder.BuildWeapon(name: "Rifle");
        var model = TestObjectBuilder.BuildModel(tools: new List<Weapon> { weapon });

        var copy = model.DeepCopy();

        Assert.NotSame(model.Tools, copy.Tools);
        Assert.NotSame(model.Tools[0], copy.Tools[0]);
        Assert.Equal(model.Tools[0].WeaponName, copy.Tools[0].WeaponName);
    }

    [Fact]
    public void SquadDeepCopy_ClonesCompositionAbilitiesEmbarkedAndIdentityFields()
    {
        var embarked = TestObjectBuilder.BuildSquad(name: "Passengers", composition: new List<Model> { TestObjectBuilder.BuildModel("Passenger", 1) });
        var squad = TestObjectBuilder.BuildSquad(
            name: "Transport",
            type: new List<string> { "Vehicle", "Transport" },
            composition: new List<Model> { TestObjectBuilder.BuildModel("Driver", 3) },
            abilities: new List<SquadAbility> { SquadAbilities.BrainBlock });
        squad.EmbarkedSquad = embarked;

        var copy = squad.DeepCopy();

        Assert.NotSame(squad.Composition, copy.Composition);
        Assert.NotSame(squad.Composition[0], copy.Composition[0]);
        Assert.NotSame(squad.SquadAbilities, copy.SquadAbilities);
        Assert.Equal(squad.SquadAbilities[0].Name, copy.SquadAbilities[0].Name);
        Assert.NotNull(copy.EmbarkedSquad);
        Assert.NotSame(squad.EmbarkedSquad, copy.EmbarkedSquad);
        Assert.Equal(copy, copy.EmbarkedSquad!.TransportedBy);
        Assert.Equal(squad.Name, copy.Name);
        Assert.Equal(squad.Hardness, copy.Hardness);
        Assert.Equal(squad.Defense, copy.Defense);
        Assert.Equal(squad.Dodge, copy.Dodge);
    }
}
