namespace Crossroads.Narrative
{
    /// <summary>
    /// Code-built content fallback: used when the scene's StoryContentLibrary asset is
    /// missing (fresh checkout, asset pipeline failure) and by the headless tests.
    /// Keeps FirstLocation playable in every case; the .asset remains the authored path.
    /// </summary>
    public class RuntimeContentSource : IEncounterSource
    {
        private readonly StoryContentData _content;

        public RuntimeContentSource(StoryContentData content)
        {
            _content = content ?? StoryContentBuilder.CreateFirstLightContent();
        }

        public RuntimeContentSource() : this(StoryContentBuilder.CreateFirstLightContent())
        {
        }

        public StoryContentData Content { get { return _content; } }
        public EncounterDefinitionData GetEncounter(string id) { return _content.FindEncounter(id); }
        public DecisionNodeData GetDecision(string id) { return _content.FindDecision(id); }
        public DialogueGraphData GetGraph(string id) { return _content.FindGraph(id); }
    }
}
