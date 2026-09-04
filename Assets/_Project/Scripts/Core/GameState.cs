using System;
using System.Collections.Generic;

namespace Crossroads.Core
{
    /// <summary>
    /// The single authoritative run state (GAME_DESIGN §4.3). Plain serializable
    /// class; every write goes through StateMutator (design rule: saves, telemetry
    /// and the codex observe all changes in one place).
    /// Dictionaries are represented as entry lists because Unity's JsonUtility
    /// cannot serialize Dictionary fields - the read/write API stays dictionary-like.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public int chapterId = 0;
        public int levelId = 0;

        public List<StringEntry> flags = new List<StringEntry>();
        public List<StringEntry> worldStates = new List<StringEntry>();   // area key -> variant key
        public List<StringBoolEntry> entities = new List<StringBoolEntry>(); // persistable spawn/entity toggles
        public List<StringIntEntry> vars = new List<StringIntEntry>();
        public List<StringIntEntry> bonds = new List<StringIntEntry>();   // npcId -> -100..100

        // ---- progression attributes (all decision-driven, all persisted) ----
        public List<StringIntEntry> reputation = new List<StringIntEntry>(); // groupId -> -100..100
        public List<StringEntry> abilities = new List<StringEntry>();        // unlocked ability ids
        public List<StringEntry> blockedAbilities = new List<StringEntry>(); // excluded by player choices
        public List<StringIntEntry> abilityLevels = new List<StringIntEntry>(); // abilityId -> level (1..max)
        public List<StringEntry> items = new List<StringEntry>();            // carried item ids (stacking)
        public List<StringIntEntry> skills = new List<StringIntEntry>();     // skillId -> level
        public List<StringEntry> unlockAreas = new List<StringEntry>();      // accessible area ids
        public string currentArea = "hall";                                  // where the player is now

        public List<ResolvedDecisionEntry> decisions = new List<ResolvedDecisionEntry>();
        public List<string> codex = new List<string>();

        // ---- world & mission state (v4: objective runtime, npc locations, interaction unlocks) ----
        public List<ObjectiveProgressEntry> objectives = new List<ObjectiveProgressEntry>(); // objective id -> phase/progress
        public List<StringEntry> npcLocations = new List<StringEntry>();      // npcId -> location key (MoveNpc effect)
        public List<StringEntry> interactionUnlocks = new List<StringEntry>(); // unlock key -> "1" (condition-gated, persisted)
        public List<StringEntry> closedAreas = new List<StringEntry>();       // area ids re-sealed after being opened

        // Affinity meters 0..100 (GAME_DESIGN §3.2). Hollow stays hidden in UI.
        public int ember;
        public int tide;
        public int stone;
        public int hollow;

        public int echoBank; // "Echoes" currency (GAME_DESIGN §3.3), used by later systems

        // ---- lookup helpers (read-only; writes belong to StateMutator) ----
        public string GetFlag(string key, string fallback = "") { return GetEntry(flags, key, fallback); }
        public string GetWorldState(string areaKey, string fallback = "") { return GetEntry(worldStates, areaKey, fallback); }
        public int GetVar(string key, int fallback = 0) { return GetEntry(vars, key, fallback); }
        public int GetBond(string npcId) { return GetEntry(bonds, npcId, 0); }
        public bool GetEntity(string key, bool fallback = false) { return GetBoolEntry(entities, key, fallback); }
        public bool HasFlag(string key) { return FindEntry(flags, key) != null; }

