extends Control

var _slides: Array = []
var _current_index := 0

@onready var _chart = %HistogramChart
@onready var _counter_label: Label = %SlideCounterLabel
@onready var _prev_button: Button = %BtnPrevSlide
@onready var _next_button: Button = %BtnNextSlide

func _ready() -> void:
	_prev_button.pressed.connect(_on_prev_pressed)
	_next_button.pressed.connect(_on_next_pressed)
	visible = false

func set_slides(slides: Array) -> void:
	_slides = slides
	_current_index = 0
	visible = _slides.size() > 0
	_render_current_slide()

func clear_slides() -> void:
	set_slides([])

func _on_prev_pressed() -> void:
	if _slides.is_empty():
		return
	_current_index = max(0, _current_index - 1)
	_render_current_slide()

func _on_next_pressed() -> void:
	if _slides.is_empty():
		return
	_current_index = min(_slides.size() - 1, _current_index + 1)
	_render_current_slide()

func _render_current_slide() -> void:
	if _slides.is_empty():
		_counter_label.text = "No histograms"
		_prev_button.disabled = true
		_next_button.disabled = true
		return

	var slide: Dictionary = _slides[_current_index]
	_counter_label.text = "Histogram %d / %d" % [_current_index + 1, _slides.size()]
	_prev_button.disabled = _current_index == 0
	_next_button.disabled = _current_index >= _slides.size() - 1

	_chart.SetData(
		slide.get("title", "Histogram"),
		slide.get("x_label", "Value"),
		slide.get("series_a_values", []),
		slide.get("series_a_color", Color.SKY_BLUE),
		slide.get("series_a_label", "Series A"),
		slide.get("series_b_values", []),
		slide.get("series_b_color", Color.INDIAN_RED),
		slide.get("series_b_label", "Series B"),
		int(slide.get("bins", 12))
	)
