using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

public partial class GameData : Node
{
    public const string WeaponsFileName = "weapons.json";
    public const string ModelsFileName = "models.json";
    public const string SquadsFileName = "squads.json";
    public const string PlayersFileName = "players.json";
    public static GameData Instance { get; private set; }
    private bool _weaponsLoaded = false;
    private bool _modelsLoaded = false;
    private bool _squadsLoaded = false;
    private bool _playersLoaded = false;
    private bool _presetDataLoaded = false;

    public List<Weapon> WeaponList = new List<Weapon>();
    public int SelectedWeaponIndex = -1;
    public List<Model> ModelList = new List<Model>();
    public int SelectedModelIndex = -1;
    public List<Squad> SquadList = new List<Squad>();
    public int SelectedSquadIndex = -1;
    public List<Player> PlayerList = new List<Player>();
    public int SelectedPlayerIndex = -1;

    public override void _Ready()
    {
        Instance = this;
    }

    public void LoadWeaponsFromFile()
    {
        if (_weaponsLoaded)
        {
            return;
        }

        var path = $"user://{WeaponsFileName}";
        if (!FileAccess.FileExists(path))
        {
            EnsurePresetDataLoaded();
            _weaponsLoaded = true;
            return;
        }

        try
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var options = new JsonSerializerOptions { IncludeFields = true };
            var loadedWeapons = JsonSerializer.Deserialize<List<Weapon>>(json, options);
            WeaponList = loadedWeapons ?? new List<Weapon>();
            _weaponsLoaded = true;
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to load weapons file: {error.Message}");
            WeaponList = new List<Weapon>();
            _weaponsLoaded = true;
        }
    }

    public void SaveWeaponsToFile()
    {
        var path = $"user://{WeaponsFileName}";
        try
        {
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            var json = JsonSerializer.Serialize(WeaponList, options);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to save weapons file: {error.Message}");
        }
    }

    public void LoadModelsFromFile()
    {
        if (_modelsLoaded)
        {
            return;
        }

        var path = $"user://{ModelsFileName}";
        if (!FileAccess.FileExists(path))
        {
            EnsurePresetDataLoaded();
            _modelsLoaded = true;
            return;
        }

        try
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var options = new JsonSerializerOptions { IncludeFields = true };
            var loadedModels = JsonSerializer.Deserialize<List<Model>>(json, options);
            ModelList = loadedModels ?? new List<Model>();
            _modelsLoaded = true;
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to load models file: {error.Message}");
            ModelList = new List<Model>();
            _modelsLoaded = true;
        }
    }

    public void SaveModelsToFile()
    {
        var path = $"user://{ModelsFileName}";
        try
        {
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            var json = JsonSerializer.Serialize(ModelList, options);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to save models file: {error.Message}");
        }
    }

    public void LoadSquadsFromFile()
    {
        if (_squadsLoaded)
        {
            return;
        }

        var path = $"user://{SquadsFileName}";
        if (!FileAccess.FileExists(path))
        {
            EnsurePresetDataLoaded();
            _squadsLoaded = true;
            return;
        }

        try
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var options = new JsonSerializerOptions { IncludeFields = true };
            var loadedSquads = JsonSerializer.Deserialize<List<Squad>>(json, options);
            SquadList = loadedSquads ?? new List<Squad>();
            _squadsLoaded = true;
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to load squads file: {error.Message}");
            SquadList = new List<Squad>();
            _squadsLoaded = true;
        }
    }

    public void SaveSquadsToFile()
    {
        var path = $"user://{SquadsFileName}";
        try
        {
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            var json = JsonSerializer.Serialize(SquadList, options);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to save squads file: {error.Message}");
        }
    }

    public void LoadPlayersFromFile()
    {
        if (_playersLoaded)
        {
            return;
        }

        var path = $"user://{PlayersFileName}";
        if (!FileAccess.FileExists(path))
        {
            PlayerList = new List<Player>();
            _playersLoaded = true;
            return;
        }

        try
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            var json = file.GetAsText();
            var options = new JsonSerializerOptions { IncludeFields = true };
            var loadedPlayers = JsonSerializer.Deserialize<List<Player>>(json, options);
            PlayerList = loadedPlayers ?? new List<Player>();
            _playersLoaded = true;
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to load players file: {error.Message}");
            PlayerList = new List<Player>();
            _playersLoaded = true;
        }
    }

    public void SavePlayersToFile()
    {
        var path = $"user://{PlayersFileName}";
        try
        {
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            var json = JsonSerializer.Serialize(PlayerList, options);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(json);
        }
        catch (Exception error)
        {
            GD.PrintErr($"Failed to save players file: {error.Message}");
        }
    }

    public void SyncModelsWithWeapons()
    {
        if (WeaponList.Count == 0 || ModelList.Count == 0)
        {
            return;
        }

        var weaponLookup = new Dictionary<string, Weapon>();
        foreach (var weapon in WeaponList)
        {
            if (!string.IsNullOrWhiteSpace(weapon.WeaponName))
            {
                weaponLookup[weapon.WeaponName] = weapon;
            }
        }

        var updated = false;
        foreach (var model in ModelList)
        {
            if (model.Tools == null)
            {
                continue;
            }

            var updatedTools = new List<Weapon>();
            foreach (var tool in model.Tools)
            {
                if (tool?.WeaponName == null)
                {
                    continue;
                }

                if (weaponLookup.TryGetValue(tool.WeaponName, out var latestWeapon))
                {
                    updatedTools.Add(latestWeapon);
                }
            }

            if (updatedTools.Count != model.Tools.Count)
            {
                model.Tools = updatedTools;
                updated = true;
            }
            else
            {
                var hasMismatch = false;
                for (int i = 0; i < updatedTools.Count; i++)
                {
                    if (!ReferenceEquals(updatedTools[i], model.Tools[i]))
                    {
                        hasMismatch = true;
                        break;
                    }
                }

                if (hasMismatch)
                {
                    model.Tools = updatedTools;
                    updated = true;
                }
            }
        }

        if (updated)
        {
            SaveModelsToFile();
        }
    }

    public void SyncSquadsWithModels()
    {
        if (ModelList.Count == 0 || SquadList.Count == 0)
        {
            return;
        }

        var modelLookup = new Dictionary<string, Model>();
        foreach (var model in ModelList)
        {
            if (!string.IsNullOrWhiteSpace(model.Name))
            {
                modelLookup[model.Name] = model;
            }
        }

        var updated = false;
        foreach (var squad in SquadList)
        {
            if (squad.Composition == null)
            {
                continue;
            }

            var updatedModels = new List<Model>();
            foreach (var model in squad.Composition)
            {
                if (model?.Name == null)
                {
                    continue;
                }

                if (modelLookup.TryGetValue(model.Name, out var latestModel))
                {
                    updatedModels.Add(latestModel);
                }
            }

            if (updatedModels.Count != squad.Composition.Count)
            {
                squad.Composition = updatedModels;
                updated = true;
            }
            else
            {
                var hasMismatch = false;
                for (int i = 0; i < updatedModels.Count; i++)
                {
                    if (!ReferenceEquals(updatedModels[i], squad.Composition[i]))
                    {
                        hasMismatch = true;
                        break;
                    }
                }

                if (hasMismatch)
                {
                    squad.Composition = updatedModels;
                    updated = true;
                }
            }
        }

        if (updated)
        {
            SaveSquadsToFile();
        }
    }

    public void EnsurePresetDataLoaded()
    {
        if (_presetDataLoaded)
        {
            return;
        }

        var weaponsPath = $"user://{WeaponsFileName}";
        var modelsPath = $"user://{ModelsFileName}";
        var squadsPath = $"user://{SquadsFileName}";
        if (FileAccess.FileExists(weaponsPath) ||
            FileAccess.FileExists(modelsPath) ||
            FileAccess.FileExists(squadsPath))
        {
            _presetDataLoaded = true;
            return;
        }

        PopulatePresetData();
        _presetDataLoaded = true;
    }

    public void PopulatePresetData()
    {
        var presetWeapons = new List<Weapon>
        {
            new Weapon("Laser Gun", 24f, "1", 4, 3, 0, "1", new List<WeaponAbility> { WeaponAbilities.Rapid1 }),
            new Weapon("Frag Gun", 24f, "1", 3, 4, 0, "1", new List<WeaponAbility> { WeaponAbilities.Rapid1 }),
            new Weapon("Bayonet", 1f, "1", 4, 3, 0, "1", new List<WeaponAbility>()),
            new Weapon("Armored Fist", 1f, "1", 3, 4, 0, "1", new List<WeaponAbility>()),
            new Weapon("Flamethrower", 12f, "D6", 1, 4, 0, "1", new List<WeaponAbility> { WeaponAbilities.IgnoresCover }),
            new Weapon("Battle Cannon", 48f, "D6+3", 4, 10, -1, "3", new List<WeaponAbility> { WeaponAbilities.Blast }),
            new Weapon("Multi-Fusion", 18f, "2", 4, 9, -4, "D6", new List<WeaponAbility> { WeaponAbilities.Fusion1 }),
            new Weapon("Laser Cannon", 48f, "1", 4, 12, -3, "D6+1", new List<WeaponAbility>()),
            new Weapon("Armored Tracks", 1f, "6", 4, 7, 0, "1", new List<WeaponAbility>()),
            new Weapon("Disc Handgun", 12f, "1", 2, 4, -1, "1", new List<WeaponAbility> { WeaponAbilities.Skirmish, WeaponAbilities.Pistol }),
            new Weapon("Psi Pole", 0f, "2", 2, 3, 0, "D3", new List<WeaponAbility> { WeaponAbilities.AntiInfantry2 }),
            new Weapon("3s Plasma Pistol", 12f, "1", 3, 8, -3, "2", new List<WeaponAbility> { WeaponAbilities.Perilous }),
            new Weapon("4s Plasma Pistol", 12f, "1", 4, 8, -3, "2", new List<WeaponAbility> { WeaponAbilities.Perilous }),
            new Weapon("Gauss Killer", 48f, "1", 4, 14, -3, "6", new List<WeaponAbility> { WeaponAbilities.HardHits }),
            new Weapon("Disc Handgun", 12f, "1", 2, 4, -1, "1", new List<WeaponAbility> { WeaponAbilities.Skirmish, WeaponAbilities.Pistol }),
            new Weapon("Psi Pole", 0f, "2", 2, 3, 0, "D3", new List<WeaponAbility> { WeaponAbilities.AntiInfantry2 }),
            new Weapon("Twin Kannon", 36f, "D3", 4, 12, -2, "D6", new List<WeaponAbility> { WeaponAbilities.Blast, WeaponAbilities.Perilous, WeaponAbilities.InjuryReroll }),
            new Weapon("Twin Shooter", 36f, "4", 4, 6, -1, "1", new List<WeaponAbility> { WeaponAbilities.Rapid2, WeaponAbilities.BonusHits1, WeaponAbilities.InjuryReroll }),
            new Weapon("Apoc Release", 200f, "20", 3, 8, -2, "2", new List<WeaponAbility> { WeaponAbilities.Blast }),
            new Weapon("Defense Lasers", 48f, "1", 3, 12, -3, "D6+1", new List<WeaponAbility>()),
            new Weapon("Maul Gun", 36f, "6", 3, 6, -2, "2", new List<WeaponAbility>()),
            new Weapon("Smash Gun", 48f, "D3", 4, 9, -3, "4", new List<WeaponAbility> { WeaponAbilities.Blast }),
            new Weapon("Multi-Blaster", 100f, "30", 3, 9, -2, "3", new List<WeaponAbility> { WeaponAbilities.BonusHits1 }),
            new Weapon("Mecha Feet", 1f, "6", 4, 12, -2, "4", new List<WeaponAbility>()),
            new Weapon("Tornado Fragger", 18f, "3", 2, 4, -1, "2", new List<WeaponAbility> { WeaponAbilities.Rapid3, WeaponAbilities.InjuryReroll }),
            new Weapon("Bike Pike", 1f, "5", 2, 7, -2, "2", new List<WeaponAbility> { WeaponAbilities.Pike })
        };

        var planeWeapons = new List<Weapon>
        {
            new Weapon("Smash Gun", 48f, "D3", 4, 9, -3, "4", new List<WeaponAbility> { WeaponAbilities.Blast }),
            new Weapon("Twin Kannon", 36f, "D3", 4, 12, -2, "D6", new List<WeaponAbility> { WeaponAbilities.Blast, WeaponAbilities.Perilous, WeaponAbilities.InjuryReroll }),
            new Weapon("Twin Shooter", 36f, "4", 4, 6, -1, "1", new List<WeaponAbility> { WeaponAbilities.Rapid2, WeaponAbilities.BonusHits1, WeaponAbilities.InjuryReroll })
        };

        var guardWeapons = new List<Weapon>
        {
            new Weapon("Laser Gun", 24f, "1", 4, 3, 0, "1", new List<WeaponAbility> { WeaponAbilities.Rapid1 }),
            new Weapon("Bayonet", 1f, "1", 4, 3, 0, "1", new List<WeaponAbility>())
        };

        var marineWeapons = new List<Weapon>
        {
            new Weapon("Frag Gun", 24f, "1", 3, 4, 0, "1", new List<WeaponAbility> { WeaponAbilities.Rapid1 }),
            new Weapon("Armored Fist", 1f, "1", 3, 4, 0, "1", new List<WeaponAbility>())
        };

        var jetBikeWeapons = new List<Weapon>
        {
            new Weapon("Tornado Fragger", 18f, "3", 2, 4, -1, "2", new List<WeaponAbility> { WeaponAbilities.Rapid3, WeaponAbilities.InjuryReroll }),
            new Weapon("Bike Pike", 1f, "5", 2, 7, -2, "2", new List<WeaponAbility> { WeaponAbilities.Pike })
        };

        var flameMarineWeapons = new List<Weapon>
        {
            new Weapon("Flamethrower", 12f, "D6", 1, 4, 0, "1", new List<WeaponAbility> { WeaponAbilities.IgnoresCover }),
            new Weapon("Armored Fist", 1f, "1", 3, 4, 0, "1", new List<WeaponAbility>())
        };

        var sergeantWeapons = new List<Weapon>
        {
            new Weapon("3s Plasma Pistol", 12f, "1", 3, 8, -3, "2", new List<WeaponAbility> { WeaponAbilities.Perilous }),
            new Weapon("Power Gauntlet", 1f, "1", 3, 8, -2, "2", new List<WeaponAbility>())
        };

        var guardSargeWeapons = new List<Weapon>
        {
            new Weapon("4s Plasma Pistol", 12f, "1", 4, 8, -3, "2", new List<WeaponAbility> { WeaponAbilities.Perilous }),
            new Weapon("Power Spear", 1f, "2", 4, 4, -2, "1", new List<WeaponAbility>())
        };

        var tankWeapons = new List<Weapon>
        {
            new Weapon("Battle Cannon", 48f, "D6+3", 4, 10, -1, "3", new List<WeaponAbility> { WeaponAbilities.Blast }),
            new Weapon("Multi-Fusion", 18f, "2", 4, 9, -4, "D6", new List<WeaponAbility> { WeaponAbilities.Fusion1 }),
            new Weapon("Laser Cannon", 48f, "1", 4, 12, -3, "D6+1", new List<WeaponAbility>()),
            new Weapon("Armored Tracks", 1f, "6", 4, 7, 0, "1", new List<WeaponAbility>())
        };

        var clairvoyantWeapons = new List<Weapon>
        {
            new Weapon("Disc Pistol", 12f, "1", 2, 4, -1, "1", new List<WeaponAbility> { WeaponAbilities.Skirmish, WeaponAbilities.Pistol }),
            new Weapon("Psi Pole", 0f, "2", 2, 3, 0, "D3", new List<WeaponAbility> { WeaponAbilities.AntiInfantry2 })
        };

        var pylonWeapon = new List<Weapon>
        {
            new Weapon("Gauss Killer", 48f, "1", 4, 14, -3, "6", new List<WeaponAbility> { WeaponAbilities.HardHits })
        };

        var hugeMechaWeapons = new List<Weapon>
        {
            new Weapon("Apoc Release", 200f, "20", 3, 8, -2, "2", new List<WeaponAbility> { WeaponAbilities.Blast }),
            new Weapon("Defense Lasers", 48f, "1", 3, 12, -3, "D6+1", new List<WeaponAbility>()),
            new Weapon("Maul Gun", 36f, "6", 3, 6, -2, "2", new List<WeaponAbility>()),
            new Weapon("Apoc Release", 200f, "20", 3, 8, -2, "2", new List<WeaponAbility> { WeaponAbilities.Blast }),
            new Weapon("Defense Lasers", 48f, "1", 3, 12, -3, "D6+1", new List<WeaponAbility>()),
            new Weapon("Maul Gun", 36f, "6", 3, 6, -2, "2", new List<WeaponAbility>()),
            new Weapon("Multi-Blaster", 100f, "30", 3, 9, -2, "3", new List<WeaponAbility> { WeaponAbilities.BonusHits1 }),
            new Weapon("Mecha Feet", 1f, "6", 4, 12, -2, "4", new List<WeaponAbility>())
        };

        var presetModels = new List<Model>
        {
            new Model("Guard", 1, 0, guardWeapons),
            new Model("Marine", 2, 0, marineWeapons),
            new Model("Marine Sarge", 2, 0, sergeantWeapons),
            new Model("Flame Marine", 2, 0, flameMarineWeapons),
            new Model("Guard Sarge", 1, 1, guardSargeWeapons),
            new Model("Medium Tank", 13, 4, tankWeapons),
            new Model("Homemade Biplane", 12, 4, planeWeapons),
            new Model("Clairvoyant", 3, 0, clairvoyantWeapons),
            new Model("Zapper Pylon", 10, 0, pylonWeapon),
            new Model("Huge Mecha", 100, 33, hugeMechaWeapons),
            new Model("Floating Biker", 5, 0, jetBikeWeapons)
        };

        var mechaSquad = new List<Model> { new Model("Huge Mecha", 100, 33, hugeMechaWeapons) };
        var bikerSquad = new List<Model>();
        for (int i = 0; i < 3; i++)
        {
            bikerSquad.Add(presetModels[10].DeepCopy());
        }

        var planeSquad = new List<Model> { new Model("Homemade Biplane", 12, 4, planeWeapons) };
        var pylonModel = new List<Model> { new Model("Zapper Pylon", 10, 0, pylonWeapon) };
        var clairvoyantModels = new List<Model> { new Model("Clairvoyant", 3, 4, clairvoyantWeapons) };

        var guardSquad = new List<Model>();
        for (int i = 0; i < 18; i++)
        {
            guardSquad.Add(presetModels[0].DeepCopy());
        }
        for (int i = 0; i < 2; i++)
        {
            guardSquad.Add(presetModels[4].DeepCopy());
        }

        var marineSquad = new List<Model>();
        for (int i = 0; i < 2; i++)
        {
            marineSquad.Add(presetModels[3].DeepCopy());
        }
        marineSquad.Add(presetModels[2].DeepCopy());
        for (int i = 0; i < 7; i++)
        {
            marineSquad.Add(presetModels[1].DeepCopy());
        }

        var tankSquad = new List<Model>();
        for (int i = 0; i < 3; i++)
        {
            tankSquad.Add(presetModels[5].DeepCopy());
        }

        var presetSquads = new List<Squad>
        {
            new Squad("Guard Squad", 6f, 3, 5, 7, 7, 7, new List<string> { "Infantry" }, false, guardSquad, 20, new List<SquadAbility> { SquadAbilities.OfficerOrder }),
            new Squad("Marine Squad", 6f, 4, 3, 7, 7, 6, new List<string> { "Infantry" }, false, marineSquad, 20, new List<SquadAbility> { SquadAbilities.Satanic }),
            new Squad("Battle Tanks", 10f, 11, 2, 13, 7, 7, new List<string> { "Vehicle" }, false, tankSquad, 3, new List<SquadAbility> { SquadAbilities.DeathExplode2 }),
            new Squad("Homemade Biplane", 99.9f, 9, 3, 4, 7, 7, new List<string> { "Aircraft", "Fly", "Vehicle" }, false, planeSquad, 1, new List<SquadAbility> { SquadAbilities.DeathExplode2 }),
            new Squad("Zapper Pylon", 0f, 8, 3, 7, 7, 7, new List<string> { "Fortification", "Vehicle" }, false, pylonModel, 1, new List<SquadAbility> { SquadAbilities.DeathExplode2, SquadAbilities.Reanimator, SquadAbilities.Teleport }),
            new Squad("Clairvoyant", 7f, 3, 6, 4, 7, 6, new List<string> { "Character", "Infantry", "Psychic" }, true, clairvoyantModels, 1, new List<SquadAbility>()),
            new Squad("Huge Mecha", 10f, 16, 2, 5, 7, 6, new List<string> { "Titanic", "Vehicle" }, false, mechaSquad, 1, new List<SquadAbility> { SquadAbilities.DeathExplode2 }),
            new Squad("MagLev Bikes", 12f, 7, 2, 4, 7, 6, new List<string> { "Mounted", "Fly" }, false, bikerSquad, 3, new List<SquadAbility> { SquadAbilities.DeathExplode3 })
        };

        var presetPlayers = new List<Player>
        {
            new Player(
                new List<Squad>
                {
                    presetSquads[0].DeepCopy(),
                    presetSquads[1].DeepCopy(),
                    presetSquads[2].DeepCopy()
                },
                6,
                new List<Order>(),
                false,
                "Alliance Vanguard",
                new List<string> { PlayerAbilities.WarriorBless, PlayerAbilities.Grim }),
            new Player(
                new List<Squad>
                {
                    presetSquads[3].DeepCopy(),
                    presetSquads[4].DeepCopy(),
                    presetSquads[7].DeepCopy()
                },
                6,
                new List<Order>(),
                true,
                "Ravager Swarm",
                new List<string> { PlayerAbilities.HiveMind, PlayerAbilities.AlienTerror, PlayerAbilities.Berserk })
        };

        WeaponList = presetWeapons;
        ModelList = presetModels;
        SquadList = presetSquads;
        PlayerList = presetPlayers;
        SaveWeaponsToFile();
        SaveModelsToFile();
        SaveSquadsToFile();
        SavePlayersToFile();
        _weaponsLoaded = true;
        _modelsLoaded = true;
        _squadsLoaded = true;
        _playersLoaded = true;
    }
}

public static class UiExtensions
{
    public static void UnselectAll(this ItemList list)
    {
        for (int i = 0; i < list.ItemCount; i++)
            list.Deselect(i);
    }

    public static void SelectItemsByText(this ItemList list, HashSet<string> texts)
    {
        list.UnselectAll();

        for (int i = 0; i < list.ItemCount; i++)
        {
            if (texts.Contains(list.GetItemText(i)))
                list.Select(i);
        }
    }
}
