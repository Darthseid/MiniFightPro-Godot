using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CreateModel : Control
{
    private LineEdit _nameInput;
    private SpinBox _healthInput;
    private SpinBox _bracketedInput;
    private ItemList _weaponList;
    private Label _titleLabel;
    private List<Weapon> _selectedWeapons = new List<Weapon>();

    private const ulong LongPressThresholdMs = 500;
    private int _pressedWeaponIndex = -1;
    private ulong _pressStartedAtMs;

    public override void _Ready()
    {
        var data = GameData.Instance;
        data.LoadWeaponsFromFile();
        data.LoadModelsFromFile();

        _titleLabel = GetNode<Label>("%TitleLabel");
        _nameInput = GetNode<LineEdit>("%ModelNameInput");
        _healthInput = GetNode<SpinBox>("%ModelHealthInput");
        _bracketedInput = GetNode<SpinBox>("%ModelBracketedInput");
        _weaponList = GetNode<ItemList>("%WeaponList");

        GetNode<Button>("%BtnSave").Pressed += OnSavePressed;
        GetNode<Button>("%BtnDiscard").Pressed += OnDiscardPressed;

        // Use GuiInput to detect presses/touches and drags for long-press behaviour.
        _weaponList.GuiInput += OnWeaponListGuiInput;

        PopulateWeaponList();
        LoadDataIfEditing();
    }

    private void PopulateWeaponList()
    {
        _weaponList.Clear();
        var data = GameData.Instance;
        foreach (var weapon in data.WeaponList)
        {
            _weaponList.AddItem(GetWeaponDisplayText(weapon));
        }
    }

    private string GetWeaponDisplayText(Weapon weapon)
    {
        var copies = _selectedWeapons.Count(w => w.WeaponName == weapon.WeaponName);
        return copies > 0 ? $"{weapon.WeaponName} (x{copies})" : weapon.WeaponName;
    }

    private void RefreshWeaponListDisplay()
    {
        var data = GameData.Instance;
        for (int i = 0; i < data.WeaponList.Count; i++)
        {
            _weaponList.SetItemText(i, GetWeaponDisplayText(data.WeaponList[i]));
        }
    }

    // Use ItemList.GetItemAtPosition with global=true to get item under the pointer.
    private int GetItemIndexAtGlobalPosition(Vector2 globalPosition)
    {
        // Godot's ItemList API supports passing global coordinates when requested.
        // Returns -1 if none.
        try
        {
            return (int)_weaponList.GetItemAtPosition(globalPosition, true);
        }
        catch
        {
            return -1;
        }
    }

    private void OnWeaponListGuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left:
                HandlePressState(mb.Pressed, mb.Position);
                break;

            case InputEventScreenTouch st:
                HandlePressState(st.Pressed, st.Position);
                break;

            case InputEventScreenDrag _:
                // Cancel pending press when dragging
                CancelPendingPress();
                break;
        }
    }

    private void HandlePressState(bool isPressed, Vector2 position)
    {
        if (isPressed)
        {
            _pressedWeaponIndex = GetItemIndexAtGlobalPosition(position);
            if (_pressedWeaponIndex >= 0)
            {
                _pressStartedAtMs = Time.GetTicksMsec();
            }
            return;
        }

        // on release
        if (_pressedWeaponIndex < 0)
            return;

        var releasedIndex = GetItemIndexAtGlobalPosition(position);
        if (releasedIndex != _pressedWeaponIndex)
        {
            // user moved off the original item => ignore
            _pressedWeaponIndex = -1;
            return;
        }

        var heldDuration = Time.GetTicksMsec() - _pressStartedAtMs;
        if (heldDuration >= LongPressThresholdMs)
        {
            // long-press => remove last copy
            RemoveWeaponCopy(releasedIndex);
        }
        else
        {
            // short tap => add (duplicates allowed)
            AddWeaponCopy(releasedIndex);
        }

        _weaponList.Deselect(releasedIndex);
        _pressedWeaponIndex = -1;
    }

    private void CancelPendingPress()
    {
        _pressedWeaponIndex = -1;
    }

    private void AddWeaponCopy(int index)
    {
        var data = GameData.Instance;
        if (index < 0 || index >= data.WeaponList.Count)
            return;

        _selectedWeapons.Add(data.WeaponList[index]);
        RefreshWeaponListDisplay();
    }

    private void RemoveWeaponCopy(int index)
    {
        var data = GameData.Instance;
        if (index < 0 || index >= data.WeaponList.Count)
            return;

        var weaponName = data.WeaponList[index].WeaponName;
        var removeIndex = _selectedWeapons.FindLastIndex(w => w.WeaponName == weaponName);
        if (removeIndex >= 0)
        {
            _selectedWeapons.RemoveAt(removeIndex);
            RefreshWeaponListDisplay();
        }
    }

    private void LoadDataIfEditing()
    {
        var data = GameData.Instance;
        if (data.SelectedModelIndex >= 0 && data.SelectedModelIndex < data.ModelList.Count)
        {
            _titleLabel.Text = "Edit Model";
            var model = data.ModelList[data.SelectedModelIndex];
            _nameInput.Text = model.Name;
            _healthInput.Value = model.StartingHealth;
            _bracketedInput.Value = model.Bracketed;

            _selectedWeapons = new List<Weapon>(model.Tools);
            RefreshWeaponListDisplay();
        }
        else
        {
            _titleLabel.Text = "Create Model";
        }
    }

    private void OnSavePressed()
    {
        var modelName = _nameInput.Text.Trim();
        var health = (int)_healthInput.Value;
        var bracketed = (int)_bracketedInput.Value;

        if (string.IsNullOrWhiteSpace(modelName) || health <= 0)
        {
            OS.Alert("Please enter valid model details.", "Validation Error");
            return;
        }

        var data = GameData.Instance;

        var model = new Model(
            modelName,
            health,
            health,
            bracketed,
            _selectedWeapons
        );

        if (data.SelectedModelIndex == -1)
        {
            data.ModelList.Add(model);
        }
        else
        {
            data.ModelList[data.SelectedModelIndex] = model;
        }

        data.SaveModelsToFile();
        data.SelectedModelIndex = -1;
        GetTree().ChangeSceneToFile("res://Scenes/ModelList.tscn");
    }

    private void OnDiscardPressed()
    {
        GameData.Instance.SelectedModelIndex = -1;
        GetTree().ChangeSceneToFile("res://Scenes/ModelList.tscn");
    }
}