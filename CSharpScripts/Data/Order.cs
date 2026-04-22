using System;
using System.Collections.Generic;
using System.Linq;

public enum OrderWindowType
{
    None,
    StartOfMeleePhase,
    EngagementPhase,
    OpponentEngagementPhaseStart,
    OpponentShootingPhaseStart,
    OnTargetedByShooting,
    OnChargeDeclared,
    StartOfMovementPhase
}

public enum OrderTargetSide
{
    None,
    Friendly,
    Enemy,
    Self
}

public enum OrderTargetType
{
    Any,
    Vehicle,
    Character,
    Shooters,
    Infantry,
    FightEligible
}

public class Order
{
    public string OrderId;
    public int OrderCost;
    public string OrderName;
    public string AvailablePhase;
    public bool TargetsEnemy;
    public string Description;
    public OrderWindowType WindowType;
    public OrderTargetSide TargetSide;
    public OrderTargetType TargetType;
    public bool RequiresTarget;

    public Order() { }

    public Order(int orderCost, string orderName, string availablePhase, bool targetsEnemy, string description)
    {
        OrderId = orderName?.Replace(" ", string.Empty) ?? string.Empty;
        OrderCost = orderCost;
        OrderName = orderName;
        AvailablePhase = availablePhase;
        TargetsEnemy = targetsEnemy;
        Description = description;
        WindowType = ParseWindowType(availablePhase);
        TargetSide = targetsEnemy ? OrderTargetSide.Enemy : OrderTargetSide.Friendly;
        TargetType = OrderTargetType.Any;
        RequiresTarget = true;
    }

    public Order(
        string orderId,
        int orderCost,
        string orderName,
        string availablePhase,
        bool targetsEnemy,
        string description,
        OrderWindowType windowType,
        OrderTargetSide targetSide,
        OrderTargetType targetType,
        bool requiresTarget)
    {
        OrderId = orderId;
        OrderCost = orderCost;
        OrderName = orderName;
        AvailablePhase = availablePhase;
        TargetsEnemy = targetsEnemy;
        Description = description;
        WindowType = windowType;
        TargetSide = targetSide;
        TargetType = targetType;
        RequiresTarget = requiresTarget;
    }

    public bool MatchesWindow(OrderWindowType currentWindow)
        { return WindowType == currentWindow; }

    public bool MatchesTargetType(Squad squad)
    {
        if (squad == null)
            return false;

        return TargetType switch
        {
            OrderTargetType.Vehicle => squad.SquadType?.Contains("Vehicle") == true,
            OrderTargetType.Character => squad.SquadType?.Contains("Character") == true,
            OrderTargetType.Infantry => squad.SquadType?.Contains("Infantry") == true,
            _ => true
        };
    }

    private static OrderWindowType ParseWindowType(string availablePhase)
    {
        return availablePhase switch
        {
            "Melee" => OrderWindowType.StartOfMeleePhase,
            "Engagement" => OrderWindowType.EngagementPhase,
            _ => OrderWindowType.None
        };
    }

    public static List<Order> BuildDefaultOrders()
    {
        return new List<Order>
		{
	new Order(
		"mano_a_mano",
		1,
		"Mano A Mano",
		"Melee",
		false,
		"Friendly Character squad gains temporary Precision on melee weapons for this melee phase.",
		OrderWindowType.StartOfMeleePhase,
		OrderTargetSide.Friendly,
		OrderTargetType.Character,
		true),

	new Order(
		"tank_shock",
		1,
		"Tank Shock",
		"Engagement",
		false,
		"Friendly Vehicle squad gains temporary Stampede until end of turn.",
		OrderWindowType.EngagementPhase,
		OrderTargetSide.Friendly,
		OrderTargetType.Vehicle,
		true),

	new Order(
		"hit_the_ground",
		1,
		"Hit The Ground",
		"Opponent Shooting",
		false,
		"When targeted in opponent shooting phase, a friendly Infantry squad gains temporary Cover Benefit and Six Plus Dodge until end of that phase.",
		OrderWindowType.OnTargetedByShooting,
		OrderTargetSide.Friendly,
		OrderTargetType.Infantry,
		true),

	new Order(
		"sudden_reflexes",
		2,
		"Sudden Reflexes",
		"Melee",
		false,
		"At start of melee phase, choose a friendly eligible squad; it gains temporary First Strike for this turn.",
		OrderWindowType.StartOfMeleePhase,
		OrderTargetSide.Friendly,
		OrderTargetType.FightEligible,
		true),

	new Order(
		"counter_charge",
		2,
		"Counter Charge",
		"Melee",
		false,
		"At the beginning of opponent melee phase, a friendly squad within 6\" of an enemy already engaging another friendly squad may immediately move into engagement.",
		OrderWindowType.StartOfMeleePhase,
		OrderTargetSide.Friendly,
		OrderTargetType.Any,
		true),

	new Order(
		"misty_retreat",
		3,
		"Misty Retreat",
		"Opponent Shooting",
		false,
		"At beginning of opponent shooting phase, remove a friendly squad into strategic reserve. It returns at the start of your next shooting phase using teleport placement and cannot charge that turn.",
		OrderWindowType.OpponentShootingPhaseStart,
		OrderTargetSide.Friendly,
		OrderTargetType.Any,
		true),

	new Order(
		"chutzpah",
		2,
		"Chutzpah",
		"Movement",
		false,
		"At beginning of active player movement phase, remove Shell Shock from a friendly squad.",
		OrderWindowType.StartOfMovementPhase,
		OrderTargetSide.Friendly,
		OrderTargetType.Any,
		true),

	new Order(
		"reactive_fire",
		1,
		"Reactive Fire",
		"Engagement",
		false,
		"At start of opponent engagement phase, arm a friendly shooter squad to fire ReactiveFire when charged.",
		OrderWindowType.OpponentEngagementPhaseStart,
		OrderTargetSide.Friendly,
		OrderTargetType.Shooters,
		true)
};
	}

    public static Order? FindById(string orderId)
        { return BuildDefaultOrders().FirstOrDefault(o => string.Equals(o.OrderId, orderId, StringComparison.OrdinalIgnoreCase)); }
}
