using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Map/location navigation HUD (task 9): current location, where you can travel,
    /// what is still locked - and WHY (the location's requirement hint from content).
    /// Presentation-only: data comes from LocationServices.MapSnapshot(), travel calls
    /// LocationServices.Travel(id) and the state moves before any visual does.
    ///
    /// Rebuilt only on location/campaign-availability events; top-left, under StateHUD.
    /// </summary>
    public class MapHUD : MonoBehaviour
    {
        private RectTransform _list;
        private bool _expanded = true;

        public static MapHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<MapHUD>();
            hud.Build(parent);
            hud.Refresh();
            return hud;
        }

        private void Build(RectTransform parent)
        {
            var panel = RuntimeMenuFactory.CreatePanel("MapPanel", parent, new Color(0.045f, 0.065f, 0.095f, 0.72f));
            var rect = panel.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(40f, -820f);
            rect.offsetMax = new Vector2(40f + 420f, -440f);

            var header = RuntimeMenuFactory.CreateButton("MapHeader", rect, "LOCATIONS", 22,
                new Color(0.10f, 0.14f, 0.19f, 0.95f), RuntimeMenuFactory.Accent);
            RuntimeMenuFactory.Stretch(header.rectTransform, 12f, 12f, 12f, 296f);
            header.onClick.AddListener(Toggle);

            _list = RuntimeMenuFactory.CreateRect("MapList", rect);
            var lrect = _list;
            lrect.anchorMin = new Vector2(0f, 1f);
            lrect.anchorMax = new Vector2(1f, 1f);
            lrect.pivot = new Vector2(0.5f, 1f);
            lrect.offsetMin = new Vector2(12f, -348f);
            lrect.offsetMax = new Vector2(-12f, -316f);
        }

        private void Toggle() { _expanded = !_expanded; Refresh(); }

        private void OnEnable()
        {
            EventBus.Subscribe<LocationArrivedEvent>(OnChanged);
            EventBus.Subscribe<LocationUnlockedEvent>(OnChanged);
            EventBus.Subscribe<LocationAvailabilityChangedEvent>(OnChanged);
            EventBus.Subscribe<StateResetEvent>(OnReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationArrivedEvent>(OnChanged);
            EventBus.Unsubscribe<LocationUnlockedEvent>(OnChanged);
            EventBus.Unsubscribe<LocationAvailabilityChangedEvent>(OnChanged);
            EventBus.Unsubscribe<StateResetEvent>(OnReset);
        }

        private void OnChanged<T>(T e) { Refresh(); }
        private void OnReset(StateResetEvent e) { Refresh(); }

        private void Refresh()
        {
            if (_list == null) return;
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                Transform child = _list.GetChild(i);
                if (child != null) Object.Destroy(child.gameObject);
            }
            if (!_expanded) return;

            List<LocationServices.MapEntry> entries = LocationServices.MapSnapshot();
            float y = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                LocationServices.MapEntry entry = entries[i];
                float height = BuildRow(entry, -y);
                y += height + 8f;
            }
        }

        /// <summary>Builds one location row; returns its height so the caller can stack.</summary>
        private float BuildRow(LocationServices.MapEntry entry, float y)
        {
            Color bg;
            Color fg;
            string label = entry.name;
            switch (entry.state)
            {
                case LocationServices.MapEntryState.Current:
                    bg = new Color(0.13f, 0.24f, 0.30f, 0.95f);
                    fg = RuntimeMenuFactory.Accent;
                    label += "   (you are here)";
                    break;
                case LocationServices.MapEntryState.TravelTo:
                    bg = new Color(0.09f, 0.13f, 0.18f, 0.95f);
                    fg = RuntimeMenuFactory.TextMain;
                    label += "   -> travel";
                    break;
                default:
                    bg = new Color(0.06f, 0.08f, 0.11f, 0.85f);
                    fg = RuntimeMenuFactory.TextDim;
                    label += "   (locked)";
                    break;
            }

            float height = 54f;
            bool hasHint = entry.state == LocationServices.MapEntryState.Locked && !string.IsNullOrEmpty(entry.hint);

            if (entry.state == LocationServices.MapEntryState.Current)
            {
                var row = RuntimeMenuFactory.CreateText("MapRow" + entry.id, _list, label, 22, fg,
                    TextAnchor.MiddleLeft, FontStyle.Bold);
                Pin(row.rectTransform, y, height);
            }
            else if (entry.state == LocationServices.MapEntryState.TravelTo)
            {
                var btn = RuntimeMenuFactory.CreateButton("MapTravel" + entry.id, _list, label, 22, bg, fg);
                Pin(btn.rectTransform, y, height);
                string id = entry.id;
                btn.onClick.AddListener(delegate { LocationServices.Travel(id); });
            }
            else
            {
                var row = RuntimeMenuFactory.CreateText("MapRow" + entry.id, _list, label, 22, fg,
                    TextAnchor.MiddleLeft, FontStyle.Normal);
                Pin(row.rectTransform, y, height);
            }

            if (hasHint)
            {
                var hint = RuntimeMenuFactory.CreateText("MapHint" + entry.id, _list, entry.hint, 18,
                    RuntimeMenuFactory.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
                Pin(hint.rectTransform, y - height, 46f);
                height += 46f;
            }
            return height;
        }

        private static void Pin(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, y - height);
            rect.offsetMax = new Vector2(0f, y);
        }
    }
}
