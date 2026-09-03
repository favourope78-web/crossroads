using System;
using System.Collections.Generic;

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
        CodexOwned         // codex contains key
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
        GrantEchoes
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

    [Serializable]
    public class StoryContentData
    {
        public List<EncounterDefinitionData> encounters = new List<EncounterDefinitionData>();
        public List<DecisionNodeData> decisions = new List<DecisionNodeData>();
        public List<DialogueGraphData> graphs = new List<DialogueGraphData>();

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
    }
}
