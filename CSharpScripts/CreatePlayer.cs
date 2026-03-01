using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CreatePlayer : Control
{
    private Label _titleLabel = null!;
    private LineEdit _playerNameInput = null!;
    private CheckBox _isAICheckBox = null!;
    private ItemList _availableSquads = null!;
    private ItemList _selectedSquads = null!;
    private ItemList _availableOrders = null!;
    private ItemList _selectedOrders = null!;
    private Button _addSquadsButton = null!;
    private Button _removeSquadsButton = null!;
    private Button _addOrdersButton = null!;
    private Button _removeOrdersButton = null!;
    private Button _abilitiesButton = null!;
    private Label _abilitiesLabel = null!;
    private AcceptDialog _abilitiesDialog = null!;
    private ItemList _abilitiesList = null!;

    private readonly List<string> _selectedAbilities = new();
    private List<string> _sortedAbilities = new();

    public override void _Ready()
    {
        var data = GameData.Instance;
        data.LoadSquadsFromFile();
        data.LoadPlayersFromFile();

        _titleLabel = GetNode<Label>("%TitleLabel");
        _playerNameInput = GetNode<LineEdit>("%PlayerNameInput");
        _isAICheckBox = GetNode<CheckBox>("%IsAICheckBox");
        _availableSquads = GetNode<ItemList>("%AvailableSquadsList");
        _selectedSquads = GetNode<ItemList>("%SelectedSquadsList");
        _availableOrders = GetNode<ItemList>("%AvailableOrdersList");
        _selectedOrders = GetNode<ItemList>("%SelectedOrdersList");
        _addSquadsButton = GetNode<Button>("%BtnAddSquads");
        _removeSquadsButton = GetNode<Button>("%BtnRemoveSquads");
        _addOrdersButton = GetNode<Button>("%BtnAddOrders");
        _removeOrdersButton = GetNode<Button>("%BtnRemoveOrders");
        _abilitiesButton = GetNode<Button>("%BtnAbilities");
        _abilitiesLabel = GetNode<Label>("%AbilitiesLabel");
        _abilitiesDialog = GetNode<AcceptDialog>("%AbilitiesDialog");
        _abilitiesList = GetNode<ItemList>("%AbilitiesList");

        GetNode<Button>("%BtnSave").Pressed += OnSavePressed;
        GetNode<Button>("%BtnDiscard").Pressed += OnDiscardPressed;
        _addSquadsButton.Pressed += OnAddSquads;
        _removeSquadsButton.Pressed += OnRemoveSquads;
        _addOrdersButton.Pressed += OnAddOrders;
        _removeOrdersButton.Pressed += OnRemoveOrders;
        _abilitiesButton.Pressed += ShowAbilitiesDialog;
        _abilitiesDialog.Confirmed += ApplyAbilitiesSelection;

        PopulateSquadLists();
        PopulateOrdersLists();
        PopulateDialogs();
        LoadDataIfEditing();
        UpdateAbilitiesLabel();
    }

    private void PopulateSquadLists()
    {
        _availableSquads.Clear();
        _selectedSquads.Clear();

        foreach (var squad in GameData.Instance.SquadList
                     .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
        {
            _availableSquads.AddItem(squad.Name);
        }
    }

    private void PopulateOrdersLists()
    {
        _availableOrders.Clear();
        _selectedOrders.Clear();
        foreach (var order in Order.BuildDefaultOrders())
        {
            _availableOrders.AddItem(order.OrderName);
        }
    }

    private void PopulateDialogs()
    {
        _abilitiesList.Clear();
        _sortedAbilities = PlayerAbilities.All
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var ability in _sortedAbilities)
            _abilitiesList.AddItem(ability);
    }

    private void LoadDataIfEditing()
    {
        var data = GameData.Instance;

        if (data.SelectedPlayerIndex >= 0 && data.SelectedPlayerIndex < data.PlayerList.Count)
        {
            _titleLabel.Text = "Edit Player";
            var player = data.PlayerList[data.SelectedPlayerIndex];

            _playerNameInput.Text = player.PlayerName;
            _isAICheckBox.ButtonPressed = player.IsAI;

            _selectedAbilities.Clear();
            _selectedAbilities.AddRange(player.PlayerAbilities ?? new List<string>());

            foreach (var squad in player.TheirSquads ?? new List<Squad>())
            {
                _selectedSquads.AddItem(squad.Name);
            }

            foreach (var order in player.Orders ?? new List<Order>())
            {
                _selectedOrders.AddItem(order.OrderName);
            }
        }
        else
        {
            _titleLabel.Text = "Create Player";
        }
    }

    private void OnAddSquads()
    {
        foreach (int index in _availableSquads.GetSelectedItems())
        {
            var name = _availableSquads.GetItemText(index);
            _selectedSquads.AddItem(name);
        }
    }

    private void OnRemoveSquads()
    {
        var selectedIndices = _selectedSquads.GetSelectedItems();
        Array.Sort(selectedIndices);

        for (int i = selectedIndices.Length - 1; i >= 0; i--)
            _selectedSquads.RemoveItem(selectedIndices[i]);
    }

    private void OnAddOrders()
    {
        foreach (int index in _availableOrders.GetSelectedItems())
        {
            var name = _availableOrders.GetItemText(index);
            _selectedOrders.AddItem(name);
        }
    }

    private void OnRemoveOrders()
    {
        var selectedIndices = _selectedOrders.GetSelectedItems();
        Array.Sort(selectedIndices);

        for (int i = selectedIndices.Length - 1; i >= 0; i--)
            _selectedOrders.RemoveItem(selectedIndices[i]);
    }

    private void ShowAbilitiesDialog()
    {
        _abilitiesList.UnselectAll();

        var selected = new HashSet<string>(_selectedAbilities);
        for (int i = 0; i < _sortedAbilities.Count; i++)
            if (selected.Contains(_sortedAbilities[i]))
                _abilitiesList.Select(i);

        _abilitiesDialog.PopupCentered();
    }

    private void ApplyAbilitiesSelection()
    {
        _selectedAbilities.Clear();

        foreach (int index in _abilitiesList.GetSelectedItems())
            if (index >= 0 && index < _sortedAbilities.Count)
                _selectedAbilities.Add(_sortedAbilities[index]);

        UpdateAbilitiesLabel();
    }

    private void UpdateAbilitiesLabel()
    {
        _abilitiesLabel.Text = _selectedAbilities.Count == 0
            ? "No abilities selected."
            : $"Abilities: {string.Join(", ", _selectedAbilities)}";
    }

    private void OnSavePressed()
    {
        var playerName = _playerNameInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(playerName) || _selectedSquads.ItemCount == 0)
        {
            OS.Alert("Please enter a player name and select squads.", "Validation Error");
            return;
        }

        var data = GameData.Instance;

        var selectedSquadNames = new List<string>();
        for (int i = 0; i < _selectedSquads.ItemCount; i++)
            selectedSquadNames.Add(_selectedSquads.GetItemText(i));

        var squadLookup = data.SquadList
            .GroupBy(squad => squad.Name)
            .ToDictionary(group => group.Key, group => group.First());

        var squads = new List<Squad>();
        foreach (var selectedName in selectedSquadNames)
        {
            if (squadLookup.TryGetValue(selectedName, out var matchedSquad))
            {
                squads.Add(matchedSquad.DeepCopy());
            }
        }

        var orderTemplates = Order.BuildDefaultOrders();
        var orderLookupByName = orderTemplates.ToDictionary(o => o.OrderName, o => o, StringComparer.OrdinalIgnoreCase);
        var selectedOrders = new List<Order>();
        for (int i = 0; i < _selectedOrders.ItemCount; i++)
        {
            var selectedName = _selectedOrders.GetItemText(i);
            if (orderLookupByName.TryGetValue(selectedName, out var template))
            {
                selectedOrders.Add(new Order(template.OrderId, template.OrderCost, template.OrderName, template.AvailablePhase, template.TargetsEnemy, template.Description, template.WindowType, template.TargetSide, template.TargetType, template.RequiresTarget));
            }
        }

        var player = new Player(
            theirSquads: squads,
            orderPoints: 0,
            orders: selectedOrders,
            isAI: _isAICheckBox.ButtonPressed,
            playerName: playerName,
            playerAbilities: new List<string>(_selectedAbilities)
        );

        if (data.SelectedPlayerIndex == -1)
            data.PlayerList.Add(player);
        else
            data.PlayerList[data.SelectedPlayerIndex] = player;

        data.SavePlayersToFile();
        data.SelectedPlayerIndex = -1;
        GetTree().ChangeSceneToFile("res://Scenes/PlayerList.tscn");
    }

    private void OnDiscardPressed()
    {
        GameData.Instance.SelectedPlayerIndex = -1;
        GetTree().ChangeSceneToFile("res://Scenes/PlayerList.tscn");
    }
}
