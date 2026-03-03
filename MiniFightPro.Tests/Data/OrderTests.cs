using System.Collections.Generic;
using Xunit;

public class OrderTests
{
    [Theory]
    [InlineData("Melee", OrderWindowType.StartOfMeleePhase)]
    [InlineData("Engagement", OrderWindowType.EngagementPhase)]
    [InlineData("Unknown", OrderWindowType.None)]
    public void Constructor_ParsesWindowType(string phase, OrderWindowType expected)
    {
        var order = new Order(1, "Test", phase, false, "desc");

        Assert.Equal(expected, order.WindowType);
    }

    [Fact]
    public void MatchesWindow_ReturnsTrueOnlyForSameWindowType()
    {
        var order = new Order(1, "Test", "Melee", false, "desc");

        Assert.True(order.MatchesWindow(OrderWindowType.StartOfMeleePhase));
        Assert.False(order.MatchesWindow(OrderWindowType.EngagementPhase));
    }

    [Fact]
    public void MatchesTargetType_Vehicle_WhenSquadContainsVehicle()
    {
        var order = new Order("id", 1, "name", "", false, "", OrderWindowType.None, OrderTargetSide.Friendly, OrderTargetType.Vehicle, true);
        var squad = TestObjectBuilder.BuildSquad(type: new List<string> { "Vehicle", "Infantry" });

        Assert.True(order.MatchesTargetType(squad));
    }

    [Fact]
    public void MatchesTargetType_Character_WhenSquadContainsCharacter()
    {
        var order = new Order("id", 1, "name", "", false, "", OrderWindowType.None, OrderTargetSide.Friendly, OrderTargetType.Character, true);
        var squad = TestObjectBuilder.BuildSquad(type: new List<string> { "Character" });

        Assert.True(order.MatchesTargetType(squad));
    }

    [Fact]
    public void MatchesTargetType_Infantry_WhenSquadContainsInfantry()
    {
        var order = new Order("id", 1, "name", "", false, "", OrderWindowType.None, OrderTargetSide.Friendly, OrderTargetType.Infantry, true);
        var squad = TestObjectBuilder.BuildSquad(type: new List<string> { "Infantry" });

        Assert.True(order.MatchesTargetType(squad));
    }

    [Fact]
    public void MatchesTargetType_Any_AlwaysTrue()
    {
        var order = new Order("id", 1, "name", "", false, "", OrderWindowType.None, OrderTargetSide.Friendly, OrderTargetType.Any, true);
        var squad = TestObjectBuilder.BuildSquad(type: new List<string> { "Anything" });

        Assert.True(order.MatchesTargetType(squad));
    }

    [Fact]
    public void BuildDefaultOrders_ReturnsNonEmptyWithStableIds_AndFindByIdCaseInsensitive()
    {
        var orders = Order.BuildDefaultOrders();

        Assert.NotEmpty(orders);
        Assert.All(orders, o => Assert.False(string.IsNullOrWhiteSpace(o.OrderId)));
        Assert.Contains(orders, o => o.OrderId == "epic_challenge");

        var found = Order.FindById("EPIC_CHALLENGE");
        Assert.NotNull(found);
        Assert.Equal("epic_challenge", found!.OrderId);
    }
}
