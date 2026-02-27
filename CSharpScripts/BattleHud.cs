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

        SetMeasureButtonEnabledVisual(false);

        if (_toastTimer != null)
            _toastTimer.Timeout += OnToastTimeout;
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

    // New: hide and clear the game-over banner
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

        // Ensure previous click/touch release doesn't hit the next dialog
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

        _actionDialog.Hide(); // extra safety
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
