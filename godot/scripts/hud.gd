class_name Hud
extends CanvasLayer
## Joystick, attack button, benchmark button, FPS label and results panel.

signal attack_pressed
signal bench_pressed

const STICK_RADIUS := 90.0

var fps_label: Label
var results_label: Label
var stick_base: Panel
var stick_knob: Panel
var stick_touch := -1
var stick_center := Vector2.ZERO
var stick_vec := Vector2.ZERO


func _ready() -> void:
	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(root)

	fps_label = Label.new()
	fps_label.position = Vector2(16, 12)
	fps_label.add_theme_font_size_override("font_size", 18)
	root.add_child(fps_label)

	results_label = Label.new()
	results_label.position = Vector2(16, 40)
	results_label.add_theme_font_size_override("font_size", 16)
	results_label.add_theme_color_override("font_outline_color", Color.BLACK)
	results_label.add_theme_constant_override("outline_size", 6)
	root.add_child(results_label)

	var bench := Button.new()
	bench.text = "Benchmark"
	bench.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	bench.offset_left = -150
	bench.offset_top = 12
	bench.offset_right = -16
	bench.offset_bottom = 56
	bench.pressed.connect(func(): results_label.text = "running..."; bench_pressed.emit())
	root.add_child(bench)

	var attack := Button.new()
	attack.text = "ATTACK"
	attack.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_RIGHT)
	attack.offset_left = -150
	attack.offset_top = -150
	attack.offset_right = -30
	attack.offset_bottom = -30
	attack.button_down.connect(func(): attack_pressed.emit())
	root.add_child(attack)

	stick_base = _circle(STICK_RADIUS, Color(1, 1, 1, 0.15))
	stick_knob = _circle(36, Color(1, 1, 1, 0.45))
	root.add_child(stick_base)
	root.add_child(stick_knob)
	_place_stick(_rest_center())


func _circle(r: float, color: Color) -> Panel:
	var p := Panel.new()
	var sb := StyleBoxFlat.new()
	sb.bg_color = color
	sb.set_corner_radius_all(int(r))
	p.add_theme_stylebox_override("panel", sb)
	p.size = Vector2(r * 2, r * 2)
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return p


func _rest_center() -> Vector2:
	var vp := get_viewport().get_visible_rect().size
	return Vector2(40 + STICK_RADIUS, vp.y - 40 - STICK_RADIUS)


func _place_stick(center: Vector2) -> void:
	stick_center = center
	stick_base.position = center - stick_base.size / 2
	stick_knob.position = center + stick_vec * STICK_RADIUS - stick_knob.size / 2


func _input(event: InputEvent) -> void:
	var half := get_viewport().get_visible_rect().size.x / 2
	if event is InputEventScreenTouch:
		if event.pressed and stick_touch == -1 and event.position.x < half:
			stick_touch = event.index
			stick_vec = Vector2.ZERO
			_place_stick(event.position)
		elif not event.pressed and event.index == stick_touch:
			stick_touch = -1
			stick_vec = Vector2.ZERO
			_place_stick(_rest_center())
	elif event is InputEventScreenDrag and event.index == stick_touch:
		stick_vec = ((event.position - stick_center) / STICK_RADIUS).limit_length(1.0)
		_place_stick(stick_center)
	elif event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_SPACE:
			attack_pressed.emit()
		elif event.keycode == KEY_B:
			results_label.text = "running..."
			bench_pressed.emit()


## Joystick or keyboard direction on the ground plane (x right, z towards the camera).
func move_vector() -> Vector3:
	var v := stick_vec
	if v == Vector2.ZERO:
		v = Input.get_vector("ui_left", "ui_right", "ui_up", "ui_down")
		if Input.is_key_pressed(KEY_A): v.x -= 1
		if Input.is_key_pressed(KEY_D): v.x += 1
		if Input.is_key_pressed(KEY_W): v.y -= 1
		if Input.is_key_pressed(KEY_S): v.y += 1
	return Vector3(v.x, 0, v.y).limit_length(1.0)


func set_fps(fps: float) -> void:
	fps_label.text = "%d fps" % fps


func show_results(text: String) -> void:
	results_label.text = text
