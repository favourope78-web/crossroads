# WORLD_OBJECTIVE_REPORT.md — World State & Mission/Objective System
### CROSSROADS Foundation (Phase 2e systems, built on the Phase 2a–2d systems)

| | |
|---|---|
| **Date** | 2026-09-04 |
| **Branch** | `feat/world-objectives` (on top of `main` @ `b198a20` power/ability system) |
| **Scene** | `Assets/Scenes/Prototype/FirstLocation.unity` (environment, Ari, NPCs **unchanged**; world additions additive) |
| **Design refs** | `GAME_DESIGN.md` §4.2/§4.3 (data-driven rules + single write path), §5.2 (the city remembers), §9.2 (NPC fate states), §12 (save), §13.4 (services); `DEVELOPMENT_PLAN.md` M2 systems core |
| **This phase** | 13/13 requirements of the **World State & Mission/Objective System** task implemented; **566/566** headless checks (132 new); compile clean in both input defines; static validation 0 warnings; save schema v3→v4 with migration |

---

## 1. Systems created / changed

### Created — `Assets/_Project/Scripts/Gameplay/World/` (the reusable system)

| File | Role |
|------|------|
| `ObjectiveSystem.cs` | `ObjectiveManager` (pure C#): data-driven mission runtime. Lifecycle `Hidden → Available → Active → Completed / Failed / Cancelled`, persisted through `StateMutator` (single write path). Evaluates **only on state events** (no per-frame work). Consequences/failure-consequences apply through `EffectApplier`, so a completed mission can do anything a decision can. Also `ObjectiveView` (UI-agnostic checklist/counter snapshot) |
| `WorldStateSystem.cs` | The world-state façade (pure C#): **open/closed areas** (unlock + re-seal), **changed objects** (persisted entity toggles), **NPC locations** (persisted location keys), **story flags**, **completed objectives**, **unlocked interactions** (condition-gated registry persisted per player — includes ability-dependent rows), world variants. `SummaryLines()`/`Describe()` for HUD & tests |
| `WorldActionInteractable.cs` | The reusable scene-side **world action**: availability = data conditions (any state, incl. `AbilityOwned`), use = data effects through `EffectApplier`, optional repeat counter (`useCountVar`/`maxUses`), optional consume-on-spent (`consumeEntityKey`), locked/spent notices. This is how objectives get completed by playing |
| `NpcRelocator.cs` | Applies **persisted NPC locations** to the scene at load + live on `NpcRelocatedEvent`; delegates the move to `NpcAgent.RelocateTo` (which pins the routine so the NPC stays) |
| `WorldServices.cs` | Owns the two managers over the live `GameServices` state (sits in Gameplay because the systems use the Narrative condition/effect whitelists — `GameServices` itself is untouched, avoiding an asmdef cycle). Wires the autosave hooks (§12.3) for objective/world changes and re-inits on run reset |

Supporting new files: `Core/WorldEvents.cs` (`ObjectivePhase` + `ObjectiveChangedEvent`, `AreaClosed/ReopenedEvent`, `NpcRelocatedEvent`, `InteractionUnlockedEvent`, `VarChangedEvent`), `UI/ObjectiveHUD.cs` (mobile objective tracker). Tests: `scripts/decision_system_tests/WorldTests.cs`.

### Changed (additive only — no working system rewritten)

| System | Change |
|--------|--------|
| `GameState` / `StateMutator` | +4 persisted collections (`objectives`, `npcLocations`, `interactionUnlocks`, `closedAreas`) with dictionary-style read helpers; +write paths that publish the new events; `SetVar` now publishes `VarChangedEvent` (objective counters react without polling) |
| `SaveData`/`SaveSystem` | Schema **v3 → v4**; new collections normalized on load; older files upgraded in memory (v3 file with no mission state loads + live-evaluates, tested) |
| `ContentData` | +`ObjectiveDefinitionData` (id, title, description, type, offer/complete/fail conditions, counter, steps, consequences, failure consequences, follow-ups, notices), `ObjectiveStepData`, `WorldInteractionData`, `StoryContentData.objectives/worldInteractions`; +condition types `ObjectiveActive/Completed/Failed`, `WorldStateIs`; +effects `MoveNpc`, `CloseArea`, `ReopenArea`, `UnlockInteraction` |
| `ConditionEvaluator`/`EffectApplier`/`EffectNotices` | New whitelist cases (incl. the `MoveNpc` notice: "Sera moves to annex gate") |
| `NpcAgent` | +`RelocateTo(point)` (moves + pins routine); subscribes `ObjectiveChangedEvent`/`WorldStateChangedEvent`/`AreaClosedEvent` so NPCs re-resolve when missions/world change (objective-driven fate states react live) |
| `AreaGate` | `Reapply` also respects re-sealed areas (`closedAreas`) — a gate stays shut after `CloseArea` even if it was unlocked before |
| `StoryModeBootstrap` / `GameUIBootstrap` | Boot `WorldServices.Init()` after `GameServices.Init`; attach the `ObjectiveHUD` |
| `StoryContentBuilder` + `story_content.json` + generators | New content (below); `gen_story_content.py` serializes objectives/worldInteractions into the asset + writes the new script/folder metas; `gen_firstlocation_scene.py` emits the new scene objects; `validate_assets.py` cross-checks all of it; `compile_check.sh` includes `Gameplay/World/` |

## 2. Objectives implemented (6, all data)

| ID | Type | Offered by | Completes by | Notable |
|----|------|-----------|--------------|---------|
| `obj_ember_beacon` — *Silence the Choir Beacon* | Main | Decision A (`ember_reach`) | **Ability-gated** world action (`ember_pulse`) | Consequences: annex variant `quiet`, ember cache spawns, choir rep −10, folk +5, sera bond +4, **Sera relocates** to `annex_gate`, +10 echoes → follow-up |
| `obj_ember_cache` — *Claim the Ember Cache* | Side | `ObjectiveCompleted(beacon)` | one-shot world action | Grants `ember_core` + echoes |
| `obj_tide_keepsake` — *The Twins' Keepsake* | Main | Decision B (`tide_clear`) | 2-step checklist (find crate → **return item to the twins**) | Twins swap (anxious despawn / calm spawn), hall variant `twins_blessed`, sera +8 → follow-up |
| `obj_tide_report` — *Tell Mara What the Light Did* | Side | `ObjectiveCompleted(keepsake)` | **a dialogue decision** (2 options, both complete it, different effects) | Mara gains the "report" interaction only after the keepsake objective |
| `obj_stone_barricade` — *Steady the North Barricade* | **Crisis (failable)** | Decision C (`stone_still`) | counter `brace_count` 0/2 (or one-shot ability wedge with `stone_ward`) | **Fails** if you seal your echo at the shrine: barricade falls, rubble spawns, wardens −6, **annex re-sealed** → recovery |
| `obj_stone_rebuild` — *Clear the Fallen Barricade* | Recovery | `ObjectiveFailed(barricade)` | counter `rubble_count` 0/2 (world-state-gated action: only exists when `hall=barricade_fell`) | Repairs rep + **reopens the annex** |

## 3. Decision branches demonstrated (acceptance criteria)

```
Decision A (ember_reach)          Decision B (tide_clear)          Decision C (stone_still)
→ obj_ember_beacon (Active)       → obj_tide_keepsake (Active)     → obj_stone_barricade (Active)
→ NPC: Sera "Watchful"            → NPC: Sera "Grateful"           → NPC: Sera "Intrigued"
→ beacon answers ONLY ember       → crate exists ONLY on tide      → barricade braces ONLY on stone
→ complete → annex=quiet,         → complete → twins swap,         → brace 2/2 → held, Sera "Steadied"
   cache spawns, SERA MOVES          hall=twins_blessed,            → OR seal echo at shrine → FAILED:
   to the annex gate (persisted),    follow-up via DIALOGUE with      rubble, annex sealed,
   Sera "Vanguard"                   Mara ("tell all" vs "keep        recovery objective → clear
→ follow-up: ember cache            light" both complete,             0/2 → annex reopens
                                     different bonds)                 → hall=passage_cleared
```

Tests prove: player A has *Claim the Ember Cache* while player B never can; player B has *Tell Mara What the Light Did* while player A never can; player C sees *Clear the Fallen Barricade*; the three players' interaction-unlock sets, Sera titles and hall variants all differ.

## 4. World-state changes tracked (task 2 coverage)

- **Open/closed areas**: `unlockAreas` + new `closedAreas` (stone failure seals the annex; recovery reopens; `AreaGate` respects it) — persisted + events.
- **Changed objects**: entity toggles — beacon (consumed), ember cache (spawned), crate (consumed), barricade (falls), rubble (spawns), twins swap — all replayed by `StoryWorldState` on load.
- **NPC locations/states**: `npcLocations` (`sera → annex_gate` via the `MoveNpc` effect, applied by `NpcRelocator`, pinned routine) + objective-driven fate states (Sera Vanguard/Steadied, Mara Heartened) that change title, mood line and behaviour numbers.
- **Story flags / world variants**: `annex=quiet`, `hall=twins_blessed / barricade_held / barricade_fell / passage_cleared`.
- **Completed objectives**: objective phases persisted (v4 save).
- **Unlocked interactions**: 7-row condition-gated registry persisted per player (`choir_beacon_channel` only for ember carriers, etc.).
- **Ability-dependent interactions**: the beacon channel requires `ember_pulse` (sealed players lose it again), the ward-stone wedge requires `stone_ward` — both feed the unlock registry.

## 5. The playable sequence (FirstLocation)

Explore the hall → Mara notices you (proximity + INTERACT) → **The First Light** decision → *your path's objective appears* (ObjectiveHUD top-right + toast) → walk to the path's object (beacon in the annex via the energy seal / crate by the east columns / barricade at the north passage) → complete it through world actions (ability-gated for ember, item delivery for tide, bracing for stone) → **the world changes** (cache spawns / twins calm / barricade holds or falls) → **NPCs react** (Sera's title, mood and behaviour change; Mara gains a new interaction on the tide path) → **a follow-up objective unlocks** and can be completed — and everything survives a restart.

## 6. Verification

| Check | Result |
|-------|--------|
| Headless test suite (`FlowTests.exe`, incl. new `WorldTests`) | **566 / 566 passed** (432 prior + 131 new + 3 updated NPC-contract checks; new sections [30]–[39]) |
| New test coverage | objective unlocking by decision · completion + consequences · follow-up chains · **failure** (shrine seal topples the barricade) + recovery · decision-dependent objectives (all three paths) · **ability-dependent** interactions (lock, use, re-lock after sealing) · NPC reactions (titles, interactions, behaviour numbers) · world-state API (areas/objects/npc locations/flags/unlocks/events incl. idempotency) · **save/load persistence** (phases, checklist progress, entities, variants, npc locations, unlock registry, no re-applied consequences, continue-after-restart) · different-paths acceptance · v3→v4 save migration |
| Compile checks | `scripts/compile_check.sh` clean in **both** `ENABLE_LEGACY_INPUT_MANAGER` and `ENABLE_INPUT_SYSTEM` |
| Asset validation | `scripts/validate_assets.py` — GUID integrity, asset↔JSON parity **incl. objectives + worldInteractions**, JSON↔C# builder string parity, scene sanity incl. the 7 new world objects + relocator bindings — **PASSED (0 warnings)** |
| Assets regenerated | `gen_story_content.py` + `gen_firstlocation_scene.py` re-run: `CL_C1_StoryContent.asset` (7 encounters, 5 decisions, 7 graphs, 6 objectives, 7 world interactions, 2 NPCs, 3 items), scene 135 → 142 roots, new script metas in the GUID registry |
| Scene binding checks | validator asserts `ChoirBeacon`, `EmberCache`, `KeepsakeCrate`, `TwinsReturnPoint`, `Barricade`, `WardStone`, `Rubble`, `Seq_Tide_Calm`, `NpcRelocator`→`Loc_Sera_AnnexGate`, entity bindings for all world objects, and script references for `WorldActionInteractable.cs`/`NpcRelocator.cs` |

Re-run: `cd scripts/decision_system_tests && mcs -langversion:latest -define:ENABLE_LEGACY_INPUT_MANAGER -out:FlowTests.exe TestJson.cs FlowTests.cs WorldTests.cs ../unity-stub/UnityStub.cs ../../Assets/_Project/Scripts/Core/*.cs ../../Assets/_Project/Scripts/Narrative/*.cs ../../Assets/_Project/Scripts/Narrative/Content/*.cs ../../Assets/_Project/Scripts/Narrative/Abilities/*.cs ../../Assets/_Project/Scripts/Gameplay/*.cs ../../Assets/_Project/Scripts/Gameplay/Interaction/*.cs ../../Assets/_Project/Scripts/Gameplay/Abilities/*.cs ../../Assets/_Project/Scripts/Gameplay/WorldState/*.cs ../../Assets/_Project/Scripts/Gameplay/World/*.cs ../../Assets/_Project/Scripts/Gameplay/NPC/*.cs ../../Assets/_Project/Scripts/UI/*.cs ../../Assets/Game/Scripts/FirstLocationBootstrap.cs ../../Assets/Game/Scripts/ThirdPersonCameraController.cs && mono FlowTests.exe`

## 7. Notes & fixes found while "checking well"

1. **Empty fail-conditions ≠ always-failing.** `ConditionEvaluator` (correctly) treats an empty condition list as *always true*; the first evaluator draft therefore failed every unfailable objective instantly. `ObjectiveManager` now treats an empty `failConditions` list as *unfailable* (test: unfailable objectives never leave Active on their own).
2. **Autosave coverage gap.** World actions that consume objects or hand over items after the last decision previously autosaved *before* the consume (flag→objective autosave fired mid-action), losing the consume on restart. `WorldServices` now autosaves on `EntityStateChangedEvent` + `ItemChangedEvent` too — persistence test compares full entity sets across restart.
3. **Latent content split fixed.** `story_content.json` said area `annex`/"North Annex" while `StoryContentBuilder.cs` said `east_annex`/"East Annex" (the scene gate persisted `annex`). Unified on `annex` (the persisted key) — the objective `CloseArea`/`ReopenArea` consequences and the validator now agree; `ProgressionIndex.AreaName("annex")` resolves properly.
4. **Assembly layering.** `GameServices` (Narrative, references only Core) cannot own Gameplay types without an asmdef cycle; `WorldServices` (Gameplay) boots the world systems right after it — the same pattern the scene bootstrap uses, so the headless tests boot identical services.
5. **Objective UI is data-driven and event-driven.** The HUD rebuilds its checklist only on `ObjectiveChangedEvent`/load/reset; important transitions flash the panel border and toast via the existing `ToastUI`.

## 8. Commit

Feature commit: `feat/world-objectives` — see `git log -1` (hash recorded in the chat report; this file ships with the change).
