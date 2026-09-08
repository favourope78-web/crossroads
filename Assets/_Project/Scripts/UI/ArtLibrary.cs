using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Scene-wired art library (art production pass). Holds the project's ORIGINAL
    /// production art (menu keyart, story stills, wordmark) as sprite references bound
    /// by the scene generator - same pattern as GameAudio's clip fields. UI surfaces ask
    /// ArtLibrary for art and degrade gracefully to the procedural HudTheme panels when
    /// a reference is missing (tests, editor scrub) so nothing ever renders a missing-
    /// asset placeholder to a player.
    /// </summary>
    public class ArtLibrary : MonoBehaviour
    {
        public Sprite logo;
        public Sprite keyartMenu;
        public Sprite stillFracture;
        public Sprite stillPier;
        public Sprite stillCity;
        public Sprite stillDawn;

        public static ArtLibrary Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Story intro beats in play order: (still, chapter label, caption).</summary>
        public static readonly (SpriteKind still, string label, string caption)[] IntroBeats =
        {
            (SpriteKind.StillFracture, "PROLOGUE",
             "The night the sky broke, the city learned its oldest lesson:\nevery fracture is also a door."),
            (SpriteKind.StillPier, "BEFORE",
             "You were ten. Mara had a paper kite, Dax had a dare,\nand the summer had not ended yet."),
            (SpriteKind.StillCity, "NOW",
             "Ten years on, the echoes of that night still answer to a voice like yours.\nThe Trode is waiting to ask its question."),
        };

        public enum SpriteKind { Logo, KeyartMenu, StillFracture, StillPier, StillCity, StillDawn }

        public static Sprite Get(SpriteKind kind)
        {
            var lib = Instance;
            if (lib == null) return null;
            switch (kind)
            {
                case SpriteKind.Logo: return lib.logo;
                case SpriteKind.KeyartMenu: return lib.keyartMenu;
                case SpriteKind.StillFracture: return lib.stillFracture;
                case SpriteKind.StillPier: return lib.stillPier;
                case SpriteKind.StillCity: return lib.stillCity;
                case SpriteKind.StillDawn: return lib.stillDawn;
                default: return null;
            }
        }

        /// <summary>True when the library carries the keyart/menu art (screens can branch their layout on this).</summary>
        public static bool HasKeyart { get { return Get(SpriteKind.KeyartMenu) != null; } }

        /// <summary>Attaches a full-bleed art image (cover-scaled, aspect-preserved) to a parent. Returns null-IO-free:</summary>
        public static RectTransform CreateArt(string name, RectTransform parent, SpriteKind kind, Color tint)
        {
            var sprite = Get(kind);
            var panel = RuntimeMenuFactory.CreatePanel(name, parent, tint);
            var img = panel;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
            }
            RuntimeMenuFactory.Stretch(img.rectTransform);
            return img.rectTransform;
        }
    }
}
