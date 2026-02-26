using Godot;
using System;

public partial class PlayerList : Control
{
    [Export] public PackedScene ListItemScene = GD.Load<PackedScene>("res://Scenes/PlayerRow.tscn");
    [Export] public string CreatePlayerScenePath = "res://Scenes/CreatePlayer.tscn";

    private VBoxContainer _listContainer;
    private ConfirmationDialog _deleteDialog;
    private int _pendingDeleteIndex = -1;

    public override void _Ready()
    {
        _listContainer = GetNode<VBoxContainer>("%ListContainer");
        _deleteDialog = new ConfirmationDialog();
        AddChild(_deleteDialog);
        _deleteDialog.Confirmed += HandleDeleteConfirmed;

        GetNode<Button>("%BtnCreate").Pressed += OnCreatePressed;
        GetNode<Button>("%BtnBack").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");

        var data = GameData.Instance;
        data.LoadPlayersFromFile();
        data.SelectedPlayerIndex = -1;
        PopulateList();
    }

    private void OnCreatePressed()
    {
        var data = GameData.Instance;
        if (data.PlayerList.Count > 1000)
        {
            OS.Alert("You have too many players.", "Limit reached");
            return;
        }

        TryNavigateToCreatePlayer();
    }

    private void TryNavigateToCreatePlayer()
    {
        if (string.IsNullOrWhiteSpace(CreatePlayerScenePath) || !ResourceLoader.Exists(CreatePlayerScenePath))
        {
            OS.Alert("Create Player scene is not available yet.", "Unavailable");
            return;
        }

        GetTree().ChangeSceneToFile(CreatePlayerScenePath);
    }

    private void PopulateList()
    {
        if (_listContainer == null) return;

        foreach (Node child in _listContainer.GetChildren())
        {
            child.QueueFree();
        }

        var data = GameData.Instance;
        if (data == null)
        {
            GD.PrintErr("GameData Instance is null!");
            return;
        }

        for (int i = 0; i < data.PlayerList.Count; i++)
        {
            var item = ListItemScene.Instantiate<PlayerListItem>();
            _listContainer.AddChild(item);
            item.Setup(data.PlayerList[i].PlayerName, i);

            item.PlayerClicked += (idx) => {
                data.SelectedPlayerIndex = idx;
                TryNavigateToCreatePlayer();
            };
            item.PlayerDeleteRequested += (idx) => {
                RequestDelete(idx);
            };
        }
    }

    private void RequestDelete(int index)
    {
        var data = GameData.Instance;
        if (index < 0 || index >= data.PlayerList.Count)
        {
            return;
        }

        _pendingDeleteIndex = index;
        var playerName = data.PlayerList[index].PlayerName;
        _deleteDialog.Title = "Delete Player";
        _deleteDialog.DialogText = $"Are you sure you want to delete '{playerName}'?";
        _deleteDialog.PopupCentered();
    }

    private void HandleDeleteConfirmed()
    {
        var data = GameData.Instance;
        if (_pendingDeleteIndex < 0 || _pendingDeleteIndex >= data.PlayerList.Count)
        {
            _pendingDeleteIndex = -1;
            return;
        }

        data.PlayerList.RemoveAt(_pendingDeleteIndex);
        data.SavePlayersToFile();
        _pendingDeleteIndex = -1;
        PopulateList();
    }
}
