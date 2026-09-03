using UnityEngine;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Thin ScriptableObject carriers for the data-driven content (GAME_DESIGN §4.2).
    /// The content itself lives in the plain serializable POCOs (ContentData.cs) so the
    /// same payload is used by the headless tests and, in editor, by hand-authored assets.
    /// Future encounters = new .asset files under Assets/_Project/Data/Decisions|Dialogue.
    /// </summary>
    public class DecisionNodeAsset : ScriptableObject
    {
        [Tooltip("Plain-data decision definition; see GameState/DecisionManager.")]
        public DecisionNodeData data = new DecisionNodeData();
    }

    public class DialogueGraphAsset : ScriptableObject
    {
        public DialogueGraphData data = new DialogueGraphData();
    }

    public class StoryContentLibraryAsset : ScriptableObject, IEncounterSource
    {
        public StoryContentData data = new StoryContentData();

        public StoryContentData Content { get { return data; } }

        public EncounterDefinitionData GetEncounter(string id) { return data != null ? data.FindEncounter(id) : null; }
        public DecisionNodeData GetDecision(string id) { return data != null ? data.FindDecision(id) : null; }
        public DialogueGraphData GetGraph(string id) { return data != null ? data.FindGraph(id) : null; }
    }
}
