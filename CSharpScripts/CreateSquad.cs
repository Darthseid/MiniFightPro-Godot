using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CreateSquad : Control
{
    private const int MaxModelsPerSquad = 70;

    private LineEdit _nameInput = null!;
    private SpinBox _movementInput = null!;
    private SpinBox _hardnessInput = null!;
    private SpinBox _defenseInput = null!;
    private SpinBox _dodgeInput = null!;
    private SpinBox _damageResistanceInput = null!;
    private SpinBox _braveryInput = null!;
    private ItemList _availableModels = null!;
    private ItemList _selectedModels = null!;
    private Button _addModelsButton = null!;
    private Button _removeModelsButton = null!;
    private Button _squadTypeButton = null!;
    private Label _squadTypeLabel = null!;
    private Button _abilitiesButton = null!;
    private Label _abilitiesLabel = null!;
    private Label _titleLabel = null!;
    private AcceptDialog _squadTypesDialog = null!;
    private ItemList _squadTypesList = null!;
    private AcceptDialog _abilitiesDialog = null!;
    private ItemList _abilitiesList = null!;
    private OptionButton _variableAbilityBaseDropdown = null!;
    private LineEdit _variableAbilityModifierInput = null!;
    private Button _addVariableAbilityButton = null!;

    private readonly List<string> _selectedSquadTypes = new();
    private readonly List<SquadAbility> _selectedAbilities = new();

    private List<string> _sortedSquadTypes = new();
    private List<SquadAbility> _sortedAbilities = new();
    private List<SquadAbility> _sortedVariableBaseAbilities = new();

    private static IReadOnlyList<string> SquadTypes => Squad.KnownSquadTypes;
    private static IReadOnlyList<SquadAbility> AbilityOptions => SquadAbilities.All;


    public override void _Ready()
    {
        var data = GameData.Instance;
        data.LoadModelsFromFile();
        data.LoadSquadsFromFile();

        _titleLabel = GetNode<Label>("%TitleLabel");
        _nameInput = GetNode<LineEdit>("%UnitNameInput");
        _movementInput = GetNode<SpinBox>("%MovementInput");
        _hardnessInput = GetNode<SpinBox>("%HardnessInput");
        _defenseInput = GetNode<SpinBox>("%DefenseInput");
        _dodgeInput = GetNode<SpinBox>("%DodgeInput");
        _damageResistanceInput = GetNode<SpinBox>("%DamageResistanceInput");
        _braveryInput = GetNode<SpinBox>("%BraveryInput");
        _availableModels = GetNode<ItemList>("%AvailableModelsList");
        _selectedModels = GetNode<ItemList>("%SelectedModelsList");
        _addModelsButton = GetNode<Button>("%BtnAddModels");
        _removeModelsButton = GetNode<Button>("%BtnRemoveModels");
        _squadTypeButton = GetNode<Button>("%BtnSquadTypes");
        _squadTypeLabel = GetNode<Label>("%SquadTypeLabel");
        _abilitiesButton = GetNode<Button>("%BtnAbilities");
        _abilitiesLabel = GetNode<Label>("%AbilitiesLabel");
        _squadTypesDialog = GetNode<AcceptDialog>("%SquadTypesDialog");
        _squadTypesList = GetNode<ItemList>("%SquadTypesList");
        _abilitiesDialog = GetNode<AcceptDialog>("%AbilitiesDialog");
        _abilitiesList = GetNode<ItemList>("%AbilitiesList");
        _variableAbilityBaseDropdown = GetNode<OptionButton>("%VariableAbilityBase");
        _variableAbilityModifierInput = GetNode<LineEdit>("%VariableAbilityModifier");
        _addVariableAbilityButton = GetNode<Button>("%BtnAddVariableAbility");

        GetNode<Button>("%BtnSave").Pressed += OnSavePressed;
        GetNode<Button>("%BtnDiscard").Pressed += OnDiscardPressed;
        _addModelsButton.Pressed += OnAddModels;
        _removeModelsButton.Pressed += OnRemoveModels;
        _squadTypeButton.Pressed += ShowSquadTypesDialog;
        _abilitiesButton.Pressed += ShowAbilitiesDialog;
        _squadTypesDialog.Confirmed += ApplySquadTypesSelection;
        _abilitiesDialog.Confirmed += ApplyAbilitiesSelection;
        _addVariableAbilityButton.Pressed += AddVariableAbility;

        PopulateModelLists();
        PopulateDialogs();
        PopulateVariableAbilityDropdown();
        LoadDataIfEditing();
        UpdateSquadTypeLabel();
        UpdateAbilitiesLabel();
    }

    private void PopulateModelLists()
    {
        _availableModels.Clear();
        _selectedModels.Clear();

        foreach (var model in GameData.Instance.ModelList.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            _availableModels.AddItem(model.Name);
    }

    private void PopulateDialogs()
    {
        _squadTypesList.Clear();
        _sortedSquadTypes = SquadTypes
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var squadType in _sortedSquadTypes)
            _squadTypesList.AddItem(squadType);

        _abilitiesList.Clear();
        _sortedAbilities = AbilityOptions
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var ability in _sortedAbilities)
            _abilitiesList.AddItem(ability.Name);
    }

    private void PopulateVariableAbilityDropdown()
    {
        _variableAbilityBaseDropdown.Clear();
        _sortedVariableBaseAbilities = SquadAbilities.VariableBaseAbilities
            .OrderBy(ability => ability.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var baseAbility in _sortedVariableBaseAbilities)
            _variableAbilityBaseDropdown.AddItem(baseAbility.Name);
    }

    private void LoadDataIfEditing()
    {
        var data = GameData.Instance;

        if (data.SelectedSquadIndex >= 0 && data.SelectedSquadIndex < data.SquadList.Count) //This means that there is a squad selected for editing. If -1, we're creating a new squad.
        {
            _titleLabel.Text = "Edit Squad";
            var squad = data.SquadList[data.SelectedSquadIndex];

            _nameInput.Text = squad.Name;
            _movementInput.Value = squad.Movement;
            _hardnessInput.Value = squad.Hardness;
            _defenseInput.Value = squad.Defense;
            _dodgeInput.Value = squad.Dodge;
            _damageResistanceInput.Value = squad.DamageResistance;
            _braveryInput.Value = squad.Bravery;

            _selectedSquadTypes.Clear();
            _selectedSquadTypes.AddRange(squad.SquadType ?? new List<string>());

            _selectedAbilities.Clear();
            _selectedAbilities.AddRange(squad.SquadAbilities ?? new List<SquadAbility>());

            foreach (var model in squad.Composition ?? new List<Model>())
                _selectedModels.AddItem(model.Name);
        }
        else
            _titleLabel.Text = "Create Squad";
    }

    private void OnAddModels()
    {
        foreach (int index in _availableModels.GetSelectedItems())
        {
            if (_selectedModels.ItemCount >= MaxModelsPerSquad)
            {
                OS.Alert($"A squad cannot have more than {MaxModelsPerSquad} models.", "Limit reached");
                return;
            }
            var name = _availableModels.GetItemText(index);
            _selectedModels.AddItem(name);
        }
    }

    private void OnRemoveModels()
    {
        var selectedIndices = _selectedModels.GetSelectedItems();
        Array.Sort(selectedIndices);

        for (int i = selectedIndices.Length - 1; i >= 0; i--)
            _selectedModels.RemoveItem(selectedIndices[i]);
    }

    private void ShowSquadTypesDialog()
    {
        _squadTypesList.UnselectAll();
        for (int i = 0; i < _sortedSquadTypes.Count; i++)
            if (_selectedSquadTypes.Contains(_sortedSquadTypes[i]))
                _squadTypesList.Select(i);
        _squadTypesDialog.PopupCentered();
    }

    private void ApplySquadTypesSelection()
    {
        _selectedSquadTypes.Clear();
        foreach (int index in _squadTypesList.GetSelectedItems())
            if (index >= 0 && index < _sortedSquadTypes.Count)
                _selectedSquadTypes.Add(_sortedSquadTypes[index]);

        UpdateSquadTypeLabel();
    }

    private void UpdateSquadTypeLabel()
    {
        _squadTypeLabel.Text = _selectedSquadTypes.Count == 0
            ? "No squad types selected."
            : $"Squad Types: {string.Join(", ", _selectedSquadTypes)}";
    }

    private void ShowAbilitiesDialog()
    {
        _abilitiesList.UnselectAll();

        var selectedNames = new HashSet<string>(_selectedAbilities.Where(a => !a.IsVariableGenerated).Select(a => a.Name));
        for (int i = 0; i < _sortedAbilities.Count; i++)
            if (selectedNames.Contains(_sortedAbilities[i].Name))
                _abilitiesList.Select(i);

        _abilitiesDialog.PopupCentered();
    }

    private void ApplyAbilitiesSelection()
    {
        var variableAbilities = _selectedAbilities.Where(ability => ability.IsVariableGenerated).ToList();

        _selectedAbilities.Clear();

        foreach (int index in _abilitiesList.GetSelectedItems())
            if (index >= 0 && index < _sortedAbilities.Count)
                _selectedAbilities.Add(_sortedAbilities[index]);

        _selectedAbilities.AddRange(variableAbilities);
        UpdateAbilitiesLabel();
    }

    private void AddVariableAbility()
    {
        var selectedIndex = _variableAbilityBaseDropdown.Selected;
        if (selectedIndex < 0 || selectedIndex >= _sortedVariableBaseAbilities.Count)
        {
            OS.Alert("Choose a base variable squad ability.", "Validation Error");
            return;
        }

        var modifierInput = (_variableAbilityModifierInput.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(modifierInput))
        {
            OS.Alert("Enter a modifier (example: 2 or D6).", "Validation Error");
            return;
        }

        if (!DiceHelpers.IsDamageExpressionValid(modifierInput))
        {
            OS.Alert("Modifier must be an integer or D3/D6 format.", "Format Error");
            return;
        }

        var baseAbility = _sortedVariableBaseAbilities[selectedIndex];
        var generated = SquadAbilities.CreateVariableAbility(baseAbility, modifierInput);

        if (_selectedAbilities.Any(a => a.Name.Equals(generated.Name, StringComparison.OrdinalIgnoreCase)))
        {
            OS.Alert($"Ability '{generated.Name}' is already selected.", "Validation Error");
            return;
        }

        _selectedAbilities.Add(generated);
        _variableAbilityModifierInput.Text = string.Empty;
        UpdateAbilitiesLabel();
    }


    private void UpdateAbilitiesLabel()
    {
        _abilitiesLabel.Text = _selectedAbilities.Count == 0
            ? "No abilities selected."
            : $"Abilities: {string.Join(", ", _selectedAbilities.Select(a => a.Name))}";
    }

    private void OnSavePressed()
    {
        var squadName = _nameInput.Text.Trim();
        var movement = (float)_movementInput.Value;
        var hardness = (int)_hardnessInput.Value;
        var defense = (int)_defenseInput.Value;
        var dodge = (int)_dodgeInput.Value;
        var damageResistance = (int)_damageResistanceInput.Value;
        var bravery = (int)_braveryInput.Value;

        if (string.IsNullOrWhiteSpace(squadName) || movement <= 0 ||
            _selectedModels.ItemCount == 0 || _selectedSquadTypes.Count == 0)
        {
            OS.Alert("Please fill in all fields properly & choose models.", "Validation Error");
            return;
        }

        if (_selectedModels.ItemCount > MaxModelsPerSquad)
        {
            OS.Alert($"A squad cannot have more than {MaxModelsPerSquad} models.", "Validation Error");
            return;
        }

        if (hardness < 1 ||
            defense < 1 || defense > 7 ||
            dodge < 1 || dodge > 7 ||
            damageResistance < 1 || damageResistance > 7 ||
            bravery < 1)
        {
            OS.Alert("Hardness/Bravery must be at least 1. Defense, Dodge, and Damage Resistance must be 1-7.", "Validation Error");
            return;
        }

        var data = GameData.Instance;

        var selectedModelNames = new List<string>();
        for (int i = 0; i < _selectedModels.ItemCount; i++)
            selectedModelNames.Add(_selectedModels.GetItemText(i));

        var modelLookup = data.ModelList
            .GroupBy(model => model.Name)
            .ToDictionary(group => group.Key, group => group.First());

        var composition = new List<Model>();
        foreach (var selectedName in selectedModelNames)
        {
            if (modelLookup.TryGetValue(selectedName, out var matchedModel))
                composition.Add(matchedModel);
        }

        var squad = new Squad(
            name: squadName,
            movement: movement,
            hardness: hardness,
            defense: defense,
            dodge: dodge,
            damageResistance: damageResistance,
            bravery: bravery,
            squadType: new List<string>(_selectedSquadTypes),
            shellShock: false,
            composition: composition,
            startingModelSize: composition.Count,
            squadAbilities: new List<SquadAbility>(_selectedAbilities)
        );

        if (data.SelectedSquadIndex == -1)
            data.SquadList.Add(squad);
        else
            data.SquadList[data.SelectedSquadIndex] = squad;

        data.SaveSquadsToFile();
        data.SelectedSquadIndex = -1;
        GetTree().ChangeSceneToFile("res://Scenes/SquadList.tscn");
    }

    private void OnDiscardPressed()
    {
        GameData.Instance.SelectedSquadIndex = -1;
        GetTree().ChangeSceneToFile("res://Scenes/SquadList.tscn");
    }
}
