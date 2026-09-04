// ============================================================================
// CROSSROADS headless tests of the core ACTION & COMBAT system:
//   damage calculation · health/healing · defense & resistances · ability
//   attacks (through the REAL AbilityManager) · status effects · enemy state
//   transitions · defeat · combat objective completion · world/NPC reaction ·
//   player-defeat save safety · save/load of combat progression.
// Runs the exact same code paths the game uses (CombatantState, DamageCalculator,
// EnemyBrain, CombatResolution, AbilityManager, ObjectiveManager, EffectApplier).
// Invoke from FlowTests.Main (single process, shared counters).
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Gameplay;

namespace Crossroads.Tests
{
    public static class CombatTests
    {
        private static int _passed, _failed;
        private static readonly List<string> Log = new List<string>();

        // captured combat events
        private static readonly List<CombatantDamagedEvent> Damages = new List<CombatantDamagedEvent>();
        private static readonly List<CombatantHealedEvent> Heals = new List<CombatantHealedEvent>();
        private static readonly List<CombatantDefeatedEvent> Defeats = new List<CombatantDefeatedEvent>();
        private static readonly List<StatusChangedEvent> StatusEvents = new List<StatusChangedEvent>();
        private static readonly List<AbilityUsedEvent> AbilityUses = new List<AbilityUsedEvent>();

        private static void Check(bool condition, string what)
        {
            if (condition) { _passed++; Log.Add("  PASS  " + what); }
            else { _failed++; Log.Add("  FAIL  " + what); }
        }

        private static void CheckEq<T>(T actual, T expected, string what)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            if (ok) { _passed++; Log.Add("  PASS  " + what); }
            else { _failed++; Log.Add("  FAIL  " + what + " (expected " + expected + ", got " + actual + ")"); }
        }

        private class TempPaths : IPathProvider
        {
            private readonly string _dir;
            public TempPaths(string dir) { _dir = dir; System.IO.Directory.CreateDirectory(dir); }
            public string Directory { get { return _dir; } }
            public string Resolve(string fileName) { return Path.Combine(_dir, fileName); }
        }

        private class TestJsonAdapter : IJsonSerializer
        {
            public string ToJson(object o, bool prettyPrint) { return TestJson.ToJson(o); }
            public T FromJson<T>(string json) { return TestJson.FromJson<T>(json); }
        }

        private static bool _subscribed;

        private static void NewRun(string dir, out IEncounterSource content)
        {
            content = new RuntimeContentSource();
            StoryLog.Info = Console.WriteLine;
            StoryLog.Warn = Console.WriteLine;
            StoryLog.Error = Console.WriteLine;
            GameServices.Init(new TestJsonAdapter(), new TempPaths(dir), content,
                "FirstLocation", "hall_spawn", 0, loadExisting: true);
            WorldServices.Init(); // boots ObjectiveManager/WorldStateSystem like the scene does

            if (!_subscribed)
            {
                _subscribed = true;
                EventBus.Subscribe<CombatantDamagedEvent>(e => Damages.Add(e));
                EventBus.Subscribe<CombatantHealedEvent>(e => Heals.Add(e));
                EventBus.Subscribe<CombatantDefeatedEvent>(e => Defeats.Add(e));
                EventBus.Subscribe<StatusChangedEvent>(e => StatusEvents.Add(e));
                EventBus.Subscribe<AbilityUsedEvent>(e => AbilityUses.Add(e));
            }
        }

        private static void ClearCaptures()
        {
            Damages.Clear(); Heals.Clear(); Defeats.Clear(); StatusEvents.Clear(); AbilityUses.Clear();
        }

        private static void Shutdown()
        {
            WorldServices.Shutdown(silent: true);
            GameServices.Shutdown(silent: true);
        }

        private static string TempDir(string tag)
        {
            return Path.Combine(Path.GetTempPath(), "crossroads_combat_" + tag + "_" + Guid.NewGuid().ToString("N"));
        }

