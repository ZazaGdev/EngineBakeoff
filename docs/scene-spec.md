# Test scene spec

Both engines build this scene from code at start-up, with these exact numbers. Metres,
seconds, degrees. Y is up; the camera looks towards negative Z.

## Room

- Floor: `floor_tile_large` (4 x 4 m), 6 x 6 tiles, centres at x, z = -10, -6, -2, 2, 6, 10.
- Walls: `wall` (4 m wide), 6 per side, centred on the floor edge at +-12, facing inwards.
- Pillars: `pillar` at (+-6, 0, +-6).
- Playable area: x and z clamped to +-11.

## Light

- One directional light, pitch -50, yaw 30, intensity 1.0, white, shadows on,
  shadow resolution 2048, shadow distance 30.
- Ambient: flat colour (0.35, 0.35, 0.42), no sky, background colour (0.05, 0.05, 0.07).
- No other lights. No fog, bloom, SSAO or other post effects. MSAA off. Render scale 1.0.

## Player

- `Knight` with only `Knight_*`, `1H_Sword` and `Badge_Shield` visible.
- Speed 4 m/s, turns to face the move direction.
- Animations: `Idle`, `Running_A` (loop), `1H_Melee_Attack_Chop` (once).
- Attack: cooldown 0.8 s. Hits every enemy within 2.2 m whose direction from the player
  has a dot product above 0.3 with the player's facing. A hit enemy respawns at once at
  its own spawn point.

## Enemies

- 20 x `Skeleton_Minion`. Spawn point i (0-19): angle i x 18 degrees, radius 10,
  position (10 cos a, 0, 10 sin a).
- Speed 2.2 m/s straight at the player, facing the player.
- Separation: each pair closer than 1.4 m is pushed apart equally.
- At 1.5 m or closer they stop and play `1H_Melee_Attack_Chop` every 1.6 s; otherwise
  `Running_A`. No damage to the player (it would end the benchmark run).

## Camera

- Perspective, vertical field of view 50, positioned at player + (0, 10, 8), looking at
  the player.

## Controls

- Joystick: bottom left, radius 90 points. Attack button: bottom right.
- "Benchmark" button: top right. Live FPS label: top left.
- Desktop: WASD or arrows to move, Space to attack, B to start the benchmark.

## Benchmark

- Frame cap 60 fps (vsync).
- On start: all enemies return to their spawn points, the player to the centre.
- 3 s warm-up, then 60 s recorded. Autopilot: target point (6 cos 0.5t, 0, 6 sin 0.5t),
  the player moves towards it at normal speed and attacks every 1.0 s.
- Each frame's interval is recorded from the real clock (not the engine's smoothed delta). At the end the screen shows: engine and version,
  frames, average ms, 99th percentile ms, worst ms, frames over 33.3 ms, and the average
  FPS. The results stay on screen until the next run.
