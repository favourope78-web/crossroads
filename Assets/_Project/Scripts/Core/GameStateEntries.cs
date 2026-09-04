using System;

namespace Crossroads.Core
{
    /// <summary>Serializable string -> string entry (JsonUtility-safe alternative to Dictionary).</summary>
    [Serializable]
    public class StringEntry
    {
        public string key = "";
        public string value = "";
        public StringEntry() { }
        public StringEntry(string k, string v) { key = k; value = v; }
    }

    /// <summary>Serializable string -> int entry.</summary>
    [Serializable]
    public class StringIntEntry
    {
        public string key = "";
        public int value;
        public StringIntEntry() { }
        public StringIntEntry(string k, int v) { key = k; value = v; }
    }

    /// <summary>Serializable string -> bool entry.</summary>
    [Serializable]
    public class StringBoolEntry
    {
        public string key = "";
        public bool value;
        public StringBoolEntry() { }
        public StringBoolEntry(string k, bool v) { key = k; value = v; }
    }

    /// <summary>A resolved decision (persisted). Written by DecisionManager via StateMutator.</summary>
    [Serializable]
    public class ResolvedDecisionEntry
    {
        public string decisionId = "";
        public string optionId = "";
        public string summary = "";
        public string resolvedAt = "";
        public ResolvedDecisionEntry() { }
        public ResolvedDecisionEntry(string decisionId, string optionId, string summary)
        {
            this.decisionId = decisionId;
            this.optionId = optionId;
            this.summary = summary;
            resolvedAt = DateTime.UtcNow.ToString("s");
        }
    }

    /// <summary>Objective runtime state (persisted). Phase + measurable progress.</summary>
    [Serializable]
    public class ObjectiveProgressEntry
    {
        public string id = "";
        public int phase;          // ObjectivePhase (int: JsonUtility-safe)
        public int progress;       // steps/counter units passed when last persisted
        public ObjectiveProgressEntry() { }
        public ObjectiveProgressEntry(string id, int phase, int progress)
        {
            this.id = id; this.phase = phase; this.progress = progress;
        }
    }

    /// <summary>Affinity delta delivered to UI feedback ("Ember +10 -> 25").</summary>
    [Serializable]
    public class AffinityDelta
    {
        public string line = "";
        public int amount;
        public int newTotal;
        public AffinityDelta() { }
        public AffinityDelta(string line, int amount, int newTotal)
        {
            this.line = line; this.amount = amount; this.newTotal = newTotal;
        }
    }

    /// <summary>Canonical affinity line identifiers (GAME_DESIGN §3.2).</summary>
    public static class AffinityLine
    {
        public const string Ember = "ember";
        public const string Tide = "tide";
        public const string Stone = "stone";
        public const string Hollow = "hollow";

        public static string DisplayName(string line)
        {
            switch (line)
            {
                case Ember: return "Ember";
                case Tide: return "Tide";
                case Stone: return "Stone";
                case Hollow: return "Hollow";
                default: return line;
            }
        }

        public static bool TryParse(string s, out string line)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "ember": case "fire": case "red": line = Ember; return true;
                case "tide": case "water": case "teal": line = Tide; return true;
                case "stone": case "earth": case "ochre": line = Stone; return true;
                case "hollow": case "void": line = Hollow; return true;
                default: line = s; return false;
            }
        }
    }
}
