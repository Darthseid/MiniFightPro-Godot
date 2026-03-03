using System;
using System.Collections.Generic;
using System.Linq;

public enum FirstAttackerMode
{
    SquadA,
    SquadB,
    Random
}

public sealed class DuelConfig
{
    public float RangeInches { get; set; } = 12f;
    public int RoundCap { get; set; } = 20;
    public int NoDamageRoundLimit { get; set; } = 3;
    public FirstAttackerMode FirstAttacker { get; set; } = FirstAttackerMode.Random;
}

public sealed class DuelResult
{
    public bool IsDraw { get; set; }
    public bool SquadAWon { get; set; }
    public bool FirstAttackerWon { get; set; }
    public int RoundsElapsed { get; set; }
    public bool HitRoundCap { get; set; }
    public int WinnerRemainingHealth { get; set; }
    public long SquadAAttacks { get; set; }
    public long SquadBAttacks { get; set; }
    public long SquadAPenetratingInjuries { get; set; }
    public long SquadBPenetratingInjuries { get; set; }
    public long SquadADamageDealt { get; set; }
    public long SquadBDamageDealt { get; set; }
}

public sealed class DuelBatchResult
{
    public int Trials { get; set; }
    public int SquadAWins { get; set; }
    public int SquadBWins { get; set; }
    public int Draws { get; set; }
    public double AverageRoundsToResolve { get; set; }
    public double AverageWinnerHealthRemainingWhenASquadWins { get; set; }
    public double AverageWinnerHealthRemainingWhenBSquadWins { get; set; }
    public double FirstAttackerWinPercent { get; set; }
    public double SquadAPenetratingInjuryRate { get; set; }
    public double SquadBPenetratingInjuryRate { get; set; }
    public double AverageSquadADamagePerTrial { get; set; }
    public double AverageSquadBDamagePerTrial { get; set; }
}

public sealed class SimSquadState
{
    public Squad BaseSquad { get; }
    public Squad WorkingSquad { get; }
    public int[] CurrentModelHp { get; private set; }
    public bool IsVehicleOrMonster { get; }
    public bool IsInfantry { get; }
    public bool HasFirstStrike => WorkingSquad.SquadAbilities.Any(a => a?.Innate == "Hit First");
    public bool HasFightOnDeath => WorkingSquad.SquadAbilities.Any(a => a?.Innate == "Hit@End");

    private bool _secondLifeTriggered;

    public SimSquadState(Squad baseSquad)
    {
        BaseSquad = baseSquad;
        WorkingSquad = baseSquad.DeepCopy();
        IsVehicleOrMonster = WorkingSquad.SquadType.Any(t => t == "Vehicle" || t == "Monster");
        IsInfantry = WorkingSquad.SquadType.Contains("Infantry");
        SyncHpArray();
    }

    public bool IsDestroyed => CurrentModelHp.Length == 0 || CurrentModelHp.All(h => h <= 0);

    public int TotalRemainingHealth => CurrentModelHp.Where(h => h > 0).Sum();

    public void SyncHpArray()
    {
        CurrentModelHp = WorkingSquad.Composition.Select(m => m.Health).ToArray();
    }

    public IEnumerable<int> AliveIndices()
    {
        for (var i = 0; i < CurrentModelHp.Length; i++)
        {
            if (CurrentModelHp[i] > 0)
            {
                yield return i;
            }
        }
    }

    public void RemoveDeadAndHandleSecondLife()
    {
        if (WorkingSquad.Composition.Count == 0)
        {
            SyncHpArray();
            return;
        }

        var allDead = WorkingSquad.Composition.All(model => model.Health <= 0);
        var hasSecondLife = WorkingSquad.SquadAbilities.Any(a => a?.Innate == "2nd Life");
        if (allDead && hasSecondLife && !_secondLifeTriggered)
        {
            for (var i = 0; i < WorkingSquad.Composition.Count; i++)
            {
                var model = WorkingSquad.Composition[i];
                model.Health = Math.Max(1, model.StartingHealth / 2);
            }

            _secondLifeTriggered = true;
            WorkingSquad.SquadAbilities.RemoveAll(a => a?.Innate == "2nd Life");
            SyncHpArray();
            return;
        }

        WorkingSquad.Composition = WorkingSquad.Composition.Where(model => model.Health > 0).ToList();
        SyncHpArray();
    }
}
