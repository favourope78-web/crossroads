namespace Crossroads.Narrative
{
    /// <summary>
    /// Content provider contract. Implemented by the ScriptableObject library asset
    /// (authorable content) and by RuntimeContentSource (code-built fallback + headless tests),
    /// so the runner never cares where the content came from.
    /// </summary>
    public interface IEncounterSource
    {
        StoryContentData Content { get; }
        EncounterDefinitionData GetEncounter(string id);
        DecisionNodeData GetDecision(string id);
        DialogueGraphData GetGraph(string id);
    }
}
