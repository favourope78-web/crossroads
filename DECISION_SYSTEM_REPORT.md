# DECISION_SYSTEM_REPORT.md — First Playable Interaction & Decision System
### CROSSROADS Foundation (Phase 2-native systems, built on the Phase 0–1 prototypes)

| | |
|---|---|
| **Date** | 2026-09-03 |
| **Branch** | `feat/decision-system` (merged to `dev` + `main`) |
| **Scene** | `Assets/Scenes/Prototype/FirstLocation.unity` (unchanged environment + player) |
| **Character** | Ari prototype v1 — **unchanged** (only a movement input-lock gate was added) |
| **Environment** | Fracture Hall kit — **unchanged geometry**; story additions are additive objects |
| **Design refs** | `GAME_DESIGN.md` §2.1/§4 (decision system), §5.2 (world state), §8.3 (interaction), §9.1 (Mara), §12 (save), §13.4 (services) |

---

## 1. What was built (maps 1:1 to the task list)

| # | Task | Where |
|---|------|-------|
| 1 | Reusable interaction system | `PlayerInteraction` + `Interactable` base (proximity scan, nearest-wins, priority ties) — doors/holo-panels keep working |
| 2 | Clear interaction prompt when close | Mobile **[INTERACT]** button bottom-left (`InteractionHUD`), label per target ("TALK TO MARA") |
| 3 | First story encounter from GAME_DESIGN | **"The First Light"** — Mara in the Fracture Hall, the awakening beat (§11.2 P→C1L1); content in `CL_C1_StoryContent.asset` + `StoryContentBuilder` |
| 4 | Short dialogue/story event | `EncounterFlow` + `DialogueUI` (typewriter, speaker chip, tap-to-advance) |
| 5 | 2–3 meaningful choices | Exactly 3, one per affinity line (Ember / Tide / Stone) |
| 6 | Choice stored persistently | `ResolvedDecisionEntry` in `GameState.decisions`, autosaved at resolution |
| 7 | Choices produce different consequences | Flags + affinity + Mara bond + **world state** + **spawnable consequence objects** + different aftermath dialogue + different re-talk dialogue (all condition-gated) |
| 8 | `DecisionManager` | Register / Store / Check (`IsResolved`, `ResolvedOption`) / Expose (`AllDecisions`, `VisibleOptions`, `Get`) |
| 9 | Branching test A→A, B→B, C→C | Headless suite: 3 flows, each asserts its own state (see §4) |
| 10 | Data-driven, no code changes per encounter | Content = `StoryContentData` POCOs via ScriptableObject asset; adding an encounter = one asset entry (recipe in §5) |
| 11 | Decisions persist after restart | JSON at `persistentDataPath/crossroads/save_slot_0.json`; boot reloads and replays world state |
| 12 | Android/mobile UI | Runtime-built uGUI: SafeArea, ≥88dp targets, big choice cards, landscape layout per §8.1 |

---

## 2. Architecture (design §13.4 service architecture, kept headless-testable)

```
Assets/_Project/Scripts/
  Core/       Crossroads.Core        (no deps; pure C# except 2 tiny Unity adapters)
    GameState / StateMutator         - single authoritative state; ALL writes via mutator
    EventBus + StoryEvents           - typed pub/sub (decision/dialogue/state/save events)
    SaveSystem, SaveData             - JSON slots, atomic .tmp->replace, schemaVersion, autosave mirror
    InputLock                        - global input gate while dialogue/choices are up
    Point3, ProximitySelector        - engine-free proximity rules (unit-tested)
    AppServices / StoryLog           - service locator + injectable logging
  Narrative/  Crossroads.Narrative   (refs Core)
    Content/  StoryContentLibraryAsset (ScriptableObject, authorable content)
              StoryContentBuilder      (code-built fallback + test content)
    DecisionManager                   - register / present / resolve / expose
    EncounterFlow                     - dialogue+decision state machine (runner)
    ConditionEvaluator / EffectApplier- §4.2 whitelists
    GameServices                      - typed facade: State, Decisions, Encounters, Save
  Gameplay/   Crossroads.Gameplay   (refs Core, Narrative)
    Interaction/ Interactable, DoorInteractable (moved, GUIDs preserved)
    Interaction/ PlayerInteraction, StoryEncounterNPC (Mara)
    WorldState/ StoryWorldState       - replays persisted consequence objects at boot
    StoryModeBootstrap                - boots services, loads save, autosaves on app pause
  UI/         Crossroads.UI         (refs Core, Narrative, Gameplay)
    GameUIBootstrap (scene), RuntimeMenuFactory, DialogueUI, InteractionHUD,
    StateHUD (affinity meters + DEV reset), ToastUI (post-choice feedback), SafeAreaFitter
```

