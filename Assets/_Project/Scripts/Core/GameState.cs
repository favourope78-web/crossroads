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

        public List<ResolvedDecisionEntry> decisions = new List<ResolvedDecisionEntry>();
        public List<string> codex = new List<string>();

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
    }
}
