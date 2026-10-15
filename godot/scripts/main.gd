extends Node3D
## Builds the test scene from docs/scene-spec.md and runs the game loop.

const ROOM_HALF := 11.0
const PLAYER_SPEED := 4.0
const ATTACK_COOLDOWN := 0.8
const ATTACK_RANGE := 2.2
const ENEMY_COUNT := 20
const ENEMY_SPEED := 2.2
const ENEMY_STOP := 1.5
const ENEMY_ATTACK_EVERY := 1.6
const SEPARATION := 1.4
const CAMERA_OFFSET := Vector3(0, 10, 8)

const KnightScene := preload("res://assets/Knight.glb")
const SkeletonScene := preload("res://assets/Skeleton_Minion.glb")
const FloorScene := preload("res://assets/floor_tile_large.glb")
const WallScene := preload("res://assets/wall.glb")
const PillarScene := preload("res://assets/pillar.glb")

const KNIGHT_HIDDEN := ["1H_Sword_Offhand", "Rectangle_Shield", "Round_Shield", "Spike_Shield", "2H_Sword"]

var player: Node3D
var player_anim: AnimationPlayer
var player_facing := Vector3(0, 0, 1)
var attack_timer := 0.0
var enemies: Array[Dictionary] = []
var camera: Camera3D
var hud: Hud
var bench: Bench


func _ready() -> void:
	_build_environment()
	_build_room()
	_build_player()
	_build_enemies()
	camera = Camera3D.new()
	camera.fov = 50
	add_child(camera)
	hud = Hud.new()
	add_child(hud)
	hud.attack_pressed.connect(_try_attack)
	hud.bench_pressed.connect(_start_bench)
	bench = Bench.new()
	_update_camera()
	# Desktop check: `-- --autobench=SECONDS` runs a short benchmark and quits.
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--autobench="):
			bench.duration = float(arg.get_slice("=", 1))
			bench.quit_when_done = true
			_start_bench()


func _build_environment() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.05, 0.05, 0.07)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.35, 0.35, 0.42)
	env.ambient_light_energy = 1.0
	env.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	var world_env := WorldEnvironment.new()
	world_env.environment = env
	add_child(world_env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, 30, 0)
	sun.light_energy = 1.0
	sun.shadow_enabled = true
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = 30
	add_child(sun)


func _build_room() -> void:
	for ix in 6:
		for iz in 6:
			var tile: Node3D = FloorScene.instantiate()
			tile.position = Vector3(-10 + ix * 4, 0, -10 + iz * 4)
			add_child(tile)
	for i in 6:
		var c := -10.0 + i * 4.0
		_add_wall(Vector3(c, 0, -12), 0)
		_add_wall(Vector3(c, 0, 12), 180)
		_add_wall(Vector3(-12, 0, c), 90)
		_add_wall(Vector3(12, 0, c), -90)
	for p in [Vector3(-6, 0, -6), Vector3(6, 0, -6), Vector3(-6, 0, 6), Vector3(6, 0, 6)]:
		var pillar: Node3D = PillarScene.instantiate()
		pillar.position = p
		add_child(pillar)


func _add_wall(pos: Vector3, yaw: float) -> void:
	var wall: Node3D = WallScene.instantiate()
	wall.position = pos
	wall.rotation_degrees.y = yaw
	add_child(wall)


func _build_player() -> void:
	player = KnightScene.instantiate()
	add_child(player)
	for hidden in KNIGHT_HIDDEN:
		var n := player.find_child(hidden, true, false)
		if n:
			n.visible = false
	player_anim = player.find_child("AnimationPlayer", true, false)
	_set_loops(player_anim)
	player_anim.play("Idle")


func _build_enemies() -> void:
	for i in ENEMY_COUNT:
		var e: Node3D = SkeletonScene.instantiate()
		add_child(e)
		var anim: AnimationPlayer = e.find_child("AnimationPlayer", true, false)
		if i == 0:
			_set_loops(anim)
		enemies.append({"node": e, "anim": anim, "spawn": _spawn_point(i), "cooldown": 0.0})
	_reset_positions()


func _spawn_point(i: int) -> Vector3:
	var a := deg_to_rad(i * 18.0)
	return Vector3(10 * cos(a), 0, 10 * sin(a))


func _set_loops(anim: AnimationPlayer) -> void:
	for name in ["Idle", "Running_A"]:
		anim.get_animation(name).loop_mode = Animation.LOOP_LINEAR


func _reset_positions() -> void:
	player.position = Vector3.ZERO
	for e in enemies:
		e.node.position = e.spawn
		e.cooldown = 0.0


func _start_bench() -> void:
	_reset_positions()
	bench.start()


func _process(delta: float) -> void:
	var move := Vector3.ZERO
	var auto_attack := false
	if bench.running:
		var r := bench.autopilot(delta, player.position)
		move = r.move
		auto_attack = r.attack
	else:
		move = hud.move_vector()

	if move.length() > 0.05:
		player_facing = move.normalized()
		player.position += move.limit_length(1.0) * PLAYER_SPEED * delta
		player.position.x = clampf(player.position.x, -ROOM_HALF, ROOM_HALF)
		player.position.z = clampf(player.position.z, -ROOM_HALF, ROOM_HALF)
		player.rotation.y = atan2(player_facing.x, player_facing.z)
		if attack_timer <= 0 and player_anim.current_animation != "Running_A":
			player_anim.play("Running_A", 0.15)
	elif attack_timer <= 0 and player_anim.current_animation != "Idle":
		player_anim.play("Idle", 0.15)

	attack_timer -= delta
	if auto_attack:
		_try_attack()

	_update_enemies(delta)
	_update_camera()
	hud.set_fps(Engine.get_frames_per_second())
	if bench.running and bench.record(delta):
		hud.show_results(bench.results_text())
		for arg in OS.get_cmdline_user_args():
			if arg.begins_with("--shot="):
				get_viewport().get_texture().get_image().save_png(arg.get_slice("=", 1))


func _try_attack() -> void:
	if attack_timer > 0:
		return
	attack_timer = ATTACK_COOLDOWN
	player_anim.play("1H_Melee_Attack_Chop", 0.05)
	for e in enemies:
		var to: Vector3 = e.node.position - player.position
		to.y = 0
		if to.length() < ATTACK_RANGE and to.normalized().dot(player_facing) > 0.3:
			e.node.position = e.spawn
			e.cooldown = 0.0


func _update_enemies(delta: float) -> void:
	for e in enemies:
		var node: Node3D = e.node
		var to := player.position - node.position
		to.y = 0
		var dist := to.length()
		if dist > 0.01:
			node.rotation.y = atan2(to.x, to.z)
		e.cooldown -= delta
		if dist > ENEMY_STOP:
			node.position += to / dist * ENEMY_SPEED * delta
			if e.anim.current_animation != "Running_A":
				e.anim.play("Running_A", 0.15)
		elif e.cooldown <= 0:
			e.cooldown = ENEMY_ATTACK_EVERY
			e.anim.play("1H_Melee_Attack_Chop", 0.1)
		elif not e.anim.is_playing():
			e.anim.play("Idle", 0.1)
	for i in enemies.size():
		for j in range(i + 1, enemies.size()):
			var a: Node3D = enemies[i].node
			var b: Node3D = enemies[j].node
			var d := b.position - a.position
			d.y = 0
			var l := d.length()
			if l < SEPARATION and l > 0.0001:
				var push := d / l * (SEPARATION - l) * 0.5
				a.position -= push
				b.position += push


func _update_camera() -> void:
	camera.position = player.position + CAMERA_OFFSET
	camera.look_at(player.position, Vector3.UP)
