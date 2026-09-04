using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// The branching-story runtime (campaign framework). A PURE evaluation engine:
    ///   Chapter -> Story Beat -> (Encounter/Decision/Objective already in the world) ->
    ///   Beat resolves -> Branch routes the run -> Consequences/Unlocks -> Next beats.
    ///
    /// It owns NO state of its own - everything derives from GameState (decisions,
    /// NPC bonds, abilities, world state, objective outcomes, flags/vars) and what it
    /// derived is recorded back through StateMutator (resolved beats, taken branches,
    /// completed chapters, journal). That makes it idempotent, save/load-transparent
    /// and headless-testable: re-running Refresh() after a load reproduces the route.
    ///
    /// Non-linear by construction: chapters gate on entry CONDITIONS (not order) so
    /// several can be live at once; beats order through small requiredBeatIds graphs,
    /// not strict sequences; objective FAILURE is a first-class trigger - failing a
    /// mission routes the story down a different branch instead of ending it.
    /// </summary>
    public class CampaignManager
    {
        private readonly List<CampaignChapterData> _chapters;
        private readonly GameStateManager _progress;

        private readonly List<CampaignChapterData> _active = new List<CampaignChapterData>();
        private readonly List<StoryBeatData> _available = new List<StoryBeatData>();
        private bool _dirty;

        public CampaignManager(List<CampaignChapterData> chapters, GameStateManager progress)
        {
            _chapters = chapters ?? new List<CampaignChapterData>();
            _progress = progress;
        }

        public GameStateManager Progress { get { return _progress; } }

        // ---------------------------------------------------------------- lifecycle
        public void BindEvents()
        {
            EventBus.Subscribe<DecisionResolvedEvent>(OnStateChanged);
            EventBus.Subscribe<ObjectiveChangedEvent>(OnStateChanged);
            EventBus.Subscribe<AbilityUnlockedEvent>(OnStateChanged);
            EventBus.Subscribe<VarChangedEvent>(OnStateChanged);
            EventBus.Subscribe<EntityStateChangedEvent>(OnStateChanged);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        public void UnbindEvents()
        {
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnStateChanged);
            EventBus.Unsubscribe<ObjectiveChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnStateChanged);
            EventBus.Unsubscribe<VarChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<EntityStateChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void OnStateChanged<T>(T e)
        {
            Refresh(); // cheap: bounded condition re-evaluation, zero allocations below the UI layer
        }

        private void OnStateReset(StateResetEvent e)
        {
            _active.Clear();
            _available.Clear();
            Refresh();
        }

        /// <summary>Snapshot accessors for the UI and tests (rebuilt during Refresh).</summary>
        public List<CampaignChapterData> ActiveChapters { get { return _active; } }
        public List<StoryBeatData> AvailableBeats { get { return _available; } }

        /// <summary>The beat the UI should point at next (lowest priority number among available).</summary>
        public StoryBeatData CurrentBeat
        {
            get
            {
                StoryBeatData best = null;
                for (int i = 0; i < _available.Count; i++)
                    if (best == null || _available[i].priority < best.priority) best = _available[i];
                return best;
            }
        }

        public CampaignChapterData ChapterOf(StoryBeatData beat)
        {
            if (beat == null) return null;
            for (int i = 0; i < _chapters.Count; i++)
            {
                CampaignChapterData ch = _chapters[i];
                if (ch == null || ch.beats == null) continue;
                for (int b = 0; b < ch.beats.Count; b++)
                    if (ch.beats[b] == beat) return ch;
            }
            return null;
        }

        /// <summary>The journal (story log), oldest-first, as recorded in the save.</summary>
        public List<string> Journal
        {
            get { return _progress != null && _progress.State != null ? _progress.State.State.campaignJournal : null; }
        }

        // ---------------------------------------------------------------- evaluation
        /// <summary>
        /// Full re-evaluation. Idempotent; safe to call on boot, on any state event and
        /// after a load. Cascades (a branch may unlock the next beat) by iterating until
        /// stable with a bounded pass count.
        /// </summary>
        public void Refresh()
        {
            if (_progress == null || _progress.State == null) return;

            _dirty = false;
            bool outer = true;
            int guard = 0;
            while (outer && guard++ < 4) // a completion may unlock+start the next chapter in one pass
            {
                outer = false;
                ActivateChapters();

                // resolve beats until stable (branches/effects may cascade)
                int passes = 0, maxPasses = TotalBeatCount() + 2;
                bool changed = true;
                while (changed && passes++ < maxPasses)
                {
                    changed = false;
                    for (int i = 0; i < _active.Count; i++)
                    {
                        CampaignChapterData ch = _active[i];
                        if (ch.beats == null) continue;
                        for (int b = 0; b < ch.beats.Count; b++)
                        {
                            StoryBeatData beat = ch.beats[b];
                            if (beat == null || _progress.State.State.CampaignBeatResolved(beat.id)) continue;
                            if (!PrereqsResolved(beat, _progress.State.State)) continue;
                            if (!ConditionEvaluator.Evaluate(beat.offerConditions, _progress.State)) continue;
                            if (!TriggerSatisfied(beat, _progress)) continue;
                            Resolve(beat, ch);
                            changed = true;
                        }
                    }
                }

                // chapter completion: if anything completed, loop once more so a chapter
                // whose entry flag JUST appeared can activate, journal and resolve in-kind
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    CampaignChapterData ch = _active[i];
                    if (!ConditionEvaluator.Evaluate(ch.completionConditions, _progress.State)) continue;
                    CompleteChapter(ch);
                    _active.RemoveAt(i);
                    outer = true;
                }
            }

            RebuildAvailable();

            if (_dirty)
            {
                var ids = new List<string>();
                for (int i = 0; i < _active.Count; i++) ids.Add(_active[i].id);
                EventBus.Publish(new CampaignChangedEvent { snapshotChapterIds = ids });
            }
        }

        private void ActivateChapters()
        {
            GameState state = _progress.State.State;
            _active.Clear();
            for (int i = 0; i < _chapters.Count; i++)
            {
                CampaignChapterData ch = _chapters[i];
                if (ch == null || state.CampaignChapterCompleted(ch.id)) continue;
                if (!ConditionEvaluator.Evaluate(ch.entryConditions, _progress.State)) continue;
                if (!_active.Contains(ch)) _active.Add(ch);
            }

            // journal chapter starts (once per run, guarded by a state flag)
            for (int i = 0; i < _active.Count; i++)
            {
                CampaignChapterData ch = _active[i];
                if (!state.HasFlag("chap_started_" + ch.id))
                {
                    _progress.State.SetFlag("chap_started_" + ch.id, "1");
                    _progress.State.AddCampaignJournalLine(ch.title + (string.IsNullOrEmpty(ch.subtitle) ? "" : " - " + ch.subtitle));
                    EventBus.Publish(new CampaignChapterStartedEvent { chapterId = ch.id, title = ch.title });
                    _dirty = true;
                }
            }
        }

        private int TotalBeatCount()
        {
            int n = 0;
            for (int i = 0; i < _chapters.Count; i++)
                if (_chapters[i] != null && _chapters[i].beats != null) n += _chapters[i].beats.Count;
            return n;
        }

        private bool PrereqsResolved(StoryBeatData beat, GameState state)
        {
            if (beat.requiredBeatIds == null) return true;
            for (int i = 0; i < beat.requiredBeatIds.Count; i++)
                if (!state.CampaignBeatResolved(beat.requiredBeatIds[i])) return false;
            return true;
        }

        private bool TriggerSatisfied(StoryBeatData beat, GameStateManager progress)
        {
            switch (beat.resolveTrigger)
            {
                case BeatTrigger.DecisionMade:
                    return !string.IsNullOrEmpty(beat.resolveKey) && progress.State.HasDecision(beat.resolveKey);
                case BeatTrigger.ObjectiveCompleted:
                    return !string.IsNullOrEmpty(beat.resolveKey) &&
                           (int)progress.State.GetObjectivePhase(beat.resolveKey) == (int)ObjectivePhase.Completed;
                case BeatTrigger.ObjectiveFailed:
                    return !string.IsNullOrEmpty(beat.resolveKey) &&
                           (int)progress.State.GetObjectivePhase(beat.resolveKey) == (int)ObjectivePhase.Failed;
                default:
                    return ConditionEvaluator.Evaluate(beat.resolveConditions, progress.State);
            }
        }

        private void Resolve(StoryBeatData beat, CampaignChapterData chapter)
        {
            _progress.State.MarkCampaignBeat(beat.id);
            _progress.State.AddCampaignJournalLine(beat.journalText);
            if (beat.onResolveEffects != null && beat.onResolveEffects.Count > 0)
                EffectApplier.Apply(beat.onResolveEffects, _progress.State);
            EventBus.Publish(new CampaignBeatResolvedEvent
            {
                beatId = beat.id, chapterId = chapter.id, title = beat.title, journalText = beat.journalText
            });
            _dirty = true;

            // branch point: first matching branch in authored order routes the run
            if (chapter.branches == null) return;
            for (int i = 0; i < chapter.branches.Count; i++)
            {
                CampaignBranchData br = chapter.branches[i];
                if (br == null || br.fromBeatId != beat.id) continue;
                if (_progress.State.State.CampaignBranchTaken(br.id)) continue;
                if (!ConditionEvaluator.Evaluate(br.requiredConditions, _progress.State)) continue;
                TakeBranch(br);
                break; // one route per branch point
            }
        }

        private void TakeBranch(CampaignBranchData br)
        {
            _progress.State.MarkCampaignBranch(br.id);
            if (!string.IsNullOrEmpty(br.label))
                _progress.State.AddCampaignJournalLine(br.label);
            if (br.effects != null && br.effects.Count > 0)
                EffectApplier.Apply(br.effects, _progress.State);
            EventBus.Publish(new CampaignBranchTakenEvent
            {
                branchId = br.id, fromBeatId = br.fromBeatId, toBeatId = br.toBeatId, label = br.label
            });
            _dirty = true;
        }

        private void CompleteChapter(CampaignChapterData ch)
        {
            _progress.State.MarkCampaignChapter(ch.id);
            _progress.State.AddCampaignJournalLine(ch.completionJournal);
            if (ch.completionEffects != null && ch.completionEffects.Count > 0)
                EffectApplier.Apply(ch.completionEffects, _progress.State);
            EventBus.Publish(new CampaignChapterCompletedEvent
            {
                chapterId = ch.id, title = ch.title, journalText = ch.completionJournal
            });
            _dirty = true;
        }

        private void RebuildAvailable()
        {
            _available.Clear();
            if (_progress == null || _progress.State == null) return;
            for (int i = 0; i < _active.Count; i++)
            {
                CampaignChapterData ch = _active[i];
                if (ch.beats == null) continue;
                for (int b = 0; b < ch.beats.Count; b++)
                {
                    StoryBeatData beat = ch.beats[b];
                    if (beat == null || _progress.State.State.CampaignBeatResolved(beat.id)) continue;
                    if (!PrereqsResolved(beat, _progress.State.State)) continue;
                    if (!ConditionEvaluator.Evaluate(beat.offerConditions, _progress.State)) continue;
                    _available.Add(beat);
                }
            }
        }
    }
}
