using Godot;
using System;
using System.Threading.Tasks;

public static class DiceRoller
{
    private static readonly RandomNumberGenerator _rng = new();
    private static IDicePresenter _presenter;

    public static event Action<RollEvent> DiceRolled;

    static DiceRoller()
    {
        _rng.Randomize();
    }

    public static void Initialize(IDicePresenter presenter)
    {
        _presenter = presenter;
    }

    public static int RollRandom(int sides = 6)
    {
        return _rng.RandiRange(1, sides);
    }

    public static void RerollDie(RollEvent e, int index)
    {
        if (e == null || index < 0 || index >= e.Results.Count)
        {
            return;
        }

        if (e.RerolledFlags[index])
        {
            return;
        }

        var newValue = RollRandom(e.Sides);
        e.Results[index] = newValue;
        e.RerolledFlags[index] = true;
    }

    public static async Task<RollEvent> PresentAndRollAsync(int sides, int count, RollContext ctx, bool isRerollBatch = false)
    {
        if (_presenter == null)
        {
            throw new InvalidOperationException("DiceRoller is not initialized. Ensure DicePresenter is created and DiceRoller.Initialize(...) is called before combat.");
        }

        var results = new System.Collections.Generic.List<int>(count);
        for (var i = 0; i < count; i++)
        {
            results.Add(RollRandom(sides));
        }

        var rerolledFlags = new bool[count];
        var rollEvent = new RollEvent(
            Guid.NewGuid(),
            sides,
            results,
            rerolledFlags,
            _presenter.ActivePlayerTeamId,
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
