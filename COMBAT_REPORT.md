# Combat & Action System — Build Report

Phase 5 of the CROSSROADS prototype: the core Action and Combat System, built **on top of** the existing player character, `AbilityManager`, `GameStateManager`, `DecisionManager`, NPC framework, `WorldStateManager`/objective system, save/load and the content/asset pipeline. No working system was rewritten — every integration is additive.

**Deliverable summary:** a reusable data-driven combat runtime (`Assets/_Project/Scripts/Gameplay/Combat/`), a player combat controller (move/aim/basic attack/abilities/dodge), ONE enemy prototype (the Choir Warden) driven by an event/state FSM with zero per-frame allocations, a combat HUD, combat ↔ world/objective integration through the existing condition/effect whitelists, a combat test area in the west transept of FirstLocation, Android-suitable configuration, and a headless acceptance test that plays the entire encounter chain end-to-end.

---

## 1. Systems created / changed

**Created — `Assets/_Project/Scripts/Gameplay/Combat/` (reusable, data-driven, no scene hardcoded):**

| File | What it is |
|---|---|
| `CombatEvents.cs` | `CombatantDamaged/Healed/Defeated`, `StatusChanged`, `EnemyStateChanged` events on the existing `EventBus` — combat is observable exactly like decisions/world state |
| `Combatant.cs` | `CombatantState` pure-C# combatant: health/defense/resistances, `ApplyDamage`/`Heal`/`ReviveFull`/`RestoreHealth`, statuses (`ApplyStatus`/`TickStatuses`), `MoveSpeedMultiplier`, `IsImmunity`. Includes `DamageCalculator` (deterministic formula) and `ActiveStatus` |
| `EnemyBrain.cs` | Pure FSM: `Dormant→Idle→Alert→Approach→AttackWindup→AttackRecover`, `Stagger`, `Defeat`. Ticks with injected world state (`IEnemyWorld`) — fully unit-testable, no Unity types |
| `CombatResolution.cs` | Resolves an `AbilityUsedEvent` payload into combat (`ResolveAbilityAttack`), melee-arc target queries (`InMeleeArc`), and `DefeatEnemy`/`DefeatPlayer` consequences delivered through the **existing `EffectApplier`** |
| `EnemyAgent.cs` | Unity host for `EnemyBrain` + `CombatantState`: story-gated activation (the enemy def's `activationConditions`), hit-flash material swap, sink-on-defeat, zero-allocation ticks (reused static buffers) |
| `PlayerCombatController.cs` | Android-suitable player combat: basic attack, dodge (distance + guard status + cooldown), hp persistence to a save var, defeat → data-driven consequences → revive at checkpoint (never destroys the save) |
| `CombatDirector.cs` | Scene-level spine: enemy registry, lazy player bridge, routes `AbilityUsedEvent` → attacks, static-buffer spatial queries for targets |

**Created — UI:** `Assets/_Project/Scripts/UI/CombatHUD.cs` (hp bar + status chips, ATTACK/DODGE touch buttons, enemy bar + state label, damage/defeat flash; wired in `GameUIBootstrap`).

**Changed (additive only):**
- `Narrative/Content/ContentData.cs` — new serializable data: `DamageType`/`AttackDelivery` enums, `DamageResistEntry`, `StatusEffectDefinitionData`, `AttackDefinitionData`, `AbilityCombatData`, `EnemyDefinitionData`, `CombatSettingsData` (+ `FindStatusEffect`/`FindAbilityCombat`/`FindEnemy` lookups)
- `Narrative/Content/StoryContentBuilder.cs` — full combat content (§2), the hunt objective, Sera's combat-reaction state
- `Gameplay/PlayerPrototypeController.cs` — one static `ExternalSpeedMultiplier` hook (status slow / dodge never touch the working locomotion code)
- `Gameplay/StoryModeBootstrap.cs` — attaches `PlayerCombatController` to the existing player
- Asset pipeline: `add_combat_content.py` (one-shot JSON merge), `gen_story_content.py` (serializes the 4 combat collections), `gen_firstlocation_scene.py` (combat test area), `validate_assets.py` (combat parity + scene needles), `compile_check.sh` (Combat folder)

## 2. Data-driven definitions (all in content, zero code constants)

- **Damage types:** `Kinetic / Ember / Tide / Stone / Hollow` — per-combatant resistance table (`1` neutral, `0.8` resisted, `1.25` vulnerable)
- **Attack types:** `MeleeArc` (range + arcDegrees + windup + cooldown) and `RadiusPulse` (radius) — one `AttackDefinitionData` shape for the player strike, the enemy attack, and ability payloads
- **Health/defense:** max health, flat defense (subtracted after resistances)
- **Status effects:** `echo_burn` (4s, −4/s DoT), `suppression` (2.5s, move ×0.65), `dodge_guard` (0.45s, full immunity), `tide_soothe` (3s, +6/s HoT)
- **Ability attacks:** `ember_pulse` (Ember 10/power + burn), `tide_mend` (Tide 3/power damage + 12/power self-heal + HoT), `stone_ward` (Stone 8/power + suppression) — scaling = the ability's **current level row** (power), so upgrades change combat
- **Enemy type:** the Choir Warden archetype (§4) — hp 60, def 3, resist table, speed/detect/leash/attack numbers, nested `warden_smite` attack (Hollow 12, 70° arc, 0.7s windup, 2.2s cd, applies suppression), story `activationConditions`, `onDefeatEffects`
- **Player settings:** hp 100, def 2, Hollow ×0.9, `player_strike` (Kinetic 10, 2.8m, 110°, 0.9s cd), dodge (3.6m / 0.28s / 1.6s cd + guard), `healthVarKey: player_hp`, `onPlayerDefeat` effects

Damage formula (no RNG, fully testable): **`max(1, raw × resistMultiplier − defense)`**.

## 3. Player actions & ability integration (tasks 4–5)

- **Move/look** — the existing `PlayerPrototypeController` joystick locomotion, untouched except the speed-multiplier hook (statuses/dodge scale it)
- **Basic attack** — `ATTACK` button / F key: melee-arc query through the director's reused static buffers, deterministic damage via `DamageCalculator`
- **Abilities** — the existing `AbilityHUD` buttons fire `AbilityUsedEvent` exactly as before; `CombatDirector` subscribes and resolves each payload against the ability's `AbilityCombatData`. **No ability logic was duplicated** — cooldowns, costs, level scaling all stay in `AbilityManager`; combat only *consumes* the event
- **Dodge** — burst displacement via the locomotion hook + `dodge_guard` immunity window + cooldown, all three numbers from data

## 4. Enemy prototype: the Choir Warden (tasks 6–7)

One archetype, complete lifecycle: **Dormant** (story-gated by `DecisionWas dec_c1_hall_first_light`) → **Idle** → **Alert** (9m detection) → **Approach** (1.55 m/s) → **AttackWindup** (0.7s telegraph) → strike (Hollow 12 + suppression) → **AttackRecover**; takes damage (**Stagger** 0.35s + hit-flash), leashes back at 15m, and on death fires `onDefeatEffects` through `EffectApplier` (drives the world, §6) then sinks out of the scene. Behaviour is **event/state driven**: the brain is a pure FSM ticked with `IEnemyWorld`; the agent reuses pre-allocated buffers/lists (no `Update` allocations, no LINQ, no per-frame closures) — Android-suitable by construction.

## 5. Combat feedback & test area (tasks 8, 12, 13)

- **CombatHUD:** player hp bar + status chips, ATTACK/DODGE touch buttons (same pointer pattern as the existing HUDs), active-enemy bar + live state label, red damage flash, defeat banner — rebuilt only on combat events
- **Test area:** the west transept of the existing hall — `ChoirWarden` object (EnemyAgent, capsule body, hit-flash materials), `WardenWreckage` (spawned by the defeat effect), `CombatDirector` object, both entities bound into `StoryWorldState` (`choir_warden` default-on, `warden_wreckage` default-off) so defeat persists across save/load
- **Android config:** touch-first controls, no physics queries per frame, static-buffer target queries, deterministic math (no per-hit allocations), lightweight primitive meshes + two authored materials

## 6. Combat ↔ world/objectives/progression (tasks 1, 9–10)

Encounter → objective → world change, all through existing systems:

1. First decision made → warden's `activationConditions` pass (enemy activates) **and** `obj_warden_hunt` (Crisis) offers + autostarts — same condition whitelist as dialogue
2. Player defeats the warden → `onDefeatEffects` via `EffectApplier`: `warden_driven_off +1` (var), `choir_warden` entity off / `warden_wreckage` on, 15 echoes, `c1_warden_felled` codex
3. `VarAtLeast warden_driven_off 1` completes the objective → consequences: choir rep −5, folk rep +4, **sera bond +5**, 10 echoes, codex entry — and Sera's new *"Shieldmate"* NPC state (checked first, so it wins) changes her behaviour/mood in the scene
4. **Defeat never destroys the save:** falling applies `onPlayerDefeat` (times_felled +1, mara bond +1, codex) and revives at the checkpoint with full hp; `player_hp` persists in the save vars. Both paths are asserted across a save/load restart in test [48]

## 7. Verification

| Check | Result |
|---|---|
| Headless suite (`FlowTests.exe`, incl. new `CombatTests`) | **689 / 689 passed** (566 prior + 123 new/updated; new sections **[40]–[49]**) |
| Combat test coverage | damage formula (all resist edges, min-1 floor) · health clamp + single defeat event (corpse hits are no-ops) · defense/resistance on the real archetype · statuses (DoT kill via event, HoT, expiry incl. **final-tick fix**, immunity blocks, refresh) · ability attacks from real `AbilityUsedEvent`s incl. level scaling & blocked abilities · full enemy FSM walk incl. stagger/leash/dialogue-safety · **end-to-end acceptance flow** (decision → active enemy → strike + ember → damage events → defeat → objective completes → rep/bond/codex/echo changes) · player defeat policy + **save/load persistence** (times_felled, player_hp, no save destruction) · path identity (ember burst vs tide sustain) |
| Compile checks | `scripts/compile_check.sh` clean in **both** `ENABLE_LEGACY_INPUT_MANAGER` and `ENABLE_INPUT_SYSTEM` |
| Asset validation | `scripts/validate_assets.py` — GUID integrity, asset↔JSON parity **incl. statusEffects/abilityCombat/enemies/combat settings**, JSON↔C# builder string parity, scene sanity incl. `ChoirWarden`/`WardenWreckage`/`CombatDirector` + entity bindings + `EnemyAgent.cs`/`CombatDirector.cs` references — **PASSED (0 warnings)** |
| Assets regenerated | `gen_story_content.py` + `gen_firstlocation_scene.py`: asset now carries 4 statuses, 3 ability payloads, 1 enemy, full combat settings, 7 objectives; scene 142 → 145 roots; 2 new materials + 9 script metas in the GUID registry |

Re-run: `cd scripts/decision_system_tests && mcs -langversion:latest -define:ENABLE_LEGACY_INPUT_MANAGER -out:FlowTests.exe TestJson.cs FlowTests.cs WorldTests.cs CombatTests.cs ../unity-stub/UnityStub.cs ../../Assets/_Project/Scripts/Core/*.cs ../../Assets/_Project/Scripts/Narrative/*.cs ../../Assets/_Project/Scripts/Narrative/Content/*.cs ../../Assets/_Project/Scripts/Narrative/Abilities/*.cs ../../Assets/_Project/Scripts/Gameplay/*.cs ../../Assets/_Project/Scripts/Gameplay/Interaction/*.cs ../../Assets/_Project/Scripts/Gameplay/Abilities/*.cs ../../Assets/_Project/Scripts/Gameplay/WorldState/*.cs ../../Assets/_Project/Scripts/Gameplay/World/*.cs ../../Assets/_Project/Scripts/Gameplay/Combat/*.cs ../../Assets/_Project/Scripts/Gameplay/NPC/*.cs ../../Assets/_Project/Scripts/UI/*.cs ../../Assets/Game/Scripts/FirstLocationBootstrap.cs ../../Assets/Game/Scripts/ThirdPersonCameraController.cs && mono FlowTests.exe`

## 8. Notes & fixes found while "checking well"

1. **Status final-tick bug (real engine bug, fixed).** `TickStatuses` dropped the last tick of any status whose duration ended exactly at the current frame (`remaining > 0` guard). Guard is now `remaining + dt > 0` — a 4s `echo_burn` delivers all 4 ticks. Tests pin this.
2. **Defeat is exactly-once by construction.** `ApplyDamage` routes DoT kills through the same defeat path as strikes; the `Alive` guard makes corpse hits/heals no-ops — tests assert one `CombatantDefeatedEvent` per combatant.
3. **Var changes don't autosave by themselves** (counters churn); the player controller therefore persists explicitly on defeat/revive via the existing `PersistNow(autosaveMirror:true)` — test [48] mirrors the controller and proves `times_felled`/`player_hp` survive a restart.
4. **No Narrative→Gameplay dependency added.** Combat content lives in `ContentData` (Narrative) as plain data; the Gameplay runtime reads it through the existing content service. Consequences flow only through `EffectApplier` — one write path for decisions, objectives and combat alike.
5. **Ability integration is consume-only.** `CombatDirector` subscribes to the same `AbilityUsedEvent` the HUD/VFX already use; blocked/cooling-down abilities never emit, so combat can't fire them — asserted in test [45].

## 9. Commit

See the feature commit recorded below.
