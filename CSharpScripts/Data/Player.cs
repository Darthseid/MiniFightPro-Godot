using System;
using System.Collections.Generic;
using System.Linq;

public class Player
{
    public List<Squad> TheirSquads;
    public int OrderPoints;
    public List<Order> Orders;
    public bool IsAI;
    public string PlayerName;
    public List<string> PlayerAbilities;

    public Player() { }

    public Player(
        List<Squad> theirSquads,
        int orderPoints,
        List<Order> orders,
        bool isAI,
        string playerName,
        List<string> playerAbilities)
    {
        TheirSquads = theirSquads ?? new List<Squad>();
        OrderPoints = orderPoints;
        Orders = orders ?? new List<Order>();
        IsAI = isAI;
        PlayerName = playerName;
        PlayerAbilities = playerAbilities ?? new List<string>();
    }

    public List<Model> TheirModels => (TheirSquads ?? new List<Squad>())
        .SelectMany(s => s.Composition ?? new List<Model>())
        .ToList();

    public List<Weapon> TheirWeapons => TheirModels
        .SelectMany(model => model.Tools ?? new List<Weapon>())
        .ToList();

    public Player DeepCopy()
    {
        return new Player(
            (TheirSquads ?? new List<Squad>()).Select(s => s.DeepCopy()).ToList(),
            OrderPoints,
            (Orders ?? new List<Order>()).Select(o => new Order(o.OrderCost, o.OrderName, o.AvailablePhase, o.TargetsEnemy, o.Description)).ToList(),
            IsAI,
            PlayerName,
            (PlayerAbilities ?? new List<string>()).ToList());
    }
}

public static class PlayerAbilities
{
    public const string HiveMind = "Hive Mind";
    public const string AlienTerror = "Alien Terror";
    public const string Grim = "Grim";
    public const string WarriorBless = "Warrior Bless";
    public const string Martial = "Martial Stances";
    public const string Berserk = "Berserk";
    public const string Grief = "Demonic Grief";
    public const string Subroutines = "Subroutines";

    public static readonly IReadOnlyList<string> All = new List<string>
    {
        HiveMind,
        AlienTerror,
        Grim,
        WarriorBless,
        Martial,
        Berserk,
        Grief,
        Subroutines
    };
}
