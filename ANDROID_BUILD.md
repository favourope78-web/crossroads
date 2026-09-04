# Android Development APK — Build & Launch Verification (task 15)

The sandbox that develops this repo has **no Unity editor, Android SDK or device** — the APK
build therefore ships as *fully configured + scripted*, to run on a dev machine (2 commands) or
on GitHub Actions (zero local install). This is the same hand-off pattern the earlier phases
used for editor verification ("runtime verification pending a machine with the editor").

## What is already in the repo

| Piece | Path | What it does |
|---|---|---|
| Android player settings (seed) | `ProjectSettings/ProjectSettings.asset` | API 24+, ARM64 + ARMv7, IL2CPP, landscape-only, `com.favourope78.crossroads`, quality tiers Low/Balanced with mobile budgets (no vsync stall, shadow distance 15/25 m) |
| Scene list | `ProjectSettings/EditorBuildSettings.asset` | `FirstLocation.unity` (GUID-pinned to the generator registry) |
| Authoritative build script | `Assets/Editor/AndroidDevBuild.cs` | Re-applies **every** Android setting programmatically, then builds a **development** APK (debuggable, profiler-capable) to `Builds/CrossroadsDev.apk`. Menu: *Build → CROSSROADS Dev APK (Android)* |
| Local wrapper | `scripts/build_android_apk.sh` | Finds Unity 6, runs the batchmode build, prints adb install hint |
| CI path (no local Unity) | `.github/workflows/android-apk.yml` | game-ci `unity-builder@v4` → artifact `CrossroadsDev-APK` on every push. Needs the repo secret `UNITY_LICENSE` (free personal license, one-time — see header comment) |

## Option A — build on your machine

```bash
# once: Unity Hub -> Installs -> Add modules -> Android Build Support (SDK/NDK/OpenJDK)
bash scripts/build_android_apk.sh            # auto-finds Unity 6
# or:  bash scripts/build_android_apk.sh /full/path/to/Unity
adb install -r Builds/CrossroadsDev.apk
```

## Option B — build on GitHub Actions (no local install)

1. Get a free personal Unity license file (game-ci activation docs).
2. Repo → Settings → Secrets and variables → Actions → `UNITY_LICENSE` = file contents.
3. Push to `main` → Actions → the run's artifact **CrossroadsDev-APK** is the APK.

## Launch verification checklist (run on device once)

Expected end-to-end (the full mobile loop this phase wired together):

1. **Splash → hall**: Ari spawns at the south entrance; hall + annex + west transept render; 60 fps target (Balanced), 30 floor (Low).
2. **Controls appear**: left virtual joystick + right look pad + `II` pause. No ATTACK/DODGE yet (no fight).
3. **Move/look**: joystick walks (analog, camera-relative); look pad orbits the camera; collision keeps it out of walls/annex beams.
4. **Interact**: approach Mara → contextual INTERACT (label "Talk to Mara") appears; dialogue + first-light decision (timer for stone path) work; Ari is input-locked during dialogue.
5. **Objective**: HUD offers "Silence the Choir Beacon" (or path objective); hunt objective appears after the decision.
6. **Ability**: POWERS sheet shows only the owned line; button fires pulse + VFX; cooldowns tick.
7. **Combat**: entering the west transept activates the warden → ATK/DODGE buttons appear; hit-flash, enemy bar/state, hp bar + status chips react; dodge grants the guard window; defeat sinks the warden, spawns wreckage, completes the objective; Sera's line changes to "Shieldmate".
8. **Defeat safety**: letting the warden win applies consequences, respawns Ari at full hp — **the save survives**.
9. **Pause/settings**: `II` freezes the world; sensitivity/camera distance/volume/quality steppers apply live and persist (`player_settings.json`); SAVE & CLOSE also writes the progress save.
10. **Restart the app**: decision, objectives, world (wreckage), Sera, player hp, and settings all restore.

Report the device result back (or paste `adb logcat | grep CROSSROADS`) and this file will be
updated with the verified device/OS line.
