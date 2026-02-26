using System;

public class Order
{
    public int OrderCost;
    public string OrderName;
    public string AvailablePhase;
    public bool TargetsEnemy;
    public string Description;

    public Order() { }

    public Order(int orderCost, string orderName, string availablePhase, bool targetsEnemy, string description)
    {
        OrderCost = orderCost;
        OrderName = orderName;
        AvailablePhase = availablePhase;
        TargetsEnemy = targetsEnemy;
        Description = description;
    }
}
