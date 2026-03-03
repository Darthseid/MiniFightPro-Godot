using System.Collections.Generic;

public static class TestObjectBuilder
{
    public static Weapon BuildWeapon(
        string name = "Rifle",
        float range = 24,
        string attacks = "1",
        int hitSkill = 3,
        int strength = 4,
        int ap = 0,
        string damage = "1",
        List<WeaponAbility>? specials = null)
    {
        return new Weapon(name, range, attacks, hitSkill, strength, ap, damage, specials ?? new List<WeaponAbility>());
    }

    public static Model BuildModel(string name = "Marine", int health = 2, List<Weapon>? tools = null)
    {
        return new Model(name, health, health, 0, tools ?? new List<Weapon>());
    }

    public static Squad BuildSquad(
        string name = "Squad",
        List<string>? type = null,
        List<Model>? composition = null,
        int defense = 3,
        int dodge = 6,
        int hardness = 4,
        List<SquadAbility>? abilities = null)
    {
        var models = composition ?? new List<Model> { BuildModel() };
        return new Squad(
            name,
            6,
            hardness,
            defense,
            dodge,
            0,
            6,
            type ?? new List<string> { "Infantry" },
            false,
            models,
            models.Count,
            abilities ?? new List<SquadAbility>());
    }
}