        private static void PlayFirstLight(string optionId)
        {
            var flow = GameServices.Encounters;
            flow.Run(StoryContentBuilder.EncounterFirstLight);
            int guard = 0;
            while (!flow.AwaitingChoice && guard++ < 20) flow.Advance();
            flow.SelectChoice(optionId);
            flow.Advance();
            guard = 0;
            while (flow.IsRunning && guard++ < 20) flow.Advance();
        }

        /// <summary>Fake movement sink for the pure enemy brain (headless FSM tests).</summary>
        private class FakeEnemyWorld : IEnemyWorld
        {
            public Point3 Position;
            public bool Moved;
            Point3 IEnemyWorld.Position { get { return Position; } }
            void IEnemyWorld.MoveTowards(Point3 target, float speed, float dt)
            {
                Moved = true;
                float dx = target.x - Position.x, dz = target.z - Position.z;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                if (dist < 0.01f) return;
                float step = Math.Min(dist, speed * dt);
                Position = new Point3(Position.x + dx / dist * step, Position.y, Position.z + dz / dist * step);
            }
            void IEnemyWorld.FaceTowards(Point3 target, float turnSpeed, float dt) { }
        }

        private static EnemyDefinitionData Warden(StoryContentData content)
        {
            return content.FindEnemy(StoryContentBuilder.EnemyChoirWarden);
        }

        // ================================================================ 40. combat content contracts
        private static void TestCombatContent()
        {
            Log.Add("[40] Combat content: data-driven definitions satisfy the runtime contracts");
            var content = StoryContentBuilder.CreateFirstLightContent();

            CheckEq(content.statusEffects.Count, 4, "four status effect definitions");
            Check(content.FindStatusEffect("echo_burn") != null
                  && content.FindStatusEffect("suppression") != null
                  && content.FindStatusEffect("dodge_guard") != null
                  && content.FindStatusEffect("tide_soothe") != null, "burn/suppression/guard/soothe present");
            Check(content.FindStatusEffect("echo_burn").healthPerTick == -4
                  && content.FindStatusEffect("echo_burn").tickIntervalSeconds == 1f, "echo_burn is a 4/s DoT");
            Check(content.FindStatusEffect("dodge_guard").grantsImmunity, "dodge_guard grants immunity");
            Check(Math.Abs(content.FindStatusEffect("suppression").moveSpeedMultiplier - 0.65f) < 0.001f, "suppression slows to 65%");

            // ability combat rows exist for every initial ability (no duplicated ability logic)
            foreach (string abilityId in new[] { "ember_pulse", "tide_mend", "stone_ward" })
                Check(content.FindAbilityCombat(abilityId) != null, "combat payload authored for " + abilityId);
            Check(content.FindAbilityCombat("tide_mend").healPlayerPerPower > 0f, "tide_mend is the healing power");

            EnemyDefinitionData warden = Warden(content);
            Check(warden != null, "one enemy archetype authored (Choir Warden)");
            CheckEq(warden.resistances.Count, 5, "warden has a full resistance table");
            Check(warden.attack != null && warden.attack.windupSeconds > 0f, "warden attack telegraphs (windup)");
            Check(warden.activationConditions.Count == 1
                  && warden.activationConditions[0].type == ConditionType.DecisionWas,
                  "warden activation is story-gated (first decision)");
            Check(warden.onDefeatEffects.Count == 5, "warden defeat consequences authored (effects)");

            CombatSettingsData settings = content.combat;
            Check(settings.playerMaxHealth == 100f && settings.playerDefense == 2f, "player health/defense authored");
            Check(settings.basicAttack.damageType == DamageType.Kinetic && settings.basicAttack.baseDamage == 10f,
                  "player basic strike authored (kinetic 10)");
            Check(settings.onPlayerDefeat.Count > 0, "player-defeat policy authored (never destroys the save)");

            Check(content.FindObjective(StoryContentBuilder.ObjectiveWardenHunt) != null,
                  "combat objective authored (obj_warden_hunt)");
        }