**Event flow of the first encounter**

```
tap INTERACT → StoryEncounterNPC.OnInteract → EncounterFlow.Run("c1_hall_first_light")
  → DialogueLineEvent(×3)      [Mara intro; UI typewriter; tap = Advance]
  → DecisionPromptEvent        [3 choice cards; timer if timeLimit>0 (D2)]
  → SelectChoice("ember_reach" …)
      → DecisionManager.Resolve: EffectApplier → StateMutator writes:
          flag c1_hall_drive=ember · Ember +10 · bond mara +5 ·
          world hall=ember · entity ember_marker=on · codex ×2 · echoes +15
      → ResolvedDecisionEntry recorded → autosave (atomic JSON)
  → afterText line (narration) → Advance → condition-gated aftermath line
      (after_ember / after_tide / after_stone — depends on the stored flag)
  → DialogueEndedEvent → input unlock
restart: StoryModeBootstrap loads save → StoryWorldState activates the markers the
  saved decision produced → re-talk shows the *other* opener (DecisionWas condition)
  and never re-presents the resolved decision.
```

**Consequences per choice (branching test A/B/C)** — all persisted, all re-evaluated after restart:

| | A — `ember_reach` | B — `tide_clear` | C — `stone_still` |
|---|---|---|---|
| Flag `c1_hall_drive` | ember | tide | stone |
| Affinity | Ember 10 | Tide 10 | Stone 10 |
| Mara bond | +5 | +10 | +3 |
| World state `hall` | ember | tide | stone |
| Spawned/active | Ember marker (red) | Tide marker (teal) + **the twins** at the east door | Stone marker (ochre) |
| Aftermath line | "columns burned red…" | "twins clear…" | "third time…" |
| Echoes | 15 | 20 | 15 |
| Re-talk opener | different per stored choice (`DecisionWas`) | same | same |

Every one of these keys/flags is readable by future content via `DecisionConditionData`
(e.g. a Chapter 2 node can gate on `FlagIs c1_hall_drive ember` or `BondAtLeast mara 10`).

---

## 3. Save format (design §12.1, JSON)

`Application.persistentDataPath/crossroads/save_slot_0.json` (+ `autosave.json` mirror):

```json
{
  "schemaVersion": 1,
  "meta": { "slotName": "Ari - FirstLocation", "timestamp": "…", "playtimeSec": 0 },
  "scene": { "sceneKey": "FirstLocation", "checkpointId": "hall_spawn" },
  "gameState": {
    "flags": [{ "key": "c1_hall_drive", "value": "ember" }],
    "worldStates": [{ "key": "hall", "value": "ember" }],
    "entities": [{ "key": "ember_marker", "value": true }],
    "bonds": [{ "key": "mara", "value": 5 }],
    "decisions": [{ "decisionId": "dec_c1_hall_first_light", "optionId": "ember_reach", "summary": "…", "resolvedAt": "…" }],
    "codex": ["c1_echo_ember", "c1_echo_first_light"],
    "ember": 10, "tide": 0, "stone": 0, "hollow": 0, "echoBank": 15
  }
}
```

- **Atomic writes**: `.tmp` → `File.Replace`; corrupt/old-schema files are refused gracefully (migration table arrives with v2).
- **Autosave triggers**: every decision resolution; `OnApplicationPause(true)` / `OnApplicationFocus(false)` (design §12.3 mobile-lifecycle rule).
- **DEV helpers**: `StoryModeBootstrap.devClearSaveOnStart` checkbox and the ✕ RESET DECISIONS button (editor/dev builds only) to replay the encounter.

---

## 4. Verification

### ✅ Static (run: `python3 scripts/validate_assets.py`)
- 237+ GUID cross-references: **0 unresolved**; registry has no duplicate GUIDs.
- `CL_C1_StoryContent.asset` parses as YAML and matches `scripts/story_content.json` field-for-field.
- Every content string in the JSON also exists in the C# fallback builder (no drift).
- Scene contains Mara NPC, 3 consequence markers (+bystanders), `StoryWorldState` bindings, both bootstrappers, `SceneRoots`, 114 root objects, inactive-by-default markers.

