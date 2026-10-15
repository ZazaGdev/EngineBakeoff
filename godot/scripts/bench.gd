class_name Bench
extends RefCounted
## 3 s warm-up, then 60 s of recorded frame intervals on autopilot.

const WARMUP := 3.0
const SLOW_MS := 33.3

var duration := 60.0
var quit_when_done := false
var running := false
var t := 0.0
var last_tick := 0
var attack_clock := 0.0
var samples: PackedFloat32Array = []
var last_results := ""


func start() -> void:
	running = true
	t = 0.0
	attack_clock = 0.0
	last_tick = 0
	samples.clear()


func autopilot(delta: float, player_pos: Vector3) -> Dictionary:
	var target := Vector3(6 * cos(0.5 * t), 0, 6 * sin(0.5 * t))
	var move := target - player_pos
	move.y = 0
	attack_clock += delta
	var attack := false
	if attack_clock >= 1.0:
		attack_clock -= 1.0
		attack = true
	return {"move": move.limit_length(1.0), "attack": attack}


## Returns true once, when the run has finished. Uses the real clock, not the engine's
## smoothed delta, so hitches show up.
func record(delta: float) -> bool:
	var now := Time.get_ticks_usec()
	t += delta
	if t > WARMUP and last_tick > 0:
		samples.append((now - last_tick) / 1000.0)
	last_tick = now
	if t >= WARMUP + duration:
		running = false
		last_results = _summarise()
		print(last_results)
		if quit_when_done:
			Engine.get_main_loop().quit()
		return true
	return false


func _summarise() -> String:
	var sorted := samples.duplicate()
	sorted.sort()
	var n := sorted.size()
	var total := 0.0
	var slow := 0
	for ms in sorted:
		total += ms
		if ms > SLOW_MS:
			slow += 1
	var avg := total / n
	var p99: float = sorted[mini(n - 1, int(ceil(n * 0.99)) - 1)]
	var info := Engine.get_version_info()
	return "Godot %s (%s)\nframes %d\navg %.2f ms (%.1f fps)\np99 %.2f ms\nworst %.2f ms\nover 33 ms: %d" % [
		info.string, RenderingServer.get_video_adapter_name(), n, avg, 1000.0 / avg, p99, sorted[n - 1], slow]


func results_text() -> String:
	return last_results
