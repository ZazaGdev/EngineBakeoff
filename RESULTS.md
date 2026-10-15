# Engine bakeoff results

Same scene in both engines (see `docs/scene-spec.md`): KayKit dungeon room, knight, 20 chasing
skeletons, one shadowed directional light plus flat ambient, on-screen joystick and attack
button, capped at 60 fps. Benchmark: 3 s warm-up, then 60 s on autopilot, every frame
interval timed with the real clock.

| | Godot 4.7.2 (Mobile renderer, Metal) | Unity 6.3 LTS 6000.3.26f1 (URP) |
|---|---|---|
| **iPhone frame time, average** | pending | pending |
| **iPhone frame time, 99th percentile** | pending | pending |
| **iPhone frame time, worst** | pending | pending |
| **iPhone frames over 33 ms** | pending | pending |
| Download size on iPhone (App Store) | 25.3 MB | 24.7 MB |
| Installed size on iPhone | 67.9 MB | 82.4 MB |
| Uploaded IPA | 27 MB | 93 MB |
| Cloud build time | about 3 min on GitHub's free macOS runner | 11.2 min on Unity Build Automation (of 100 free Mac min a month) + 1.5 min upload on GitHub |
| Desktop check (RTX 5070 Ti, 10 s, not the phone) | avg 16.67, p99 16.97, worst 17.26 ms | avg 17.24, p99 30.72, worst 52.86 ms |

Sizes are Apple's own numbers for an iPhone 16 Pro class device (`iPhone17,1`) from App
Store Connect.

## Owner steps

Steps the owner had to do by hand, counted per engine.

Shared by both (once for the whole project):
1. Allow creating the public GitHub repo.
2. Create the App Store Connect API key, save the .p8 and the issuer and team IDs.
3. Accept Apple's updated developer agreement.

Godot only: **1 step**
1. Create the "Bakeoff Godot" app record in App Store Connect.

Unity only: **6 steps**
1. Sign in to Unity Hub (Unity ID, Personal licence), needed to run the editor at all.
2. Create the Unity Cloud project, start the free Build Automation plan, make a service
   account and save its key.
3. Look up and save the Unity organization and project IDs (Unity's API cannot list them).
4. Connect the GitHub repo in the Unity dashboard, with a GitHub access token
   (Unity's API refuses to do this for a service account).
5. Allow creating an Apple Distribution certificate and profile (Unity's builder needs the
   .p12 and profile files; the Godot build signs itself in the cloud with the API key).
6. Create the "Bakeoff Unity" app record in App Store Connect.

## Notes

- Neither engine needed any paid service. Unity Build Automation did not ask for a card.
- Godot's iPhone export, signing and upload run in one GitHub workflow
  (`.github/workflows/godot-ios.yml`); every push to `godot/` makes a new TestFlight build.
- Unity builds start from `python -I scripts/uba.py build`; the IPA then goes to TestFlight
  through `.github/workflows/upload-ipa.yml`. Unity's builder cannot upload to TestFlight
  itself.
- Xcode's own upload step failed its App Store Connect check with an API key; Apple's
  `altool` uploads fine.
- Unity's uploaded IPA is over three times Godot's, but after Apple's thinning the iPhone
  download is almost the same; installed, Unity is 14.5 MB bigger. Why the IPA differs was
  not investigated.
