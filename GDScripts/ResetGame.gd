extends Button

func _on_pressed() -> void:
	var tree = get_tree()

	# Nuke everything except the root viewport
	for child in tree.root.get_children():
		child.queue_free()

	# Load and add the Music Manager
	var music_manager = load("res://Scenes/MusicManager.tscn").instantiate()
	tree.root.add_child(music_manager)

	# Now load menu clean
	tree.change_scene_to_file("res://Scenes/MainMenu.tscn")