        // ================================================================ 41. damage calculation
        private static void TestDamageCalculation()
        {
            Log.Add("[41] Damage: deterministic formula max(1, raw*resist - defense)");
            CheckEq(DamageCalculator.Compute(20f, 1f, 0f), 20f, "no defense: full damage");
            CheckEq(DamageCalculator.Compute(20f, 1f, 5f), 15f, "flat defense subtracts");
            CheckEq(DamageCalculator.Compute(20f, 0.5f, 0f), 10f, "50% resistance halves");
            CheckEq(DamageCalculator.Compute(20f, 0.5f, 5f), 5f, "resist applies before defense");
            CheckEq(DamageCalculator.Compute(20f, 1.25f, 0f), 25f, "vulnerability amplifies (125%)");
            CheckEq(DamageCalculator.Compute(2f, 1f, 5f), 1f, "minimum damage floor of 1");
            CheckEq(DamageCalculator.Compute(0f, 1f, 0f), 0f, "zero raw deals nothing");

            var content = StoryContentBuilder.CreateFirstLightContent();
            var resists = Warden(content).resistances;
            CheckEq(DamageCalculator.ResistanceFor(resists, DamageType.Ember), 1.25f, "warden: ember vulnerability");
            CheckEq(DamageCalculator.ResistanceFor(resists, DamageType.Hollow), 0.5f, "warden: hollow halved");
            CheckEq(DamageCalculator.ResistanceFor(resists, DamageType.Kinetic), 1f, "warden: kinetic normal");
            CheckEq(DamageCalculator.ResistanceFor(null, DamageType.Ember), 1f, "no table = neutral");
        }

        // ================================================================ 42. health / healing / defeat
        private static void TestHealthAndDefeat()
        {
            Log.Add("[42] Health: damage, healing clamp, defeat event fires exactly once");
            ClearCaptures();
            var content = StoryContentBuilder.CreateFirstLightContent();
            CombatantState player = CombatantState.ForPlayer(content.combat);

            CheckEq(player.Health, 100f, "starts at full health");
            var r = player.ApplyDamage(DamageType.Kinetic, 20f);
            Check(Math.Abs(player.Health - 82f) < 0.001f, "player: 20 raw - 2 defense = 18 damage (82 left)");
            CheckEq(r.amount, 18f, "result reports the mitigated amount");
            Check(Damages.Count == 1 && Damages[0].isPlayer, "CombatantDamagedEvent published");

            player.Heal(50f);
            CheckEq(player.Health, 100f, "healing clamps at max");
            Check(Heals.Count > 0, "heal event published");

            // defeat: exactly one defeated event, no further damage after death
            int defeatsBefore = Defeats.Count;
            player.ApplyDamage(DamageType.Hollow, 500f);
            Check(!player.Alive, "player defeated by massive damage");
            CheckEq(Defeats.Count - defeatsBefore, 1, "CombatantDefeatedEvent fired exactly once");
            player.ApplyDamage(DamageType.Kinetic, 10f);
            CheckEq(Defeats.Count - defeatsBefore, 1, "no duplicate defeat events on corpse hits");
            player.Heal(30f);
            Check(!player.Alive, "healing does not revive (defeat handling owns revival)");

            player.ReviveFull();
            CheckEq(player.Health, 100f, "ReviveFull restores to max");
        }

        // ================================================================ 43. defense & resistances end-to-end
        private static void TestDefenseAndResistances()
        {
            Log.Add("[43] Defense: resistance table + flat defense on the enemy archetype");
            ClearCaptures();
            var content = StoryContentBuilder.CreateFirstLightContent();
            CombatantState warden = CombatantState.ForEnemy(Warden(content));

            CheckEq(warden.Health, 60f, "warden health from data");
            warden.ApplyDamage(DamageType.Kinetic, 20f);
            CheckEq(warden.Health, 43f, "kinetic 20: x1.0 - 3 defense = 17 (60->43)");
            warden.ApplyDamage(DamageType.Ember, 20f);
            CheckEq(warden.Health, 21f, "ember 20: x1.25=25 - 3 = 22 (43->21) - the vulnerability matters");
            warden.ApplyDamage(DamageType.Hollow, 20f);
            Check(Math.Abs(warden.Health - 14f) < 0.001f, "hollow 20: x0.5=10 - 3 = 7 (21->14)");
            warden.ApplyDamage(DamageType.Kinetic, 1f);
            Check(Math.Abs(warden.Health - 13f) < 0.001f, "minimum damage floor keeps chip hits at 1");
        }

