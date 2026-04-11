using System;
using System.Threading.Tasks;

public static class DiceRoller
{
    private static IDicePresenter _presenter;

    public static event Action<RollEvent> DiceRolled;

    public static void Initialize(IDicePresenter presenter)
        { _presenter = presenter; }

    public static void RerollDie(RollEvent e, int index)
    {
        if (e == null || index < 0 || index >= e.Results.Count)
            return;

        if (e.RerolledFlags[index] || e.FateReplacedFlags[index])
            return;

        var newValue = DiceHelpers.SimpleRoll(e.Sides);
        e.Results[index] = newValue;
        e.RerolledFlags[index] = true;
    }

    public static async Task<RollEvent> PresentAndRollAsync(int sides, int count, RollContext ctx, bool isRerollBatch = false)
    {
        if (_presenter == null)
            { throw new InvalidOperationException("DiceRoller is not initialized. Ensure DicePresenter is created and DiceRoller.Initialize(...) is called before combat."); }

        var results = new System.Collections.Generic.List<int>(count);
        for (var i = 0; i < count; i++)
            results.Add(DiceHelpers.SimpleRoll(sides));

        var rerolledFlags = new bool[count];
        var fateReplacedFlags = new bool[count];
        var rollEvent = new RollEvent(
            Guid.NewGuid(),
            sides,
            results,
            rerolledFlags,
            fateReplacedFlags,
            ctx.OwnerTeamId > 0 ? ctx.OwnerTeamId : _presenter.ActivePlayerTeamId,
            ctx.Phase,
            ctx.Label,
            ctx.AttackerName,
            ctx.DefenderName,
            ctx.WeaponName,
            ctx.WeaponFingerprint,
            isRerollBatch
        );
        DiceRolled?.Invoke(rollEvent);
        await _presenter.PresentAsync(rollEvent);
        return rollEvent;
    }
}
