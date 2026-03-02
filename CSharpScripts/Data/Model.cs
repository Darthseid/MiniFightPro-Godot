using System;
using System.Collections.Generic;
using System.Linq;

public class Model
{
    public string ModelId;
    public string Name;
    public int StartingHealth;
    public int Health;
    public int Bracketed;
    public List<Weapon> Tools;
    public string DefaultImagePath;
    public string CustomImagePath;
	
	public Model() { } // ✅ for JSON

    public Model(string name, int startingHealth, int health, int bracketed, List<Weapon> tools)
    {
        ModelId = Guid.NewGuid().ToString("N");
        Name = name;
        StartingHealth = startingHealth;
        Health = health;
        Bracketed = bracketed;
        Tools = tools ?? new List<Weapon>();
        DefaultImagePath = ModelImageService.DefaultPresetImagePath;
        CustomImagePath = string.Empty;
    }

    public Model(string name, int startingHealth, int bracketed, List<Weapon> tools)
        : this(name, startingHealth, startingHealth, bracketed, tools)
    {
    }

    public Model DeepCopy()
    {
        var copied = new Model(
            Name,
            StartingHealth,
            Health,
            Bracketed,
            Tools.Select(tool => tool.DeepCopy()).ToList()
        );

        copied.ModelId = string.IsNullOrWhiteSpace(ModelId) ? Guid.NewGuid().ToString("N") : ModelId;
        copied.DefaultImagePath = string.IsNullOrWhiteSpace(DefaultImagePath) ? ModelImageService.DefaultPresetImagePath : DefaultImagePath;
        copied.CustomImagePath = CustomImagePath ?? string.Empty;

        return copied;
    }
}
