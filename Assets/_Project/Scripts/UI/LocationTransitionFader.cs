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

        [Tooltip("Extra hold on black while the location title shows (unscaled). 0 = pure blink.")]
        [SerializeField] private float titleHoldSeconds = 0.55f;

        private Image _overlay;
        private CanvasGroup _group;
        private Text _title;
        private Text _subtitle;
        private float _phase;          // 0 idle; >0 fading out; <0 fading in
        private float _hold;           // remaining black-hold (title card) seconds
        private bool _moved;           // player teleported for the pending arrival
        private Light _sun;            // cached: GameObject.Find per arrival is a scene walk
        private LocationArrivedEvent _pending;

        /// <summary>Headless seam: true while the overlay is opaque or fading.</summary>
        public bool Transitioning { get { return _phase != 0f || _hold > 0f; } }

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

            // loading card: location name + a one-line hint, centred; invisible with the overlay
            _title = RuntimeMenuFactory.CreateText("Title", rect, "", 64, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_title.rectTransform, 80f, 80f, 0f, 60f);
            _subtitle = RuntimeMenuFactory.CreateText("Subtitle", rect, "", 30, RuntimeMenuFactory.Accent, TextAnchor.MiddleCenter);
            RuntimeMenuFactory.Stretch(_subtitle.rectTransform, 80f, 80f, 120f, 0f);
        }

        private void OnEnable() { EventBus.Subscribe<LocationArrivedEvent>(OnArrived); }
        private void OnDisable() { EventBus.Unsubscribe<LocationArrivedEvent>(OnArrived); }

        private void OnArrived(LocationArrivedEvent e)
        {
            if (_group == null)
            {
                // no UI (headless / boot before Build): still teleport + relight, nothing to fade
                MovePlayerToAnchor(e);
                ApplyEnvironment(e);
                return;
            }
            _pending = e;
            _moved = false;
            _phase = fadeSeconds > 0f ? fadeSeconds : 0.0001f; // start fade-out; Update drives both halves
            _hold = e.firstVisit ? titleHoldSeconds : titleHoldSeconds * 0.5f;
            _group.blocksRaycasts = true;                      // swallow touches mid-transition
            if (_title != null)
            {
                _title.text = string.IsNullOrEmpty(e.name) ? "" : e.name;
                _subtitle.text = e.firstVisit ? "— new location —" : "";
            }
            // The move + relight happen at full black (mid-fade) so the camera never shows the
            // pop; a zero-length fade applies them right away.
            if (fadeSeconds <= 0f) { MidFade(); }
        }

        private void MidFade()
        {
            if (_moved) return;
            _moved = true;
            MovePlayerToAnchor(_pending);
            ApplyEnvironment(_pending);
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
            if (_sun == null)
            {
                GameObject sun = GameObject.Find(sunObjectName);
                _sun = sun != null ? sun.GetComponent<Light>() : null;
            }
            if (_sun != null)
            {
                _sun.color = Hex(e.envSun, Color.white);
                _sun.intensity = e.envSunIntensity;
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
            float step = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : 0.016f; // pause-proof
            if (_phase > 0f)
            {
                _phase -= step;
                _group.alpha = Mathf.Clamp01(1f - _phase / Mathf.Max(fadeSeconds, 0.0001f));
                if (_phase <= 0f)
                {
                    _group.alpha = 1f;
                    MidFade();                                   // the ONE teleport, at full black
                    if (_hold > 0f) { _phase = -0.00001f; }      // park on black while the title shows
                    else _phase = -Mathf.Max(fadeSeconds, 0.0001f);
                }
            }
            else if (_hold > 0f)
            {
                _hold -= step;
                if (_hold <= 0f) { _hold = 0f; _phase = -Mathf.Max(fadeSeconds, 0.0001f); }
            }
            else
            {
                _phase += step;
                _group.alpha = Mathf.Clamp01(1f + _phase / Mathf.Max(fadeSeconds, 0.0001f));
                if (_phase >= 0f)
                {
                    _phase = 0f; _group.alpha = 0f; _group.blocksRaycasts = false;
                    if (_title != null) { _title.text = ""; _subtitle.text = ""; }
                    _pending = default(LocationArrivedEvent);
                }
            }
        }
    }
}
