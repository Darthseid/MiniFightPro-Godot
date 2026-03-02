using System;
using System.Collections.Generic;
using System.Linq;

public enum OrderWindowType
{
    None,
    StartOfFightPhase,
    ChargePhase,
    OpponentChargePhaseStart,
    OpponentShootingPhaseStart,
    OnTargetedByShooting,
    AfterEnemyUnitFights,
    OnChargeDeclared
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
    {
        return WindowType == currentWindow;
    }

    public bool MatchesTargetType(Squad squad)
    {
        if (squad == null)
        {
            return false;
        }

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
            "Fight" => OrderWindowType.StartOfFightPhase,
            "Charge" => OrderWindowType.ChargePhase,
            _ => OrderWindowType.None
        };
    }

    public static List<Order> BuildDefaultOrders()
    {
        return new List<Order>
        {
            new Order(
                "epic_challenge",
                1,
                "Epic Challenge",
                "Fight",
                false,
                "Friendly Character squad gains temporary Precision on melee weapons for this fight phase.",
                OrderWindowType.StartOfFightPhase,
                OrderTargetSide.Friendly,
                OrderTargetType.Character,
                true),
            new Order(
                "tank_shock",
                1,
                "Tank Shock",
                "Charge",
                false,
                "Friendly Vehicle squad gains temporary Stampede until end of turn.",
                OrderWindowType.ChargePhase,
                OrderTargetSide.Friendly,
                OrderTargetType.Vehicle,
                true),
            new Order(
                "go_to_ground",
                1,
                "Go to Ground",
                "Opponent Shooting",
                false,
                "When targeted in opponent shooting phase, a friendly Infantry squad gains temporary Cover Benefit and Six Plus Dodge until end of that phase.",
                OrderWindowType.OnTargetedByShooting,
                OrderTargetSide.Friendly,
                OrderTargetType.Infantry,
                true),
            new Order(
                "counter_offensive",
                2,
                "Counter-Offensive",
                "Fight",
                false,
                "After an enemy squad fights, choose a friendly eligible squad to fight immediately next and gain temporary First Strike this turn.",
                OrderWindowType.AfterEnemyUnitFights,
                OrderTargetSide.Friendly,
                OrderTargetType.FightEligible,
                true),
            new Order(
                "heroic_intervention",
                2,
                "Heroic Intervention",
                "Fight",
                false,
                "At the beginning of opponent fight phase, a friendly squad within 6\" of an enemy already engaging another friendly squad may immediately move into engagement.",
                OrderWindowType.StartOfFightPhase,
                OrderTargetSide.Friendly,
                OrderTargetType.Any,
                true),
            new Order(
                "mists_of_deimos",
                3,
                "Mists of Deimos",
                "Opponent Shooting",
                false,
                "At beginning of opponent shooting phase, remove a friendly squad into strategic reserve. It returns at the start of your next shooting phase using teleport placement and cannot charge that turn.",
                OrderWindowType.OpponentShootingPhaseStart,
                OrderTargetSide.Friendly,
                OrderTargetType.Any,
                true),
            new Order(
                "fire_overwatch",
                1,
                "Fire Overwatch",
                "Charge",
                false,
                "At start of opponent charge phase, arm a friendly shooter squad to fire Overwatch when charged.",
                OrderWindowType.OpponentChargePhaseStart,
                OrderTargetSide.Friendly,
                OrderTargetType.Shooters,
                true)
        };
    }

    public static Order? FindById(string orderId)
    {
        return BuildDefaultOrders().FirstOrDefault(o => string.Equals(o.OrderId, orderId, StringComparison.OrdinalIgnoreCase));
    }
}
