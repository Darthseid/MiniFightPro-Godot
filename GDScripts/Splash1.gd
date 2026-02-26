extends Timer

func _on_timeout() -> void:
	get_tree().change_scene_to_file("res://Scenes/MainMenu.tscn")
