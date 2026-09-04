using System.Collections.Generic;

namespace Crossroads.Core
{
    // =====================================================================================
    // CAMPAIGN events (branching story runtime, Gameplay/Campaign). The campaign layer
    // is a PURE re-evaluation engine over GameState: it never owns state, it derives
    // everything from decisions/objectives/world state and records what it derived
    // (resolved beats, taken branches, completed chapters) back through StateMutator.
    // =====================================================================================

    /// <summary>A chapter's entry conditions passed and it is now part of the run.</summary>
    public struct CampaignChapterStartedEvent
    {
        public string chapterId;
        public string title;
    }

    /// <summary>A story beat's trigger fired (decision made / objective resolved / conditions met).</summary>
    public struct CampaignBeatResolvedEvent
    {
        public string beatId;
        public string chapterId;
        public string title;
        public string journalText;
    }

    /// <summary>One of a beat's branches matched and its consequences were applied.</summary>
    public struct CampaignBranchTakenEvent
    {
        public string branchId;
        public string fromBeatId;
        public string toBeatId;
        public string label;
    }

    /// <summary>A chapter's completion conditions passed; its completion effects are applied.</summary>
    public struct CampaignChapterCompletedEvent
    {
        public string chapterId;
        public string title;
        public string journalText;
    }

    /// <summary>Coarse "anything in the campaign moved" signal for UI refreshes.</summary>
    public struct CampaignChangedEvent
    {
        public List<string> snapshotChapterIds; // active chapter ids at publish time
    }
}
