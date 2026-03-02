using Godot;
using System.Threading;
using System.Threading.Tasks;

public partial class DicePresenter : Node, IDicePresenter
{
    [Export] public PackedScene DiceOverlayScene = GD.Load<PackedScene>("res://Scenes/UI/DiceOverlay.tscn");

    private readonly SemaphoreSlim _queueSemaphore = new(1, 1);
    private DiceOverlay _overlay;
    private TaskCompletionSource<bool>? _advanceTcs;
    private RollEvent? _currentRoll;
    private OrderManager? _orderManager;
    private bool _rerollUsedThisRoll;

    public DiceInteractionMode InteractionMode { get; private set; } = DiceInteractionMode.None;
    public int ActivePlayerTeamId { get; set; } = 1;
    public int CurrentRollOwnerTeamId => _currentRoll?.OwnerTeamId ?? -1;

    public override void _Ready()
    {
        var battle = GetParentOrNull<Battle>();
        _orderManager = battle?.OrderManager;
    }

    public async Task PresentAsync(RollEvent rollEvent)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            EnsureOverlay();
            _currentRoll = rollEvent;
            _rerollUsedThisRoll = false;
            await _overlay.ShowRollAsync(rollEvent);
            InteractionMode = DiceInteractionMode.AwaitingPlayerAdvance;
            UpdateButtons();
            await WaitForAdvanceAsync();
            _overlay.HideOverlay();
            _currentRoll = null;
            InteractionMode = DiceInteractionMode.None;
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public Task<bool> WaitForAdvanceAsync()
    {
        _advanceTcs = new TaskCompletionSource<bool>();
        return _advanceTcs.Task;
    }

    private void EnsureOverlay()
    {
        if (IsInstanceValid(_overlay))
        {
            return;
        }

        _overlay = DiceOverlayScene.Instantiate<DiceOverlay>();
        _overlay.NextPressed += OnNextPressed;
        _overlay.CommandRerollPressed += OnCommandRerollPressed;
        _overlay.DieClicked += OnDieClicked;
        AddChild(_overlay);
    }

    private void OnNextPressed()
    {
        if (InteractionMode != DiceInteractionMode.AwaitingPlayerAdvance)
        {
            return;
        }

        _advanceTcs?.TrySetResult(true);
    }

    private void OnCommandRerollPressed()
    {
        if (_currentRoll == null || _orderManager == null)
        {
            return;
        }

        if (!_orderManager.CanUseCommandReroll(ActivePlayerTeamId, _currentRoll, out var reason))
        {
            GetParentOrNull<Battle>()?.Hud?.ShowToast(reason);
            return;
        }

        InteractionMode = DiceInteractionMode.AwaitingRerollSelection;
        _overlay.SetRerollSelectionState(true);
        UpdateButtons();
    }

    private void OnDieClicked(int index)
    {
        if (_currentRoll == null || _orderManager == null || InteractionMode != DiceInteractionMode.AwaitingRerollSelection)
        {
            return;
        }

        if (!_orderManager.TryUseCommandReroll(ActivePlayerTeamId, _currentRoll, index, out var reason))
        {
            GetParentOrNull<Battle>()?.Hud?.ShowToast(reason);
            return;
        }

        DiceRoller.RerollDie(_currentRoll, index);
        _overlay.UpdateDieFace(index);
        _rerollUsedThisRoll = true;
        InteractionMode = DiceInteractionMode.AwaitingPlayerAdvance;
        _overlay.SetRerollSelectionState(false);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var canReroll = false;
        if (_currentRoll != null && _orderManager != null && !_rerollUsedThisRoll)
        {
            canReroll = _orderManager.CanUseCommandReroll(ActivePlayerTeamId, _currentRoll, out _);
        }

        _overlay.SetButtonsState(
            canAdvance: InteractionMode == DiceInteractionMode.AwaitingPlayerAdvance,
            canReroll: InteractionMode == DiceInteractionMode.AwaitingPlayerAdvance && canReroll);
    }
}
