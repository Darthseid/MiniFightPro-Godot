public sealed record RollContext(
    RollPhase Phase,
    string Label,
    string AttackerName = null,
    string DefenderName = null,
    string WeaponName = null,
    string WeaponFingerprint = null,
    bool OnlySixesHit = false
);