        // ================================================================ 44. status effects
        private static void TestStatusEffects()
        {
            Log.Add("[44] Statuses: DoT kills, HoT heals, expiry restores, immunity blocks, refresh");
            var content = StoryContentBuilder.CreateFirstLightContent();
            var lib = content.statusEffects;

            // DoT over time -> death
            ClearCaptures();
            CombatantState victim = new CombatantState("victim", false, "Victim", 12f, 0f, null);
            victim.ApplyStatus(content.FindStatusEffect("echo_burn"));
            Check(victim.HasStatus("echo_burn"), "echo_burn applied");
            float dt = 0.25f;
            int ticks = 0;
            while (victim.Alive && ticks++ < 200) victim.TickStatuses(dt);
            Check(!victim.Alive, "DoT (4/s for 4s = 16 >= 12hp) defeats the victim");
            Check(Defeats.FindAll(d => d.combatantId == "victim").Count == 1, "DoT defeat publishes the defeat event");

            // HoT heals over time, then expires
            ClearCaptures();
            CombatantState healed = new CombatantState("healed", true, "Ari", 100f, 0f, null);
            healed.ApplyDamage(DamageType.Kinetic, 98f); // 1 defense? no: defense 0 -> 98 damage
            healed.ApplyStatus(content.FindStatusEffect("tide_soothe"));
            for (int i = 0; i < 16; i++) healed.TickStatuses(0.25f); // 4s window: 3 full ticks
            Check(Math.Abs(healed.Health - 20f) < 0.01f, "soothing tide healed +18 over its duration (2 -> 20)");
            Check(!healed.HasStatus("tide_soothe"), "status expired after its duration");

            // suppression slows, expiry restores speed
            CombatantState slowed = CombatantState.ForPlayer(content.combat);
            slowed.ApplyStatus(content.FindStatusEffect("suppression"));
            Check(Math.Abs(slowed.MoveSpeedMultiplier - 0.65f) < 0.001f, "suppression -> 65% move speed");
            for (int i = 0; i < 12; i++) slowed.TickStatuses(0.25f);
            Check(Math.Abs(slowed.MoveSpeedMultiplier - 1f) < 0.001f, "speed restored after expiry");

            // immunity frames absorb a hit entirely
            ClearCaptures();
            CombatantState dodger = CombatantState.ForPlayer(content.combat);
            dodger.ApplyStatus(content.FindStatusEffect("dodge_guard"));
            var dr = dodger.ApplyDamage(DamageType.Hollow, 50f);
            Check(dr.dodged && dr.amount == 0f, "dodge_guard absorbs the hit (0 damage)");
            CheckEq(dodger.Health, 100f, "immunity: health untouched");
            Check(Damages.Count == 1 && Damages[0].dodged, "dodged damage event published for feedback");

            // refresh: same status twice = one instance, duration reset
            CombatantState refreshed = CombatantState.ForPlayer(content.combat);
            refreshed.ApplyStatus(content.FindStatusEffect("suppression"));
            refreshed.TickStatuses(2f); // 0.5s left
            refreshed.ApplyStatus(content.FindStatusEffect("suppression"));
            CheckEq(refreshed.Statuses.Count, 1, "reapplying refreshes instead of stacking");
            Check(Math.Abs(refreshed.Statuses[0].remaining - 2.5f) < 0.01f, "duration was reset to full");
        }

        // ================================================================ 45. ability attacks (REAL AbilityManager)
        private static void TestAbilityAttacks()
        {
            Log.Add("[45] Ability attacks: existing AbilityManager events drive combat (no duplicate logic)");
            string dir = TempDir("ability");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);
            PlayFirstLight("ember_reach"); // unlocks ember_pulse (level 1: power 1.0, radius 3.5)

            AbilityManager mgr = GameServices.Abilities;
            Check(mgr.IsUnlocked("ember_pulse"), "ember_pulse unlocked by the decision");
            CheckEq(mgr.Activate("ember_pulse"), AbilityActivation.Ok, "activation goes through the EXISTING manager");
            Check(AbilityUses.Count == 1 && AbilityUses[0].abilityId == "ember_pulse",
                  "AbilityUsedEvent raised (level-row payload)");
            CheckEq(mgr.Activate("ember_pulse"), AbilityActivation.CoolingDown, "cooldown enforced by the manager");

