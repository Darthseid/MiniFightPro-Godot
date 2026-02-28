using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class StepChecks
{

    public static readonly SquadAbility UsedOfficerOrder = new SquadAbility("UOO", "Used Officer Order", 0, false);
    private static readonly SquadAbility ActiveAimBoost = new SquadAbility("", "activeAimBoost", 0, false);
    private static readonly SquadAbility ActiveFightBoost = new SquadAbility("", "activeFightBoost", 0, false);
    private static readonly SquadAbility ActiveShootBoost = new SquadAbility("", "activeShootBoost", 0, false);
    private static readonly SquadAbility ActiveHeadsDown = new SquadAbility("", "activeHeadsDown", 0, false);
    private static readonly SquadAbility ActiveDuty = new SquadAbility("", "activeDuty", 0, false);

    public static Action<Squad> SquadRegenerationHandler { get; set; }

    public static async Task RoundStartChecks(Player activePlayer, Player inactivePlayer, BattleHud hud)
    {
        if (activePlayer == null || inactivePlayer == null)
        {
            return;
        }

        ApplyPlayerWideAbilities(activePlayer);
        ApplyPlayerWideAbilities(inactivePlayer);

        var activeLead = activePlayer.TheirSquads?.FirstOrDefault();
        var inactiveLead = inactivePlayer.TheirSquads?.FirstOrDefault();
        if (activeLead != null && inactiveLead != null)
        {
            await RoundStartChecks(activeLead, inactiveLead, hud);
        }
    }

    public static async Task CommandPhaseChecks(Player activePlayer, Player inactivePlayer, Squad activeSquad, Squad inactiveSquad, BattleHud hud)
    {
        if (activePlayer == null || inactivePlayer == null)
        {
            return;
        }

        ApplyPlayerWideAbilities(activePlayer);
        ApplyPlayerWideAbilities(inactivePlayer);

        var enemyReference = inactiveSquad ?? inactivePlayer.TheirSquads?.FirstOrDefault();
        if (enemyReference == null)
        {
            return;
        }

        var squadsToCheck = activePlayer.TheirSquads ?? new List<Squad>();
        if (squadsToCheck.Count == 0 && activeSquad != null)
        {
            squadsToCheck = new List<Squad> { activeSquad };
        }

        foreach (var squad in squadsToCheck)
        {
            if (squad != null)
            {
                await CommandPhaseChecks(squad, enemyReference, hud);
            }
        }
    }

    private static void ApplyPlayerWideAbilities(Player player)
    {
        if (player?.TheirSquads == null || player.PlayerAbilities == null)
        {
            return;
        }

        foreach (var squad in player.TheirSquads)
        {
            if (squad?.SquadAbilities == null)
            {
                continue;
            }

            if (player.PlayerAbilities.Contains(PlayerAbilities.HiveMind) && squad.SquadAbilities.All(a => a.Name != SquadAbilities.hiveMind.Name))
                squad.SquadAbilities.Add(SquadAbilities.hiveMind);

            if (player.PlayerAbilities.Contains(PlayerAbilities.Berserk) && squad.SquadAbilities.All(a => a.Name != SquadAbilities.berserking.Name))
                squad.SquadAbilities.Add(SquadAbilities.berserking);

            if (player.PlayerAbilities.Contains(PlayerAbilities.WarriorBless) && squad.SquadAbilities.All(a => a.Name != SquadAbilities.warriorBless.Name))
                squad.SquadAbilities.Add(SquadAbilities.warriorBless);

            if (player.PlayerAbilities.Contains(PlayerAbilities.Martial) && squad.SquadAbilities.All(a => a.Name != SquadAbilities.MartialStance.Name))
                squad.SquadAbilities.Add(SquadAbilities.MartialStance);

            if (player.PlayerAbilities.Contains(PlayerAbilities.Subroutines) && squad.SquadAbilities.All(a => a.Name != SquadAbilities.SubRoutine.Name))
                squad.SquadAbilities.Add(SquadAbilities.SubRoutine);

            if (player.PlayerAbilities.Contains(PlayerAbilities.OfficerOrder) && squad.SquadAbilities.All(a => a.Name != SquadAbilities.OfficerOrder.Name))
                squad.SquadAbilities.Add(SquadAbilities.OfficerOrder);
        }
    }

    public static List<SquadAbility> CleanupTemporaryAbilities(Squad unit)
    {
        if (unit?.SquadAbilities == null)
        {
            return new List<SquadAbility>();
        }

        var cleanedSquadAbilities = unit.SquadAbilities.Where(ability => !ability.IsTemporary).ToList();
        (unit.Composition ?? new List<Model>())
            .SelectMany(model => model.Tools)
            .DistinctBy(weapon => weapon.WeaponName)
            .ToList()
            .ForEach(weapon => weapon.Special.RemoveAll(ability => ability.IsTemporary));
        return cleanedSquadAbilities;
    }

    public static async Task CommandPhaseChecks(Squad activeSquad, Squad inactiveSquad, BattleHud hud)
    {
        if (activeSquad == null || inactiveSquad == null)
        {
            return;
        }

        if (inactiveSquad.SquadAbilities.Any(ability => ability.Innate == "Hive") && hud != null)
        {
            await ActivateAlienTerror(activeSquad, inactiveSquad, hud);
        }

        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Zombie"))
        {
            TriggerSquadRegeneration(activeSquad);
        }
        if (inactiveSquad.SquadAbilities.Any(ability => ability.Innate == "Hive") && hud != null)
        {
            await ActivateAlienTerror(activeSquad, inactiveSquad, hud);
        }
        if (activeSquad.SquadAbilities.Any(ability => ability.Name == "Officer Order") &&
            activeSquad.SquadAbilities.All(ability => ability.Innate != UsedOfficerOrder.Innate))
        {
            if (hud == null)
            {
                return;
            }

            var options = new[]
            {
                "Melee Maneuver! (+1 to melee hit)",
                "Precision Fire! (+1 to ranged hit)",
                "Volley Fire! (+1 to Rapid Fire attacks)",
                "Duck & Cover! (+1 defense, max 3+)",
                "Remain Steadfast! (-1 bravery)",
                "Roll Out! (+3 movement)"
            };
            AudioManager.Instance?.Play("bugle");
            var choice = await hud.ChooseOptionAsync($"Choose an order for {activeSquad.Name}", options);
            switch (choice)
            {
                case 0:
                    activeSquad.Composition
                        .SelectMany(model => model.Tools)
                        .DistinctBy(weapon => weapon.WeaponName)
                        .Where(weapon => weapon.IsMelee)
                        .ToList()
                        .ForEach(weapon => weapon.HitSkill = Math.Max(2, weapon.HitSkill - 1));
                    activeSquad.SquadAbilities.Add(ActiveFightBoost);
                    activeSquad.SquadAbilities.Add(UsedOfficerOrder);
                    break;
                case 1:
                    activeSquad.Composition
                        .SelectMany(model => model.Tools)
                        .DistinctBy(weapon => weapon.WeaponName)
                        .Where(weapon => !weapon.IsMelee)
                        .ToList()
                        .ForEach(weapon => weapon.HitSkill = Math.Max(2, weapon.HitSkill - 1));
                    activeSquad.SquadAbilities.Add(ActiveAimBoost);
                    activeSquad.SquadAbilities.Add(UsedOfficerOrder);
                    break;
                case 2:
                    activeSquad.Composition
                        .SelectMany(model => model.Tools)
                        .DistinctBy(weapon => weapon.WeaponName)
                        .Where(weapon => weapon.Special.Any(ability => ability.Innate == "Dakka"))
                        .ToList()
                        .ForEach(weapon =>
                        {
                            var currentAttacks = CombatHelpers.DamageParser(weapon.Attacks);
                            weapon.Attacks = (currentAttacks + 1).ToString();
                        });
                    activeSquad.SquadAbilities.Add(ActiveShootBoost);
                    activeSquad.SquadAbilities.Add(UsedOfficerOrder);
                    break;
                case 3:
                    activeSquad.Defense = Math.Max(3, activeSquad.Defense - 1);
                    activeSquad.SquadAbilities.Add(ActiveHeadsDown);
                    activeSquad.SquadAbilities.Add(UsedOfficerOrder);
                    break;
                case 4:
                    activeSquad.Bravery -= 1;
                    activeSquad.SquadAbilities.Add(ActiveDuty);
                    activeSquad.SquadAbilities.Add(UsedOfficerOrder);
                    break;
                case 5:
                    activeSquad.SquadAbilities.Add(UsedOfficerOrder);
                    break;
            }
        }
    }


    public static async Task RoundStartChecks(Squad teamASquad, Squad teamBSquad, BattleHud hud)
    {
        if (hud == null)
        {
            return;
        }

        var squads = new[] { teamASquad, teamBSquad };
        foreach (var squad in squads)
        {
            if (squad == null)
            {
                continue;
            }

            if (squad.SquadAbilities.Any(ability => ability.Innate == "Rampage"))
            {
                await Berserking(squad, hud);
            }

            if (squad.SquadAbilities.Any(ability => ability.Innate == "Angry God"))
            {
                await GenerateBlessings(squad, hud);
            }
        }
    }

    public static async Task ActivateAlienTerror(Squad activeSquad, Squad inactiveSquad, BattleHud hud)
    {
        if (activeSquad == null || inactiveSquad == null || hud == null)
        {
            return;
        }

        var useAlienTerror = await hud.ConfirmActionAsync(
            $"{inactiveSquad.Name}: Would you like to activate Alien Terror?",
            "Activate",
            "Skip"
        );

        if (!useAlienTerror)
        {
            return;
        }

        AudioManager.Instance?.Play("demonlaugh");
        activeSquad.ShellShock = ShellShockTest(activeSquad, inactiveSquad.SquadAbilities);
        inactiveSquad.SquadAbilities.RemoveAll(ability => ability.Innate == "Hive");
    }

    public static float MovementPhaseChecks(Squad activeDude)
    {
        if (activeDude.SquadAbilities.Any(ability => ability.Innate == "OO") &&
            activeDude.SquadAbilities.All(ability => ability.Innate != UsedOfficerOrder.Innate) &&
            !activeDude.ShellShock)
        {
            return 3f;
        }
        return 0f;
    }

    public static async Task<float> ChargePhaseChecks(Squad activeSquad, Squad enemySquad, bool movedAfterShooting)
    {
        await Task.Yield();
        if (movedAfterShooting)
        {
            return -12f;
        }

        var chargeBoost = activeSquad.SquadAbilities.FirstOrDefault(ability => ability.Innate == "+Charge")?.ResolveModifier() ?? 1;
        return chargeBoost;
    }

    private static async Task ShootingPhaseChecksBase(Squad activeSquad, Squad inactiveSquad)
    {
        if (activeSquad == null || inactiveSquad == null)
        {
            return;
        }

        if (inactiveSquad.SquadAbilities.Any(ability => ability.Innate == "Run Away"))
        {
            return;
        }
        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Satan"))
        {
            await Task.Yield();
        }
        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "SubRoutine"))
        {
            await Task.Yield();
        }
    }


    public static void EndOfRoundChecks(Squad activeDude)
    {
        activeDude.SquadAbilities.RemoveAll(ability => ability.Innate == UsedOfficerOrder.Innate);
        if (activeDude.SquadAbilities.Any(ability => ability.Name == ActiveAimBoost.Name))
        {
            activeDude.Composition
                .SelectMany(model => model.Tools)
                .Where(weapon => !weapon.IsMelee)
                .DistinctBy(weapon => weapon.WeaponName)
                .ToList()
                .ForEach(weapon => weapon.HitSkill += 1);
            activeDude.SquadAbilities.RemoveAll(ability => ability.Name == ActiveAimBoost.Name);
        }

        if (activeDude.SquadAbilities.Any(ability => ability.Name == ActiveFightBoost.Name))
        {
            activeDude.Composition
                .SelectMany(model => model.Tools)
                .Where(weapon => weapon.IsMelee)
                .DistinctBy(weapon => weapon.WeaponName)
                .ToList()
                .ForEach(weapon => weapon.HitSkill += 1);
            activeDude.SquadAbilities.RemoveAll(ability => ability.Name == ActiveFightBoost.Name);
        }

        if (activeDude.SquadAbilities.Any(ability => ability.Name == ActiveShootBoost.Name))
        {
            activeDude.Composition
                .SelectMany(model => model.Tools)
                .Where(weapon => weapon.Special.Any(ability => ability.Innate == "Dakka"))
                .DistinctBy(weapon => weapon.WeaponName)
                .ToList()
                .ForEach(weapon =>
                {
                    var attacks = CombatHelpers.DamageParser(weapon.Attacks) - 1;
                    weapon.Attacks = attacks.ToString();
                });
            activeDude.SquadAbilities.RemoveAll(ability => ability.Name == ActiveShootBoost.Name);
        }

        if (activeDude.SquadAbilities.Any(ability => ability.Name == ActiveHeadsDown.Name))
        {
            activeDude.Defense += 1;
            activeDude.SquadAbilities.RemoveAll(ability => ability.Name == ActiveHeadsDown.Name);
        }

        if (activeDude.SquadAbilities.Any(ability => ability.Name == ActiveDuty.Name))
        {
            activeDude.Bravery += 1;
            activeDude.SquadAbilities.RemoveAll(ability => ability.Name == ActiveDuty.Name);
        }
    }

    public static async Task GenerateBlessings(Squad activeDude, BattleHud hud)
    {
        if (activeDude == null || hud == null)
            return;

        // Roll 8d6
        var rolls = new List<int>(8);
        for (int i = 0; i < 8; i++)
            rolls.Add(DiceHelpers.SimpleRoll(6));

        // Local helpers
        static int CountOf(List<int> list, int face) => list.Count(v => v == face);

        static void RemoveOccurrences(List<int> list, int face, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int idx = list.IndexOf(face);
                if (idx >= 0) list.RemoveAt(idx);
            }
        }

        void AddAbilityIfMissing(SquadAbility ability)
        {
            if (activeDude.SquadAbilities.All(a => a.Innate != ability.Innate || string.IsNullOrEmpty(ability.Innate)))
                activeDude.SquadAbilities.Add(ability);
        }

        void AddWeaponSpecialToDistinctMelee(WeaponAbility ability)
        {
            activeDude.Composition
                .SelectMany(m => m.Tools)
                .Where(w => w.IsMelee)
                .DistinctBy(w => w.WeaponName)
                .ToList()
                .ForEach(w => w.Special.Add(ability));
        }

        // Blessing registry: Name + Apply() + CanApply()
        var blessings = new List<(string Name, Func<bool> CanApply, Action Apply)>();

        // Double 6 => Charge After Rushing
        blessings.Add((
            "Charge After Rushing (Double 6)",
            () => CountOf(rolls, 6) >= 2,
            () =>
            {
                AddAbilityIfMissing(SquadAbilities.ChargeAfterRush);
                RemoveOccurrences(rolls, 6, 2);
            }
        ));

        // Triple 4 => Charge After Rushing
        blessings.Add((
            "Charge After Rushing (Triple 4)",
            () => CountOf(rolls, 4) >= 3,
            () =>
            {
                AddAbilityIfMissing(SquadAbilities.ChargeAfterRush);
                RemoveOccurrences(rolls, 4, 3);
            }
        ));

        // Double 5 => Melee Hard Hits
        blessings.Add((
            "Melee Hard Hits (Double 5)",
            () => CountOf(rolls, 5) >= 2,
            () =>
            {
                AddWeaponSpecialToDistinctMelee(WeaponAbilities.HardHits);
                RemoveOccurrences(rolls, 5, 2);
            }
        ));

        // Double 4 => Fight After Melee Death
        blessings.Add((
            "Fight After Melee Death (Double 4)",
            () => CountOf(rolls, 4) >= 2,
            () =>
            {
                AddAbilityIfMissing(SquadAbilities.FightAfterMeleeDeath);
                RemoveOccurrences(rolls, 4, 2);
            }
        ));

        // Triple any => Melee Hard Hits
        blessings.Add((
            "Melee Hard Hits (Triple Any)",
            () => rolls.GroupBy(v => v).Any(g => g.Count() >= 3),
            () =>
            {
                AddWeaponSpecialToDistinctMelee(WeaponAbilities.HardHits);
                var triple = rolls.GroupBy(v => v).First(g => g.Count() >= 3).Key;
                RemoveOccurrences(rolls, triple, 3);
            }
        ));

        // Double 3 => Bonus Hits 1 (melee)
        blessings.Add((
            "Bonus Hits 1 (Double 3)",
            () => CountOf(rolls, 3) >= 2,
            () =>
            {
                AddWeaponSpecialToDistinctMelee(WeaponAbilities.BonusHits1);
                RemoveOccurrences(rolls, 3, 2);
            }
        ));

        // Double any => Damage Resistance boost (matches your Kotlin: damageResistance -= 1)
        blessings.Add((
            "Damage Resistance (Double Any)",
            () => rolls.GroupBy(v => v).Any(g => g.Count() >= 2),
            () =>
            {
                activeDude.DamageResistance -= 1;
                activeDude.SquadAbilities.Add(new SquadAbility("", "➗ Boost1", 0, false));
                var dbl = rolls.GroupBy(v => v).First(g => g.Count() >= 2).Key;
                RemoveOccurrences(rolls, dbl, 2);
            }
        ));

        // Double any => +2 Movement
        blessings.Add((
            "Movement Boost (+2 Movement)",
            () => rolls.GroupBy(v => v).Any(g => g.Count() >= 2),
            () =>
            {
                activeDude.Movement += 2f;
                activeDude.SquadAbilities.Add(new SquadAbility("", "Move2", 0, false));
                var dbl = rolls.GroupBy(v => v).First(g => g.Count() >= 2).Key;
                RemoveOccurrences(rolls, dbl, 2);
            }
        ));

        // Filter to valid
        var valid = blessings.Where(b => b.CanApply()).ToList();
        if (valid.Count == 0)
        {
            hud.ShowToast("No valid blessings available!");
            return;
        }

        // Pick up to 2 blessings (since BattleHud currently looks single-choice based)
        // This mimics the Android multi-choice but in two steps.
        var chosen = new List<int>();

        for (int pick = 0; pick < 2; pick++)
        {
            var names = valid.Select(v => v.Name).ToArray();
            var selection = await hud.ChooseOptionAsync($"Select Blessing {pick + 1}/2 for {activeDude.Name} (or Cancel)", names);

            // If your ChooseOptionAsync can’t cancel, you can add a "Done" option instead.
            // Here we treat out-of-range as cancel.
            if (selection < 0 || selection >= valid.Count)
                break;

            if (chosen.Contains(selection))
            {
                hud.ShowToast("Already selected that blessing.");
                pick--;
                continue;
            }

            chosen.Add(selection);

            // Optional: remove it from future picks so you don't even see duplicates
            // but keep it simple: allow and block duplicates.
        }

        foreach (var idx in chosen)
            valid[idx].Apply();
    }

    public static async Task Berserking(Squad activeDude, BattleHud hud)
    {
        if (activeDude == null || hud == null)
            return;

        var berserkName = activeDude.Name;
        var confirm = await hud.ConfirmActionAsync($"Activate Berserking this round for {berserkName}?");
        if (!confirm)
            return;

        AudioManager.Instance?.Play("battle_cry");

        // Kotlin: add marker + charge after rush
        activeDude.SquadAbilities.Add(new SquadAbility("", "buffStrengthAndAttack", 0, false));
        activeDude.SquadAbilities.Add(SquadAbilities.ChargeAfterRush);

        // Kotlin: if dodge > 5, set dodge = 5 and add marker
        if (activeDude.Dodge > 5)
        {
            activeDude.SquadAbilities.Add(new SquadAbility("", "improvedOrkInvuln", 0, false));
            activeDude.Dodge = 5;
        }

        // Melee weapons: +1 strength, +1 attacks
        activeDude.Composition
            .SelectMany(m => m.Tools)
            .Where(w => w.IsMelee)
            .DistinctBy(w => w.WeaponName)
            .ToList()
            .ForEach(w =>
            {
                w.Strength += 1;
                var a = CombatHelpers.DamageParser(w.Attacks);
                w.Attacks = (a + 1).ToString();
            });
    }

    public static bool ShellShockTest(Squad activeSquad, List<SquadAbility> inActiveAbilities)
    {
        if (activeSquad == null || inActiveAbilities == null)
        {
            return false;
        }

        var baseBravery = activeSquad.Bravery;
        var shellShockModifier = 0;
        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Bad Juju"))
        {
            shellShockModifier += 1;
        }
        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Hive"))
        {
            shellShockModifier += DiceHelpers.SimpleRoll(6);
        }
        if (inActiveAbilities.Any(ability => ability.Innate == "Grim"))
        {
            shellShockModifier -= 1;
        }

        var test = DiceHelpers.Roll2d6() + shellShockModifier < baseBravery;
        if (test && activeSquad.SquadAbilities.Any(ability => ability.Innate == "TryAgain"))
        {
            test = DiceHelpers.Roll2d6() + shellShockModifier < baseBravery;
        }

        if (test)
        {
            AudioManager.Instance?.Play("failedbravery");
        }

        // If the active squad has Bad Juju and DID NOT fail, trigger regeneration (preserve original behavior)
        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Bad Juju") && !test)
        {
            TriggerSquadRegeneration(activeSquad);
        }
        // apply D3 Pure damage to the active squad (use SimpleRoll(3) to represent D3).
        if (test && inActiveAbilities.Any(ability => ability.Innate == "Bad Juju"))
        {
            CombatRolls.AllocatePure(DiceHelpers.SimpleRoll(3), activeSquad);
        }

        return test;
    }


    public static void TriggerSquadRegeneration(Squad squad)
    {
        if (squad == null)
        {
            return;
        }

        if (SquadRegenerationHandler != null)
        {
            SquadRegenerationHandler.Invoke(squad);
            return;
        }

        // Fallback for contexts that do not have battlefield actor access.
        RegenerateSquadWithoutActors(squad);
    }

    public static void RegenerateSquadWithoutActors(Squad squad)
    {
        if (squad.Composition == null || squad.Composition.Count == 0)
        {
            return;
        }

        var remainingWounds = DiceHelpers.SimpleRoll(3);
        remainingWounds = HealSquadModels(squad, remainingWounds);

        while (remainingWounds > 0 && squad.Composition.Count < squad.StartingModelSize)
        {
            var referenceModel = squad.Composition.FirstOrDefault();
            if (referenceModel == null)
            {
                break;
            }

            var regeneratedModel = referenceModel.DeepCopy();
            regeneratedModel.Health = 1;
            squad.Composition.Add(regeneratedModel);
            remainingWounds--;
        }

        if (remainingWounds > 0)
        {
            HealSquadModels(squad, remainingWounds);
        }
    }

    public static int HealSquadModels(Squad squad, int remainingWounds)
    {
        var woundsLeft = remainingWounds;
        foreach (var model in squad.Composition)
        {
            if (woundsLeft <= 0)
            {
                break;
            }

            if (model.Health >= model.StartingHealth)
            {
                continue;
            }

            var woundsToHeal = Math.Min(woundsLeft, model.StartingHealth - model.Health);
            model.Health += woundsToHeal;
            woundsLeft -= woundsToHeal;
        }

        return woundsLeft;
    }


    public static async Task<bool> ShootingPhaseChecks(Squad activeSquad, Squad inactiveSquad, BattleHud hud)
    {
        await ShootingPhaseChecksBase(activeSquad, inactiveSquad);
        var didSelfDamage = false;

        if (activeSquad == null || inactiveSquad == null)
        {
            return false;
        }

        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Satan") && hud != null)
        {
            didSelfDamage |= await HandleSatanicPrayer(activeSquad, hud);
        }

        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "SubRoutine") && hud != null)
        {
            AudioManager.Instance?.Play("subroutine");
            var options = new[] { "Skirmish Ability", "Hefty Ability" };
            var choice = await hud.ChooseOptionAsync("Choose a Subroutine for your guns!", options);
            if (choice == 0)
            {
                activeSquad.Composition
                    .SelectMany(model => model.Tools)
                    .DistinctBy(weapon => weapon.WeaponName)
                    .ToList()
                    .ForEach(weapon => weapon.Special.Add(WeaponAbilities.SkirmishTemp));
            }
            else if (choice == 1)
            {
                activeSquad.Composition
                    .SelectMany(model => model.Tools)
                    .DistinctBy(weapon => weapon.WeaponName)
                    .ToList()
                    .ForEach(weapon => weapon.Special.Add(WeaponAbilities.HeftyTemp));
            }
        }

        return didSelfDamage;
    }

    private static async Task<bool> HandleSatanicPrayer(Squad activeSquad, BattleHud hud)
    {
        AudioManager.Instance?.Play("demonlaugh");
        var options = new[] { "Bonus Hits 1", "Hard Hits", "Nothing" };
        var choice = await hud.ChooseOptionAsync("Satanic Prayer. Choose an option at a potential cost!", options);
        switch (choice)
        {
            case 0:
                activeSquad.Composition
                    .SelectMany(model => model.Tools)
                    .DistinctBy(weapon => weapon.WeaponName)
                    .ToList()
                    .ForEach(weapon => weapon.Special.Add(WeaponAbilities.BonusHits1Temp));
                break;
            case 1:
                activeSquad.Composition
                    .SelectMany(model => model.Tools)
                    .DistinctBy(weapon => weapon.WeaponName)
                    .ToList()
                    .ForEach(weapon => weapon.Special.Add(WeaponAbilities.HardHitsTemp));
                break;
        }

        if (choice < 0 || choice == 2)
        {
            return false;
        }

        var selfHarm = DiceHelpers.Roll2d6() < activeSquad.Bravery;
        if (selfHarm)
        {
            AudioManager.Instance?.Play("perilous");
            CombatRolls.AllocatePure(DiceHelpers.SimpleRoll(3), activeSquad);
        }

        return selfHarm;
    }

    public static async Task<bool> FightPhaseChecks(Squad activeSquad, BattleHud hud)
    {
        if (activeSquad == null)
        {
            return false;
        }

        var didSelfDamage = false;
        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "martialStance") && hud != null)
        {
            AudioManager.Instance?.Play("stance");
            var options = new[] { "-1 to Hit", "Bonus Hits 1", "Hard Hits" };
            var choice = await hud.ChooseOptionAsync($"Martial Stance for {activeSquad.Name}", options);
            switch (choice)
            {
                case 0:
                    activeSquad.SquadAbilities.Add(SquadAbilities.TempMinusHitBrawl);
                    break;
                case 1:
                    activeSquad.Composition
                        .SelectMany(model => model.Tools)
                        .DistinctBy(weapon => weapon.WeaponName)
                        .ToList()
                        .ForEach(weapon => weapon.Special.Add(WeaponAbilities.BonusHits1Temp));
                    break;
                case 2:
                    activeSquad.Composition
                        .SelectMany(model => model.Tools)
                        .DistinctBy(weapon => weapon.WeaponName)
                        .ToList()
                        .ForEach(weapon => weapon.Special.Add(WeaponAbilities.HardHitsTemp));
                    break;
            }
        }

        if (activeSquad.SquadAbilities.Any(ability => ability.Innate == "Satan") && hud != null)
        {
            didSelfDamage |= await HandleSatanicPrayer(activeSquad, hud);
        }

        return didSelfDamage;
    }
}
