using System;
using System.Collections.Generic;
using System.Linq;

public enum OrderWindowType
{
    None,
    StartOfFightPhase,
    ChargePhase,
    OpponentChargePhaseStart,
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
    Shooters
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
