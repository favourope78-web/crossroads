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
            // slim chapter pill (visual pass): one line, no journal dump - the story log
            // lives in the pause/journal surfaces, not on the gameplay screen
            var panel = RuntimeMenuFactory.CreatePanel("CampaignBanner", parent, new Color(0.04f, 0.06f, 0.09f, 0.78f));
            _root = panel.gameObject;
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(-470f, -86f);
            rect.offsetMax = new Vector2(470f, -20f);

            var edge = RuntimeMenuFactory.CreatePanel("Edge", rect, new Color(0.30f, 0.85f, 0.95f, 0.5f));
            var erect = edge.rectTransform;
            erect.anchorMin = erect.anchorMax = new Vector2(0f, 0.5f);
            erect.pivot = new Vector2(0f, 0.5f);
            erect.sizeDelta = new Vector2(4f, 46f);
            erect.anchoredPosition = Vector2.zero;

            _chapter = RuntimeMenuFactory.CreateText("Chapter", rect, "", 28, RuntimeMenuFactory.Accent,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_chapter.rectTransform, 24f, 330f, 40f, 12f);

            _beat = RuntimeMenuFactory.CreateText("Beat", rect, "", 26, RuntimeMenuFactory.TextMain,
                TextAnchor.MiddleRight, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_beat.rectTransform, 24f, 24f, 40f, 12f);

            _story = RuntimeMenuFactory.CreateText("Story", rect, "", 1, new Color(0f, 0f, 0f, 0f),
                TextAnchor.MiddleCenter, FontStyle.Normal);
            RuntimeMenuFactory.Stretch(_story.rectTransform, 0f, 0f, 0f, 0f);
            _story.text = "";
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
                ? "the story waits on your next move"
                : snap.currentBeatTitle;

            // slim pill: journal + path labels are NOT dumped on the gameplay screen any more
            _story.text = "";
        }
    }
}
