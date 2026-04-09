using Godot;
using System;

public partial class WeaponList : Control
{
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

        GetNode<Button>("VBoxContainer/BtnBack").Pressed += () =>
            GetTree().ChangeSceneToFile("res://Scenes/MainMenu.tscn");
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
        if (_listContainer == null) return;   // 1. Safety check for the container (Unique Name)


        foreach (Node child in _listContainer.GetChildren())
            child.QueueFree();         // 2. Clear old items
                                       // 
        var data = GameData.Instance;  // 3. Use the Static Instance instead of GetNode("/root/GameData")
   
        if (data == null)  // 4. Check if data is null (prevents crash if Autoload hasn't finished loading)
        {
            GD.PrintErr("GameData Instance is null!");
            return;
        }
      
        for (int i = 0; i < data.WeaponList.Count; i++) // 5. Build the list
        {
            var item = ListItemScene.Instantiate<WeaponListItem>();
            _listContainer.AddChild(item);
         
            item.Setup(data.WeaponList[i].WeaponName, i);  // Pass the name and index to the row script
        
            item.WeaponClicked += (idx) => {
                data.SelectedWeaponIndex = idx;
                GetTree().ChangeSceneToFile("res://Scenes/CreateWeapon.tscn");  // Connect the signal using a C# Lambda
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
            return;

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
