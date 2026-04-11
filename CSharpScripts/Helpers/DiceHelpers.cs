using Godot;
using System;

public static class DiceHelpers
{
    private static readonly RandomNumberGenerator Rng = new RandomNumberGenerator();

    static DiceHelpers()
        { Rng.Randomize(); }

    public static int SimpleRoll(int maximum)
        { return (int)Rng.RandiRange(1, maximum); }

    public static int ReRollOnes()
    {
        var result = SimpleRoll(6);
        if (result == 1)
            result = SimpleRoll(6);
        return result;
    }

    public static int ReRollFailed(int skill)
    {
        var result = SimpleRoll(6);
        if (result < skill)
            result = SimpleRoll(6);
        return result;
    }

    public static int Roll2d6()
        { return SimpleRoll(6) + SimpleRoll(6); }

    public static float RandomPosition(int input)
    {
        var conv = (float)input;
        return conv * ((float)GD.Randf() * 2f - 1f); // This will give a random float between -input and +input
    }


    private static readonly System.Text.RegularExpressions.Regex DamageRegex =
        new(@"^\s*(\d*)\s*[dD]\s*(\d+)\s*([+-]\s*\d+)?\s*$",
            System.Text.RegularExpressions.RegexOptions.Compiled); // Matches expressions like "2d6", "d8", "3d10 + 2", "4d4 - 1"

    public static bool IsDamageExpressionValid(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        return int.TryParse(trimmed, out _) || DamageRegex.IsMatch(trimmed);
    }

    public static int DamageParser(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Damage string is empty.");

        input = input.Trim();
        if (int.TryParse(input, out var constantValue))
            return constantValue; // If it's just a number, return it as the damage

        var match = DamageRegex.Match(input);
        if (!match.Success)
            throw new ArgumentException($"Invalid damage format: '{input}'");

        var multiplierText = match.Groups[1].Value;
        var multiplier = string.IsNullOrEmpty(multiplierText) ? 1 : int.Parse(multiplierText);

        var sides = int.Parse(match.Groups[2].Value);
        if (sides <= 0 || multiplier <= 0)
            throw new ArgumentException($"Invalid dice values in: '{input}'");

        var result = 0;
        for (var i = 0; i < multiplier; i++) // Roll the specified number of dice. Stuff like 2D6 means "roll 2 six-sided dice and add them together"
            result += SimpleRoll(sides);

        if (match.Groups[3].Success)
        {
            var modText = match.Groups[3].Value.Replace(" ", "");
            result += int.Parse(modText); // Apply modifier for stuff like "D6 + 2".
        }
        return result;
    }
}
