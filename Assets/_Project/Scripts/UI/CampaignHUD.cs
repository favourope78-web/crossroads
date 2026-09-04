using System.Text;
using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Chapter/Story HUD (task 9): current chapter, the current story beat (what the player
    /// should chase next), the latest story-log line and the run's taken paths ("Path of
    /// Ember"). Pure presentation - rebuilt only on campaign events, data from
    /// CampaignServices.Snapshot(). Top-center banner; sits above the combat enemy bar
    /// (which docks lower) so nothing overlaps on any aspect ratio.
    /// </summary>
    public class CampaignHUD : MonoBehaviour
    {
        private GameObject _root;
        private Text _chapter;
        private Text _beat;
        private Text _story;
        private readonly StringBuilder _sb = new StringBuilder(64);

        public static CampaignHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<CampaignHUD>();
            hud.Build(parent);
            hud.Refresh();
            return hud;
        }

        private void Build(RectTransform parent)
        {
            var panel = RuntimeMenuFactory.CreatePanel("CampaignBanner", parent, new Color(0.045f, 0.065f, 0.095f, 0.72f));
            _root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(-430f, -124f);
            rect.offsetMax = new Vector2(430f, -16f);

            _chapter = RuntimeMenuFactory.CreateText("Chapter", rect, "", 30, RuntimeMenuFactory.Accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_chapter.rectTransform, 20f, 190f, 10f, 62f);

            _beat = RuntimeMenuFactory.CreateText("Beat", rect, "", 24, RuntimeMenuFactory.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_beat.rectTransform, 20f, 190f, 58f, 32f);

            _story = RuntimeMenuFactory.CreateText("Story", rect, "", 20, RuntimeMenuFactory.TextDim,
                TextAnchor.MiddleCenter, FontStyle.Normal);
            RuntimeMenuFactory.Stretch(_story.rectTransform, 20f, 190f, 30f, 8f);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CampaignChangedEvent>(OnCampaign);
            EventBus.Subscribe<CampaignBeatResolvedEvent>(OnCampaign);
            EventBus.Subscribe<CampaignBranchTakenEvent>(OnCampaign);
            EventBus.Subscribe<CampaignChapterCompletedEvent>(OnCampaign);
            EventBus.Subscribe<StateResetEvent>(OnReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CampaignChangedEvent>(OnCampaign);
            EventBus.Unsubscribe<CampaignBeatResolvedEvent>(OnCampaign);
            EventBus.Unsubscribe<CampaignBranchTakenEvent>(OnCampaign);
            EventBus.Unsubscribe<CampaignChapterCompletedEvent>(OnCampaign);
            EventBus.Unsubscribe<StateResetEvent>(OnReset);
        }

        private void OnCampaign<T>(T e) { Refresh(); }

        private void OnReset(StateResetEvent e) { Refresh(); }

        private void Refresh()
        {
            if (_root == null) return;
            CampaignServices.CampaignSnapshot snap = CampaignServices.Snapshot();

            if (string.IsNullOrEmpty(snap.chapterTitle))
            {
                // no active chapter (pre-story or all completed) - keep the HUD quiet
                _root.SetActive(false);
                return;
            }
            _root.SetActive(true);

            _sb.Length = 0;
            _sb.Append(snap.chapterTitle.ToUpperInvariant());
            if (!string.IsNullOrEmpty(snap.chapterSubtitle)) _sb.Append("  ·  ").Append(snap.chapterSubtitle);
            _chapter.text = _sb.ToString();

            _beat.text = string.IsNullOrEmpty(snap.currentBeatTitle)
                ? "The story waits on your next move"
                : snap.currentBeatTitle;

            // story log line + taken paths, one dim line
            _sb.Length = 0;
            if (snap.journal.Count > 0) _sb.Append(snap.journal[snap.journal.Count - 1]);
            if (snap.pathLabels.Count > 0)
            {
                if (_sb.Length > 0) _sb.Append("   |   ");
                _sb.Append("Path: ").Append(string.Join(" / ", snap.pathLabels.ToArray()));
            }
            _story.text = _sb.ToString();
        }
    }
}
