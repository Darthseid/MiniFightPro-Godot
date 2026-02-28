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

    public static async Task<RollEvent> PresentAndRollAsync(int sides, int count, RollContext ctx, bool isRerollBatch = false)
    {
        if (_presenter == null)
        {
            throw new InvalidOperationException("DiceRoller is not initialized. Ensure DicePresenter is created and DiceRoller.Initialize(...) is called before combat.");
        }

        var results = new int[count];
        for (var i = 0; i < count; i++)
        {
            results[i] = _rng.RandiRange(1, sides);
        }

        var rollEvent = new RollEvent(
            Guid.NewGuid(),
            sides,
            results,
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
