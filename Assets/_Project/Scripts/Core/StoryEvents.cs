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

    /// <summary>Fired after a choice is committed and effects applied.</summary>
    public struct DecisionResolvedEvent
    {
        public string decisionId;
        public string optionId;
        public string summary;
        public List<AffinityDelta> affinityDeltas;
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
