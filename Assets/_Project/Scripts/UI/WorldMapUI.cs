using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// WORLD MAP / TRAVEL SCREEN (VISUAL_TARGET §4/§7): the polished replacement for the
    /// always-on developer location list (old MapHUD is dev-gated now). Opened from the
    /// map button beside the mini-map: current location highlighted, reachable locations
    /// as tappable cards, locked ones show WHY (the authored requirement hint). Travel
    /// itself is the untouched LocationServices.Travel path - presentation only.
    /// </summary>
    public class WorldMapUI : MonoBehaviour
    {
        private GameObject _root;
        private RectTransform _list;
        private Text _header;

        public bool IsOpen { get { return _root != null && _root.activeSelf; } }

        public static WorldMapUI Attach(RectTransform parent)
        {
            var map = parent.gameObject.AddComponent<WorldMapUI>();
            map.Build(parent);
            return map;
        }

        private void Build(RectTransform parent)
        {
            var go = new GameObject("WorldMap");
            _root = go;
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _root.SetActive(false);

            var shade = RuntimeMenuFactory.CreatePanel("Shade", rect, new Color(0.012f, 0.02f, 0.035f, 0.92f));
            var srect = shade.rectTransform;
            srect.anchorMin = Vector2.zero;
            srect.anchorMax = Vector2.one;
            srect.offsetMin = Vector2.zero;
            srect.offsetMax = Vector2.zero;

            _header = RuntimeMenuFactory.CreateText("Header", rect, "THE CITY", 64, HudTheme.TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            _header.rectTransform.anchorMin = _header.rectTransform.anchorMax = new Vector2(0f, 1f);
            _header.rectTransform.pivot = new Vector2(0f, 1f);
            _header.rectTransform.offsetMin = new Vector2(110f, -140f);
            _header.rectTransform.offsetMax = new Vector2(900f, -48f);

            var hint = RuntimeMenuFactory.CreateText("Hint", rect, "choose where to go - the city remembers", 28,
                HudTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0f, 1f);
            hint.rectTransform.pivot = new Vector2(0f, 1f);
            hint.rectTransform.offsetMin = new Vector2(116f, -190f);
            hint.rectTransform.offsetMax = new Vector2(1000f, -142f);

            var close = RuntimeMenuFactory.CreateButton("Close", rect, "\u2715", 40,
                new Color(0.13f, 0.19f, 0.26f, 0.95f), HudTheme.TextMain);
            var crect = ((Image)close.targetGraphic).rectTransform;
            crect.anchorMin = crect.anchorMax = new Vector2(1f, 1f);
            crect.pivot = new Vector2(1f, 1f);
            crect.anchoredPosition = new Vector2(-48f, -48f);
            crect.sizeDelta = new Vector2(104f, 104f);
            close.onClick.AddListener(Close);

            _list = RuntimeMenuFactory.CreateRect("Cards", rect);
            _list.anchorMin = _list.anchorMax = new Vector2(0f, 1f);
            _list.pivot = new Vector2(0f, 1f);
            _list.offsetMin = new Vector2(110f, -1000f);
            _list.offsetMax = new Vector2(110f + 820f, -210f);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LocationArrivedEvent>(OnArrived);
            EventBus.Subscribe<LocationUnlockedEvent>(OnUnlocked);
            EventBus.Subscribe<LocationAvailabilityChangedEvent>(OnAvailability);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LocationArrivedEvent>(OnArrived);
            EventBus.Unsubscribe<LocationUnlockedEvent>(OnUnlocked);
            EventBus.Unsubscribe<LocationAvailabilityChangedEvent>(OnAvailability);
        }

        private void OnArrived(LocationArrivedEvent e) { if (IsOpen) Refresh(); }
        private void OnUnlocked(LocationUnlockedEvent e) { if (IsOpen) Refresh(); }
        private void OnAvailability(LocationAvailabilityChangedEvent e) { if (IsOpen) Refresh(); }

        public void Open()
        {
            _root.SetActive(true);
            InputLock.Set(true, "world map");
            Refresh();
        }

        public void Close()
        {
            _root.SetActive(false);
            if (InputLock.Active && InputLock.Reason == "world map") InputLock.Set(false, "world map");
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Refresh()
        {
            if (_list == null) return;
            for (int i = _list.childCount - 1; i >= 0; i--)
            {
                Transform child = _list.GetChild(i);
                if (child != null) Destroy(child.gameObject);
            }

            List<LocationServices.MapEntry> entries = LocationServices.MapSnapshot();
            float y = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                y += BuildCard(entries[i], -y) + 18f;
            }
        }

        /// <summary>One location card. Returns its height.</summary>
        private float BuildCard(LocationServices.MapEntry entry, float y)
        {
            const float height = 118f;
            bool current = entry.state == LocationServices.MapEntryState.Current;
            bool reachable = entry.state == LocationServices.MapEntryState.TravelTo;

            Color bg = current ? new Color(0.10f, 0.24f, 0.30f, 0.95f)
                : reachable ? new Color(0.10f, 0.15f, 0.21f, 0.95f)
                : new Color(0.055f, 0.075f, 0.105f, 0.9f);
            Color fg = current ? HudTheme.Accent : reachable ? HudTheme.TextMain : HudTheme.TextDim;

            GameObject cardGo;
            if (reachable)
            {
                var btn = RuntimeMenuFactory.CreateButton("Card_" + entry.id, _list, "", 32, bg, fg);
                cardGo = btn.gameObject;
                string id = entry.id;
                btn.onClick.AddListener(delegate { Travel(id); });
            }
            else
            {
                var panel = RuntimeMenuFactory.CreatePanel("Card_" + entry.id, _list, bg);
                cardGo = panel.gameObject;
            }
            var rect = cardGo.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(0f, y - height);
            rect.offsetMax = new Vector2(820f, y);

            // accent edge for the current location
            if (current)
            {
                var edge = RuntimeMenuFactory.CreatePanel("Edge", rect, HudTheme.Accent);
                var erect = edge.rectTransform;
                erect.anchorMin = erect.anchorMax = new Vector2(0f, 0.5f);
                erect.pivot = new Vector2(0f, 0.5f);
                erect.sizeDelta = new Vector2(6f, height - 16f);
                erect.anchoredPosition = new Vector2(0f, 0f);
            }

            var name = RuntimeMenuFactory.CreateText("Name", rect, entry.name, 36, fg,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.offsetMin = new Vector2(current ? 40f : 28f, -66f);
            name.rectTransform.offsetMax = new Vector2(-180f, -12f);

            string status = current ? "you are here"
                : reachable ? "tap to travel"
                : (string.IsNullOrEmpty(entry.hint) ? "locked" : entry.hint);
            var statusText = RuntimeMenuFactory.CreateText("Status", rect, status, 26,
                reachable ? HudTheme.TextDim : HudTheme.TextDim, TextAnchor.MiddleLeft, FontStyle.Italic);
            statusText.rectTransform.anchorMin = statusText.rectTransform.anchorMax = new Vector2(0f, 0f);
            statusText.rectTransform.pivot = new Vector2(0f, 0f);
            statusText.rectTransform.offsetMin = new Vector2(current ? 40f : 28f, 12f);
            statusText.rectTransform.offsetMax = new Vector2(-120f, 52f);

            var glyph = RuntimeMenuFactory.CreateText("Glyph", rect, current ? "\u25C9" : reachable ? "\u25B8" : "\u25CE",
                38, current ? HudTheme.Accent : HudTheme.TextDim, TextAnchor.MiddleRight);
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            glyph.rectTransform.pivot = new Vector2(1f, 0.5f);
            glyph.rectTransform.offsetMin = new Vector2(-110f, -30f);
            glyph.rectTransform.offsetMax = new Vector2(-30f, 30f);

            return height;
        }

        private void Travel(string id)
        {
            Close();
            LocationServices.Travel(id);
        }
    }
}