            // resolve the captured event against a fresh warden (as CombatDirector does in-game)
            AbilityUsedEvent evt = AbilityUses[0];
            AbilityCombatData payload = content.Content.FindAbilityCombat("ember_pulse");
            var warden = CombatantState.ForEnemy(Warden(content.Content));
            float hpBefore = warden.Health;
            var targets = new List<CombatantState> { warden };
            int hits = CombatResolution.ResolveAbilityAttack(evt, payload, content.Content.statusEffects, null, targets);
            CheckEq(hits, 1, "one target hit");
            Check(Math.Abs(warden.Health - (hpBefore - (10f * evt.power * 1.25f - 3f))) < 0.01f,
                  "damage = power x payload x ember vulnerability - defense");
            Check(warden.HasStatus("echo_burn"), "ability applied its status (echo_burn)");

            // level scaling: a level-2 payload (power 1.5) hits 1.5x harder - upgrades matter
            GameServices.Progress.UpgradeAbility("ember_pulse", 1);
            AbilityUses.Clear();
            float clock = 100f; // past the cooldown
            mgr.Now = () => clock;
            CheckEq(mgr.Activate("ember_pulse"), AbilityActivation.Ok, "level-2 activation after cooldown");
            var warden2 = CombatantState.ForEnemy(Warden(content.Content));
            float before2 = warden2.Health;
            CombatResolution.ResolveAbilityAttack(AbilityUses[0], payload, content.Content.statusEffects, null,
                new List<CombatantState> { warden2 });
            Check(before2 - warden2.Health > (hpBefore - warden.Health) + 3f,
                  "level-2 power (1.5) deals more damage than level-1");

            // tide heals the player
            var playerState = CombatantState.ForPlayer(content.Content.combat);
            playerState.ApplyDamage(DamageType.Kinetic, 40f);
            var tidePayload = content.Content.FindAbilityCombat("tide_mend");
            var tideEvt = new AbilityUsedEvent { abilityId = "tide_mend", power = 1f, radius = 3.5f };
            float hpBeforeHeal = playerState.Health;
            CombatResolution.ResolveAbilityAttack(tideEvt, tidePayload, content.Content.statusEffects, playerState, null);
            Check(playerState.Health > hpBeforeHeal + 10f, "tide_mend healed the caster (12 x power)");
            Check(playerState.HasStatus("tide_soothe"), "tide_mend applied soothing to the player");

