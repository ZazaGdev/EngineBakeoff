# Engine bakeoff: the same 3D test scene in Unity and Godot on iPhone

## Goal

One small 3D test scene, built the same way in Unity 6 LTS and Godot 4.7, installed on the
owner's iPhone through TestFlight. Each build measures its own frame times on the phone, so
the owner can pick the engine for the action RPG plus restaurant game. A results file gives
average and worst frame times, build size and owner steps for each.

## The scene (same in both engines)

- One dungeon room, 24 x 24 m: KayKit Dungeon Remastered floor tiles, walls and four pillars.
- Player: KayKit Knight with Idle, Running_A and 1H_Melee_Attack_Chop animations.
- 20 KayKit Skeleton_Minion enemies. They walk straight at the player (no pathfinding),
  stop at 1.5 m and play an attack. A player swing kills enemies within 2.2 m in front;
  a killed enemy respawns at a fixed edge point, so there are always 20 on screen.
- One directional light with shadows, plus flat ambient light. No other lights.
- Fixed third-person camera that follows the player.
- On-screen joystick (left) and attack button (right).
- Frame cap 60 fps in both engines.

## Measurement

A "Benchmark" button starts a 60 second autopilot run: the player walks a fixed circle and
swings every second, enemies spawn from the same seeded positions. Both engines record each
frame's interval and show on screen: average ms, 99th percentile ms, worst ms, and frames
over 33 ms. The owner reads the numbers off the phone (screenshot is enough).

## Out of scope

The actual game, the game kit, any paid service, polish beyond what the test needs.

## Files

- `assets/kaykit/` shared CC0 models and licences (copied into each engine project).
- `godot/` Godot 4.7.2 project (GDScript, Mobile renderer).
- `unity/` Unity 6 LTS project (C#, URP).
- `.github/workflows/godot-ios.yml` builds Godot on GitHub's free macOS runner and uploads
  to TestFlight.
- Unity builds on Unity Build Automation (100 free Mac minutes a month).
- `RESULTS.md` the results.
- `tools/` engine downloads, git-ignored.

## Verification

- Godot: headless import and a 5 second run of the scene on Windows with no script errors.
- Unity: batch-mode build of the scene on Windows with no compile errors.
- Both builds show in TestFlight and run on the owner's iPhone; benchmark numbers recorded.

## Risks

- Unity Build Automation may want a card on file even for free minutes: comes back to the
  owner before adding one.
- Godot's iPhone export on CI is less trodden; the GitHub runner may need Xcode version pinning.
- Apple signing needs the owner: App Store Connect API key and two app records.
- At a 60 fps cap the average sits near 16.7 ms on a fast phone; the worst and 99th
  percentile frames are the telling numbers.
