using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CreateModel : Control
{
    private const int MaxWeaponsPerModel = 30;

    private LineEdit _nameInput;
    private SpinBox _healthInput;
    private SpinBox _bracketedInput;
    private ItemList _weaponList;
    private Label _titleLabel;
    private TextureRect _imagePreview;
    private FileDialog _imageFileDialog;
    private Label _mobileImportHint;
    private Model _editingModel;
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
        _imagePreview = GetNode<TextureRect>("%ModelImagePreview");
        _imageFileDialog = GetNode<FileDialog>("%ModelImageFileDialog");
        _mobileImportHint = GetNode<Label>("%MobileImportHint");

        GetNode<Button>("%BtnSave").Pressed += OnSavePressed;
        GetNode<Button>("%BtnDiscard").Pressed += OnDiscardPressed;
        GetNode<Button>("%BtnChangeImage").Pressed += OnChangeImagePressed;
        GetNode<Button>("%BtnImportFolder").Pressed += OnImportFolderPressed;

        _imageFileDialog.FileSelected += OnImageFileSelected;

        _mobileImportHint.Text = "On mobile, copy images into: user://import then select from there.";
        EnsureImportFolderExists();

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

    private void RefreshImagePreview()
    {
        _imagePreview.Texture = ModelImageService.LoadTextureForModel(_editingModel);
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

        if (_selectedWeapons.Count >= MaxWeaponsPerModel)
        {
            OS.Alert($"A model cannot have more than {MaxWeaponsPerModel} weapons.", "Limit reached");
            return;
        }

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
            _editingModel = data.ModelList[data.SelectedModelIndex].DeepCopy();
            _nameInput.Text = _editingModel.Name;
            _healthInput.Value = _editingModel.StartingHealth;
            _bracketedInput.Value = _editingModel.Bracketed;

            _selectedWeapons = new List<Weapon>(_editingModel.Tools);
            RefreshWeaponListDisplay();
        }
        else
        {
            _titleLabel.Text = "Create Model";
            _editingModel = new Model("", 1, 1, 0, new List<Weapon>());
        }

        ModelImageService.EnsureModelIdentityAndDefault(_editingModel);
        RefreshImagePreview();
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

        if (_selectedWeapons.Count > MaxWeaponsPerModel)
        {
            OS.Alert($"A model cannot have more than {MaxWeaponsPerModel} weapons.", "Validation Error");
            return;
        }

        var data = GameData.Instance;

        _editingModel.Name = modelName;
        _editingModel.StartingHealth = health;
        _editingModel.Health = health;
        _editingModel.Bracketed = bracketed;
        _editingModel.Tools = _selectedWeapons;
        ModelImageService.EnsureModelIdentityAndDefault(_editingModel);

        if (data.SelectedModelIndex == -1)
        {
            data.ModelList.Add(_editingModel);
        }
        else
        {
            data.ModelList[data.SelectedModelIndex] = _editingModel;
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

    private void OnChangeImagePressed()
    {
        _imageFileDialog.Access = FileDialog.AccessEnum.Filesystem;
        _imageFileDialog.CurrentDir = OS.HasFeature("mobile") ? ProjectSettings.GlobalizePath("user://import") : string.Empty;
        _imageFileDialog.PopupCenteredRatio();
    }

    private void OnImportFolderPressed()
    {
        EnsureImportFolderExists();
        _imageFileDialog.Access = FileDialog.AccessEnum.Userdata;
        _imageFileDialog.CurrentDir = "user://import";
        _imageFileDialog.PopupCenteredRatio();
    }

    private void EnsureImportFolderExists()
    {
        DirAccess.MakeDirRecursiveAbsolute("user://import");
    }

    private void OnImageFileSelected(string path)
    {
        var result = ModelImageService.ImportAndApplyCustomImage(_editingModel, path);
        if (result != Error.Ok)
        {
            OS.Alert($"Could not import image: {result}", "Import Failed");
            return;
        }

        RefreshImagePreview();
    }
}
