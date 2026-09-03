using System.Collections;
using System.Text;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>
    /// Post-choice feedback toast (GAME_DESIGN §4.5: affinity glyph hint AFTER selection -
    /// the system teaches itself without spoiling choices). Shows "locked in" summary,
    /// affinity deltas in their line colors and the save confirmation.
    /// </summary>
    public class ToastUI : MonoBehaviour
    {
        private UnityEngine.UI.Text _text;
        private Coroutine _fade;
        private string _pendingDecisionBody = "";

        public static ToastUI Attach(RectTransform parent)
        {
            var toast = parent.gameObject.AddComponent<ToastUI>();
            toast.Build(parent);
            return toast;
        }

        private void Build(RectTransform parent)
        {
            var panel = RuntimeMenuFactory.CreatePanel("Toast", parent, RuntimeMenuFactory.Panel);
            var rect = panel.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(-430f, 470f);
            rect.offsetMax = new Vector2(430f, 600f);
            _text = RuntimeMenuFactory.CreateText("Text", rect, "", 32, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter);
            RuntimeMenuFactory.Stretch(_text.rectTransform, 28f, 28f, 14f, 14f);
            panel.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Subscribe<SaveCompletedEvent>(OnSaveCompleted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnDecisionResolved);
            EventBus.Unsubscribe<SaveCompletedEvent>(OnSaveCompleted);
        }

        private void OnDecisionResolved(DecisionResolvedEvent e)
        {
            var sb = new StringBuilder();
            sb.Append("locked in");
            if (e.affinityDeltas != null)
            {
                for (int i = 0; i < e.affinityDeltas.Count; i++)
                {
                    AffinityDelta d = e.affinityDeltas[i];
                    sb.Append("\n").Append(LineColor(d.line) == null ? "" : LineSymbol(d.line) + " ")
                      .Append(d.line).Append(" +").Append(d.amount).Append(" (").Append(d.newTotal).Append(")");
                }
            }
            _pendingDecisionBody = sb.ToString();
            Show(_pendingDecisionBody + "\n...saving");
        }

        private void OnSaveCompleted(SaveCompletedEvent e)
        {
            string body = _pendingDecisionBody;
            _pendingDecisionBody = "";
            Show(string.IsNullOrEmpty(body)
                ? (e.ok ? "decision saved ✓" : "save failed ✕")
                : body + "\n" + (e.ok ? "saved ✓" : "save failed ✕"));
        }

        private static string LineSymbol(string line)
        {
            switch (line)
            {
                case "Ember": return "◆";
                case "Tide": return "≈";
                case "Stone": return "▣";
                case "Hollow": return "◈";
                default: return "·";
            }
        }

        private static Color? LineColor(string line)
        {
            if (line == "Ember") return RuntimeMenuFactory.Ember;
            if (line == "Tide") return RuntimeMenuFactory.Tide;
            if (line == "Stone") return RuntimeMenuFactory.Stone;
            return null;
        }

        private void Show(string message)
        {
            _text.text = message;
            _text.color = RuntimeMenuFactory.TextMain;
            if (gameObject.activeSelf)
            {
                if (_fade != null) StopCoroutine(_fade);
            }
            else
            {
                gameObject.SetActive(true);
            }
            _fade = StartCoroutine(FadeAndHide());
        }

        private IEnumerator FadeAndHide()
        {
            yield return new WaitForSecondsRealtime(3.2f);
            if (_text != null) _text.color = RuntimeMenuFactory.TextDim;
            yield return new WaitForSecondsRealtime(0.4f);
            gameObject.SetActive(false);
            _fade = null;
        }
    }
}
