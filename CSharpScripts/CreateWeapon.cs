using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CreateWeapon : Control
{
    // Form References
    private LineEdit _nameIn = null!;
    private LineEdit _attacksIn = null!;
    private LineEdit _dmgIn = null!;
    private SpinBox _rangeIn = null!;
    private SpinBox _hsIn = null!;
    private SpinBox _strIn = null!;
    private SpinBox _apIn = null!;
    private Label _titleLabel = null!;
    private Label _selectedAbilitiesLabel = null!;
    private Button _abilitiesButton = null!;
    private AcceptDialog _abilitiesDialog = null!;
    private ItemList _abilitiesList = null!;

    private List<WeaponAbility> _currentAbilities = new();

    private static IReadOnlyList<WeaponAbility> AbilityOptions => WeaponAbilities.All;

  
    private List<WeaponAbility> _sortedAbilityOptions = new();   // Local sorted list used for UI -> model mapping to avoid index mismatches

    public override void _Ready()
    {
        GameData.Instance.LoadWeaponsFromFile();

        // Bind UI (Unique Names % set in editor)
        _titleLabel = GetNode<Label>("%TitleLabel");
        _nameIn = GetNode<LineEdit>("%NameInput");
        _rangeIn = GetNode<SpinBox>("%RangeInput");
        _attacksIn = GetNode<LineEdit>("%AttacksInput");
        _hsIn = GetNode<SpinBox>("%HitSkillInput");
        _strIn = GetNode<SpinBox>("%StrengthInput");
        _apIn = GetNode<SpinBox>("%APInput");
        _dmgIn = GetNode<LineEdit>("%DamageInput");
        _abilitiesButton = GetNode<Button>("%BtnAbilities");
        _selectedAbilitiesLabel = GetNode<Label>("%SelectedAbilitiesLabel");
        _abilitiesDialog = GetNode<AcceptDialog>("%AbilitiesDialog");
        _abilitiesList = GetNode<ItemList>("%AbilitiesList");

        GetNode<Button>("%BtnSave").Pressed += OnSavePressed;
        GetNode<Button>("%BtnDiscard").Pressed += OnDiscardPressed;
        _abilitiesButton.Pressed += ShowAbilitiesDialog;
        _abilitiesDialog.Confirmed += ApplyAbilitiesSelection;

        SetupAbilitiesList();
        LoadDataIfEditing();
        UpdateAbilitiesLabel();
    }

    private void OnSavePressed()
    {
        // Blank check
        if (string.IsNullOrWhiteSpace(_nameIn.Text))
        {
            OS.Alert("Weapon name cannot be empty.", "Validation Error");
            return;
        }

        // Bounds check
        if (_hsIn.Value < 0 || _hsIn.Value > 9 ||
            _strIn.Value < 0 || _strIn.Value > 99 ||
            _apIn.Value < -9 || _apIn.Value > 9 ||
            _rangeIn.Value < 0 || _rangeIn.Value > 999)
        {
            OS.Alert("Values are out of bounds!", "Validation Error");
            return;
        }

        // Parser validation
        if (DiceHelpers.DamageParser(_attacksIn.Text) == 0 && _attacksIn.Text != "0")
        {
            OS.Alert("Attacks must be an integer or D3/D6 format.", "Format Error");
            return;
        }
        if (DiceHelpers.DamageParser(_dmgIn.Text) == 0 && _dmgIn.Text != "0")
        {
            OS.Alert("Damage must be an integer or D3/D6 format.", "Format Error");
            return;
        }

        // Save
        var weapon = new Weapon(
            weaponName: _nameIn.Text,
            range: (float)_rangeIn.Value,
            attacks: _attacksIn.Text,
            hitSkill: (int)_hsIn.Value,
            strength: (int)_strIn.Value,
            armorPenetration: (int)_apIn.Value,
            damage: _dmgIn.Text,
            special: new List<WeaponAbility>(_currentAbilities) // copy
        );

        var data = GameData.Instance;
        if (data.SelectedWeaponIndex == -1)
            data.WeaponList.Add(weapon);
        else
            data.WeaponList[data.SelectedWeaponIndex] = weapon;

        data.SaveWeaponsToFile();
        data.SelectedWeaponIndex = -1;

        GetTree().ChangeSceneToFile("res://Scenes/WeaponList.tscn");
    }

    private void OnDiscardPressed()
    {
        GameData.Instance.SelectedWeaponIndex = -1;
        GetTree().ChangeSceneToFile("res://Scenes/WeaponList.tscn");
    }

    private void LoadDataIfEditing()
    {
        var data = GameData.Instance;

        if (data.SelectedWeaponIndex >= 0 && data.SelectedWeaponIndex < data.WeaponList.Count)
        {
            _titleLabel.Text = "Edit Weapon";

            var w = data.WeaponList[data.SelectedWeaponIndex];
            _nameIn.Text = w.WeaponName;
            _rangeIn.Value = w.Range;
            _attacksIn.Text = w.Attacks;
            _hsIn.Value = w.HitSkill;
            _strIn.Value = w.Strength;
            _apIn.Value = w.ArmorPenetration;
            _dmgIn.Text = w.Damage;

            _currentAbilities = w.Special == null
                ? new List<WeaponAbility>()
                : new List<WeaponAbility>(w.Special);
        }
        else
        {
            _titleLabel.Text = "Create New Weapon";
            _currentAbilities = new List<WeaponAbility>();
        }
    }

    private void SetupAbilitiesList()
    {
        _abilitiesList.Clear();

        // Build and cache a sorted copy so UI ordering is stable and mapping works correctly
        _sortedAbilityOptions = AbilityOptions
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < _sortedAbilityOptions.Count; i++)
        {
            _abilitiesList.AddItem(_sortedAbilityOptions[i].Name);
        }
    }

    private void ShowAbilitiesDialog()
    {
        _abilitiesList.UnselectAll();

        // Select items by matching names against the sorted UI list
        var selectedNames = new HashSet<string>(_currentAbilities.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _sortedAbilityOptions.Count; i++)
        {
            if (selectedNames.Contains(_sortedAbilityOptions[i].Name))
                _abilitiesList.Select(i);
        }

        _abilitiesDialog.PopupCentered();
    }

    private void ApplyAbilitiesSelection()
    {
        _currentAbilities.Clear();

        // Map selected indices back to the sorted ability list (not the original AbilityOptions)
        foreach (int idx in _abilitiesList.GetSelectedItems())
        {
            if (idx >= 0 && idx < _sortedAbilityOptions.Count)
                _currentAbilities.Add(_sortedAbilityOptions[idx]);
        }

        UpdateAbilitiesLabel();
    }

    private void UpdateAbilitiesLabel()
    {
        if (_currentAbilities.Count == 0)
        {
            _selectedAbilitiesLabel.Text = "No abilities selected.";
        }
        else
        {
            _selectedAbilitiesLabel.Text =
                $"Abilities: {string.Join(", ", _currentAbilities.Select(a => a.Name))}";
        }
    }
}
