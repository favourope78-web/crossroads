using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Runtime-built uGUI primitives for CROSSROADS (GAME_DESIGN §8.1/§4.5 mobile UX):
    /// no asset dependencies, SafeArea-aware, touch-friendly (>=88dp targets at reference 1920x1080).
    /// Dialogue/HUD panels are built in code so the prototype scene stays free of Canvas YAML.
    /// </summary>
    public static class RuntimeMenuFactory
    {
        // ---- palette (hall dusk-cyan accent, dark translucent panels) ----
        public static readonly Color Panel = new Color(0.045f, 0.065f, 0.095f, 0.88f);
        public static readonly Color PanelSoft = new Color(0.07f, 0.10f, 0.14f, 0.92f);
        public static readonly Color Accent = new Color(0.30f, 0.85f, 0.95f, 1f);
        public static readonly Color AccentDim = new Color(0.30f, 0.85f, 0.95f, 0.35f);
        public static readonly Color TextMain = new Color(0.93f, 0.95f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.60f, 0.66f, 0.72f, 1f);
        public static readonly Color Ember = new Color(0.95f, 0.38f, 0.22f, 1f);
        public static readonly Color Tide = new Color(0.25f, 0.80f, 0.85f, 1f);
        public static readonly Color Stone = new Color(0.85f, 0.68f, 0.32f, 1f);

        public const int RefWidth = 1920;
        public const int RefHeight = 1080;

        private static Font _font;
        private static bool _fontResolved;

        public static Font ResolveFont()
        {
            if (_fontResolved) return _font;
            _fontResolved = true;
            _font = null;
            try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (System.Exception) { _font = null; }
            if (_font == null)
            {
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch (System.Exception) { _font = null; }
            }
            if (_font == null)
            {
                Debug.LogWarning("[CROSSROADS] Built-in font unavailable - UI text will not render.");
            }
            return _font;
        }

        /// <summary>Creates the overlay canvas + scaler + EventSystem (once).</summary>
        public static Canvas CreateRoot(string name)
        {
            var root = new GameObject(name);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                AddInputModule(es);
            }
            return canvas;
        }

        private static void AddInputModule(GameObject es)
        {
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        /// <summary>Full-screen safe-area container (mobile notches/rounded corners).</summary>
        public static RectTransform CreateSafeArea(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var fitter = go.AddComponent<SafeAreaFitter>();
            fitter.Apply();
            return rect;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>1px-ish border image (Image is the only uGUI primitive we need for frames).</summary>
        public static Image CreateBorder(string name, Transform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = ResolveFont();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label, int fontSize, Color bg, Color fg)
        {
            var panel = CreatePanel(name, parent, bg);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;

            var text = CreateText("Label", panel.transform, label, fontSize, fg, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            var colors = button.colors;
            colors.highlightedColor = new Color(bg.r * 1.25f, bg.g * 1.25f, bg.b * 1.25f, bg.a);
            colors.pressedColor = new Color(bg.r * 0.8f, bg.g * 0.8f, bg.b * 0.8f, bg.a);
            button.colors = colors;
            return button;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void SetAnchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
        }
    }
}
