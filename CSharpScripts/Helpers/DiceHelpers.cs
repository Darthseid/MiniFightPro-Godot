using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class DiceHelpers
{
    private static readonly RandomNumberGenerator Rng = new RandomNumberGenerator();

    static DiceHelpers()
    {
        Rng.Randomize();
    }

    public static int SimpleRoll(int maximum)
    {
        return (int)Rng.RandiRange(1, maximum);
    }

    public static int ReRollOnes()
    {
        var result = SimpleRoll(6);
        if (result == 1)
        {
            result = SimpleRoll(6);
        }
        return result;
    }

    public static int ReRollFailed(int skill)
    {
        var result = SimpleRoll(6);
        if (result < skill)
        {
            result = SimpleRoll(6);
        }
        return result;
    }

    public static int Roll2d6()
    {
        return SimpleRoll(6) + SimpleRoll(6);
    }

    public static float RandomPosition(int input)
    {
        var conv = (float)input;
        return conv * ((float)GD.Randf() * 2f - 1f);
    }
    public static int DamageParser(string input)
    {
        // 1. Try simple integer first
        if (int.TryParse(input, out int constantValue))
        {
            return constantValue;
        }

        // 2. Regex for Dice (e.g., "2D6+1")
        // Groups: 1: Multiplier, 2: Sides, 3: Modifier (including +/-)
        var pattern = new Regex(@"(\d*)[Dd]?(\d+)([+-]\d+)?");
        var match = pattern.Match(input);

        if (match.Success)
        {
            int multiplier = string.IsNullOrEmpty(match.Groups[1].Value) ? 1 : int.Parse(match.Groups[1].Value);
            int sides = int.Parse(match.Groups[2].Value);

            int total = 0;
            for (int i = 0; i < multiplier; i++)
            {
                total += SimpleRoll(sides);
            }

            if (!string.IsNullOrEmpty(match.Groups[3].Value))
            {
                total += int.Parse(match.Groups[3].Value);
            }

            return total;
        }

        GD.PrintErr("Invalid damage/attack input: " + input);
        return 0;
    }
}
