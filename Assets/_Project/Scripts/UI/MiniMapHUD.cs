using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// CIRCULAR RADAR MINI-MAP (VISUAL_TARGET §4, task sheet "MINI-MAP" zone).
    /// Top-right: glass disc + rim, player arrow centred (always pointing where the camera
    /// looks), dots for NPCs (cyan), live enemies (red) and points of interest (gold),
    /// with out-of-range contacts pinned to the rim and a rotating north marker.
    ///
    /// Cost: one 5 Hz refresh that iterates the WorldMarkers registry (no scene scans),
    /// a pooled set of 20 dot Images reused forever, zero allocations per refresh.
    /// </summary>
    public class MiniMapHUD : MonoBehaviour
    {
        private const int DotPool = 20;
        private const float RefreshSeconds = 0.2f;   // 5 Hz
        private const float RangeMeters = 24f;

        private RectTransform _disc;          // the rotating north ring parent? no - static disc
        private Image _north;
        private RectTransform _arrow;
        private readonly Image[] _dots = new Image[DotPool];
        private readonly bool[] _pinned = new bool[DotPool];
        private float _nextRefresh;
        private Transform _player;
        private float _nextPlayerSearch;
        private float _size = 260f;

        public static MiniMapHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<MiniMapHUD>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            // ---- backdrop disc ----
            var discGo = new GameObject("MiniMap");
            _disc = discGo.AddComponent<RectTransform>();
            _disc.SetParent(parent, false);
            _disc.anchorMin = _disc.anchorMax = new Vector2(1f, 1f);
            _disc.pivot = new Vector2(1f, 1f);
            _disc.anchoredPosition = new Vector2(-HudTheme.Corner, -(HudTheme.Corner + HudTheme.SmallButton + 24f));
            _disc.sizeDelta = new Vector2(_size, _size);

            var back = discGo.AddComponent<Image>();
            back.sprite = UiShapes.Circle;
            back.color = new Color(0.03f, 0.045f, 0.07f, 0.80f);
            back.raycastTarget = false;

            var ring = CreateChild("Ring", _disc, UiShapes.Ring, HudTheme.Stroke);
            StretchFull(ring.rectTransform);
            // inner faint ring for depth
            var inner = CreateChild("InnerRing", _disc, UiShapes.Ring, HudTheme.StrokeDim);
            inner.rectTransform.sizeDelta = new Vector2(_size * 0.72f, _size * 0.72f);
            inner.rectTransform.anchoredPosition = Vector2.zero;

            // ---- north marker ("N" chip on the rim, rotates with heading) ----
            var northGo = new GameObject("North");
            var nrect = northGo.AddComponent<RectTransform>();
            nrect.SetParent(_disc, false);
            nrect.sizeDelta = new Vector2(34f, 34f);
            _north = northGo.AddComponent<Image>();
            _north.sprite = UiShapes.Circle;
            _north.color = new Color(0.10f, 0.16f, 0.22f, 0.92f);
            _north.raycastTarget = false;
            var nText = RuntimeMenuFactory.CreateText("N", nrect, "N", 20, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            StretchFull(nText.rectTransform);
            nText.raycastTarget = false;

            // ---- player arrow (centre, camera-forward = up) ----
            var arrowGo = new GameObject("PlayerArrow");
            _arrow = arrowGo.AddComponent<RectTransform>();
            _arrow.SetParent(_disc, false);
            _arrow.sizeDelta = new Vector2(42f, 42f);
            var arrowImg = arrowGo.AddComponent<Image>();
            arrowImg.sprite = UiShapes.Arrow;
            arrowImg.color = HudTheme.TextMain;
            arrowImg.raycastTarget = false;

            // ---- dot pool ----
            for (int i = 0; i < DotPool; i++)
            {
                var go = new GameObject("Dot" + i);
                var r = go.AddComponent<RectTransform>();
                r.SetParent(_disc, false);
                r.sizeDelta = new Vector2(16f, 16f);
                var img = go.AddComponent<Image>();
                img.sprite = UiShapes.Circle;
                img.raycastTarget = false;
                go.SetActive(false);
                _dots[i] = img;
            }
        }

        private static Image CreateChild(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            var r = go.AddComponent<RectTransform>();
            r.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void StretchFull(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private void Update()
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + RefreshSeconds;
            Refresh();
        }

        private void Refresh()
        {
            // resolve the player once (cheap tag lookup at 0.5 Hz until found)
            if (_player == null)
            {
                if (Time.time < _nextPlayerSearch) return;
                _nextPlayerSearch = Time.time + 0.5f;
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                _player = player.transform;
            }
            var cam = Camera.main;
            if (cam == null) return;

            float yaw = MiniMapMath.YawFromForward(cam.transform.forward);
            float radius = _size * 0.5f - 18f;
            float ppm = MiniMapMath.PixelsPerMeterForRange(RangeMeters, radius);

            // north chip rotates around the rim
            float north = MiniMapMath.NorthHeading(yaw);
            float rad = (90f - north) * Mathf.Deg2Rad; // 0 deg = up
            _north.rectTransform.anchoredPosition = new Vector2(
                Mathf.Sin(rad) * (radius - 12f), Mathf.Cos(rad) * (radius - 12f));

            // contacts
            Vector3 p = _player.position;
            int used = 0;
            int count = WorldMarkers.Count;
            for (int i = 0; i < count && used < DotPool; i++)
            {
                WorldMarkers.Entry e = WorldMarkers.Get(i);
                if (e == null || e.transform == null) continue;
                Vector3 t = e.transform.position;
                Vector2 delta = new Vector2(t.x - p.x, t.z - p.z);
                float distSq = delta.sqrMagnitude;
                if (distSq > RangeMeters * RangeMeters * 9f) continue; // way out - not even rim-pinned

                bool pinned;
                Vector2 pos = MiniMapMath.ToMapPosition(delta, yaw, ppm, radius, out pinned);
                Image dot = _dots[used];
                dot.gameObject.SetActive(true);
                dot.rectTransform.anchoredPosition = pos;
                _pinned[used] = pinned;

                Color c;
                float dotSize;
                switch (e.kind)
                {
                    case MarkerKind.Enemy: c = HudTheme.Bad; dotSize = 20f; break;
                    case MarkerKind.Npc: c = HudTheme.Tide; dotSize = 16f; break;
                    default: c = HudTheme.Stone; dotSize = 14f; break;
                }
                // rim-pinned contacts fade to a hollow hint instead of a hard dot
                c.a = pinned ? 0.55f : 0.95f;
                dot.color = c;
                dot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
                used++;
            }
            for (int i = used; i < DotPool; i++)
                if (_dots[i].gameObject.activeSelf) _dots[i].gameObject.SetActive(false);
        }
    }
}
