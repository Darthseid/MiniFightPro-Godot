using System;
using System.Reflection;
using Godot;

public static class RngTestHelper
{
    public static void SeedDiceHelpers(ulong seed)
    {
        var rngField = typeof(DiceHelpers).GetField("Rng", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DiceHelpers.Rng private static field was not found.");

        var rng = rngField.GetValue(null) as RandomNumberGenerator
            ?? throw new InvalidOperationException("DiceHelpers.Rng is not a RandomNumberGenerator instance.");

        rng.Seed = seed;
    }

    public static int PredictDiceSum(ulong seed, int multiplier, int sides, int modifier = 0)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        var total = 0;
        for (var i = 0; i < multiplier; i++)
        {
            total += (int)rng.RandiRange(1, sides);
        }

        return total + modifier;
    }
}