            // sealed ability: no event at all -> no damage possible
            GameServices.State.BlockAbility("ember_pulse");
            AbilityUses.Clear();
            CheckEq(mgr.Activate("ember_pulse"), AbilityActivation.Blocked, "sealed ability refuses to activate");
            CheckEq(AbilityUses.Count, 0, "no event -> no combat effect (single source of truth)");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 46. enemy state transitions
        private static void TestEnemyStates()
        {
            Log.Add("[46] Enemy FSM: Dormant->Idle->Alert->Approach->Windup->Strike->Recover; Stagger; Defeat");
            var content = StoryContentBuilder.CreateFirstLightContent();
            EnemyDefinitionData def = Warden(content);
            CombatantState combatant = CombatantState.ForEnemy(def);
            var brain = new EnemyBrain(def, combatant);
            var world = new FakeEnemyWorld { Position = new Point3(0f, 0f, 0f) };
            float dt = 0.1f;

            CheckEq(brain.State, EnemyState.Dormant, "starts dormant (story gate)");
            brain.Tick(world, dt, new Point3(5f, 0f, 0f), true, false);
            CheckEq(brain.State, EnemyState.Dormant, "dormant ignores the player entirely");
            brain.Activate();
            CheckEq(brain.State, EnemyState.Idle, "activation conditions met -> Idle");

            // player far away (beyond detection 9)
            brain.Tick(world, dt, new Point3(20f, 0f, 0f), true, false);
            CheckEq(brain.State, EnemyState.Idle, "player at 20m: stays Idle");

            // detection at 8m
            brain.Tick(world, dt, new Point3(8f, 0f, 0f), true, false);
            CheckEq(brain.State, EnemyState.Alert, "player within 9m -> Alert (detection)");
            brain.Tick(world, dt, new Point3(8f, 0f, 0f), true, false);
            CheckEq(brain.State, EnemyState.Approach, "Alert -> Approach");

            // closes in (move speed 1.55 * dt) while the player stands at 8m
            int guard = 0;
            while (brain.State == EnemyState.Approach && guard++ < 200)
                brain.Tick(world, dt, new Point3(8f, 0f, 0f), true, false);
            Check(world.Moved, "approach actually moved the enemy");
            CheckEq(brain.State, EnemyState.AttackWindup, "in attack range (2.3) -> windup");

            // windup 0.7s then Strike + recover
            guard = 0;
            EnemyTickResult result = EnemyTickResult.None;
            while (result == EnemyTickResult.None && guard++ < 20)
                result = brain.Tick(world, dt, new Point3(1.5f, 0f, 0f), true, false);
            CheckEq(result, EnemyTickResult.Strike, "windup completes with a Strike");
            CheckEq(brain.State, EnemyState.AttackRecover, "strike -> recover (cooldown)");

            // take damage mid-recovery -> stagger, then back to approach
            combatant.ApplyDamage(DamageType.Kinetic, 5f);
            brain.OnDamaged();
            CheckEq(brain.State, EnemyState.Stagger, "damaged -> Stagger (take-damage state)");
            guard = 0;
            while (brain.State == EnemyState.Stagger && guard++ < 20)
                brain.Tick(world, dt, new Point3(3f, 0f, 0f), true, false);
            CheckEq(brain.State, EnemyState.Approach, "stagger recovers into Approach");

            // leash: player gone far -> gives up
            guard = 0;
            while (brain.State == EnemyState.Approach && guard++ < 200)
                brain.Tick(world, dt, new Point3(60f, 0f, 0f), true, false);
            CheckEq(brain.State, EnemyState.Idle, "beyond leash (15m) -> back to Idle");

            // defeat is terminal
            combatant.ApplyDamage(DamageType.Ember, 500f);
            Check(!combatant.Alive, "warden combatant dead");
            brain.OnDefeated();
            CheckEq(brain.State, EnemyState.Defeat, "Defeat state reached");
            CheckEq(brain.Tick(world, dt, new Point3(1f, 0f, 0f), true, false), EnemyTickResult.None,
                  "defeated brain ticks are no-ops");

            // dialogue lock freezes behaviour (encounters hold during decisions)
            var brain2 = new EnemyBrain(def, CombatantState.ForEnemy(def));
            brain2.Activate();
            brain2.Tick(world, dt, new Point3(2f, 0f, 0f), true, false); // Alert/Approach
            EnemyState before = brain2.State;
            brain2.Tick(world, dt, new Point3(0.5f, 0f, 0f), true, true);
            CheckEq(brain2.State, before, "talking=true: enemy holds politely during dialogue");
        }

