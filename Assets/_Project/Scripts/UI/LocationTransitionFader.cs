using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Transition handling for the location system (task: Android-suitable loading).
    /// All three prototype locations live in ONE generated scene as zones, so travel is:
    ///
    ///   fade to black (0.2s, unscaled - never blocks input longer than the blink)
    ///   -> teleport the player to the location's anchor (LocationAnchor_&lt;id&gt;)
    ///   -> apply the location's environment profile (ambient/fog/sun from CONTENT,
    ///      carried by the event - the scene holds no second copy of the data)
    ///   -> fade back in
    ///
    /// No scene loads on the hot path: that is the whole point for Android (no hitches,
    /// no additive-async juggling while the world is still the size of one hall). When
    /// the world outgrows one scene, LocationDefinitionData.sceneKey already carries the
    /// target scene and this is the ONE component that changes.
    ///
    /// State-first design: LocationManager has ALREADY moved the run (area, visits,
    /// world-state changes) when the event arrives; this component is presentation only.
    /// Headless tests never instantiate it - which is why it does no game logic.
    /// </summary>
    public class LocationTransitionFader : MonoBehaviour
    {
        [Tooltip("Seconds per fade half (out then in). Unscaled time - pauses can't stall it.")]
        [SerializeField] private float fadeSeconds = 0.2f;

        [Tooltip("Scene object named X is the anchor for location id X (generated: LocationAnnex etc.).")]
        [SerializeField] private string anchorPrefix = "LocationAnchor_";

        [Tooltip("Directional light the per-location sun profile drives (generated scene: 'Directional Light').")]
        [SerializeField] private string sunObjectName = "Directional Light";

        private Image _overlay;
        private CanvasGroup _group;
        private float _phase;          // 0 idle; >0 fading out; <0 fading in
        private LocationArrivedEvent _pending;

        public static LocationTransitionFader Attach(RectTransform parent)
        {
            var fader = parent.gameObject.AddComponent<LocationTransitionFader>();
            fader.Build(parent);
            return fader;
        }

        private void Build(RectTransform parent)
        {
            // full-screen black overlay at the top of the UI stack; starts transparent
            var canvas = gameObject.GetComponentInParent<Canvas>();
            RectTransform root = canvas != null ? canvas.transform as RectTransform : parent;
            var go = RuntimeMenuFactory.CreateRect("LocationFade", root);
            _overlay = go.gameObject.AddComponent<Image>();
            _overlay.color = Color.black;
            _group = go.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false; // never eat input while invisible
            var rect = _overlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _overlay.rectTransform.SetAsLastSibling();
        }

        private void OnEnable() { EventBus.Subscribe<LocationArrivedEvent>(OnArrived); }
        private void OnDisable() { EventBus.Unsubscribe<LocationArrivedEvent>(OnArrived); }

        private void OnArrived(LocationArrivedEvent e)
        {
            ApplyEnvironment(e);
            if (_group == null) return;
            _pending = e;
            _phase = fadeSeconds > 0f ? fadeSeconds : 0.0001f; // start fade-out; Update drives both halves
            _group.blocksRaycasts = true;                      // swallow touches mid-transition
            MovePlayerToAnchor(e);
        }

        /// <summary>Content is the single source: hex "rrggbb" -> Color (no ColorUtility dependency).</summary>
        private static Color Hex(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 6) return fallback;
            int r, g, b;
            if (!int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r)) return fallback;
            if (!int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g)) return fallback;
            if (!int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) return fallback;
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private void ApplyEnvironment(LocationArrivedEvent e)
        {
            RenderSettings.ambientLight = Hex(e.envAmbient, RenderSettings.ambientLight);
            RenderSettings.fog = e.envFogDensity > 0f;
            RenderSettings.fogColor = Hex(e.envFog, RenderSettings.fogColor);
            RenderSettings.fogDensity = e.envFogDensity;
            GameObject sun = GameObject.Find(sunObjectName);
            Light light = sun != null ? sun.GetComponent<Light>() : null;
            if (light != null)
            {
                light.color = Hex(e.envSun, Color.white);
                light.intensity = e.envSunIntensity;
            }
            StoryLog.Log("[LOCATIONS] environment -> " + e.envProfile +
                " (ambient " + e.envAmbient + ", fog " + e.envFogDensity + ", sun " + e.envSun + ")");
        }

        private void MovePlayerToAnchor(LocationArrivedEvent e)
        {
            if (string.IsNullOrEmpty(e.checkpointId)) return;
            GameObject anchor = GameObject.Find(anchorPrefix + e.locationId);
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (anchor != null && player != null && player.transform != null && anchor.transform != null)
            {
                player.transform.position = anchor.transform.position;
                player.transform.rotation = anchor.transform.rotation;
            }
        }

        private void Update()
        {
            if (_group == null || _phase == 0f) return;
            float step = Time.unscaledTime > 0f ? Time.deltaTime : 0.016f; // stub: fixed 16ms
            if (_phase > 0f)
            {
                _phase -= step;
                _group.alpha = Mathf.Clamp01(1f - _phase / Mathf.Max(fadeSeconds, 0.0001f));
                if (_phase <= 0f) { _phase = -Mathf.Max(fadeSeconds, 0.0001f); MovePlayerToAnchor(_pending); }
            }
            else
            {
                _phase += step;
                _group.alpha = Mathf.Clamp01(1f + _phase / Mathf.Max(fadeSeconds, 0.0001f));
                if (_phase >= 0f) { _phase = 0f; _group.alpha = 0f; _group.blocksRaycasts = false; _pending = default(LocationArrivedEvent); }
            }
        }
    }
}
