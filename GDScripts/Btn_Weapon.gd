extends Button

@onready var click_sound: AudioStreamPlayer = $"../MenuSound"

func _on_pressed() -> void:
	click_sound.play()
	await get_tree().create_timer(0.5).timeout
	get_tree().change_scene_to_file("res://Scenes/WeaponList.tscn")
