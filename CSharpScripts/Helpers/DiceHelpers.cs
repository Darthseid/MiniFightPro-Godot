using Godot;
using System;

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

}
