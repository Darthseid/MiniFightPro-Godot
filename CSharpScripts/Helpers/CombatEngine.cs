using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class CombatEngine
{
    private static float _currentDistance;

    public readonly struct WeaponBatch
    {
        public WeaponBatch(Weapon weapon, int totalAttacks)
        {
            Weapon = weapon;
            TotalAttacks = totalAttacks;
        }

        public Weapon Weapon { get; }
        public int TotalAttacks { get; }
    }

    public readonly struct WeaponResolutionSummary
    {
        public WeaponResolutionSummary(string weaponName, int attacks, int hits, int injuries, int penetratingInjuries)
        {
            WeaponName = weaponName;
            Attacks = attacks;
            Hits = hits;
            Injuries = injuries;
            PenetratingInjuries = penetratingInjuries;
        }

        public string WeaponName { get; }
        public int Attacks { get; }
        public int Hits { get; }
        public int Injuries { get; }
        public int PenetratingInjuries { get; }
    }

    public readonly struct AttackResult
    {
        public AttackResult(int totalDamageApplied, bool defenderDied, List<WeaponResolutionSummary> weaponSummaries)
        {
            TotalDamageApplied = totalDamageApplied;
            DefenderDied = defenderDied;
            WeaponSummaries = weaponSummaries;
        }

        public int TotalDamageApplied { get; }
        public bool DefenderDied { get; }
        public List<WeaponResolutionSummary> WeaponSummaries { get; }
    }

    private const bool DebugDamageAllocation = false;

    private static bool IsActorAlive(BattleModelActor actor)
    { return actor != null && actor.BoundModel != null && actor.BoundModel.Health > 0; }

    private static int CountLivingActors(List<BattleModelActor> actors)
    {
        var count = 0;
        for (int i = 0; i < actors.Count; i++)
        {
            if (IsActorAlive(actors[i]))
                count++;
        }

        return count;
    }

    private static void DebugAllocationLog(string message)
    {
        if (!DebugDamageAllocation)
            return;
        GD.Print($"[CombatEngine.AllocateDamage] {message}");
    }

    private static int AllocateDamage(
        BattleModelActor attacker,
        List<BattleModelActor> defenderActors,
        Squad defenderSquad,
        Weapon weapon,
        int unsavedInjuries,
        ref BattleModelActor currentRecipient
    )
    {
        if (defenderActors == null || defenderSquad == null || weapon == null || unsavedInjuries <= 0)
            return 0;
        var totalApplied = 0;
        for (int injuryIndex = 0; injuryIndex < unsavedInjuries; injuryIndex++)
        {
            currentRecipient = SelectDamageRecipient(currentRecipient, defenderActors, weapon);
            if (!IsActorAlive(currentRecipient))
            {
                DebugAllocationLog($"No living recipient for injury {injuryIndex + 1}/{unsavedInjuries}; stopping allocation.");
                break;
            }

            var distance = attacker != null
                ? BoardGeometry.DistanceInches(attacker, currentRecipient)
                : _currentDistance;
            var halfRange = distance <= weapon.Range / 2f;
            var baseDamage = DiceHelpers.DamageParser(weapon.Damage);
            var finalDamage = CombatHelpers.DamageMods(baseDamage, defenderSquad.SquadAbilities, weapon.Special, halfRange);
            if (finalDamage <= 0)
            {
                DebugAllocationLog($"Injury {injuryIndex + 1}/{unsavedInjuries}: rolled non-positive damage {finalDamage}, skipped.");
                continue;
            }

            var healthBefore = currentRecipient.BoundModel.Health;
            currentRecipient.ApplyDamage(finalDamage);
            totalApplied += finalDamage;
            var healthAfter = currentRecipient.BoundModel.Health;

            DebugAllocationLog(
                $"Injury {injuryIndex + 1}/{unsavedInjuries} -> {currentRecipient.Name}: damage={finalDamage}, hp {healthBefore}->{healthAfter}.");

            if (healthAfter <= 0)
            {
                DebugAllocationLog($"Recipient {currentRecipient.Name} died; next injury will retarget.");
                currentRecipient = null;
            }
        }

        return totalApplied;
    }

    private static string ResolveWeaponHitSoundKey(Weapon weapon)
    {
        if (weapon == null)
            return "rifleshot.mp3";

        var manager = AudioManager.Instance;
        if (manager == null)
            return string.IsNullOrWhiteSpace(weapon.HitSfxKey) ? "rifleshot.mp3" : weapon.HitSfxKey;

        var normalized = manager.NormalizeWeaponHitKey(weapon.HitSfxKey);
        return string.IsNullOrEmpty(normalized) ? "rifleshot.mp3" : normalized;
    }

    private static string BuildWeaponToast(WeaponResolutionSummary summary)
    {
        var builder = new StringBuilder();
        builder.Append($"{summary.WeaponName} ");
        builder.Append($"Hits: {summary.Hits} ");
        builder.Append($"Injuries: {summary.Injuries} ");
        builder.Append($"Penetrating injuries: {summary.PenetratingInjuries}");
        return builder.ToString();
    }

    private static string BuildWeaponFingerprint(Weapon weapon) //Creates a string that uniquely identifies a weapon's key characteristics for grouping purposes.
    {
        var specials = string.Join("|", weapon.Special.Select(ability => $"{ability.Innate}:{ability.Modifier}"));
        return $"{weapon.WeaponName}::{weapon.Attacks}::{weapon.Damage}::{weapon.Range}::{weapon.HitSkill}::{weapon.Strength}::{weapon.ArmorPenetration}::{weapon.IsMelee}::{specials}";
    }

    private static async Task<int> ResolvePerilousRecoilAsync(
        Squad attackerSquad,
        List<BattleModelActor> attackerActors,
        Weapon weapon,
        BattleHud battleHud
    )
    {
        if (attackerSquad == null || attackerActors == null || weapon?.Special == null)
            return 0;

        if (!weapon.Special.Any(ability => ability?.Innate == "Self-Inflict"))
            return 0;

        var fingerprint = BuildWeaponFingerprint(weapon);
        var recoilHits = 0;

        foreach (var attackerActor in attackerActors.Where(IsActorAlive))
        {
            var hasPerilousWeapon = attackerActor?.BoundModel?.Tools
                ?.Any(tool => tool != null && BuildWeaponFingerprint(tool) == fingerprint) == true;
            if (!hasPerilousWeapon)
                continue;

            var perilousRoll = await DiceRoller.PresentAndRollAsync(
                6,
                1,
                new RollContext(
                    RollPhase.Other,
                    "Perilous Test (D6)",
                    attackerSquad.Name,
                    null,
                    weapon.WeaponName,
                    BuildWeaponFingerprint(weapon),
                    false,
                    attackerActor.TeamId));
            if (perilousRoll.Results.FirstOrDefault() != 1) // Only a roll of 1 causes recoil damage, regardless of modifiers.
                continue;

            recoilHits++;
            if (attackerSquad.SquadType.Contains("Infantry"))
            {
                attackerActor.BoundModel.Health = 0;
                battleHud?.ShowToast($"Perilous! {attackerActor.BoundModel.Name} was destroyed.", 2f);
                AudioManager.Instance?.Play("perilous");
            }
            else
            {
                attackerActor.BoundModel.Health = System.Math.Max(0, attackerActor.BoundModel.Health - 3);
                battleHud?.ShowToast($"Perilous! {attackerActor.BoundModel.Name} suffers 3 pure damage.", 2f);
                AudioManager.Instance?.Play("perilous");
            }
            attackerActor.RefreshHp();
        }
        return recoilHits;
    }

    public static List<Weapon> GetEffectiveWeaponsForPhase(Squad attackerSquad, bool isMelee)
    {
        if (attackerSquad == null)
            return new List<Weapon>();

        var effectiveWeapons = attackerSquad.Composition
            .Where(model => model != null && model.Health > 0)
            .SelectMany(model => model.Tools)
            .Where(weapon => weapon != null && weapon.IsMelee == isMelee)
            .ToList();

        var hasFiringDeck = !isMelee
            && attackerSquad.SquadType?.Contains("Transport") == true
            && attackerSquad.EmbarkedSquad != null
            && attackerSquad.SquadAbilities?.Any(ability => ability?.Innate == "Firing Deck") == true;

        if (hasFiringDeck)
        {
            var passengerWeapons = attackerSquad.EmbarkedSquad.Composition
                .Where(model => model != null && model.Health > 0)
                .SelectMany(model => model.Tools)
                .Where(weapon => weapon != null && !weapon.IsMelee)
                .Select(weapon => weapon.DeepCopy());

            effectiveWeapons.AddRange(passengerWeapons);
        }
        return effectiveWeapons;
    }

    public static List<WeaponBatch> BuildWeaponBatches(Squad attackerSquad, bool isMelee)
    { return BuildWeaponBatches(attackerSquad, isMelee, null); }


    public static List<WeaponBatch> BuildWeaponBatches(Squad attackerSquad, bool isMelee, string requiredWeaponFingerprint)
    {
        if (attackerSquad == null)
            return new List<WeaponBatch>();

        var eligibleWeapons = GetEffectiveWeaponsForPhase(attackerSquad, isMelee)
            .Where(weapon => weapon != null && weapon.IsMelee == isMelee);

        return BuildWeaponBatchesFromWeapons(eligibleWeapons, requiredWeaponFingerprint);
    }

    private static List<WeaponBatch> BuildWeaponBatchesFromWeapons(IEnumerable<Weapon> eligibleWeapons, string requiredWeaponFingerprint)
    {
        if (eligibleWeapons == null)
            return new List<WeaponBatch>();

        if (!string.IsNullOrWhiteSpace(requiredWeaponFingerprint))
        {
            eligibleWeapons = eligibleWeapons
                .Where(weapon => BuildWeaponFingerprint(weapon) == requiredWeaponFingerprint);
        }

        return eligibleWeapons
            .GroupBy(BuildWeaponFingerprint)
            .Select(group =>
            {
                var representative = group.First();
                var totalAttacks = group.Sum(weapon => DiceHelpers.DamageParser(weapon.Attacks));
                return new WeaponBatch(representative, totalAttacks);
            })
            .Where(batch => batch.TotalAttacks > 0)
            .OrderBy(batch => batch.Weapon?.WeaponName)
            .ThenBy(batch => batch.Weapon?.Range ?? 0f)
            .ToList();
    }

    private static List<WeaponBatch> BuildWeaponBatchesForVisibleAttackers(
        Squad attackerSquad,
        IEnumerable<BattleModelActor> attackerActors,
        bool isMelee,
        string requiredWeaponFingerprint,
        System.Func<BattleModelActor, bool> canActorShootTarget)
    {
        if (attackerSquad == null || attackerActors == null || canActorShootTarget == null)
            return new List<WeaponBatch>();

        var visibleWeapons = attackerActors
            .Where(IsActorAlive)
            .Where(canActorShootTarget)
            .Select(actor => actor.BoundModel)
            .Where(model => model != null)
            .SelectMany(model => model.Tools ?? new List<Weapon>())
            .Where(weapon => weapon != null && weapon.IsMelee == isMelee)
            .ToList();

        var hasFiringDeck = !isMelee
            && attackerSquad.SquadType?.Contains("Transport") == true
            && attackerSquad.EmbarkedSquad != null
            && attackerSquad.SquadAbilities?.Any(ability => ability?.Innate == "Firing Deck") == true
            && attackerActors.Any(canActorShootTarget);

        if (hasFiringDeck)
        {
            var passengerWeapons = attackerSquad.EmbarkedSquad.Composition
                .Where(model => model != null && model.Health > 0)
                .SelectMany(model => model.Tools)
                .Where(weapon => weapon != null && !weapon.IsMelee)
                .Select(weapon => weapon.DeepCopy());

            visibleWeapons.AddRange(passengerWeapons);
        }

        return BuildWeaponBatchesFromWeapons(visibleWeapons, requiredWeaponFingerprint);
    }

    public static string GetWeaponFingerprint(Weapon weapon)
    { return BuildWeaponFingerprint(weapon); }

    private static bool HasPrecision(Weapon weapon)
    { return weapon?.Special?.Any(ability => ability?.Innate == "rareFirst") == true; }

    private static BattleModelActor SelectLeastCommonNamedDefender(List<BattleModelActor> defenders)
    {
        var living = defenders.Where(IsActorAlive).ToList();
        if (living.Count == 0)
            return null;

        var nameCounts = living
            .GroupBy(a => a.BoundModel?.Name ?? a.Name)
            .ToDictionary(g => g.Key, g => g.Count()); // Count how many living actors share each name.

        return living.MinBy(a =>
        {
            var n = a.BoundModel?.Name ?? a.Name;
            return (nameCounts[n], n);
        });
    }

    private static BattleModelActor SelectFurthestFromSquadCenter(List<BattleModelActor> defenders)
    {
        var living = defenders.Where(IsActorAlive).ToList();
        if (living.Count == 0)
            return null;


        var center = living  // Compute center
            .Select(a => a.GlobalPosition)
            .Aggregate(Vector2.Zero, (sum, pos) => sum + pos) / living.Count;


        return living.MaxBy(a => a.GlobalPosition.DistanceSquaredTo(center)); // Pick actor with maximum squared distance
    }

    private static BattleModelActor SelectDamageRecipient(
        BattleModelActor preferred,
        List<BattleModelActor> defenders,
        Weapon weapon
    )
    {
        if (HasPrecision(weapon))
        {
            if (IsActorAlive(preferred))
                return preferred;
            return SelectLeastCommonNamedDefender(defenders) ?? SelectFurthestFromSquadCenter(defenders);
        }

        return SelectFurthestFromSquadCenter(defenders);
    }

    public static AttackResult ResolveWeaponBatchesAttack(
        Squad attackerSquad,
        MoveVars attackerMove,
        Model defenderModel,
        Squad defenderSquad,
        bool isMelee,
        List<WeaponBatch> weaponBatches
    )
    {
        if (defenderModel == null || weaponBatches == null || weaponBatches.Count == 0)
            return new AttackResult(0, false, new List<WeaponResolutionSummary>());

        var totalApplied = 0;
        var remainingHealth = defenderModel.Health;
        var weaponSummaries = new List<WeaponResolutionSummary>();

        foreach (var batch in weaponBatches)
        {
            var weapon = batch.Weapon;
            var hasLineOfSight = true;
            if (!isMelee && !CombatHelpers.CheckValidShooting(attackerSquad, attackerMove, weapon, defenderSquad, _currentDistance, hasLineOfSight))
                continue;

            var attacks = batch.TotalAttacks;
            if (attacks <= 0)
                continue;
            var weaponHitSoundKey = ResolveWeaponHitSoundKey(weapon);
            var modifiers = CombatHelpers.ObtainModifiers(
                weapon,
                attackerSquad,
                attackerMove,
                defenderSquad,
                false,
                isMelee,
                _currentDistance
            );
            var (hits, hardHits) = CombatRolls.HitSequence(
                attacks,
                weapon.HitSkill,
                modifiers.HitMod,
                modifiers.HitReroll,
                modifiers.CritThreshold,
                weapon.Special
            );
            hits += hardHits;

            for (int i = 0; i < hardHits; i++)
                AudioManager.Instance?.PlayStaggeredJitter("critical", hardHits, 0.02f);

            if (hits > 0)
                AudioManager.Instance?.PlayStaggeredJitter(weaponHitSoundKey, attacks, 0.02f);
            else if (weapon.IsMelee)
                AudioManager.Instance?.Play("meleemiss");
            else
                AudioManager.Instance?.Play("rangedmiss");

            var defenderHardness = defenderSquad.Hardness;
            if (attackerSquad?.SquadAbilities.Any(ability => ability.Innate == "AIDS") == true && _currentDistance <= 9f)
                defenderHardness = System.Math.Max(1, defenderHardness - 1);
            var (injuries, devastating) = CombatRolls.WoundSequence(
                hits,
                weapon.Strength,
                defenderHardness,
                modifiers.WoundMod,
                modifiers.WoundReroll,
                weapon.Special,
                modifiers.AntiThreshold
            );
            injuries += devastating;
            var unsaved = CombatRolls.SaveSequence(injuries, defenderSquad.Defense, modifiers.DefenseMod, defenderSquad.Dodge);
            if (weapon.IsMelee && unsaved > 0)
                AudioManager.Instance?.Play("chomp");
            else if (injuries > 0 && unsaved == 0)
                AudioManager.Instance?.Play("defensepassed");

            for (int i = 0; i < unsaved && remainingHealth > 0; i++)
            {
                var baseDamage = DiceHelpers.DamageParser(weapon.Damage);
                var halfRange = _currentDistance <= weapon.Range / 2f;
                var finalDamage = CombatHelpers.DamageMods(baseDamage, defenderSquad.SquadAbilities, weapon.Special, halfRange);
                var applied = finalDamage > remainingHealth ? remainingHealth : finalDamage;
                totalApplied += applied;
                remainingHealth -= applied;
            }

            weaponSummaries.Add(new WeaponResolutionSummary(
                weapon.WeaponName,
                attacks,
                hits,
                injuries,
                unsaved
            ));

            if (weapon.Special?.Any(ability => ability?.Innate == "1 Shot") == true)
                weapon.Attacks = "0";

            if (remainingHealth <= 0)
                break;
        }
        return new AttackResult(totalApplied, remainingHealth <= 0, weaponSummaries);
    }

    public static AttackResult ResolveAttack(
        Model attackerModel,
        Squad attackerSquad,
        MoveVars attackerMove,
        Model defenderModel,
        Squad defenderSquad,
        bool isMelee
    )
    {
        if (attackerModel == null || defenderModel == null)
            return new AttackResult(0, false, new List<WeaponResolutionSummary>());

        var weapons = attackerModel.Tools
            .Where(weapon => isMelee ? weapon.IsMelee : !weapon.IsMelee)
            .ToList();
        if (weapons.Count == 0)
            return new AttackResult(0, false, new List<WeaponResolutionSummary>());

        var weaponBatches = weapons
            .Select(weapon => new WeaponBatch(weapon, DiceHelpers.DamageParser(weapon.Attacks)))
            .ToList();

        return ResolveWeaponBatchesAttack(
            attackerSquad,
            attackerMove,
            defenderModel,
            defenderSquad,
            isMelee,
            weaponBatches
        );
    }

    public static async Task ResolveAttack(
        BattleModelActor attacker,
        BattleModelActor defender,
        bool isFight,
        Squad teamASquad,
        Squad teamBSquad,
        List<BattleModelActor> teamAActors,
        List<BattleModelActor> teamBActors,
        MoveVars teamAMove,
        MoveVars teamBMove,
        BattleHud battleHud,
        BattleField battleField,
        System.Action checkVictory,
        System.Action<Squad, Squad, int> handleExplosionProcess = null,
        bool onlySixesHit = false,
        bool hasLineOfSight = true
    )
    {
        if (attacker == null || defender == null)
            return;

        var attackerSquad = attacker.TeamId == 1 ? teamASquad : teamBSquad;
        var defenderSquad = attacker.TeamId == 1 ? teamBSquad : teamASquad;
        var attackerActors = attacker.TeamId == 1 ? teamAActors : teamBActors;
        var defenderActors = attacker.TeamId == 1 ? teamBActors : teamAActors;

        if (attackerSquad == null || defenderSquad == null)
            return;
        _currentDistance = BoardGeometry.DistanceInches(attacker, defender);
        var result = ResolveAttack(
            attacker.BoundModel,
            attackerSquad,
            CombatHelpers.GetMoveVarsForTeam(attacker.TeamId, teamAMove, teamBMove),
            defender.BoundModel,
            defenderSquad,
            isFight
        );
        var aliveBefore = CountLivingActors(defenderActors);
        if (result.TotalDamageApplied > 0)
            defender.ApplyDamage(result.TotalDamageApplied);
        foreach (var summary in result.WeaponSummaries)
        {
            battleHud?.ShowToast(BuildWeaponToast(summary), 2f);
            await Task.Delay(2000);
        }
        var aliveAfter = CountLivingActors(defenderActors);
        var demiseCheck = System.Math.Max(0, aliveBefore - aliveAfter);
        if (demiseCheck > 0)
            handleExplosionProcess?.Invoke(defenderSquad, attackerSquad, demiseCheck);
        RemoveDeadModels(defenderActors, defenderSquad, battleField);
        checkVictory?.Invoke();
    }

    public static async Task ResolveBatchedAttack(
        BattleModelActor attacker,
        BattleModelActor defender,
        bool isFight,
        Squad teamASquad,
        Squad teamBSquad,
        List<BattleModelActor> teamAActors,
        List<BattleModelActor> teamBActors,
        MoveVars teamAMove,
        MoveVars teamBMove,
        BattleHud battleHud,
        BattleField battleField,
        System.Action checkVictory,
        System.Action<Squad, Squad, int> handleExplosionProcess = null,
        string selectedWeaponFingerprint = null,
        bool onlySixesHit = false,
        bool hasLineOfSight = true,
        System.Func<BattleModelActor, bool> canAttackerActorShootTarget = null
    )
    {
        if (attacker == null || defender == null)
            return;

        var attackerSquad = attacker.TeamId == 1 ? teamASquad : teamBSquad;
        var defenderSquad = attacker.TeamId == 1 ? teamBSquad : teamASquad;
        var attackerActors = attacker.TeamId == 1 ? teamAActors : teamBActors;
        var defenderActors = attacker.TeamId == 1 ? teamBActors : teamAActors;

        if (attackerSquad == null || defenderSquad == null)
            return;

        _currentDistance = BoardGeometry.ClosestDistanceInches(teamAActors, teamBActors);
        var weaponBatches = !isFight && canAttackerActorShootTarget != null
            ? BuildWeaponBatchesForVisibleAttackers(attackerSquad, attackerActors, false, selectedWeaponFingerprint, canAttackerActorShootTarget)
            : BuildWeaponBatches(attackerSquad, isFight, selectedWeaponFingerprint);
        var attackerMove = CombatHelpers.GetMoveVarsForTeam(attacker.TeamId, teamAMove, teamBMove);
        var currentRecipient = defender;

        foreach (var batch in weaponBatches)
        {
            var weapon = batch.Weapon;
            if (!isFight &&
                !CombatHelpers.CheckValidShooting(attackerSquad, attackerMove, weapon, defenderSquad, _currentDistance, hasLineOfSight))
                continue;

            var aliveBeforeWeapon = CountLivingActors(defenderActors);
            var attacks = batch.TotalAttacks;
            if (attacks <= 0)
                continue;
            var weaponHitSoundKey = ResolveWeaponHitSoundKey(weapon);
            var indirectShot = !isFight && !hasLineOfSight && weapon.Special.Any(ability => ability.Innate == WeaponAbilities.IndirectFire.Innate);
            if (indirectShot)
                GD.Print($"[Indirect Fire] {attackerSquad.Name} firing {weapon.WeaponName} indirectly at {defenderSquad.Name}.");

            var modifiers = CombatHelpers.ObtainModifiers(
                weapon,
                attackerSquad,
                attackerMove,
                defenderSquad,
                indirectShot,
                isFight,
                _currentDistance,
                indirectShot ? -1 : 0
            );

            var hitContext = new RollContext(
                RollPhase.Hit,
                $"To Hit ({weapon.WeaponName})",
                attackerSquad?.Name,
                defenderSquad?.Name,
                weapon.WeaponName,
                CombatEngine.GetWeaponFingerprint(weapon),
                onlySixesHit,
                attacker.TeamId
            );
            var (hits, hardHits) = await CombatRolls.HitSequenceAsync(
                attacks,
                weapon.HitSkill,
                modifiers.HitMod,
                modifiers.HitReroll,
                modifiers.CritThreshold,
                weapon.Special,
                hitContext
            );
            hits += hardHits;

            for (int i = 0; i < hardHits; i++)
                AudioManager.Instance?.Play("critical");

            if (hits > 0)
                AudioManager.Instance?.PlayStaggeredJitter(weaponHitSoundKey, attacks, 0.02f);
            else if (weapon.IsMelee)
                AudioManager.Instance?.Play("meleemiss");
            else
                AudioManager.Instance?.Play("rangedmiss");

            var defenderHardness = defenderSquad.Hardness;
            if (attackerSquad?.SquadAbilities.Any(ability => ability.Innate == "AIDS") == true && _currentDistance <= 9f)
                defenderHardness = System.Math.Max(1, defenderHardness - 1);
            var woundContext = new RollContext(
                RollPhase.Wound,
                $"To Wound ({weapon.WeaponName})",
                attackerSquad?.Name,
                defenderSquad?.Name,
                weapon.WeaponName,
                CombatEngine.GetWeaponFingerprint(weapon),
                onlySixesHit,
                attacker.TeamId
            );
            var (injuries, devastating) = await CombatRolls.WoundSequenceAsync(
                hits,
                weapon.Strength,
                defenderHardness,
                modifiers.WoundMod,
                modifiers.WoundReroll,
                weapon.Special,
                modifiers.AntiThreshold,
                woundContext
            );
            injuries += devastating;
            var saveContext = new RollContext(
                RollPhase.Save,
                $"Saves ({defenderSquad?.Name})",
                attackerSquad?.Name,
                defenderSquad?.Name,
                weapon.WeaponName,
                CombatEngine.GetWeaponFingerprint(weapon),
                false,
                attacker.TeamId == 1 ? 2 : 1
            );
            var dodge = defenderSquad.Dodge > 6 && defenderSquad.SquadAbilities.Any(a => a.Innate == SquadAbilities.SixPlusDodge.Innate) ? defenderSquad.Dodge - 1 : defenderSquad.Dodge;
            var unsaved = await CombatRolls.SaveSequenceAsync(injuries, defenderSquad.Defense, modifiers.DefenseMod, dodge, saveContext);
            if (weapon.IsMelee && unsaved > 0)
                AudioManager.Instance?.Play("chomp");
            else if (injuries > 0 && unsaved == 0)
                AudioManager.Instance?.Play("defensepassed");

            if (HasPrecision(weapon))
                currentRecipient = SelectLeastCommonNamedDefender(defenderActors);
            else
                currentRecipient = SelectFurthestFromSquadCenter(defenderActors);

            AllocateDamage(
                attacker,
                defenderActors,
                defenderSquad,
                weapon,
                unsaved,
                ref currentRecipient
            );

            var summary = new WeaponResolutionSummary(
                weapon.WeaponName,
                attacks,
                hits,
                injuries,
                unsaved
            );

            if (weapon.Special?.Any(ability => ability?.Innate == "1 Shot") == true)
                weapon.Attacks = "0";

            battleHud?.ShowToast(BuildWeaponToast(summary), 2f);
            await Task.Delay(2000);

            await ResolvePerilousRecoilAsync(attackerSquad, attackerActors, weapon, battleHud);
            RemoveDeadModels(attackerActors, attackerSquad, battleField);
            checkVictory?.Invoke();

            var aliveAfterWeapon = CountLivingActors(defenderActors);
            var demiseCheck = System.Math.Max(0, aliveBeforeWeapon - aliveAfterWeapon);
            if (demiseCheck > 0)
                handleExplosionProcess?.Invoke(defenderSquad, attackerSquad, demiseCheck);

            RemoveDeadModels(defenderActors, defenderSquad, battleField);
            checkVictory?.Invoke();

            if (CountLivingActors(defenderActors) == 0)
                break;
        }
    }

    public static async Task ResolveBatchedAttackForTeam(
        int attackerTeamId,
        bool isFight,
        List<WeaponBatch> weaponBatchesOverride,
        Squad teamASquad,
        Squad teamBSquad,
        List<BattleModelActor> teamAActors,
        List<BattleModelActor> teamBActors,
        MoveVars teamAMove,
        MoveVars teamBMove,
        BattleHud battleHud,
        BattleField battleField,
        System.Action checkVictory,
        System.Action<Squad, Squad, int> handleExplosionProcess = null,
        bool onlySixesHit = false,
        bool hasLineOfSight = true
    )
    {
        var attackerSquad = attackerTeamId == 1 ? teamASquad : teamBSquad;
        var defenderSquad = attackerTeamId == 1 ? teamBSquad : teamASquad;
        var attackerActors = attackerTeamId == 1 ? teamAActors : teamBActors;
        var defenderActors = attackerTeamId == 1 ? teamBActors : teamAActors;

        if (attackerSquad == null || defenderSquad == null || defenderActors == null || defenderActors.Count == 0)
            return;

        _currentDistance = BoardGeometry.ClosestDistanceInches(teamAActors, teamBActors);
        var attackerMove = CombatHelpers.GetMoveVarsForTeam(attackerTeamId, teamAMove, teamBMove);
        var weaponBatches = weaponBatchesOverride ?? BuildWeaponBatches(attackerSquad, isFight);
        var attackerActor = attackerActors.FirstOrDefault(IsActorAlive);
        var currentRecipient = SelectFurthestFromSquadCenter(defenderActors);

        foreach (var batch in weaponBatches)
        {
            var weapon = batch.Weapon;
            if (weapon == null)
                continue;

            if (!isFight && !CombatHelpers.CheckValidShooting(attackerSquad, attackerMove, weapon, defenderSquad, _currentDistance, hasLineOfSight))
                continue;

            var attacks = batch.TotalAttacks;
            if (attacks <= 0)
                continue;
            var aliveBeforeWeapon = CountLivingActors(defenderActors);
            var weaponHitSoundKey = ResolveWeaponHitSoundKey(weapon);
            var indirectShot = !isFight && !hasLineOfSight && weapon.Special.Any(ability => ability.Innate == WeaponAbilities.IndirectFire.Innate);
            if (indirectShot)
                GD.Print($"[Indirect Fire] {attackerSquad.Name} firing {weapon.WeaponName} indirectly at {defenderSquad.Name}.");

            var modifiers = CombatHelpers.ObtainModifiers(weapon, attackerSquad, attackerMove, defenderSquad, indirectShot, isFight, _currentDistance, indirectShot ? -1 : 0);
            var hitContext = new RollContext(
                RollPhase.Hit,
                $"To Hit ({weapon.WeaponName})",
                attackerSquad?.Name,
                defenderSquad?.Name,
                weapon.WeaponName,
                CombatEngine.GetWeaponFingerprint(weapon),
                onlySixesHit,
                attackerTeamId
            );
            var (hits, hardHits) = await CombatRolls.HitSequenceAsync(attacks, weapon.HitSkill, modifiers.HitMod, modifiers.HitReroll, modifiers.CritThreshold, weapon.Special, hitContext);
            hits += hardHits;

            for (int i = 0; i < hardHits; i++)
                AudioManager.Instance?.Play("critical");

            if (hits > 0)
                AudioManager.Instance?.PlayStaggeredJitter(weaponHitSoundKey, attacks, 0.02f);
            else if (weapon.IsMelee)
                AudioManager.Instance?.Play("meleemiss");
            else
                AudioManager.Instance?.Play("rangedmiss");

            var defenderHardness = defenderSquad.Hardness;
            if (attackerSquad?.SquadAbilities.Any(ability => ability.Innate == "AIDS") == true && _currentDistance <= 9f)
                defenderHardness = System.Math.Max(1, defenderHardness - 1);
            var woundContext = new RollContext(
                RollPhase.Wound,
                $"To Wound ({weapon.WeaponName})",
                attackerSquad?.Name,
                defenderSquad?.Name,
                weapon.WeaponName,
                CombatEngine.GetWeaponFingerprint(weapon),
                onlySixesHit,
                attackerTeamId
            );
            var (injuries, devastating) = await CombatRolls.WoundSequenceAsync(hits, weapon.Strength, defenderHardness, modifiers.WoundMod, modifiers.WoundReroll, weapon.Special, modifiers.AntiThreshold, woundContext);
            injuries += devastating;
            var saveContext = new RollContext(
                RollPhase.Save,
                $"Saves ({defenderSquad?.Name})",
                attackerSquad?.Name,
                defenderSquad?.Name,
                weapon.WeaponName,
                CombatEngine.GetWeaponFingerprint(weapon),
                false,
                attackerTeamId == 1 ? 2 : 1
            );
            var dodge = defenderSquad.Dodge > 6 && defenderSquad.SquadAbilities.Any(a => a.Innate == SquadAbilities.SixPlusDodge.Innate) ? defenderSquad.Dodge - 1 : defenderSquad.Dodge;
            var penetrating = await CombatRolls.SaveSequenceAsync(injuries, defenderSquad.Defense, modifiers.DefenseMod, dodge, saveContext);
            if (weapon.IsMelee && penetrating > 0)
                AudioManager.Instance?.Play("chomp");
            else if (injuries > 0 && penetrating == 0)
                AudioManager.Instance?.Play("defensepassed");

            AllocateDamage(attackerActor, defenderActors, defenderSquad, weapon, penetrating, ref currentRecipient);

            var summary = new WeaponResolutionSummary(weapon.WeaponName, attacks, hits, injuries, penetrating);

            if (weapon.Special?.Any(ability => ability?.Innate == "1 Shot") == true)
                weapon.Attacks = "0";

            battleHud?.ShowToast(BuildWeaponToast(summary), 2f);
            await Task.Delay(2000);

            await ResolvePerilousRecoilAsync(attackerSquad, attackerActors, weapon, battleHud);
            RemoveDeadModels(attackerActors, attackerSquad, battleField);
            checkVictory?.Invoke();

            var aliveAfterWeapon = CountLivingActors(defenderActors);
            var demiseCheck = System.Math.Max(0, aliveBeforeWeapon - aliveAfterWeapon);
            if (demiseCheck > 0)
                handleExplosionProcess?.Invoke(defenderSquad, attackerSquad, demiseCheck);

            RemoveDeadModels(defenderActors, defenderSquad, battleField);
            checkVictory?.Invoke();

            if (CountLivingActors(defenderActors) == 0)
                break;
        }
    }

    public static void RemoveDeadModels(List<BattleModelActor> actors, Squad squad, BattleField battleField = null)
    {
        if (actors == null || squad == null)
            return;

        var hasSelfResurrection = squad.SquadAbilities.Any(ability => ability.Innate == "2nd Life");
        var allDead = actors.Count > 0 && actors.All(actor => actor?.BoundModel == null || actor.BoundModel.Health <= 0);
        if (hasSelfResurrection && allDead)
        {
            foreach (var actor in actors)
            {
                if (actor?.BoundModel == null)
                    continue;

                var halfHealth = actor.BoundModel.StartingHealth / 2;
                actor.BoundModel.Health = System.Math.Max(1, halfHealth);
                actor.RefreshHp();
            }

            squad.SquadAbilities.RemoveAll(ability => ability.Innate == "2nd Life");
            GD.Print("[Ability Triggered] Squad ability '2nd Life' triggered (squad resurrected at half health rounded down).");
            return;
        }

        for (int i = actors.Count - 1; i >= 0; i--)
        {
            var actor = actors[i];
            if (actor == null || actor.BoundModel == null)
                continue;
            if (actor.BoundModel.Health <= 0)
            {
                if (DiceHelpers.SimpleRoll(100) <= 20)
                    AudioManager.Instance?.PlayStaggeredJitter("wilhelm_scream", 1, 0.03f);
                squad.Composition.Remove(actor.BoundModel);
                battleField?.UnregisterActor(actor);
                actor.QueueFree();
                actors.RemoveAt(i);
            }
        }
    }
}