### ✅ Headless flow tests — **115 passed / 0 failed** (`scripts/decision_system_tests/`, mcs+mono)
```
walk → prompt appears/priority ties/disappears        [proximity rules]
Flow A/B/C: dialogue → 3 choices → select → state A/B/C asserted
  (affinity, bond, world state, entities, codex, echoes, aftermath line)
autosave on resolution → file on disk
RESTART: new services load from disk → decision/flags/affinity/bond/entities restored
  → re-talk: no re-prompt, variant opener, matching aftermath, no double record
DecisionManager: register, condition-gating (AffinityAtLeast), exposed history, D2 timeout resolve
SaveSystem: atomic write, round-trip, corrupt-file tolerance, delete
Content contracts: unknown encounter = clean no-op
```
Run it again: `cd scripts/decision_system_tests && mcs -langversion:latest -out:FlowTests.exe TestJson.cs FlowTests.cs ../unity-stub/UnityStub.cs ../../Assets/_Project/Scripts/Core/*.cs ../../Assets/_Project/Scripts/Narrative/*.cs ../../Assets/_Project/Scripts/Narrative/Content/*.cs && mono FlowTests.exe`

### ✅ Full C# compile check (both input modes)
All assemblies compile via mcs against the dev-only Unity stub (`scripts/unity-stub/`), once with `ENABLE_LEGACY_INPUT_MANAGER` and once with `ENABLE_INPUT_SYSTEM` — both clean.

### ⏳ Runtime verification — pending Unity Editor (same hand-off as character/environment)
One Play click in the editor (run menu *CROSSROADS ▸ Prototype ▸ Build Ari Prefab & Test Scene* once first):
1. Log `[CROSSROADS] Game UI ready` + `[CROSSROADS] PlayerInteraction ready on Ari`; no console errors.
2. Walk (WASD) from spawn toward Mara until `TALK TO MARA` appears bottom-left; tap it (or E).
3. Typewriter dialogue → tap through 3 lines → 3 choice cards appear; choose one.
4. Toast: `locked in ◆ Ember +10 (10) … saved ✓`; affinity strip in the top-left updates; the matching marker lights up (and the twins appear on the Tide path).
5. **Stop Play → Play again** → marker still lit, Mara's opener changed, HUD shows `decisions 1/1` — that is the persistence proof.
6. Tap Mara again → no re-prompt; the condition-gated aftermath line plays.

---

## 5. Adding a future encounter (no core changes)

1. Create `Assets/_Project/Data/Decisions/<Name>.asset` (type `StoryContentLibraryAsset`) **or** append to the existing library asset in the inspector:
   - `decisions`: one `DecisionNodeData` (id, promptText, options with conditions/effects per the §4.2 whitelist).
   - `graphs`: one `DialogueGraphData` (nodes with `nextId`/`branchPrefix`/`decisionId`/conditions).
   - `encounters`: `{id, npcName, graphId, startNodeId}`.
2. Drop/prepend a `StoryEncounterNPC` on any GameObject and set **encounterId** = the new id.
3. Optional: bind consequence objects in `StoryWorldState.entities` (key = your `SpawnEntity` effect key).
4. Conditions/effects are a fixed whitelist — a *new type* is a one-line enum entry + one switch case (by design §4.2), everything else stays data.
5. Keep the code-built mirror in `StoryContentBuilder` in sync if you want the runtime fallback/inspector parity — or run `scripts/gen_story_content.py` after editing `scripts/story_content.json` (validated by `validate_assets.py`).

## 6. Notes & fixes found while "checking well"

- **Fixed a latent v1 scene bug**: `gen_firstlocation_scene.py` emitted `m_LocalRotation` as identity for every object, so 39 pieces placed with yaw (walls, doors, glazing) would have rendered unrotated in Unity (never caught because the look-dev renders were Blender-side). The emitter now computes `q = qy⊗qx⊗qz` (verified: unit quaternions, yaw 90 → 0/0.707/0/0.707).
- **JsonUtility limitation** (design said `Dictionary`): dictionaries are not serializable by `JsonUtility`, so `flags/vars/bonds/entities/worldStates` are entry-lists with dictionary-like helpers — same shape, JSON stays readable/diffable.
- **Content container**: decisions/graphs/encounters live in one `StoryContentLibrary` asset (id-keyed) instead of one asset per node — same authoring model, fewer hand-authored cross-refs; the per-decision/per-graph asset classes are kept for later split.
- **D2 (timed) choices**: data + runner + UI countdown fully implemented (timeout option auto-resolve); the first encounter is intentionally untimed (D1) per §4.1.
- **`dev` branch was stale** (sat on the initial commit, missing both prototype phases) — recreated feature branch off `main` and brought `dev` up to date when merging.
- Movement: `PlayerPrototypeController` gained only an `InputLock` gate (dialogue/decision lock per §4.5); no locomotion changes. The prototype's IMGUI `InteractInput` was superseded by the uGUI interaction system.
