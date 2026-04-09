using Godot;
using System;

public partial class ModelList : Control
{
    [Export] public PackedScene ListItemScene = GD.Load<PackedScene>("res://Scenes/ModelRow.tscn");
    [Export] public string CreateModelScenePath = "res://Scenes/CreateModel.tscn";

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
        data.LoadModelsFromFile();
        data.SelectedModelIndex = -1;
        PopulateList();
    }

    private void OnCreatePressed()
    {
        var data = GameData.Instance;
        if (data.ModelList.Count > 3000) // Arbitrary limit to prevent excessive memory usage
        {
            OS.Alert("You have too many models.", "Limit reached");
            return;
        }
        TryNavigateToCreateModel();
    }

    private void TryNavigateToCreateModel()
    {
        if (string.IsNullOrWhiteSpace(CreateModelScenePath) || !ResourceLoader.Exists(CreateModelScenePath))
        {
            OS.Alert("Create Model scene is not available yet.", "Unavailable");
            return;
        }

        GetTree().ChangeSceneToFile(CreateModelScenePath);
    }

    private void PopulateList()
    {
        if (_listContainer == null) return;

        foreach (Node child in _listContainer.GetChildren())
            child.QueueFree(); // Clear existing items

        var data = GameData.Instance;
        if (data == null)
        {
            GD.PrintErr("GameData Instance is null!");
            return;
        }

        for (int i = 0; i < data.ModelList.Count; i++)
        {
            var item = ListItemScene.Instantiate<ModelListItem>();
            _listContainer.AddChild(item);
            item.Setup(data.ModelList[i].Name, i);

            item.ModelClicked += (idx) => {
                data.SelectedModelIndex = idx;
                TryNavigateToCreateModel();
            };
            item.ModelDeleteRequested += (idx) => {
                RequestDelete(idx);
            };
        }
    }

    private void RequestDelete(int index)
    {
        var data = GameData.Instance;
        if (index < 0 || index >= data.ModelList.Count)
            return;

        _pendingDeleteIndex = index;
        var modelName = data.ModelList[index].Name;
        _deleteDialog.Title = "Delete Model";
        _deleteDialog.DialogText = $"Are you sure you want to delete '{modelName}'?";
        _deleteDialog.PopupCentered();
    }

    private void HandleDeleteConfirmed()
    {
        var data = GameData.Instance;
        if (_pendingDeleteIndex < 0 || _pendingDeleteIndex >= data.ModelList.Count)
        {
            _pendingDeleteIndex = -1;
            return;
        }

        data.ModelList.RemoveAt(_pendingDeleteIndex);
        data.SaveModelsToFile();
        _pendingDeleteIndex = -1;
        PopulateList();
    }
}
