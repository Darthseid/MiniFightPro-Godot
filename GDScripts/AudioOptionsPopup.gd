extends PopupPanel

const MUSIC_BUS_NAME := "Music"
const SFX_BUS_NAME := "SFX"

@onready var _music_slider: HSlider = %MusicSlider
@onready var _sfx_slider: HSlider = %SfxSlider
@onready var _music_value_label: Label = %MusicValueLabel
@onready var _sfx_value_label: Label = %SfxValueLabel

func _ready() -> void:
	_music_slider.min_value = 0
	_music_slider.max_value = 100
	_music_slider.step = 1
	_music_slider.value_changed.connect(_on_music_slider_changed)

	_sfx_slider.min_value = 0
	_sfx_slider.max_value = 100
	_sfx_slider.step = 1
	_sfx_slider.value_changed.connect(_on_sfx_slider_changed)

	sync_from_buses()

func OpenPopup() -> void:
	sync_from_buses()
	popup_centered(Vector2i(420, 220))

func sync_from_buses() -> void:
	var music_percent := _get_bus_volume_percent(MUSIC_BUS_NAME)
	var sfx_percent := _get_bus_volume_percent(SFX_BUS_NAME)

	_music_slider.set_value_no_signal(music_percent)
	_sfx_slider.set_value_no_signal(sfx_percent)

	_update_music_label(music_percent)
	_update_sfx_label(sfx_percent)

func _on_music_slider_changed(value: float) -> void:
	_set_bus_volume_percent(MUSIC_BUS_NAME, value)
	_update_music_label(value)

func _on_sfx_slider_changed(value: float) -> void:
	_set_bus_volume_percent(SFX_BUS_NAME, value)
	_update_sfx_label(value)

func _update_music_label(percent: float) -> void:
	_music_value_label.text = "%d%%" % int(round(percent))

func _update_sfx_label(percent: float) -> void:
	_sfx_value_label.text = "%d%%" % int(round(percent))

func _get_bus_volume_percent(bus_name: String) -> float:
	var bus_index := AudioServer.get_bus_index(bus_name)
	if bus_index < 0:
		return 100.0

	if AudioServer.is_bus_mute(bus_index):
		return 0.0

	var db := AudioServer.get_bus_volume_db(bus_index)
	var linear := db_to_linear(db)
	return clampf(linear * 100.0, 0.0, 100.0)

func _set_bus_volume_percent(bus_name: String, percent: float) -> void:
	var bus_index := AudioServer.get_bus_index(bus_name)
	if bus_index < 0:
		return

	var clamped := clampf(percent, 0.0, 100.0)
	var linear := clamped / 100.0

	if linear <= 0.0001:
		AudioServer.set_bus_mute(bus_index, true)
		AudioServer.set_bus_volume_db(bus_index, -80.0)
		return

	AudioServer.set_bus_mute(bus_index, false)
	AudioServer.set_bus_volume_db(bus_index, linear_to_db(linear))