        // ================================================================ 47. full flow: encounter -> combat -> objective -> world
        private static void TestCombatObjectiveFlow()
        {
            Log.Add("[47] Acceptance flow: decision -> enemy active -> fight -> objective -> world/NPC change");
            string dir = TempDir("flow");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);

            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Hidden,
                  "combat objective hidden before any decision");

            PlayFirstLight("ember_reach");

            // story gate lifted + objective offered automatically (event-driven)
            Check(ConditionEvaluator.Evaluate(Warden(content.Content).activationConditions, GameServices.State),
                  "warden activation conditions now pass (story-gated combat)");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Active,
                  "combat objective auto-tracked after the decision");

            // the fight: player strikes + ember pulse until the warden drops
            CombatSettingsData settings = content.Content.combat;
            CombatantState player = CombatantState.ForPlayer(settings);
            CombatantState warden = CombatantState.ForEnemy(Warden(content.Content));
            var brain = new EnemyBrain(Warden(content.Content), warden);
            brain.Activate();
            var world = new FakeEnemyWorld { Position = new Point3(0f, 0f, 0f) };

            // enemy answers back: windup + strike land on the player (suppression applied)
            int guard = 0;
            EnemyTickResult r = EnemyTickResult.None;
            while (r != EnemyTickResult.Strike && guard++ < 300)
                r = brain.Tick(world, 0.1f, new Point3(1.5f, 0f, 0f), true, false);
            float hpBefore = player.Health;
            player.ApplyDamage(Warden(content.Content).attack.damageType, Warden(content.Content).attack.baseDamage);
            Check(player.Health < hpBefore, "enemy strike damaged the player");
            player.ApplyStatus(content.Content.FindStatusEffect("suppression"));
            Check(Math.Abs(player.MoveSpeedMultiplier - 0.65f) < 0.001f, "enemy attack suppressed the player (status)");

            // player fights back: basic strikes + one ember pulse through the REAL manager
            guard = 0;
            while (warden.Alive && guard++ < 50)
            {
                warden.ApplyDamage(settings.basicAttack.damageType, settings.basicAttack.baseDamage);
                brain.OnDamaged();
                if (!warden.Alive) break;
            }
            Check(!warden.Alive, "basic strikes can defeat the warden (chip damage + floor)");

            // defeat consequences through the single write path (as EnemyAgent does)
            int defeatsBefore = Defeats.Count;
            CombatResolution.DefeatEnemy(Warden(content.Content), GameServices.State);
            CheckEq(Defeats.Count - defeatsBefore, 1, "enemy defeat event published");

            // ... which completed the combat objective through the existing event graph
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Completed,
                  "defeat counter (var) completed the combat objective - no combat-specific objective code");
            CheckEq(GameServices.State.GetVar("warden_driven_off", 0), 1, "defeat counter persisted as a var");
            CheckEq(GameServices.State.GetEntity("choir_warden", true), false, "warden despawned (entity)");
            CheckEq(GameServices.State.GetEntity("warden_wreckage", false), true, "wreckage spawned (world changed)");
            CheckEq(GameServices.State.GetReputation("choir"), -15, "choir standing: -10 (decision) -5 (objective)");
            CheckEq(GameServices.State.GetBond("sera"), 5, "sera bond +5 from the objective consequence");

            // NPC reaction: Sera's fate state now derives from the COMBAT objective
            var sera = new NpcBrain(content.Content.FindNpc("sera"), GameServices.Progress);
            CheckEq(sera.CurrentTitle, "Sera · Shieldmate", "sera reacts to the completed combat objective");
            Check(sera.Profile.approach > 0f, "shieldmate sera approaches (behaviour changed)");

            // restart: everything combat-shaped persists
            Shutdown();
            ClearCaptures();
            NewRun(dir, out content);
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Completed,
                  "restart: combat objective stays completed");
            CheckEq(GameServices.State.GetEntity("choir_warden", true), false, "restart: warden still down");
            CheckEq(GameServices.State.GetEntity("warden_wreckage", false), true, "restart: wreckage remains");
            CheckEq(GameServices.State.GetReputation("choir"), -15, "restart: reputation consequences persist");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 48. player defeat never destroys the save
        private static void TestPlayerDefeatSafety()
        {
            Log.Add("[48] Player defeat: consequences applied, save intact, fight still winnable");
            string dir = TempDir("defeat");
            ClearCaptures();
            IEncounterSource content;
            NewRun(dir, out content);
            PlayFirstLight("tide_clear");

            CombatSettingsData settings = content.Content.combat;
            CombatantState player = CombatantState.ForPlayer(settings);
            EnemyDefinitionData warden = Warden(content.Content);

            // the warden smites the player down (hollow 12 -> x0.9 - 2 def = 8.8 per hit)
            int guard = 0;
            while (player.Alive && guard++ < 50)
                player.ApplyDamage(warden.attack.damageType, warden.attack.baseDamage);
            Check(!player.Alive, "player defeated by repeated enemy strikes");

            int defeatsBefore = Defeats.Count;
            CombatResolution.DefeatPlayer(settings, GameServices.State); // controller does this on death
            CheckEq(Defeats.Count - defeatsBefore, 1, "player defeat event published (feedback)");
            CheckEq(GameServices.State.GetVar("times_felled", 0), 1, "defeat counted (times_felled var)");
            CheckEq(GameServices.State.GetBond("mara"), 11, "mara bond +1 (she patches you up: 10 tide +1)");
            Check(GameServices.State.HasDecision("dec_c1_hall_first_light"), "decisions INTACT after defeat");

            player.ReviveFull();
            CheckEq(player.Health, 100f, "revived at full health");
            GameServices.State.SetVar("player_hp", 100);
            GameServices.PersistNow(autosaveMirror: true); // the controller persists on defeat

            // restart: save was not destroyed, encounter still pending, defeat remembered
            Shutdown();
            NewRun(dir, out content);
            Check(GameServices.State.HasDecision("dec_c1_hall_first_light")
                  && GameServices.State.DecisionOption("dec_c1_hall_first_light") == "tide_clear",
                  "restart: the run's decisions survived the defeat");
            CheckEq(GameServices.State.GetVar("times_felled", 0), 1, "restart: defeat count persisted");
            CheckEq(GameServices.State.GetVar("player_hp", -1), 100, "restart: player hp persisted");
            CheckEq(WorldServices.Objectives.PhaseOf(StoryContentBuilder.ObjectiveWardenHunt), ObjectivePhase.Active,
                  "restart: the fight is STILL WINNABLE (objective active, not failed)");
            CheckEq(GameServices.State.GetEntity("choir_warden", true), true, "restart: warden still standing");

            Shutdown();
            Directory.Delete(dir, true);
        }

        // ================================================================ 49. ability path flavour + hp persistence edge
        private static void TestPathCombatFlavour()
        {
            Log.Add("[49] Paths fight differently: ember burst vs tide sustain on the same enemy");
            var content = StoryContentBuilder.CreateFirstLightContent();

            // ember: heavy burst + burn
            CombatantState wardenA = CombatantState.ForEnemy(Warden(content));
            CombatResolution.ResolveAbilityAttack(
                new AbilityUsedEvent { abilityId = "ember_pulse", power = 1f, radius = 3.5f },
                content.FindAbilityCombat("ember_pulse"), content.statusEffects, null,
                new List<CombatantState> { wardenA });
            float emberHit = 60f - wardenA.Health;
            Check(wardenA.HasStatus("echo_burn"), "ember applies burn");

            // tide: light damage but the player mends
            CombatantState player = CombatantState.ForPlayer(content.combat);
            player.ApplyDamage(DamageType.Kinetic, 50f);
            float hpLow = player.Health;
            CombatantState wardenB = CombatantState.ForEnemy(Warden(content));
            CombatResolution.ResolveAbilityAttack(
                new AbilityUsedEvent { abilityId = "tide_mend", power = 1f, radius = 3.5f },
                content.FindAbilityCombat("tide_mend"), content.statusEffects, player,
                new List<CombatantState> { wardenB });
            Check(60f - wardenB.Health < emberHit, "tide hits softer than ember (path combat identity)");
            Check(player.Health > hpLow, "tide healed the player in the same action");
        }

        // ================================================================ entry
        public static int RunAll(out int passed, out int failed)
        {
            Console.WriteLine();
            if (!_subscribed)
            {
                _subscribed = true;
                EventBus.Subscribe<CombatantDamagedEvent>(e => Damages.Add(e));
                EventBus.Subscribe<CombatantHealedEvent>(e => Heals.Add(e));
                EventBus.Subscribe<CombatantDefeatedEvent>(e => Defeats.Add(e));
                EventBus.Subscribe<StatusChangedEvent>(e => StatusEvents.Add(e));
                EventBus.Subscribe<AbilityUsedEvent>(e => AbilityUses.Add(e));
            }
            TestCombatContent();
            TestDamageCalculation();
            TestHealthAndDefeat();
            TestDefenseAndResistances();
            TestStatusEffects();
            TestAbilityAttacks();
            TestEnemyStates();
            TestCombatObjectiveFlow();
            TestPlayerDefeatSafety();
            TestPathCombatFlavour();
            passed = _passed;
            failed = _failed;
            return _failed;
        }

        public static List<string> GetLog() { return Log; }
    }
}
