using System;

public enum MovementType
{
    Standard,
    Rush,
    Retreat
}

public class MoveVars
{
    public bool Move;
    public bool Rush;
    public bool Retreat;

    public MoveVars(bool move, bool rush, bool retreat)
    {
        Move = move;
        Rush = rush;
        Retreat = retreat;
    }
}

public class DiceModifiers
{
    public int HitMod;
    public int WoundMod;
    public int HitReroll;
    public int WoundReroll;
    public int DefenseMod;
    public int CritThreshold;
    public int AntiThreshold;

    public DiceModifiers(
        int hitMod,
        int woundMod,
        int hitReroll,
        int woundReroll,
        int defenseMod,
        int critThreshold,
        int antiThreshold
    )
    {
        HitMod = hitMod;
        WoundMod = woundMod;
        HitReroll = hitReroll;
        WoundReroll = woundReroll;
        DefenseMod = defenseMod;
        CritThreshold = critThreshold;
        AntiThreshold = antiThreshold;
    }
}
