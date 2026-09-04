using System;
using System.Collections.Generic;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    // =====================================================================================
    // Data-driven story content (GAME_DESIGN §4.2). Everything is plain serializable data
    // ("all decision content is data, not code"); ScriptableObject assets are thin carriers
    // and the headless tests drive the same POCOs directly.
    // Adding a new encounter = one StoryContentLibrary asset (or a StoryContentBuilder entry),
    // never a change to the core system.
    // =====================================================================================

    /// <summary>Condition whitelist (§4.2) - gates option visibility and dialogue variants.</summary>
    public enum ConditionType
    {
        FlagIs,            // flags[key] == value
        FlagIsNot,         // flags[key] != value (or unset)
        FlagMissing,       // flags[key] unset
        VarAtLeast,        // vars[key] >= amount
        AffinityAtLeast,   // affinity key >= amount
        BondAtLeast,       // bond key >= amount
        DecisionWas,       // decision key resolved with option == value
        DecisionNotMade,   // decision key NOT resolved
        CodexOwned,        // codex contains key
        ReputationAtLeast, // reputation key >= amount  (progression)
        ItemHeld,          // items contains key
        AbilityOwned,      // abilities contains key
        AreaUnlocked,      // unlockAreas contains key
        SkillAtLeast,      // skills[key] >= amount
        EchoesAtLeast,     // echoBank >= amount
        AbilityLevelBelow, // ability level < amount (upgrade gates)
        ObjectiveActive,   // objective key is being tracked right now
        ObjectiveCompleted,// objective key was completed (mission history)
        ObjectiveFailed,   // objective key was failed (recovery paths)
        WorldStateIs       // worldStates[key] == value (the city remembers)
    }

    /// <summary>Effect whitelist (§4.2) - applied by EffectApplier on selection.</summary>
    public enum EffectType
    {
        SetFlag,
        ClearFlag,
        AddAffinity,
        SetAffinity,
        AddBond,
        SetVar,
        AddVar,
        SetWorldState,
        SpawnEntity,       // key = entity id, active/dormant = value ("1"/"0")
        AddCodex,
        GrantEchoes,
        AddReputation,     // key = group id  (progression)
        SetReputation,
        UnlockAbility,     // key = ability id
        AddSkillLevel,     // key = skill id
        AddItem,           // key = item id
        RemoveItem,        // key = item id
        UnlockArea,        // key = area id
        UpgradeAbility,    // key = ability id, amount = +levels (sets level 1 on first unlock)
        BlockAbility,      // key = ability id (excluded by your choices; wins over unlocked)
        MoveNpc,           // key = npc id, value = location key (relocates the NPC, persisted)
        CloseArea,         // key = area id (re-seals an opened area)
        ReopenArea,        // key = area id (lifts a seal)
        UnlockInteraction  // key = world interaction unlock key (marks it available, persisted)
    }

    [Serializable]
    public class DecisionConditionData
    {
        public ConditionType type = ConditionType.FlagIs;
        public string key = "";
        public string value = "";
        public int amount;
    }

    [Serializable]
    public class DecisionEffectData
    {
        public EffectType type = EffectType.SetFlag;
        public string key = "";
        public string value = "";
        public int amount;
    }

    [Serializable]
    public class DecisionOptionData
    {
        public string id = "";
        public string text = "";
        public string afterText = "";                       // player's spoken line after choosing
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
        public List<DecisionEffectData> effects = new List<DecisionEffectData>();
    }

    [Serializable]
    public class DecisionNodeData
    {
        public string id = "";
        public string promptText = "";
        public float timeLimitSeconds;                      // 0 = untimed (D1); >0 = pressure choice (D2)
        public int timeoutOptionIndex = 0;                  // D2 timer auto-select (design: "hesitate" outcome)
        public string codexEntryId = "";
        public List<DecisionOptionData> options = new List<DecisionOptionData>();

        public DecisionOptionData FindOption(string optionId)
        {
            for (int i = 0; i < options.Count; i++)
                if (options[i] != null && options[i].id == optionId) return options[i];
            return null;
        }
    }

    [Serializable]
    public class DialogueNodeData
    {
        public string id = "";
        public string speaker = "";
        public string text = "";
        public string nextId = "";                          // explicit next node
        public string branchPrefix = "";                    // pick the first node whose id starts with this AND whose conditions pass
        public string decisionId = "";                      // embedded decision node (§4.2: dialogue graph with embedded DecisionNode)
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
        public bool end;                                    // explicit end-of-dialogue
    }

    [Serializable]
    public class DialogueGraphData
    {
        public string id = "";
        public List<DialogueNodeData> nodes = new List<DialogueNodeData>();

        public DialogueNodeData Find(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].id == nodeId) return nodes[i];
            return null;
        }

        /// <summary>All nodes whose id starts with prefix (branch selection pool).</summary>
        public List<DialogueNodeData> FindByPrefix(string prefix)
        {
            var list = new List<DialogueNodeData>();
            if (string.IsNullOrEmpty(prefix)) return list;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].id.StartsWith(prefix)) list.Add(nodes[i]);
            return list;
        }
    }

    [Serializable]
    public class EncounterDefinitionData
    {
        public string id = "";
        public string npcName = "";
        public string graphId = "";
        public string startNodeId = "start";
    }

    // =====================================================================================
    // Progression definitions (data-driven player attributes, §progression): reputation
    // groups, abilities/skills, items/resources, areas. Gameplay code only ever uses these
    // ids + the names from here - new rows never require code changes.
    // =====================================================================================

    /// <summary>Ability families (UI grouping / future rules). Active = player-activated.</summary>
    public enum AbilityCategory
    {
        Active = 0,
        Passive = 1,
        Utility = 2
    }

    /// <summary>
    /// One behaviour row of an ability at a given level. Upgrade rows change the numbers,
    /// so "upgrading" genuinely changes how the ability behaves (cooldown/radius/power).
    /// </summary>
    [Serializable]
    public class AbilityLevelData
    {
        public int level = 1;          // 1-based
        public float cooldown;         // seconds between activations
        public float power;            // gameplay magnitude (effect-specific meaning)
        public float radius;           // effect radius (metres)
        public float duration;         // effect duration (seconds)
        public int energyCost;         // echoes consumed per activation (0 = free)
        public string description = "";// UI text of what this level does
    }

    /// <summary>
    /// Pure data definition of a power (task: ID, name, description, category, unlock
    /// conditions, required decisions/flags, energy/cost, cooldown, visual/audio refs,
    /// upgrade information). Unlocks happen through decisions via the UnlockAbility effect;
    /// the data declares the intended path (unlockConditions + hint) for UI and checks.
    /// </summary>
    [Serializable]
    public class AbilityDefinitionData
    {
        public string id = "";
        public string name = "";
        public string line = "";              // ember | tide | stone | hollow
        public string description = "";
        public AbilityCategory category = AbilityCategory.Active;
        public string unlockHint = "";        // UI "how to unlock" line
        public List<DecisionConditionData> unlockConditions = new List<DecisionConditionData>();
        public string vfxRef = "";            // visual reference (addressable/path key)
        public string sfxRef = "";            // audio reference
        public int echoCostPerLevel = 10;     // shrine upgrade cost rule (data-driven)
        public List<AbilityLevelData> levels = new List<AbilityLevelData>();

        public int MaxLevel { get { return levels != null && levels.Count > 0 ? levels.Count : 1; } }

        /// <summary>Row for a level (clamped to [1..MaxLevel]); fallback to level 1 row.</summary>
        public AbilityLevelData LevelRow(int level)
        {
            if (levels == null || levels.Count == 0) return null;
            for (int i = 0; i < levels.Count; i++)
                if (levels[i] != null && levels[i].level == level) return levels[i];
            return levels[0];
        }
    }

    [Serializable]
    public class SkillDefinitionData
    {
        public string id = "";
        public string name = "";
        public int maxLevel = 3;
    }

    [Serializable]
    public class ItemDefinitionData
    {
        public string id = "";
        public string name = "";
        public string description = "";
    }

    [Serializable]
    public class ReputationGroupData
    {
        public string id = "";
        public string name = "";
    }

    [Serializable]
    public class AreaDefinitionData
    {
        public string id = "";
        public string name = "";
    }

    [Serializable]
    public class ProgressionContentData
    {
        public List<AbilityDefinitionData> abilities = new List<AbilityDefinitionData>();
        public List<SkillDefinitionData> skills = new List<SkillDefinitionData>();
        public List<ItemDefinitionData> items = new List<ItemDefinitionData>();
        public List<ReputationGroupData> reputationGroups = new List<ReputationGroupData>();
        public List<AreaDefinitionData> areas = new List<AreaDefinitionData>();
    }

    /// <summary>Data-driven gate rule: when conditions match, the gate opens (or stays shut with text).</summary>
    [Serializable]
    public class GateRuleData
    {
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
        public bool opens;
        public string text = "";
    }

    // =====================================================================================
    // NPC definitions (§9 - cast & fate states). One character = one definition (this data)
    // + one prefab (scene NpcAgent.avatarPrefab when real meshes exist) + conditions that
    // drive their look, behaviour and dialogue. Adding a character = adding a row here;
    // the framework (NpcBrain/NpcLogic/NpcAgent) never changes.
    // =====================================================================================

    /// <summary>Behaviour presets the runtime understands (GAME_DESIGN §9.2: max 2 profiles per NPC).</summary>
    public enum NpcPersonality
    {
        Reserved = 0, // stands, watches, never approaches
        Friendly = 1, // approaches the player when idle and nearby
        Wary = 2,     // keeps distance: backs away if the player gets too close
        Curious = 3   // approaches, but stops at a respectful distance
    }

    /// <summary>Base movement/social behaviour numbers (overridable per fate-state).</summary>
    [Serializable]
    public class NpcBehaviourData
    {
        public NpcPersonality personality = NpcPersonality.Reserved;
        public bool facesPlayer = true;   // turn to face the player when they are near
        public float reactRadius = 4.5f;  // distance at which the NPC notices the player
        public float approachDistance = 0f; // >0: walks toward player when idle & in react radius
        public float avoidDistance = 0f;    // >0: steps back if the player comes closer than this
        public float talkDistance = 2.2f;   // stops approaching at this distance
        public float moveSpeed = 1.1f;      // m/s
        public float turnSpeed = 6f;        // rad-ish slerp factor per second
        public bool usesRoutine = false;    // walks the routine loop when the player is away
    }

    /// <summary>One waypoint of an NPC's routine loop (dwell = seconds idle at the stop).</summary>
    [Serializable]
    public class NpcStopData
    {
        public Point3 position;
        public float dwellSeconds = 2f;
    }

    /// <summary>
    /// A fate-state variant (GAME_DESIGN §5.4/§9.2): when the conditions match the player's
    /// current state, this NPC switches title, mood line and behaviour overrides.
    /// First matching state wins; -1 on an override means "inherit the base behaviour".
    /// </summary>
    [Serializable]
    public class NpcStateData
    {
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
        public string title = "";
        public string moodLine = "";        // one-liner shown when this state activates live
        public float approachDistance = -1f; // >0 overrides base; -1 inherit; 0 = never approach
        public float avoidDistance = -1f;    // >0 overrides base; -1 inherit; 0 = never avoid
        public float moveSpeed = -1f;        // >0 overrides base; -1 inherit
        public float reactRadius = -1f;      // >0 overrides base; -1 inherit
    }

    /// <summary>An available interaction: label + encounter (dialogue graph), condition-gated.</summary>
    [Serializable]
    public class NpcInteractionData
    {
        public string id = "talk";
        public string label = "Talk";       // INTERACT button label
        public string encounterId = "";     // dialogue graph to run (see EncounterDefinitionData)
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
    }

    /// <summary>
    /// Complete data-driven NPC definition (task: reusable NPC framework, modular data).
    /// The runtime resolves everything from here: display name, personality + behaviour,
    /// fate states (conditions -> title/mood/behaviour), available interactions,
    /// routine. Character visuals follow CHARACTER_REFERENCE sheetRef (one canonical
    /// character = one canonical prefab, consistent across scenes).
    /// </summary>
    [Serializable]
    public class NpcDefinitionData
    {
        public string id = "";              // unique NPC id (bond key, decision keys)
        public string displayName = "";     // "Mara"
        public string sheetRef = "";        // CHARACTER_REFERENCE sheet id (REF-02 ...)
        public string description = "";     // one-line personality blurb (journal/debug)
        public NpcBehaviourData behaviour = new NpcBehaviourData();
        public List<NpcStateData> states = new List<NpcStateData>();
        public List<NpcInteractionData> interactions = new List<NpcInteractionData>();
        public List<NpcStopData> routine = new List<NpcStopData>();

        public NpcInteractionData FindInteraction(string id)
        {
            if (interactions == null) return null;
            for (int i = 0; i < interactions.Count; i++)
                if (interactions[i] != null && interactions[i].id == id) return interactions[i];
            return null;
        }
    }

    // =====================================================================================
    // Combat definitions (core action system, Gameplay/Combat). All data: damage types,
    // attack shapes, health/defense numbers, status effects, ability attack payloads and
    // enemy archetypes. The combat runtime (CombatantState/DamageCalculator/EnemyBrain/
    // EnemyAgent/PlayerCombatController) never hardcodes a number - adding an enemy,
    // status or ability behaviour = adding rows here (mirrored in story_content.json).
    // =====================================================================================

    /// <summary>Damage channels (GAME_DESIGN §3 power lines + kinetic for plain force).</summary>
    public enum DamageType
    {
        Kinetic = 0,   // plain force (basic strikes, falls)
        Ember = 1,     // heat (ember_pulse line)
        Tide = 2,      // pressure/water (tide_mend line)
        Stone = 3,     // stillness/weight (stone_ward line)
        Hollow = 4     // the Choir's own channel (enemy archetypes)
    }

    /// <summary>How an attack finds its targets (more arrive with the real combat phase).</summary>
    public enum AttackDelivery
    {
        MeleeArc = 0,   // cone in front of the attacker (range + arcDegrees)
        RadiusPulse = 1 // radial burst around the attacker (radius)
    }

    /// <summary>One damage-type multiplier row on a combatant (1 = full damage, 0.5 = resisted, 1.25 = vulnerable).</summary>
    [Serializable]
    public class DamageResistEntry
    {
        public DamageType type = DamageType.Kinetic;
        public float multiplier = 1f;
    }

    /// <summary>
    /// Timed combat modifier (task: status effects). Pure data: duration, optional periodic
    /// health delta (DoT/HoT), movement/attack multipliers, damage immunity (dodge guard).
    /// </summary>
    [Serializable]
    public class StatusEffectDefinitionData
    {
        public string id = "";
        public string name = "";
        public string description = "";
        public float durationSeconds = 3f;
        public float tickIntervalSeconds = 0f;     // 0 = no periodic health change
        public int healthPerTick;                  // negative = damage over time, positive = heal
        public float moveSpeedMultiplier = 1f;     // applied to the combatant's locomotion
        public float attackRateMultiplier = 1f;    // applied to attack cooldowns (future)
        public bool grantsImmunity;                // dodge guard / stone ward frames
    }

    /// <summary>
    /// An attack shape + numbers (task: attack types + damage dealing). Used for the player's
    /// basic strike and for enemy attacks; ability attacks reuse these fields via AbilityCombatData.
    /// </summary>
    [Serializable]
    public class AttackDefinitionData
    {
        public string id = "";
        public string name = "";
        public DamageType damageType = DamageType.Kinetic;
        public AttackDelivery delivery = AttackDelivery.MeleeArc;
        public float baseDamage = 10f;
        public float range = 2.8f;                 // melee arc reach / pulse inner range
        public float arcDegrees = 110f;            // melee arc half-width in degrees
        public float radius = 0f;                  // pulse radius (0 = melee arc)
        public float windupSeconds = 0f;           // telegraph before the strike lands
        public float cooldownSeconds = 1f;
        public List<string> applyStatusIds = new List<string>(); // applied to the target on hit
    }

    /// <summary>
    /// Combat payload of an EXISTING ability (task: abilities must use the existing ability
    /// definitions, never duplicate them). Damage/heal scale with the ability's CURRENT level
    /// row (AbilityUsedEvent.power) - so upgrades genuinely change combat behaviour.
    /// </summary>
    [Serializable]
    public class AbilityCombatData
    {
        public string abilityId = "";
        public DamageType damageType = DamageType.Kinetic;
        public float damagePerPower = 8f;          // enemy damage = level-row power * this
        public float healPlayerPerPower;           // player heal  = level-row power * this
        public List<string> applyStatusToTargets = new List<string>();
        public List<string> applyStatusToPlayer = new List<string>();
    }

    /// <summary>
    /// Complete data-driven enemy archetype (task: enemy types). Health/defense/resistances,
    /// movement + detection numbers, its attack (AttackDefinitionData), activation conditions
    /// (combat encounters can be gated on story state) and on-defeat consequences delivered
    /// through the SAME EffectApplier whitelist decisions/objectives use - so defeating an
    /// enemy can move the world, NPCs and objectives with zero extra code.
    /// </summary>
    [Serializable]
    public class EnemyDefinitionData
    {
        public string id = "";
        public string displayName = "";
        public string description = "";
        public string sheetRef = "";               // CHARACTER_REFERENCE sheet (visual bible)
        public float maxHealth = 50f;
        public float defense = 2f;                 // flat mitigation after resistances
        public List<DamageResistEntry> resistances = new List<DamageResistEntry>();
        public float moveSpeed = 1.5f;
        public float turnSpeed = 5f;
        public float detectionRadius = 9f;         // Idle -> Alert
        public float leashRadius = 14f;            // Alert/Approach -> Idle (gives up)
        public float attackRange = 2.2f;
        public float staggerSeconds = 0.35f;       // TakeDamage reaction
        public AttackDefinitionData attack = new AttackDefinitionData();
        public List<DecisionConditionData> activationConditions = new List<DecisionConditionData>();
        public List<DecisionEffectData> onDefeatEffects = new List<DecisionEffectData>(); // EffectApplier
    }

    /// <summary>
    /// Player-side combat settings (health/defense + basic attack + dodge), plus the
    /// consequences of falling in combat - data-driven so a defeat NEVER destroys the save:
    /// it applies these effects (a counter, a bond, a flag...) and the player gets back up.
    /// </summary>
    [Serializable]
    public class CombatSettingsData
    {
        public float playerMaxHealth = 100f;
        public float playerDefense = 2f;
        public List<DamageResistEntry> playerResistances = new List<DamageResistEntry>();
        public AttackDefinitionData basicAttack = new AttackDefinitionData();
        public float dodgeDistance = 3.6f;
        public float dodgeDurationSeconds = 0.28f;
        public float dodgeCooldownSeconds = 1.6f;
        public string dodgeStatusId = "dodge_guard";
        public string healthVarKey = "player_hp";  // persisted between sessions (vars)
        public List<DecisionEffectData> onPlayerDefeat = new List<DecisionEffectData>();
    }

    // =====================================================================================
    // CAMPAIGN definitions (branching story framework). A campaign is fully data: chapters
    // contain story beats and branches; the CampaignManager only re-evaluates conditions
    // against live state - a designer adds chapters/branches through content, never code.
    // Beats resolve on a decision, an objective outcome (completed OR failed - failure is
    // a route, not a dead end), or plain conditions. Branches route the run and deliver
    // consequences/unlocks through the same EffectApplier whitelist everything else uses.
    // =====================================================================================

    /// <summary>What finishes a story beat.</summary>
    public enum BeatTrigger
    {
        Conditions = 0,        // resolveConditions become true (empty = resolves when offered)
        DecisionMade = 1,      // the named decision was made (any option)
        ObjectiveCompleted = 2,// the named objective reached Completed
        ObjectiveFailed = 3    // the named objective reached Failed - failure branches the story
    }

    /// <summary>One story beat: a journal moment + its trigger + consequences + branch point.</summary>
    [Serializable]
    public class StoryBeatData
    {
        public string id = "";
        public string title = "";
        public string journalText = "";              // added to the story log when resolved
        public List<DecisionConditionData> offerConditions = new List<DecisionConditionData>();
        public BeatTrigger resolveTrigger = BeatTrigger.Conditions;
        public string resolveKey = "";               // decisionId / objectiveId ("" for Conditions)
        public List<DecisionConditionData> resolveConditions = new List<DecisionConditionData>();
        public List<string> requiredBeatIds = new List<string>(); // small graph: non-linear ordering
        public List<DecisionEffectData> onResolveEffects = new List<DecisionEffectData>();
        public int priority;                          // availability ordering for the UI (lower = sooner)
    }

    /// <summary>A route out of a beat: first matching branch (authored order) fires.</summary>
    [Serializable]
    public class CampaignBranchData
    {
        public string id = "";
        public string fromBeatId = "";
        public string toBeatId = "";                  // the beat this route feeds (docs/tests/validation)
        public string label = "";                     // player-facing path name ("Path of Ember")
        public List<DecisionConditionData> requiredConditions = new List<DecisionConditionData>();
        public List<DecisionEffectData> effects = new List<DecisionEffectData>(); // consequences + unlocks
    }

    /// <summary>A chapter: entry-gated beats + branches + completion. Multiple chapters may be
    /// active at once (non-linear); completion unlocks whatever its effects say.</summary>
    [Serializable]
    public class CampaignChapterData
    {
        public string id = "";
        public string title = "";
        public string subtitle = "";
        public string description = "";
        public List<DecisionConditionData> entryConditions = new List<DecisionConditionData>();
        public List<StoryBeatData> beats = new List<StoryBeatData>();
        public List<CampaignBranchData> branches = new List<CampaignBranchData>();
        public List<DecisionConditionData> completionConditions = new List<DecisionConditionData>();
        public List<DecisionEffectData> completionEffects = new List<DecisionEffectData>();
        public string completionJournal = "";
    }

    // =====================================================================================
    // Objective / mission definitions (GAME_DESIGN §5-§7: "objectives are authored per
    // path"; DEVELOPMENT_PLAN M2 systems core). Everything is plain serializable data:
    // a mission = one ObjectiveDefinitionData row; the ObjectiveManager never hardcodes
    // a mission. Objectives react to decisions through the same condition whitelist as
    // dialogue (offerConditions), complete/fail on live state, and deliver consequences
    // through the same effect whitelist (EffectApplier - single write path).
    // =====================================================================================

    /// <summary>Mission categories (UI grouping + tone).</summary>
    public enum ObjectiveType
    {
        Main = 0,        // the current chapter's spine (one per path)
        Side = 1,        // optional content opened by a state change
        Crisis = 2,      // timed/failable pressure (failure has consequences)
        Recovery = 3     // offered after a failure to repair the damage
    }

    /// <summary>
    /// One checklist step of an objective: a short player-facing line + the state
    /// conditions that mark it done. Steps that are already true tick themselves
    /// (the world may have moved ahead of the mission text).
    /// </summary>
    [Serializable]
    public class ObjectiveStepData
    {
        public string text = "";
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
    }

    /// <summary>
    /// Complete data-driven objective definition. Fields per the mission-system spec:
    /// unique ID, title, description, type, requirements (offerConditions), completion
    /// conditions, failure conditions (where appropriate), consequences, follow-up
    /// objectives. Optional counter (var + target) gives "n/N" progress; steps give a
    /// checklist. No code changes are needed to add missions - only rows here.
    /// </summary>
    [Serializable]
    public class ObjectiveDefinitionData
    {
        public string id = "";
        public string title = "";
        public string description = "";
        public ObjectiveType type = ObjectiveType.Main;
        public string areaId = "";               // where this plays out (UI flavor)
        public string giverNpcId = "";           // who it comes from, if anyone ("" = the world)

        // requirements - when the objective becomes available to the player
        public List<DecisionConditionData> offerConditions = new List<DecisionConditionData>();
        public bool autoActivate = true;         // main-path objectives track themselves once offered

        // completion - all must hold (plus every step + the counter target)
        public List<DecisionConditionData> completeConditions = new List<DecisionConditionData>();

        // failure - fires while the objective is Available/Active (leave empty for unfailable)
        public List<DecisionConditionData> failConditions = new List<DecisionConditionData>();

        // measurable progress (optional): "n/N counterText" + ordered checklist
        public string counterVar = "";
        public int counterTarget;
        public string counterText = "";
        public List<ObjectiveStepData> steps = new List<ObjectiveStepData>();

        // consequences - applied through EffectApplier on resolution (single write path)
        public List<DecisionEffectData> consequences = new List<DecisionEffectData>();
        public List<DecisionEffectData> failureConsequences = new List<DecisionEffectData>();

        // follow-ups - objective ids released for offering once this one resolves
        public List<string> followUps = new List<string>();

        public string completionNotice = "";     // important toast text
        public string failureNotice = "";        // important toast text

        public bool UsesCounter { get { return !string.IsNullOrEmpty(counterVar) && counterTarget > 0; } }
    }

    /// <summary>
    /// A world interaction whose availability is part of tracked world state: when the
    /// conditions first pass, the unlock key is persisted (InteractionUnlockedEvent) -
    /// so "what this player may touch" survives restarts and differs per path.
    /// </summary>
    [Serializable]
    public class WorldInteractionData
    {
        public string key = "";
        public string label = "";
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
    }

    [Serializable]
    public class StoryContentData
    {
        public List<EncounterDefinitionData> encounters = new List<EncounterDefinitionData>();
        public List<DecisionNodeData> decisions = new List<DecisionNodeData>();
        public List<DialogueGraphData> graphs = new List<DialogueGraphData>();
        public ProgressionContentData progression = new ProgressionContentData();
        public List<NpcDefinitionData> npcs = new List<NpcDefinitionData>();
        public List<ObjectiveDefinitionData> objectives = new List<ObjectiveDefinitionData>();
        public List<WorldInteractionData> worldInteractions = new List<WorldInteractionData>();

        // ---- combat content (core action system) ----
        public List<CampaignChapterData> chapters = new List<CampaignChapterData>();
        public List<StatusEffectDefinitionData> statusEffects = new List<StatusEffectDefinitionData>();
        public List<AbilityCombatData> abilityCombat = new List<AbilityCombatData>();
        public List<EnemyDefinitionData> enemies = new List<EnemyDefinitionData>();
        public CombatSettingsData combat = new CombatSettingsData();

        public EncounterDefinitionData FindEncounter(string id)
        {
            for (int i = 0; i < encounters.Count; i++) if (encounters[i] != null && encounters[i].id == id) return encounters[i];
            return null;
        }
        public DecisionNodeData FindDecision(string id)
        {
            for (int i = 0; i < decisions.Count; i++) if (decisions[i] != null && decisions[i].id == id) return decisions[i];
            return null;
        }
        public DialogueGraphData FindGraph(string id)
        {
            for (int i = 0; i < graphs.Count; i++) if (graphs[i] != null && graphs[i].id == id) return graphs[i];
            return null;
        }
        public NpcDefinitionData FindNpc(string id)
        {
            if (npcs == null) return null;
            for (int i = 0; i < npcs.Count; i++) if (npcs[i] != null && npcs[i].id == id) return npcs[i];
            return null;
        }
        public ObjectiveDefinitionData FindObjective(string id)
        {
            if (objectives == null) return null;
            for (int i = 0; i < objectives.Count; i++) if (objectives[i] != null && objectives[i].id == id) return objectives[i];
            return null;
        }
        public WorldInteractionData FindWorldInteraction(string key)
        {
            if (worldInteractions == null) return null;
            for (int i = 0; i < worldInteractions.Count; i++) if (worldInteractions[i] != null && worldInteractions[i].key == key) return worldInteractions[i];
            return null;
        }
        public CampaignChapterData FindChapter(string id)
        {
            if (chapters == null) return null;
            for (int i = 0; i < chapters.Count; i++)
                if (chapters[i] != null && chapters[i].id == id) return chapters[i];
            return null;
        }

        public StoryBeatData FindBeat(string beatId, out CampaignChapterData owner)
        {
            owner = null;
            if (chapters == null) return null;
            for (int i = 0; i < chapters.Count; i++)
            {
                CampaignChapterData ch = chapters[i];
                if (ch == null || ch.beats == null) continue;
                for (int b = 0; b < ch.beats.Count; b++)
                    if (ch.beats[b] != null && ch.beats[b].id == beatId) { owner = ch; return ch.beats[b]; }
            }
            return null;
        }

        public StatusEffectDefinitionData FindStatusEffect(string id)
        {
            if (statusEffects == null) return null;
            for (int i = 0; i < statusEffects.Count; i++) if (statusEffects[i] != null && statusEffects[i].id == id) return statusEffects[i];
            return null;
        }
        public AbilityCombatData FindAbilityCombat(string abilityId)
        {
            if (abilityCombat == null) return null;
            for (int i = 0; i < abilityCombat.Count; i++) if (abilityCombat[i] != null && abilityCombat[i].abilityId == abilityId) return abilityCombat[i];
            return null;
        }
        public EnemyDefinitionData FindEnemy(string id)
        {
            if (enemies == null) return null;
            for (int i = 0; i < enemies.Count; i++) if (enemies[i] != null && enemies[i].id == id) return enemies[i];
            return null;
        }
    }
}
