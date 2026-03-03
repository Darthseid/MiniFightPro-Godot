using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public static class Dice
{
    private static readonly RandomNumberGenerator Rng = new RandomNumberGenerator();
    private static readonly Regex DamageRegex =
        new(@"^\s*(\d*)\s*[dD]\s*(\d+)\s*([+-]\s*\d+)?\s*$", RegexOptions.Compiled);

    static Dice()
    {
        Rng.Randomize();
    }

    public static int Roll(int sides)
    {
        return (int)Rng.RandiRange(1, sides);
    }

    public static int Roll(int sides, int count)
    {
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            total += Roll(sides);
        }

        return total;
    }

    public static IEnumerable<int> RollMany(int sides, int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return Roll(sides);
        }
    }

    public static int CountSuccesses(IEnumerable<int> rolls, Func<int, bool> successPredicate)
    {
        if (rolls == null || successPredicate == null)
        {
            return 0;
        }

        return rolls.Count(successPredicate);
    }

    public static int RerollOnes(int sides = 6)
    {
        var result = Roll(sides);
        if (result == 1)
        {
            result = Roll(sides);
        }

        return result;
    }

    public static int RerollFailed(int successThreshold, int sides = 6)
    {
        var result = Roll(sides);
        if (result < successThreshold)
        {
            result = Roll(sides);
        }

        return result;
    }

    public static int Roll2D6()
    {
        return Roll(6, 2);
    }

    public static bool IsExpressionValid(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        return int.TryParse(trimmed, out _) || DamageRegex.IsMatch(trimmed);
    }

    public static int ParseExpression(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Damage string is empty.");
        }

        input = input.Trim();

        if (int.TryParse(input, out var constantValue))
        {
            return constantValue;
        }

        var match = DamageRegex.Match(input);
        if (!match.Success)
        {
            throw new ArgumentException($"Invalid damage format: '{input}'");
        }

        var multiplierText = match.Groups[1].Value;
        var multiplier = string.IsNullOrEmpty(multiplierText) ? 1 : int.Parse(multiplierText);

        var sides = int.Parse(match.Groups[2].Value);
        if (sides <= 0 || multiplier <= 0)
        {
            throw new ArgumentException($"Invalid dice values in: '{input}'");
        }

        var result = Roll(sides, multiplier);

        if (match.Groups[3].Success)
        {
            var modText = match.Groups[3].Value.Replace(" ", "");
            result += int.Parse(modText);
        }

        return result;
    }
}
