using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public enum RollPhase
{
    Hit,
    Wound,
    Save,
    Other
}

public sealed record RollContext(
    RollPhase Phase,
    string Label,
    string AttackerName = null,
    string DefenderName = null,
    string WeaponName = null,
    string WeaponFingerprint = null,
    bool OnlySixesHit = false,
    int OwnerTeamId = 0
);

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

public interface IDicePresenter
{
    int ActivePlayerTeamId { get; set; }
    Task PresentAsync(RollEvent rollEvent);
    Task<bool> WaitForRushAsync();
}
