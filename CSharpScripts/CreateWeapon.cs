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
    private OptionButton _variableAbilityBaseDropdown = null!;
    private LineEdit _variableAbilityModifierInput = null!;
    private Button _addVariableAbilityButton = null!;
    private OptionButton _hitSoundDropdown = null!;

    private readonly List<string> _hitSoundKeys = new();

    private List<WeaponAbility> _currentAbilities = new();

    private static IReadOnlyList<WeaponAbility> AbilityOptions => WeaponAbilities.All;

    private List<WeaponAbility> _sortedAbilityOptions = new();
    private List<WeaponAbility> _sortedVariableBaseOptions = new();

    public override void _Ready()
    {
        GameData.Instance.LoadWeaponsFromFile();

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
        _variableAbilityBaseDropdown = GetNode<OptionButton>("%VariableAbilityBase");
        _variableAbilityModifierInput = GetNode<LineEdit>("%VariableAbilityModifier");
        _addVariableAbilityButton = GetNode<Button>("%BtnAddVariableAbility");
        _hitSoundDropdown = GetNode<OptionButton>("%HitSoundDropdown");

        GetNode<Button>("%BtnSave").Pressed += OnSavePressed;
        GetNode<Button>("%BtnDiscard").Pressed += OnDiscardPressed;
        _abilitiesButton.Pressed += ShowAbilitiesDialog;
        _abilitiesDialog.Confirmed += ApplyAbilitiesSelection;
        _addVariableAbilityButton.Pressed += AddVariableAbility;

        SetupAbilitiesList();
        SetupVariableAbilityOptions();
        SetupHitSoundDropdown();
        LoadDataIfEditing();
        UpdateAbilitiesLabel();
    }

    private void OnSavePressed()
    {
        if (string.IsNullOrWhiteSpace(_nameIn.Text))
        {
            OS.Alert("Weapon name cannot be empty.", "Validation Error");
            return;
        }

        if (_hsIn.Value < 0 || _hsIn.Value > 9 ||
            _strIn.Value < 0 || _strIn.Value > 99 ||
            _apIn.Value < -9 || _apIn.Value > 0 ||
            _rangeIn.Value < 0 || _rangeIn.Value > 999)
        {
            OS.Alert("Values are out of bounds!", "Validation Error");
            return;
        }

        if (!Dice.IsExpressionValid(_attacksIn.Text))
        {
            OS.Alert("Attacks must be an integer or D3/D6 format.", "Format Error");
            return;
        }
        if (!Dice.IsExpressionValid(_dmgIn.Text))
        {
            OS.Alert("Damage must be an integer or D3/D6 format.", "Format Error");
            return;
        }

        var selectedSoundKey = GetSelectedHitSoundKey();

        var weapon = new Weapon(
            weaponName: _nameIn.Text,
            range: (float)_rangeIn.Value,
            attacks: _attacksIn.Text,
            hitSkill: (int)_hsIn.Value,
            strength: (int)_strIn.Value,
            armorPenetration: (int)_apIn.Value,
            damage: _dmgIn.Text,
            special: new List<WeaponAbility>(_currentAbilities),
            hitSfxKey: selectedSoundKey
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
            SelectHitSoundByKey(w.HitSfxKey);
        }
        else
        {
            _titleLabel.Text = "Create New Weapon";
            _currentAbilities = new List<WeaponAbility>();
            SelectHitSoundByKey(string.Empty);
        }
    }

    private void SetupAbilitiesList()
    {
        _abilitiesList.Clear();

        _sortedAbilityOptions = AbilityOptions
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < _sortedAbilityOptions.Count; i++)
        {
            _abilitiesList.AddItem(_sortedAbilityOptions[i].Name);
        }
    }

    private void SetupVariableAbilityOptions()
    {
        _variableAbilityBaseDropdown.Clear();
        _sortedVariableBaseOptions = WeaponAbilities.VariableBaseAbilities
            .OrderBy(ability => ability.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var baseAbility in _sortedVariableBaseOptions)
        {
            _variableAbilityBaseDropdown.AddItem(baseAbility.Name);
        }
    }


    private void SetupHitSoundDropdown()
    {
        _hitSoundDropdown.Clear();
        _hitSoundKeys.Clear();

        _hitSoundDropdown.AddItem("(Default)");
        _hitSoundDropdown.SetItemMetadata(0, string.Empty);
        _hitSoundKeys.Add(string.Empty);

        const string weaponSoundPath = "res://Assets/WeaponSounds";
        if (!DirAccess.DirExistsAbsolute(weaponSoundPath))
        {
            return;
        }

        var discovered = new List<string>();
        using var dir = DirAccess.Open(weaponSoundPath);
        if (dir == null)
        {
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            var fileName = dir.GetNext();
            if (string.IsNullOrEmpty(fileName))
            {
                break;
            }

            if (dir.CurrentIsDir() || !fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            discovered.Add(fileName);
        }
        dir.ListDirEnd();

        foreach (var soundKey in discovered.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var display = System.IO.Path.GetFileNameWithoutExtension(soundKey);
            _hitSoundDropdown.AddItem(display);
            var index = _hitSoundDropdown.ItemCount - 1;
            _hitSoundDropdown.SetItemMetadata(index, soundKey);
            _hitSoundKeys.Add(soundKey);
        }
    }

    private string GetSelectedHitSoundKey()
    {
        if (_hitSoundDropdown.Selected < 0 || _hitSoundDropdown.Selected >= _hitSoundDropdown.ItemCount)
        {
            return string.Empty;
        }

        var metadata = _hitSoundDropdown.GetItemMetadata(_hitSoundDropdown.Selected);
        return metadata.VariantType == Variant.Type.Nil ? string.Empty : metadata.AsString();
    }

    private void SelectHitSoundByKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            _hitSoundDropdown.Select(0);
            return;
        }

        for (int i = 0; i < _hitSoundDropdown.ItemCount; i++)
        {
            var metadata = _hitSoundDropdown.GetItemMetadata(i);
            var optionKey = metadata.VariantType == Variant.Type.Nil ? string.Empty : metadata.AsString();
            if (string.Equals(optionKey, key, StringComparison.OrdinalIgnoreCase))
            {
                _hitSoundDropdown.Select(i);
                return;
            }
        }

        _hitSoundDropdown.Select(0);
    }

    private void ShowAbilitiesDialog()
    {
        _abilitiesList.UnselectAll();

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
        var variableAbilities = _currentAbilities.Where(ability => ability.IsVariableGenerated).ToList();

        _currentAbilities.Clear();

        foreach (int idx in _abilitiesList.GetSelectedItems())
        {
            if (idx >= 0 && idx < _sortedAbilityOptions.Count)
                _currentAbilities.Add(_sortedAbilityOptions[idx]);
        }

        _currentAbilities.AddRange(variableAbilities);
        UpdateAbilitiesLabel();
    }

    private void AddVariableAbility()
    {
        var selectedIndex = _variableAbilityBaseDropdown.Selected;
        if (selectedIndex < 0 || selectedIndex >= _sortedVariableBaseOptions.Count)
        {
            OS.Alert("Choose a base variable weapon ability.", "Validation Error");
            return;
        }

        var modifierInput = (_variableAbilityModifierInput.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(modifierInput))
        {
            OS.Alert("Enter a modifier (example: 2 or D6).", "Validation Error");
            return;
        }

        if (!Dice.IsExpressionValid(modifierInput))
        {
            OS.Alert("Modifier must be an integer or D3/D6 format.", "Format Error");
            return;
        }

        var baseAbility = _sortedVariableBaseOptions[selectedIndex];
        var generated = WeaponAbilities.CreateVariableAbility(baseAbility, modifierInput);

        if (_currentAbilities.Any(a => a.Name.Equals(generated.Name, StringComparison.OrdinalIgnoreCase)))
        {
            OS.Alert($"Ability '{generated.Name}' is already selected.", "Validation Error");
            return;
        }

        _currentAbilities.Add(generated);
        _variableAbilityModifierInput.Text = string.Empty;
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
