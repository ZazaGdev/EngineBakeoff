# EngineBakeoff

The same small 3D test scene built in Unity 6 LTS and in Godot 4.7, put on the owner's
iPhone through TestFlight, to pick the engine for the owner's game: "an RPG dungeon
crawler plus a restaurant keeper ... by day a business restaurant owner and by night a
dungeon RPG crawler ... more action heavy 3D".

Plan: `docs/plans/2026-10-15-engine-bakeoff.md`. Results: `RESULTS.md`.

## Rules

- The repo is public. Never commit Apple certificates, keys, provisioning profiles, the
  App Store Connect API key, Unity licence files or anything from `secrets/`. Signing
  material lives in GitHub repository secrets and Unity Build Automation only.
- Engines come from official sources only (godotengine.org or the godotengine GitHub
  releases with SHA512-SUMS checked, Unity Hub from unity.com). They live in `tools/`,
  which is git-ignored.
- Models are CC0 only (KayKit by Kay Lousberg). Keep the licence files next to them.
- Keep the two scenes equal: same models, room, enemy count, light, camera and
  benchmark run. A change to one scene goes into the other in the same commit.
- Nothing paid (Unity plans, extra build minutes, rented Macs) without asking the owner.
