using Godot;
using System;

public partial class WeaponList : Control
{
    // Point these to your actual folder paths
    [Export] public PackedScene ListItemScene = GD.Load<PackedScene>("res://Scenes/WeaponRow.tscn");

    private VBoxContainer _listContainer;
    private ConfirmationDialog _deleteDialog;
    private int _pendingDeleteIndex = -1;

    public override void _Ready()
    {
        _listContainer = GetNode<VBoxContainer>("%ListContainer"); // Use unique name
        _deleteDialog = new ConfirmationDialog();
        AddChild(_deleteDialog);
        _deleteDialog.Confirmed += HandleDeleteConfirmed;

        // Connect Back Button
        GetNode<Button>("VBoxContainer/BtnBack").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");

        // Connect Create Button
        GetNode<Button>("VBoxContainer/BtnCreate").Pressed += () =>
            OnCreatePressed();

        var data = GameData.Instance;
        data.LoadWeaponsFromFile();
        data.SelectedWeaponIndex = -1;
        PopulateList();
    }

    private void OnCreatePressed()
    {
        var data = GameData.Instance;
        if (data.WeaponList.Count > 3000)
        {
            OS.Alert("You have too many weapons.", "Limit reached");
            return;
        }

        GetTree().ChangeSceneToFile("res://Scenes/CreateWeapon.tscn");
    }

    private void PopulateList()
    {
        // 1. Safety check for the container (Unique Name)
        if (_listContainer == null) return;

        // 2. Clear old items
        foreach (Node child in _listContainer.GetChildren())
        {
            child.QueueFree();
        }

        // 3. Use the Static Instance instead of GetNode("/root/GameData")
        var data = GameData.Instance;

        // 4. Check if data is null (prevents crash if Autoload hasn't finished loading)
        if (data == null)
        {
            GD.PrintErr("GameData Instance is null!");
            return;
        }

        // 5. Build the list
        for (int i = 0; i < data.WeaponList.Count; i++)
        {
            var item = ListItemScene.Instantiate<WeaponListItem>();
            _listContainer.AddChild(item);

            // Pass the name and index to the row script
            item.Setup(data.WeaponList[i].WeaponName, i);

            // Connect the signal using a C# Lambda
            item.WeaponClicked += (idx) => {
                data.SelectedWeaponIndex = idx;
                GetTree().ChangeSceneToFile("res://Scenes/CreateWeapon.tscn");
            };
            item.WeaponDeleteRequested += (idx) => {
                RequestDelete(idx);
            };
        }
    }

    private void RequestDelete(int index)
    {
        var data = GameData.Instance;
        if (index < 0 || index >= data.WeaponList.Count)
        {
            return;
        }

        _pendingDeleteIndex = index;
        var weaponName = data.WeaponList[index].WeaponName;
        _deleteDialog.Title = "Delete Weapon";
        _deleteDialog.DialogText = $"Are you sure you want to delete '{weaponName}'?";
        _deleteDialog.PopupCentered();
    }

    private void HandleDeleteConfirmed()
    {
        var data = GameData.Instance;
        if (_pendingDeleteIndex < 0 || _pendingDeleteIndex >= data.WeaponList.Count)
        {
            _pendingDeleteIndex = -1;
            return;
        }

        data.WeaponList.RemoveAt(_pendingDeleteIndex);
        data.SaveWeaponsToFile();
        _pendingDeleteIndex = -1;
        PopulateList();
    }
}
