using Godot;
using System.Threading;
using System.Threading.Tasks;

public partial class DicePresenter : Node, IDicePresenter
{
    [Export] public PackedScene DiceOverlayScene = GD.Load<PackedScene>("res://Scenes/UI/DiceOverlay.tscn");

    private readonly SemaphoreSlim _queueSemaphore = new(1, 1);
    private DiceOverlay _overlay;
    private TaskCompletionSource<bool>? _rushTcs;
    private RollEvent? _currentRoll;
    private bool _rerollUsedThisRoll;
    private bool _fateReplacementLockedThisRoll;

    public DiceInteractionMode InteractionMode { get; private set; } = DiceInteractionMode.None;
    public int ActivePlayerTeamId { get; set; } = 1;
    public int CurrentRollOwnerTeamId => _currentRoll?.OwnerTeamId ?? -1;

    private Battle? GetBattle()
    {
        return GetParentOrNull<Battle>();
    }

    private OrderManager? GetOrderManager()
    {
        return GetBattle()?.OrderManager;
    }

    public async Task PresentAsync(RollEvent rollEvent)
    {
        await _queueSemaphore.WaitAsync();
        try
        {
            EnsureOverlay();
            _currentRoll = rollEvent;
            _rerollUsedThisRoll = false;
            _fateReplacementLockedThisRoll = false;
            await _overlay.ShowRollAsync(rollEvent);
            RefreshFateSixHud();
            InteractionMode = DiceInteractionMode.AwaitingPlayerRush;
            UpdateButtons();
            await WaitForRushAsync();
            _overlay.HideOverlay();
            _currentRoll = null;
            InteractionMode = DiceInteractionMode.None;
        }
        finally
        {
            _queueSemaphore.Release();
        }
    }

    public Task<bool> WaitForRushAsync()
    {
        _rushTcs = new TaskCompletionSource<bool>();
        return _rushTcs.Task;
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
        if (InteractionMode != DiceInteractionMode.AwaitingPlayerRush && InteractionMode != DiceInteractionMode.AwaitingRerollSelection)
        {
            return;
        }

        if (InteractionMode == DiceInteractionMode.AwaitingRerollSelection)
        {
            _overlay.SetRerollSelectionState(false);
            InteractionMode = DiceInteractionMode.AwaitingPlayerRush;
            UpdateButtons();
        }

        _rushTcs?.TrySetResult(true);
    }

    private void OnCommandRerollPressed()
    {
        var orderManager = GetOrderManager();
        if (_currentRoll == null || orderManager == null)
        {
            return;
        }

        if (!orderManager.CanUseCommandReroll(_currentRoll.OwnerTeamId, _currentRoll, out var reason))
        {
            GetBattle()?.Hud?.ShowToast(reason);
            return;
        }

        InteractionMode = DiceInteractionMode.AwaitingRerollSelection;
        _fateReplacementLockedThisRoll = true;
        _overlay.SetRerollSelectionState(true);
        UpdateButtons();
    }

    private void OnDieClicked(int index)
    {
        var orderManager = GetOrderManager();
        var battle = GetBattle();
        if (_currentRoll == null || orderManager == null || battle == null)
        {
            return;
        }

        if (InteractionMode == DiceInteractionMode.AwaitingRerollSelection)
        {
            if (!orderManager.TryUseCommandReroll(_currentRoll.OwnerTeamId, _currentRoll, index, out var rerollReason))
            {
                battle.Hud?.ShowToast(rerollReason);
                return;
            }

            DiceRoller.RerollDie(_currentRoll, index);
            _overlay.UpdateDieFace(index);
            _rerollUsedThisRoll = true;
            InteractionMode = DiceInteractionMode.AwaitingPlayerRush;
            _overlay.SetRerollSelectionState(false);
            UpdateButtons();
            return;
        }

        if (InteractionMode != DiceInteractionMode.AwaitingPlayerRush)
        {
            return;
        }

        if (_currentRoll.OwnerTeamId != ActivePlayerTeamId)
        {
            return;
        }

        if (battle.IsTeamAI(ActivePlayerTeamId))
        {
            return;
        }

        var player = battle.GetPlayerByTeam(ActivePlayerTeamId);
        if (_fateReplacementLockedThisRoll)
        {
            return;
        }

        if (player?.HasStrandedMiracle != true)
        {
            return;
        }

        if (player.FateSixPool <= 0)
        {
            battle.Hud?.ShowToast("No Psychic Sixes remaining.");
            return;
        }

        if (index < 0 || index >= _currentRoll.Results.Count)
        {
            return;
        }

        if (_currentRoll.RerolledFlags[index])
        {
            battle.Hud?.ShowToast("Rerolled dice cannot be fate-replaced.");
            return;
        }

        if (_currentRoll.FateReplacedFlags[index])
        {
            battle.Hud?.ShowToast("This die was already fate-replaced.");
            return;
        }

        if (!battle.TryConsumeFateSix(ActivePlayerTeamId))
        {
            battle.Hud?.ShowToast("No Psychic Sixes remaining.");
            return;
        }

        _currentRoll.Results[index] = 6;
        _currentRoll.FateReplacedFlags[index] = true;
        _overlay.UpdateDieFace(index);
        battle.Hud?.ShowToast("Fate Six used");
        RefreshFateSixHud();
    }

    public void RefreshFateSixHud()
    {
        var battle = GetBattle();
        if (battle == null || _overlay == null)
        {
            return;
        }

        var p1 = battle.TeamAPlayer;
        var p2 = battle.TeamBPlayer;
        _overlay.SetFateSixes(
            battle.GetFateSixPool(1),
            p1?.HasStrandedMiracle == true,
            battle.GetFateSixPool(2),
            p2?.HasStrandedMiracle == true);
    }

    private void UpdateButtons()
    {
        var canReroll = false;
        var orderManager = GetOrderManager();
        if (_currentRoll != null && orderManager != null && !_rerollUsedThisRoll)
        {
            canReroll = orderManager.CanUseCommandReroll(_currentRoll.OwnerTeamId, _currentRoll, out _);
        }

        _overlay.SetButtonsState(
            canRush: InteractionMode == DiceInteractionMode.AwaitingPlayerRush || InteractionMode == DiceInteractionMode.AwaitingRerollSelection,
            canReroll: InteractionMode == DiceInteractionMode.AwaitingPlayerRush && canReroll);

        if (InteractionMode == DiceInteractionMode.AwaitingRerollSelection)
        {
            _overlay.SetRerollSelectionState(true);
        }
        else if (InteractionMode == DiceInteractionMode.AwaitingPlayerRush)
        {
            _overlay.SetNormalSelectionState(true);
        }
        else
        {
            _overlay.SetNormalSelectionState(false);
        }
    }
}
