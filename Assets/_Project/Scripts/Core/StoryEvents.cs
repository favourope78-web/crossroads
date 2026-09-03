using System;
using System.Collections.Generic;

namespace Crossroads.Core
{
    // ------------------------------------------------------------------ UI / interaction events
    /// <summary>Fired by PlayerInteraction when the nearest interactable changes.</summary>
    public struct InteractPromptEvent
    {
        public bool visible;
        public string label;
        public string interactableId;
        public float priority;
    }

    /// <summary>Fired when an encounter dialogue run starts.</summary>
    public struct DialogueStartedEvent
    {
        public string encounterId;
        public string npcTitle;   // e.g. "Mara · Bonded" (from NpcFateDriver) - shown as a chip
    }

    /// <summary>Fired for every spoken line of the encounter (UI renders + typewriter).</summary>
    public struct DialogueLineEvent
    {
        public string encounterId;
        public string speaker;
        public string text;
        public bool hasNext;
    }

    /// <summary>Fired when the encounter ends (UI closes the sheet).</summary>
    public struct DialogueEndedEvent
    {
        public string encounterId;
    }

    /// <summary>A selectable choice view (UI-agnostic payload).</summary>
    [Serializable]
    public class DecisionChoiceView
    {
        public string optionId = "";
        public string text = "";
        public DecisionChoiceView() { }
        public DecisionChoiceView(string id, string textValue) { optionId = id; text = textValue; }
    }

    /// <summary>Fired when the runner needs the player to choose (3 options for the first encounter).</summary>
    public struct DecisionPromptEvent
    {
        public string decisionId;
        public string promptText;
        public List<DecisionChoiceView> choices;
        public float timeLimitSeconds;      // 0 = untimed (D1); >0 = pressure choice (D2)
        public int timeoutOptionIndex;      // auto-resolved option when the timer expires
    }

    /// <summary>A short, UI-ready line describing what a choice changed (toast/hud).</summary>
    [Serializable]
    public struct ChangeNotice
    {
        public string category;   // affinity | rep | bond | ability | item | skill | area | resource | flag | codex | world
        public string text;       // e.g. "Ember +10 (10)", "Ability: Ember Pulse", "Mara +10"

        public ChangeNotice(string category, string text) { this.category = category; this.text = text; }
    }

    /// <summary>Fired after a choice is committed and effects applied.</summary>
    public struct DecisionResolvedEvent
    {
        public string decisionId;
        public string optionId;
        public string summary;
        public List<AffinityDelta> affinityDeltas;
        public List<ChangeNotice> notices;   // brief "what changed" indication (§change-notice rule)
    }

    // ------------------------------------------------------------------ state change events
    public struct FlagChangedEvent
    {
        public string key;
        public string value;
    }

    public struct AffinityChangedEvent
    {
        public string line;
        public int delta;
        public int total;
    }

    public struct BondChangedEvent
    {
        public string npcId;
        public int delta;
        public int total;
    }

    public struct WorldStateChangedEvent
    {
        public string areaKey;
        public string variantKey;
    }

    // ------------------------------------------------------------------ progression events
    public struct ReputationChangedEvent
    {
        public string groupId;
        public int delta;
        public int total;
    }

    public struct AbilityUnlockedEvent
    {
        public string abilityId;
    }

    public struct SkillChangedEvent
    {
        public string skillId;
        public int delta;
        public int level;
    }

    public struct ItemChangedEvent
    {
        public string itemId;
        public bool added;
        public int count;
    }

    public struct AreaUnlockedEvent
    {
        public string areaId;
    }

    public struct AreaChangedEvent
    {
        public string areaId;
    }

    /// <summary>Generic one-shot notice (gates, pickups, world events) for the toast UI.</summary>
    public struct NoticeRequestEvent
    {
        public string text;
    }

    /// <summary>
    /// Fired when an NPC's state-driven look/behaviour/interactions change
    /// (bond, flag, decision, item, reputation...). UI uses it for nameplates,
    /// the report uses it as the live "NPC reacts to you" signal.
    /// </summary>
    public struct NpcStatusChangedEvent
    {
        public string npcId;
        public string title;      // resolved display title (may include tier/mood)
        public int bond;
        public string bondTier;   // Hostile | Wary | New | Warm | Bonded | Kin
        public string moodLine;   // authored one-liner describing the reaction ("" = none)
    }

    public struct EntityStateChangedEvent
    {
        public string entityKey;
        public bool active;
    }

    // ------------------------------------------------------------------ save / lifecycle events
    /// <summary>Fired when a save file is (re)loaded at boot.</summary>
    public struct StateLoadedEvent
    {
        public bool hadSave;
        public string path;
    }

    public struct SaveCompletedEvent
    {
        public bool ok;
        public string path;
        public int decisionCount;
        public string error;
    }

    public struct StateResetEvent
    {
    }

    public struct InputLockEvent
    {
        public bool locked;
        public string reason;
    }
}
