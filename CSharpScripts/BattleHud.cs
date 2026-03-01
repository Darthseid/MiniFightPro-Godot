using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class BattleHud : Control
{
    private Label? _toastLabel;
    private Timer? _toastTimer;
    private PanelContainer? _toastRoot;
    private ConfirmationDialog? _actionDialog;
    private ConfirmationDialog? _optionDialog;
    private ItemList? _optionList;
    private Control? _gameOverOverlay;
    private Label? _gameOverLabel;
    private Button? _measureButton;
    private Label? _player1OrdersPoints;
    private Label? _player2OrdersPoints;
    private VBoxContainer? _player1OrdersList;
    private VBoxContainer? _player2OrdersList;

    [Signal] public delegate void NextPhasePressedEventHandler();
    [Signal] public delegate void MeasureRequestedEventHandler();

    public override void _Ready()
    {
        _toastLabel = GetNodeOrNull<Label>("%ToastLabel");
        _toastTimer = GetNodeOrNull<Timer>("%ToastTimer");
        _toastRoot = GetNodeOrNull<PanelContainer>("%Toast");
        _actionDialog = GetNodeOrNull<ConfirmationDialog>("%ActionDialog");
        _optionDialog = GetNodeOrNull<ConfirmationDialog>("%OptionDialog");
        _optionList = GetNodeOrNull<ItemList>("%OptionList");
        _gameOverOverlay = GetNodeOrNull<Control>("%GameOverOverlay");
        _gameOverLabel = GetNodeOrNull<Label>("%GameOverLabel");
        _player1OrdersPoints = GetNodeOrNull<Label>("%Player1OrderPoints");
        _player2OrdersPoints = GetNodeOrNull<Label>("%Player2OrderPoints");
        _player1OrdersList = GetNodeOrNull<VBoxContainer>("%Player1OrdersList");
        _player2OrdersList = GetNodeOrNull<VBoxContainer>("%Player2OrdersList");

        var nextButton = GetNodeOrNull<Button>("%BtnNextPhase");
        if (nextButton != null)
        {
            nextButton.Pressed += () => EmitSignal(SignalName.NextPhasePressed);
        }

        _measureButton = GetNodeOrNull<Button>("%BtnMeasure");
        if (_measureButton != null)
        {
            _measureButton.Pressed += () => EmitSignal(SignalName.MeasureRequested);
        }

        var player1Toggle = GetNodeOrNull<Button>("%Player1OrdersToggle");
        if (player1Toggle != null)
        {
            player1Toggle.Pressed += () => ToggleOrderPanel("%Player1OrdersBody");
        }

        var player2Toggle = GetNodeOrNull<Button>("%Player2OrdersToggle");
        if (player2Toggle != null)
        {
            player2Toggle.Pressed += () => ToggleOrderPanel("%Player2OrdersBody");
        }

        SetMeasureButtonEnabledVisual(false);

        if (_toastTimer != null)
            _toastTimer.Timeout += OnToastTimeout;
    }

    private void ToggleOrderPanel(string path)
    {
        var panel = GetNodeOrNull<Control>(path);
        if (panel != null)
        {
            panel.Visible = !panel.Visible;
        }
    }

    public void ConfigureOrders(Player player1, Player player2, OrderManager manager, Action<int, string> onOrderPressed)
    {
        if (player1 == null || player2 == null || manager == null || onOrderPressed == null)
        {
            return;
        }

        if (_player1OrdersPoints != null)
        {
            _player1OrdersPoints.Text = $"Order Points: {player1.OrderPoints}";
        }

        if (_player2OrdersPoints != null)
        {
            _player2OrdersPoints.Text = $"Order Points: {player2.OrderPoints}";
        }

        RenderOrderButtons(_player1OrdersList, player1, 1, manager, onOrderPressed);
        RenderOrderButtons(_player2OrdersList, player2, 2, manager, onOrderPressed);
    }

    private static void RenderOrderButtons(VBoxContainer? container, Player player, int playerId, OrderManager manager, Action<int, string> onOrderPressed)
    {
        if (container == null)
        {
            return;
        }

        foreach (var child in container.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var order in player.Orders ?? new List<Order>())
        {
            var button = new Button();
            if (manager.IsOrderUsable(playerId, order, out var reason))
            {
                button.Disabled = false;
                button.TooltipText = "Ready";
            }
            else
            {
                button.Disabled = true;
                button.TooltipText = string.IsNullOrWhiteSpace(reason) ? "Unavailable" : reason;
            }

            button.Text = $"{order.OrderName} (Cost {order.OrderCost})";
            var orderId = order.OrderId;
            button.Pressed += () => onOrderPressed(playerId, orderId);
            container.AddChild(button);
        }
    }

    public void ShowToast(string text, float seconds = 4f)
    {
        if (_toastLabel == null || _toastTimer == null || _toastRoot == null) return;

        _toastLabel.Text = text;
        _toastRoot.Visible = true;
        _toastTimer.Start(seconds);
    }

    public void ShowGameOverBanner(string text)
    {
        if (_gameOverOverlay == null || _gameOverLabel == null)
        {
            return;
        }

        _gameOverLabel.Text = text;
        _gameOverOverlay.Visible = true;
    }

    public void HideGameOverBanner()
    {
        if (_gameOverOverlay == null)
        {
            return;
        }

        _gameOverOverlay.Visible = false;
        if (_gameOverLabel != null)
        {
            _gameOverLabel.Text = string.Empty;
        }
    }

    private void OnToastTimeout()
    {
        if (_toastRoot == null) return;
        _toastRoot.Visible = false;
    }

    public async Task<bool> ConfirmActionAsync(string message, string yesText = "Yes", string noText = "No")
    {
        if (_actionDialog == null)
            return false;

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var tcs = new TaskCompletionSource<bool>();

        void Cleanup()
        {
            _actionDialog.Confirmed -= OnConfirmed;
            _actionDialog.Canceled -= OnCanceled;
        }

        void OnConfirmed()
        {
            Cleanup();
            tcs.TrySetResult(true);
        }

        void OnCanceled()
        {
            Cleanup();
            tcs.TrySetResult(false);
        }

        _actionDialog.Hide();
        _actionDialog.DialogText = message;
        _actionDialog.GetOkButton().Text = yesText;
        _actionDialog.GetCancelButton().Text = noText;

        _actionDialog.Confirmed += OnConfirmed;
        _actionDialog.Canceled += OnCanceled;

        _actionDialog.PopupCentered();
        _actionDialog.GetOkButton().GrabFocus();

        return await tcs.Task;
    }

    public Task<int> ChooseOptionAsync(string title, IReadOnlyList<string> options)
    {
        if (_optionDialog == null || _optionList == null)
        {
            return Task.FromResult(-1);
        }

        var tcs = new TaskCompletionSource<int>();

        void Cleanup()
        {
            _optionDialog.Confirmed -= OnConfirmed;
            _optionDialog.Canceled -= OnCanceled;
        }

        void OnConfirmed()
        {
            Cleanup();
            var selected = _optionList.GetSelectedItems();
            tcs.TrySetResult(selected.Length > 0 ? selected[0] : -1);
        }

        void OnCanceled()
        {
            Cleanup();
            tcs.TrySetResult(-1);
        }

        _optionList.Clear();
        foreach (var option in options)
        {
            _optionList.AddItem(option);
        }
        _optionList.SelectMode = ItemList.SelectModeEnum.Single;
        _optionDialog.Title = title;
        _optionDialog.Confirmed += OnConfirmed;
        _optionDialog.Canceled += OnCanceled;
        _optionDialog.PopupCentered();

        return tcs.Task;
    }

    public void SetMeasureButtonEnabledVisual(bool enabled)
    {
        if (_measureButton == null)
        {
            return;
        }

        _measureButton.Modulate = enabled ? new Color(1f, 0.92f, 0.35f, 1f) : Colors.White;
    }
}
