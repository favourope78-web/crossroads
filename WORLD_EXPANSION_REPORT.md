# World Expansion & Location System — Report

Phase goal: a **scalable world structure** where locations carry NPCs, encounters, objectives,
decisions, abilities, combat and consequences — built on every existing system (CampaignManager,
GameStateManager, DecisionManager, NPCs, AbilityManager, WorldStateManager, objectives, combat,
player/mobile, save/load, content→asset→builder→validation pipeline). Three polished
interconnected prototype locations prove the architecture; the world stays small by design.
No working system was rewritten — the location layer *derives* from the same state.

**Result: 962/962 checks · asset validation clean (162 scene roots) · both compile configs green ·
unlocks/current/visits persist through save v5 unchanged · Android build config verified.**

---

## 1. Locations created (all data, `story_content.json → locations`)

| Location | Kind | Purpose | Unlock rule(s) | Connections |
|---|---|---|---|---|
| **Fracture Hall** (`hall`) | Hub | Exploration/story: the Trode's question, campaign beats, Mara + Sera, the way to everywhere | none — open from the start | annex, tidewell |
| **North Annex** (`annex`) | Combat | The choir beacon objective, the **Choir Warden** (relocated here — the Choir sent it for the beacon), the hidden ember cache, the echo shrine | ANY of `ember_pulse` / `tide_mend` / `stone_ward` (mirrors the scene's energy-seal gate; OR across rules, AND within) | hall |
| **Tidewell Shrine** (`tidewell`) | NPC | Sera's drowned shrine (NEW east room): she relocates here on the tide decision, keepsake objective, confide/talk | `DecisionWas(dec_c1_hall_first_light, tide_clear)` — a decision-gated location | hall |

Location ids deliberately **share the area-id namespace** (`GameState.currentArea`/`unlockAreas`):
the scene's AreaTriggers write the same keys, so physical position and the travel graph can
never desync. Task 6's examples all exist: decision → annex/tidewell, ability → annex + the
hidden `ember_cache_open` interaction (now needs `ember_pulse` **and** the silenced-beacon flag).

## 2. Architecture & scene structure

```
Gameplay/Locations/LocationManager.cs (276 ln)  — pure evaluation: unlock rules (gate
    language), connection-graph travel, first-arrival world changes (once, flag-guarded),
    open-by-design hubs. Owns NO state; derives everything from GameState.
Gameplay/Locations/LocationServices.cs (117 ln) — boot facade (after CampaignServices),
    autosave on arrival/unlock, MapSnapshot() for the UI.
Core/LocationEvents.cs (49 ln)                   — Unlocked/Departed/Arrived(everything the
    scene needs: checkpoint + env profile)/AvailabilityChanged.
UI/MapHUD.cs (161 ln)                            — current / travel-to (buttons) / locked +
    requirement hints, from content data. Collapsible, top-left under StateHUD.
UI/LocationTransitionFader.cs (139 ln)           — Android-suitable transition: 0.2s unscaled
    fade -> teleport to LocationAnchor_<id> -> apply env profile. NO scene loads on the hot
    path (single generated scene, three zones); sceneKey per location keeps multi-scene open.
```

Scene (regenerated, 145→**162 roots**): the Tidewell Shrine room east of the hall (through the
existing east door — floor/walls/columns/truss, the tide-lit `TidewellPool`, `TidewellLamp`),
`AreaTrigger_Tidewell`, three `LocationAnchor_*` travel anchors, a `Sera_Tidewell` relocation
target, the Warden + its wreckage moved into the annex, and two new materials
(`M_Tide_Pool`, `M_Tidewell_Stone`). `Assets/Game/Locations/{FractureHall,NorthAnnex,TidewellShrine}/`
hold one **env-kit prefab** each (sun preset mirroring the content profile —
`Env_Hall/Annex/Tidewell.prefab`); `Assets/Game/Environment/` continues to own the kit +
materials. Recurring characters (Mara, Sera, Warden) reuse their canonical assets/materials —
no visual replacements. `PROJECT_MAP.png` regenerated from the new scene.

Boot order: `GameServices.Init → WorldServices.Init → CampaignServices.Init →
LocationServices.Init` (StoryModeBootstrap + tests). UI: MapHUD + LocationTransitionFader
attached in `GameUIBootstrap`.

## 3. Persistence & restoration (no schema change)

Everything already lived in save v5: `unlockAreas` (unlocks), `currentArea` (current location),
flags (`loc_visited_<id>` visits), `worldStates`/entities/objectives (per-location world).
`LocationManager.Refresh()` re-derives the rest after load — so restart restores the current
location, unlocks and visits with **zero migration** (proven by [70] on a v5 slot written
before the location layer existed… and by every older phase's suite still passing).

## 4. Persistent changes demonstrated

- Silence the beacon in the annex → return to the hall → **Mara's confide dialogue leads with
  "The north went quiet an hour ago"** (new content node, flag-conditioned) — the hub visibly
  remembers what happened elsewhere ([68], [71], and again after restart).
- Sera relocates to the tidewell on the tide decision (`MoveNpc`) / to the annex gate when the
  beacon objective completes — NPC location is global, restored on every return and restart.
- First arrival applies `worldStates: annex=reached / tidewell=lit` exactly once
  (`loc_visited_` guard, [67]); entity/objective state survives any number of travels ([68],[70]).

## 5. Tests (`scripts/decision_system_tests/LocationTests.cs`, [64]–[71], 109 checks)

Content contracts (kinds, symmetric graph, reference + env-profile integrity) · data-driven
unlocking (ability rules per route, decision rule, locked hints) · travel validation (unknown /
locked / not-connected edges, arrival events with checkpoint + env payload) · returning +
first-visit-once semantics · persistent world changes (hall reflects the annex; hidden
ability-gated interaction) · two-player decision divergence (A: annex only; B: annex + tidewell
+ relocated Sera) · save/load restart · the full task-14 vertical flow end-to-end incl. restart.

**Full suite: 962 passed / 0 failed** (story/world/combat/mobile/campaign/locations).

## 6. Validation & compile

- `validate_assets.py` → **VALIDATION PASSED, 0 warnings** — now also checks: locations
  asset↔JSON field parity (deep: unlock rules, conditions, effects, env), connection symmetry,
  npc/encounter/objective reference integrity, kind coverage, scene needles (anchors, tidewell
  trigger, Sera binding), and one guid-valid prefab per location kit.
- `compile_check.sh` → both `ENABLE_LEGACY_INPUT_MANAGER` and `ENABLE_INPUT_SYSTEM` compile
  (16 warnings, all pre-existing-style CS0414).
- Scene binding verified by validation (162 roots; AreaTriggers hall/annex/tidewell;
  NpcRelocator bindings reference Transforms).

## 7. Android development build

`scripts/build_android_apk.sh` + `Assets/Editor/AndroidDevBuild.cs` remain fully configured;
the build-scene GUID (`…005a`, FirstLocation.unity) is unchanged by regeneration. Executing the
APK still needs a Unity/Android machine — same hand-off as the mobile phase (this sandbox has
no Unity editor; the script reports exactly that). Everything the build consumes — scene,
content asset, scripts, metas, GUID registry — is regenerated and validated.

## 8. What a designer does next

Add a location = one entry in `story_content.json` (`locations`): id/name/kind, sceneKey +
checkpoint, unlock rules (same condition language as everything else), connections, who/what
lives there, first-arrival effects, env profile → `gen_story_content.py` (asset) →
`gen_firstlocation_scene.py` if it needs geometry → `validate_assets.py`. No LocationManager
change — proven by the tidewell being 100% data + generated geometry.

## 9. Commit

Feature commit: **`e017141`** — *"World expansion & location system: data-driven locations (LocationManager/LocationServices, LocationEvents, MapHUD, LocationTransitionFader), three prototype locations (hall hub / annex combat with relocated Warden / tide-gated Tidewell Shrine with relocated Sera), area-id-namespace travel graph, first-arrival world changes, ability-gated hidden cache, env profiles + per-location kits, scene regen (162 roots) + validation, LocationTests 64-71; 962/962 checks + validation + both compile configs"*.
