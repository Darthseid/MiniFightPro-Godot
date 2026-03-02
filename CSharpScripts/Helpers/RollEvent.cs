using System;
using System.Collections.Generic;

public sealed record RollEvent(
    Guid Id,
    int Sides,
    List<int> Results,
    bool[] RerolledFlags,
    bool[] FateReplacedFlags,
    int OwnerTeamId,
    RollPhase Phase,
    string Label,
    string AttackerName,
    string DefenderName,
    string WeaponName,
    string WeaponFingerprint,
    bool IsRerollBatch
);
