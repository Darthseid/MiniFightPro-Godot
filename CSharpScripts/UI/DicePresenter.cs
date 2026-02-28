using Godot;
using System.Threading;
using System.Threading.Tasks;

public partial class DicePresenter : Node, IDicePresenter
{
    [Export] public PackedScene DiceOverlayScene = GD.Load<PackedScene>("res://Scenes/UI/DiceOverlay.tscn");

    private readonly SemaphoreSlim _queueSemaphore = new(1, 1);
    private DiceOverlay _overlay;

    public async Task PresentAsync(RollEvent rollEvent)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            EnsureOverlay();
            await _overlay.ShowRollAsync(rollEvent);
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    private void EnsureOverlay()
    {
        if (IsInstanceValid(_overlay))
        {
            return;
        }

        _overlay = DiceOverlayScene.Instantiate<DiceOverlay>();
        AddChild(_overlay);
    }
}
