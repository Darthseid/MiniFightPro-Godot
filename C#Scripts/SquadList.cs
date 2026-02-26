using Godot;
using System;

public partial class SquadList : Control
{
    [Export] public PackedScene ListItemScene = GD.Load<PackedScene>("res://Scenes/SquadRow.tscn");
    [Export] public string CreateSquadScenePath = "res://Scenes/CreateSquad.tscn";

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
        data.LoadSquadsFromFile();
        data.SelectedSquadIndex = -1;
        PopulateList();
    }

    private void OnCreatePressed()
    {
        var data = GameData.Instance;
        if (data.SquadList.Count > 3000)
        {
            OS.Alert("You have too many squads.", "Limit reached");
            return;
        }

        TryNavigateToCreateSquad();
    }

    private void TryNavigateToCreateSquad()
    {
        if (string.IsNullOrWhiteSpace(CreateSquadScenePath) || !ResourceLoader.Exists(CreateSquadScenePath))
        {
            OS.Alert("Create Squad scene is not available yet.", "Unavailable");
            return;
        }

        GetTree().ChangeSceneToFile(CreateSquadScenePath);
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

        for (int i = 0; i < data.SquadList.Count; i++)
        {
            var item = ListItemScene.Instantiate<SquadListItem>();
            _listContainer.AddChild(item);
            item.Setup(data.SquadList[i].Name, i);

            item.SquadClicked += (idx) => {
                data.SelectedSquadIndex = idx;
                TryNavigateToCreateSquad();
            };
            item.SquadDeleteRequested += (idx) => {
                RequestDelete(idx);
            };
        }
    }

    private void RequestDelete(int index)
    {
        var data = GameData.Instance;
        if (index < 0 || index >= data.SquadList.Count)
        {
            return;
        }

        _pendingDeleteIndex = index;
        var squadName = data.SquadList[index].Name;
        _deleteDialog.Title = "Delete Squad";
        _deleteDialog.DialogText = $"Are you sure you want to delete '{squadName}'?";
        _deleteDialog.PopupCentered();
    }

    private void HandleDeleteConfirmed()
    {
        var data = GameData.Instance;
        if (_pendingDeleteIndex < 0 || _pendingDeleteIndex >= data.SquadList.Count)
        {
            _pendingDeleteIndex = -1;
            return;
        }

        data.SquadList.RemoveAt(_pendingDeleteIndex);
        data.SaveSquadsToFile();
        _pendingDeleteIndex = -1;
        PopulateList();
    }
}