        // ---- progression helpers (read-only; writes belong to StateMutator) ----
        public int GetReputation(string groupId, int fallback = 0) { return GetEntry(reputation, groupId, fallback); }
        public bool HasAbility(string abilityId) { return ContainsKey(abilities, abilityId); }
        public int AbilityCount(string abilityId) { return CountKeys(abilities, abilityId); }
        public bool HasBlockedAbility(string abilityId) { return ContainsKey(blockedAbilities, abilityId); }
        public int GetAbilityLevel(string abilityId, int fallback = 0) { return GetEntry(abilityLevels, abilityId, fallback); }
        public bool HasItem(string itemId) { return ContainsKey(items, itemId); }
        public int ItemCount(string itemId) { return CountKeys(items, itemId); }
        public int GetSkill(string skillId, int fallback = 0) { return GetEntry(skills, skillId, fallback); }
        public bool IsAreaUnlocked(string areaId) { return ContainsKey(unlockAreas, areaId); }
        public string CurrentArea { get { return string.IsNullOrEmpty(currentArea) ? "hall" : currentArea; } }
        public bool HasCodex(string id) { return codex != null && codex.Contains(id); }
        public ResolvedDecisionEntry GetDecision(string decisionId)
        {
            if (decisions == null) return null;
            for (int i = 0; i < decisions.Count; i++)
                if (decisions[i] != null && decisions[i].decisionId == decisionId) return decisions[i];
            return null;
        }
        public bool HasDecision(string decisionId) { return GetDecision(decisionId) != null; }
        public string DecisionOption(string decisionId)
        {
            var d = GetDecision(decisionId);
            return d != null ? d.optionId : "";
        }

        public int GetAffinity(string line)
        {
            switch ((line ?? "").ToLowerInvariant())
            {
                case "ember": return ember;
                case "tide": return tide;
                case "stone": return stone;
                case "hollow": return hollow;
                default: return 0;
            }
        }

        // ---- world & mission lookups (read-only; writes belong to StateMutator) ----
        public ObjectiveProgressEntry GetObjectiveEntry(string objectiveId)
        {
            if (objectives == null) return null;
            for (int i = 0; i < objectives.Count; i++)
                if (objectives[i] != null && objectives[i].id == objectiveId) return objectives[i];
            return null;
        }

        public int GetObjectivePhase(string objectiveId, int fallback = (int)ObjectivePhase.Hidden)
        {
            var e = GetObjectiveEntry(objectiveId);
            return e != null ? e.phase : fallback;
        }

        public int GetObjectiveProgress(string objectiveId, int fallback = 0)
        {
            var e = GetObjectiveEntry(objectiveId);
            return e != null ? e.progress : fallback;
        }

        public string GetNpcLocation(string npcId, string fallback = "")
        {
            return GetEntry(npcLocations, npcId, fallback);
        }

        public bool HasInteractionUnlock(string unlockKey)
        {
            return ContainsKey(interactionUnlocks, unlockKey);
        }

        public bool IsAreaClosed(string areaId)
        {
            return ContainsKey(closedAreas, areaId);
        }

        public void CopyAffinitiesFrom(GameState other)
        {
            ember = other.ember; tide = other.tide; stone = other.stone; hollow = other.hollow;
        }

        // ---- internal list helpers ----
        private static string GetEntry(List<StringEntry> list, string key, string fallback)
        {
            var e = FindEntry(list, key);
            return e != null ? e.value : fallback;
        }
        private static int GetEntry(List<StringIntEntry> list, string key, int fallback)
        {
            var e = FindEntry(list, key);
            return e != null ? e.value : fallback;
        }
        private static bool GetBoolEntry(List<StringBoolEntry> list, string key, bool fallback)
        {
            var e = FindEntry(list, key);
            return e != null ? e.value : fallback;
        }
        internal static StringEntry FindEntry(List<StringEntry> list, string key)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].key == key) return list[i];
            return null;
        }
        internal static StringIntEntry FindEntry(List<StringIntEntry> list, string key)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].key == key) return list[i];
            return null;
        }
        internal static StringBoolEntry FindEntry(List<StringBoolEntry> list, string key)
        {
            if (list == null) return null;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].key == key) return list[i];
            return null;
        }

        private static bool ContainsKey(List<StringEntry> list, string key)
        {
            return FindEntry(list, key) != null;
        }

        private static int CountKeys(List<StringEntry> list, string key)
        {
            if (list == null) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].key == key) n++;
            return n;
        }
    }
}
