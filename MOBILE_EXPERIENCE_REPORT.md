# Mobile Player Experience System — Build Report

Phase 6 of the CROSSROADS prototype: the **Mobile Player Experience** — one playable mobile
game out of everything already built. Every existing system (player, camera, movement,
interaction, dialogue/EncounterFlow, DecisionManager, GameStateManager, AbilityManager,
WorldStateManager/objectives, combat, save/load) is used **as-is**; all integrations are
additive. No new major gameplay mechanics.

---

## 1. Input system created — `Assets/_Project/Scripts/Gameplay/Input/` (reusable, pure)

| File | What it is |
|---|---|
| `InputBus.cs` | The single input hub. UGUI widgets **produce** (movement, look deltas, button presses); player controller / camera / combat **consume**. Deadzone-filtered analog movement, destructive look-delta consumption, exactly-once press edges, per-button availability gating with a change event, one-place `InputLock` enforcement, zero allocations. Includes `JoystickFilter` (circular deadzone + rim clamp + remap) and `InputTuning` |
| `InputSettings.cs` | Serializable player settings (`lookSensitivity`, `invertLookY`, `cameraDistance`, `cameraSmoothing`, `buttonScale`, `controlOpacity`, `leftHanded`, `audioVolume`, `qualityLevel`, `showTouchControls`) + `InputSettingsStore` (own `player_settings.json` through the same `IJsonSerializer`/`IPathProvider` seams the SaveSystem uses — **never bumps the save schema**, corrupt files fall back to defaults) + pure `SettingsNudge` (step/clamp per setting) + `SettingId` |
| `CameraRigMath.cs` | Pure camera math: orbit offsets, indoor-sensible pitch clamps, collision policy (*pull in instantly, ease out speed-limited*), indoor height-bias scaling |
| `CombatPresence.cs` | "Is a fight live?" predicate over the enemy registry (story-gated/dormant and defeated enemies don't count) — reused by the rig and the tests |

Touch widgets (`UI/`): `VirtualJoystick.cs` (fixed zone + floating knob, analog, mouse-testable),
`TouchLookPad.cs` (transparent look surface under every button), `MobileControlsUI.cs` (the rig:
layout, settings application, combat/interact gating, 4 Hz presence poll), `PauseMenuUI.cs`
(pause/settings overlay). All produce into the InputBus; none contain gameplay logic.

**Controls delivered (task 2):** left virtual joystick · right look pad · contextual INTERACT
(label follows the target, e.g. "Brace the Barricade") · ATK + DODGE (combat-gated) · ability
buttons (POWERS sheet, owned-only) · `II` pause. **Configuration (task 3):** scale, opacity,
left-handed mirror, hide-touch-controls; layout zones are disjoint by construction (joystick
bottom-left under INTERACT, action cluster bottom-right, POWERS top-right, look pad underneath
everything).

## 2. Camera improvements (task 4) — `ThirdPersonCameraController` upgraded in place

Smooth follow + smooth rotation (kept) → full **orbit rig**: yaw/pitch from look input with
sensitivity + invert; **sphere-cast collision avoidance** (snap in behind walls/annex beams,
speed-limited ease-out, 1.1 m framing floor); **distance setting** (2.6–6.0 m via pause menu);
**indoor behaviour** (headroom probe scales the height bias so framing stays on the player);
two probes/frame, zero allocations. Desktop mouse fallback when the bus is idle. Movement
became **camera-relative** in `PlayerPrototypeController` (joystick-up always means "away from
camera"; world-axis fallback headless) — a 12-line additive change that had to come with the
free camera.

## 3. HUD/UI created (tasks 5–9)

- **Interaction→buttons (5):** `PlayerInteraction`'s `InteractPromptEvent` now drives
  `InputBus.SetAvailable(Interact, …)` — the interaction system itself decides when a mobile
  action button makes sense.
- **Ability UI (6):** `AbilityHUD` hides locked lines entirely — only owned abilities show
  (blocked-but-owned lines stay visible and say why). Event-driven refresh unchanged.
- **Combat controls (7):** ATK/DODGE moved from `CombatHUD` into the rig and appear **only**
  while `CombatPresence` sees a live enemy; they vanish after the fight.
- **Pause/settings (8):** `PauseMenuUI` — Resume, sensitivity / camera-distance / volume /
  quality steppers (pure `SettingsNudge`), SAVE & CLOSE (settings file + progress save).
  Pause freezes `Time.timeScale`; settings live-persist and survive an app kill.
- **HUD (9):** existing health/status (CombatHUD), objective tracker (ObjectiveHUD),
  contextual prompt (InteractionHUD), ability sheet, toasts — now sitting above the rig in one
  canvas with the rig's CanvasGroup for opacity.
- **Scaling (10):** existing `CanvasScaler` (1920×1080 reference, width/height match 0.5) +
  `SafeAreaFitter` (notches/rounded corners, re-applies on orientation/resize) + the rig scales
  from settings — verified across aspect ratios by anchors (no absolute pixel layout crosses
  safe-area boundaries).

## 4. Mobile performance notes (task 11 + "issues discovered")

No per-frame allocations anywhere in the new paths: InputBus is static state; widgets only do
vector math in event handlers; rig polls combat presence at **4 Hz** (not per frame); camera is
2 sphere casts + SmoothDamp; pause menu is event-driven. Findings: (a) *discovered* pre-existing
`PlayerInteraction.RefreshCache()` allocates one list per second — accepted (1 small GC alloc/s,
bounded, keeps the code simple); documented rather than churned. (b) `AbilityHUD` cooldown text
already refreshes on an interval, not per frame — kept. (c) The 30/60 fps quality mapping caps
battery burn on Low. (d) `showTouchControls: Never` fully disables the rig for gamepad/desktop
playtesting.

## 5. Verification (tasks 12–14)

| Check | Result |
|---|---|
| Headless suite (incl. new `MobileExperienceTests` **[50]–[56]**) | **765 / 765 passed** (689 prior + 76 new) |
| New test coverage | joystick deadzone/analog/clamp/normalization · look accumulate+consume-once · press-edge exactly-once · availability gating · InputLock zeroes movement/look/buttons · settings defaults/nudge clamps/file roundtrip/corrupt-file fallback/hostile-file clamping · camera orbit math, pitch clamps, collision pull-in/ease-out/min-floor, indoor bias · combat-control gating incl. dormant/defeated/multi-enemy · ability ownership filter (0 rows → 1 owned → blocked-stays-visible) · **full gameplay loop** [55]/[56]: launch → load → decide → objective → ability (real manager) → fight → world+NPC reaction → save → restart → **everything restored** (hp, warden state, wreckage, objective, decision, sera, settings file) |
| Compile checks | `scripts/compile_check.sh` clean in **both** `ENABLE_LEGACY_INPUT_MANAGER` and `ENABLE_INPUT_SYSTEM` |
| Asset validation | `scripts/validate_assets.py` — **PASSED (0 warnings)** (GUID integrity incl. 9 new script metas + `Gameplay/Input`/`Editor` folder metas; asset↔JSON↔builder parity; scene sanity + reference-type checks) |
| Scene binding | scene unchanged by design (all new UI is runtime-built by the already-bound `GameUIBootstrap`; camera keeps its referenced component) — regenerated + revalidated to prove idempotence |

Re-run: `cd scripts/decision_system_tests && mcs -langversion:latest -define:ENABLE_LEGACY_INPUT_MANAGER -out:FlowTests.exe TestJson.cs FlowTests.cs WorldTests.cs CombatTests.cs MobileExperienceTests.cs ../unity-stub/UnityStub.cs ../../Assets/_Project/Scripts/Core/*.cs ../../Assets/_Project/Scripts/Narrative/*.cs ../../Assets/_Project/Scripts/Narrative/Content/*.cs ../../Assets/_Project/Scripts/Narrative/Abilities/*.cs ../../Assets/_Project/Scripts/Gameplay/*.cs ../../Assets/_Project/Scripts/Gameplay/Interaction/*.cs ../../Assets/_Project/Scripts/Gameplay/Abilities/*.cs ../../Assets/_Project/Scripts/Gameplay/WorldState/*.cs ../../Assets/_Project/Scripts/Gameplay/World/*.cs ../../Assets/_Project/Scripts/Gameplay/Combat/*.cs ../../Assets/_Project/Scripts/Gameplay/Input/*.cs ../../Assets/_Project/Scripts/Gameplay/NPC/*.cs ../../Assets/_Project/Scripts/UI/*.cs ../../Assets/Game/Scripts/FirstLocationBootstrap.cs ../../Assets/Game/Scripts/ThirdPersonCameraController.cs && mono FlowTests.exe`

## 6. Android build result (task 15)

**Honest status: configured and scripted — not executed in this sandbox** (no Unity editor,
Android SDK or device available here; same hand-off pattern as earlier phases' editor
verification). Delivered:

- `ProjectSettings/ProjectSettings.asset` (seeded: API 24+, ARM64+ARMv7, IL2CPP, landscape,
  `com.favourope78.crossroads`, mobile quality tiers) + `EditorBuildSettings.asset` (scene pinned)
- `Assets/Editor/AndroidDevBuild.cs` — authoritative configure-then-build (re-applies every
  setting via API; development build with profiler connection) → `Builds/CrossroadsDev.apk`
- `scripts/build_android_apk.sh` — local 1-command wrapper (`adb install -r` hint included)
- `.github/workflows/android-apk.yml` — game-ci builder → APK artifact on every push (needs the
  one-time free `UNITY_LICENSE` repo secret)
- `ANDROID_BUILD.md` — both paths + a 10-point on-device launch-verification checklist covering
  the entire gameplay loop

## 7. Notes & fixes found while "checking well"

1. **Namespace collision (real compile find).** Adding `Crossroads.Gameplay.Input` made bare
   `Input.` references inside `Crossroads.Gameplay` files resolve to the new namespace —
   keyboard fallbacks broke. Fixed by qualifying `UnityEngine.Input` in the three consumers;
   noted as a standing rule for any Gameplay file that touches Unity input.
2. **Pause must never strand a frozen game.** `PauseMenuUI.OnDestroy` restores `timeScale` —
   a scene change mid-pause can otherwise ship a permanently-frozen build.
3. **Settings live-persist on every nudge** (not just on close): an Android app kill mid-session
   keeps the player's sensitivity/camera choices. Corrupt/hand-edited settings clamp on load and
   never block launch (tested).
4. **Camera-relative movement was forced by the free camera.** With an orbit rig, world-axis
   joystick movement fights the player's expectations; the additive camera-basis mapping keeps
   the prototype controller intact for the headless/desktop paths.
5. **UI availability is policy, visibility is presentation.** The rig listens to ONE signal
   (`InputBus` availability + `CombatPresence`) and owns all touch buttons; HUDs stayed
   presentation-only — which is why [53]'s gating tests needed no UI instantiation at all.

## 8. Commit

Feature commit: **`1707982`** — *"Mobile player experience: Gameplay/Input (InputBus, JoystickFilter, InputSettings+store, CameraRigMath, CombatPresence), touch rig (VirtualJoystick, TouchLookPad, MobileControlsUI, PauseMenuUI with live-persisting settings), orbit camera with collision avoidance + indoor bias, camera-relative movement, combat-gated ATK/DODGE + interaction-driven INTERACT availability, owned-only ability sheet, Android dev-APK config + build script + CI workflow; 765/765 tests + validation + both compile configs"* (41 files, +2250/−59).
