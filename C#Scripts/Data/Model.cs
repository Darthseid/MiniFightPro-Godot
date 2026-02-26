using System;
using System.Collections.Generic;
using System.Linq;

public class Model
{
    public string Name;
    public int StartingHealth;
    public int Health;
    public int Bracketed;
    public List<Weapon> Tools;
	
	public Model() { } // ✅ for JSON

    public Model(string name, int startingHealth, int health, int bracketed, List<Weapon> tools)
    {
        Name = name;
        StartingHealth = startingHealth;
        Health = health;
        Bracketed = bracketed;
        Tools = tools ?? new List<Weapon>();
    }

    public Model(string name, int startingHealth, int bracketed, List<Weapon> tools)
        : this(name, startingHealth, startingHealth, bracketed, tools)
    {
    }

    public Model DeepCopy()
    {
        return new Model(
            Name,
            StartingHealth,
            Health,
            Bracketed,
            Tools.Select(tool => tool.DeepCopy()).ToList()
        );
    }
}
