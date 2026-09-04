using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Campaign facade (same pattern as WorldServices): boots the CampaignManager over the
    /// initialized GameServices, hooks campaign autosaves (any campaign movement persists
    /// through the existing save pipeline - schema v5 carries beats/branches/chapters/journal),
    /// and exposes read snapshots for the UI. Gameplay layer, references Narrative only.
    /// Boot order: GameServices.Init -> WorldServices.Init -> CampaignServices.Init.
    /// </summary>
    public static class CampaignServices
    {
        public static bool IsInitialized { get; private set; }

        /// <summary>Campaign runtime: chapters, beats, branches, journal.</summary>
        public static CampaignManager Campaign { get; private set; }

        private static bool _autosaveHooked;

        public static void Init()
        {
            Shutdown(silent: true);
            if (!GameServices.IsInitialized || GameServices.Content == null)
            {
                StoryLog.LogWarning("[CROSSROADS] CampaignServices.Init: GameServices not initialized");
                return;
            }

            StoryContentData content = GameServices.Content.Content;
            Campaign = new CampaignManager(content != null ? content.chapters : null, GameServices.Progress);
            Campaign.BindEvents();
            Campaign.Refresh(); // derive the current route from the loaded save (idempotent)

            if (!_autosaveHooked)
            {
                _autosaveHooked = true;
                EventBus.Subscribe<CampaignChangedEvent>(OnCampaignChanged);
                EventBus.Subscribe<CampaignBeatResolvedEvent>(OnCampaignBeat);
                EventBus.Subscribe<CampaignBranchTakenEvent>(OnCampaignBranch);
                EventBus.Subscribe<CampaignChapterCompletedEvent>(OnCampaignChapter);
            }

            IsInitialized = true;
            StoryLog.Log("[CROSSROADS] CampaignServices ready (" +
                (content != null && content.chapters != null ? content.chapters.Count.ToString() : "0") + " chapters)");
        }

        private static void OnCampaignChanged(CampaignChangedEvent e) { Persist(); }
        private static void OnCampaignBeat(CampaignBeatResolvedEvent e) { Persist(); }
        private static void OnCampaignBranch(CampaignBranchTakenEvent e) { Persist(); }
        private static void OnCampaignChapter(CampaignChapterCompletedEvent e) { Persist(); }

        private static void Persist()
        {
            if (!IsInitialized) return;
            GameServices.PersistNow(autosaveMirror: true);
        }

        public static void Shutdown(bool silent = false)
        {
            if (Campaign != null) { Campaign.UnbindEvents(); Campaign = null; }
            IsInitialized = false;
        }

        // ---------------------------------------------------------------- UI snapshot
        /// <summary>Everything the Chapter/Story HUD shows, in one allocation-free-ish call.</summary>
        public struct CampaignSnapshot
        {
            public string chapterTitle;
            public string chapterSubtitle;
            public string currentBeatTitle;
            public string currentBeatJournal;
            public List<string> journal;          // story log (oldest-first)
            public List<string> pathLabels;       // taken branch labels ("Path of Ember")
        }

        public static CampaignSnapshot Snapshot()
        {
            var snap = new CampaignSnapshot
            {
                chapterTitle = "", chapterSubtitle = "", currentBeatTitle = "", currentBeatJournal = "",
                journal = new List<string>(), pathLabels = new List<string>()
            };
            if (!IsInitialized || Campaign == null) return snap;

            List<CampaignChapterData> active = Campaign.ActiveChapters;
            if (active.Count > 0)
            {
                snap.chapterTitle = active[0].title;
                snap.chapterSubtitle = active[0].subtitle;
            }
            StoryBeatData beat = Campaign.CurrentBeat;
            if (beat != null)
            {
                snap.currentBeatTitle = beat.title;
                snap.currentBeatJournal = beat.journalText;
            }
            if (Campaign.Journal != null) snap.journal.AddRange(Campaign.Journal);

            // taken branch labels, in authored order across chapters
            if (GameServices.Content != null && GameServices.Content.Content != null)
            {
                List<CampaignChapterData> chapters = GameServices.Content.Content.chapters;
                if (chapters != null)
                {
                    for (int i = 0; i < chapters.Count; i++)
                    {
                        CampaignChapterData ch = chapters[i];
                        if (ch == null || ch.branches == null) continue;
                        for (int b = 0; b < ch.branches.Count; b++)
                        {
                            CampaignBranchData br = ch.branches[b];
                            if (br == null || string.IsNullOrEmpty(br.label)) continue;
                            if (GameServices.State.State.CampaignBranchTaken(br.id)) snap.pathLabels.Add(br.label);
                        }
                    }
                }
            }
            return snap;
        }
    }
}
