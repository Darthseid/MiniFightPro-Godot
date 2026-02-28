using System;

public sealed record RollEvent(
    Guid Id,
    int Sides,
    int[] Results,
    RollPhase Phase,
    string Label,
    string AttackerName,
    string DefenderName,
    string WeaponName,
    string WeaponFingerprint,
    bool IsRerollBatch
);
