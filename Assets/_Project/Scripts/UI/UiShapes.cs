using UnityEngine;

namespace Crossroads.UI
{
    /// <summary>
    /// Procedurally generated UI sprites (visual transformation pass). The prototype HUD
    /// was built from raw rectangle Images - the single biggest reason it read as a debug
    /// build. This factory bakes a handful of tiny anti-aliased shapes (circle, ring,
    /// rounded panel, compass arrow, diamond) into Textures ONCE per session - no asset
    /// dependencies, ~40 KB of RGBA each, filtered bilinearly. Everything the mobile HUD
    /// builds afterwards composes these, so the whole UI shares one visual language.
    /// Headless-safe: generation is pure math, no scene access.
    /// </summary>
    public static class UiShapes
    {
        private const int Size = 96;
        private static Sprite _circle;
        private static Sprite _ring;
        private static Sprite _roundRect;
        private static Sprite _arrow;
        private static Sprite _diamond;
        private static Sprite _pill;

        public static Sprite Circle { get { return _circle != null ? _circle : (_circle = Bake(CircleAlpha)); } }
        public static Sprite Ring { get { return _ring != null ? _ring : (_ring = Bake(RingAlpha)); } }
        public static Sprite RoundRect { get { return _roundRect != null ? _roundRect : (_roundRect = Bake(RoundRectAlpha)); } }
        public static Sprite Arrow { get { return _arrow != null ? _arrow : (_arrow = Bake(ArrowAlpha)); } }
        public static Sprite Diamond { get { return _diamond != null ? _diamond : (_diamond = Bake(DiamondAlpha)); } }
        public static Sprite Pill { get { return _pill != null ? _pill : (_pill = Bake(PillAlpha)); } }

        /// <summary>Bakes one 96x96 smooth-alpha texture into a centred sprite.</summary>
        private static Sprite Bake(System.Func<float, float, float> alphaAt)
        {
            var tex = new Texture2D(Size, Size);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // pixel-centre coordinates in -1..1
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    float a = alphaAt(u, v);
                    pixels[y * Size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }

        // ---------------------------------------------------------------- shape fields
        // All distances are in the -1..1 square; ~1.5 px of smoothstep = anti-aliased edge.

        private static float CircleAlpha(float u, float v)
        {
            float d = Mathf.Sqrt(u * u + v * v);
            return Edge(d, 1f, 0.035f);
        }

        private static float RingAlpha(float u, float v)
        {
            float d = Mathf.Sqrt(u * u + v * v);
            return Edge(d, 0.86f, 0.045f) * (1f - Edge(d, 0.70f, 0.045f));
        }

        private static float RoundRectAlpha(float u, float v)
        {
            // rounded square: distance to a rounded box (radius 0.34)
            const float r = 0.34f;
            float qx = Mathf.Abs(u) - (1f - r);
            float qy = Mathf.Abs(v) - (1f - r);
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            float d = Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            return Edge(d, 0f, 0.03f);
        }

        private static float PillAlpha(float u, float v)
        {
            // stadium: rounded only left+right, flat top+bottom
            const float r = 0.5f;
            float qx = Mathf.Abs(u) - (1f - r);
            float ox = Mathf.Max(qx, 0f);
            float d = Mathf.Sqrt(ox * ox + Mathf.Max(0f, Mathf.Abs(v) - 1f + 0.06f) * Mathf.Max(0f, Mathf.Abs(v) - 1f + 0.06f)) + Mathf.Min(qx, 0f) - r;
            return Edge(d, 0f, 0.03f) * Step(Mathf.Abs(v), 0.97f, 0.03f);
        }

        private static float ArrowAlpha(float u, float v)
        {
            // upward triangle centred in the square (minimap player arrow)
            float d = Mathf.Max(u * 0.92f + v * 0.34f - 0.18f, -v - 0.86f);
            return Edge(-d, 0f, 0.03f);
        }

        private static float DiamondAlpha(float u, float v)
        {
            float d = Mathf.Abs(u) + Mathf.Abs(v);
            return Edge(d, 0.92f, 0.04f);
        }

        // ---------------------------------------------------------------- helpers
        private static float Edge(float d, float threshold, float softness)
        {
            // 1 inside (d &lt; threshold), smooth falloff across +-softness
            return Mathf.Clamp01((threshold - d) / softness + 0.5f);
        }

        private static float Step(float v, float threshold, float softness)
        {
            return Mathf.Clamp01((threshold - v) / softness + 0.5f);
        }
    }

    /// <summary>
    /// The one-look visual language of the CROSSROADS mobile UI (VISUAL_TARGET §4/§9):
    /// dark glass panels, thin cyan strokes, round touch targets, consistent spacing.
    /// Every HUD element draws its colours/geometry from here so no component can drift.
    /// </summary>
    public static class HudTheme
    {
        // ---- glass panels ----
        public static readonly Color Glass = new Color(0.047f, 0.067f, 0.098f, 0.86f);
        public static readonly Color GlassSoft = new Color(0.055f, 0.078f, 0.114f, 0.62f);
        public static readonly Color GlassDeep = new Color(0.028f, 0.042f, 0.063f, 0.97f);
        public static readonly Color Stroke = new Color(0.30f, 0.85f, 0.95f, 0.30f);
        public static readonly Color StrokeDim = new Color(0.30f, 0.85f, 0.95f, 0.14f);

        // ---- semantic colours (canonical, from RuntimeMenuFactory) ----
        public static readonly Color Accent = new Color(0.30f, 0.85f, 0.95f, 1f);
        public static readonly Color TextMain = new Color(0.93f, 0.95f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.60f, 0.66f, 0.72f, 1f);
        public static readonly Color Ember = new Color(0.95f, 0.38f, 0.22f, 1f);
        public static readonly Color Tide = new Color(0.25f, 0.80f, 0.85f, 1f);
        public static readonly Color Stone = new Color(0.85f, 0.68f, 0.32f, 1f);
        public static readonly Color Hollow = new Color(0.62f, 0.38f, 0.92f, 1f);
        public static readonly Color Good = new Color(0.32f, 0.78f, 0.55f, 1f);
        public static readonly Color Warn = new Color(0.85f, 0.68f, 0.32f, 1f);
        public static readonly Color Bad = new Color(0.86f, 0.36f, 0.26f, 1f);

        // ---- touch geometry (reference 1920x1080; >= 88dp targets) ----
        public const float BigButton = 210f;    // attack
        public const float MidButton = 150f;    // dodge
        public const float AbilityButton = 132f;
        public const float SmallButton = 104f;  // pause / map
        public const float Corner = 40f;

        /// <summary>Health bar colour by fraction (green -> amber -> red).</summary>
        public static Color HealthFill(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (fraction > 0.5f) return Good;
            if (fraction > 0.25f) return Warn;
            return Bad;
        }

        /// <summary>Canonical colour for an ability/choice line id.</summary>
        public static Color LineColor(string line)
        {
            switch ((line ?? "").ToLowerInvariant())
            {
                case "ember": return Ember;
                case "tide": return Tide;
                case "stone": return Stone;
                case "hollow": return Hollow;
                default: return Accent;
            }
        }
    }
}
