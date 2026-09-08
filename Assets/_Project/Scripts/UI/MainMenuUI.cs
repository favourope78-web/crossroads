using Crossroads.Core;
using Crossroads.Gameplay;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// MAIN MENU (VISUAL_TARGET §7): the game's front door, presented over the live hall
    /// (no scene switch - Android hot path stays clean). Title + CONTINUE (only when an
    /// autosave exists) / NEW GAME (with confirm card) / SETTINGS (shared panel) /
    /// CREDITS / QUIT. Input-locked while open; the gameplay HUD and touch rig recede so
    /// the first frame a player sees is a game front door, not a developer tool.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        private GameObject _root;
        private GameObject _continueButton;
        private GameObject _confirmCard;
        private GameObject _creditsCard;
        private Text _saveStatus;
        private CanvasGroup _group;
        private bool _open;
        private bool _hadSave;

        public bool IsOpen { get { return _open; } }

        public static MainMenuUI Attach(RectTransform parent)
        {
            var menu = parent.gameObject.AddComponent<MainMenuUI>();
            menu.Build(parent);
            return menu;
        }

        private void Build(RectTransform parent)
        {
            var go = new GameObject("MainMenu");
            _root = go;
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _group = go.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;

            // backdrop: ORIGINAL keyart (art production pass) under a deep glass gradient,
            // stronger at the left where the menu sits - falls back to the plain glass shade
            if (ArtLibrary.HasKeyart)
            {
                var keyart = ArtLibrary.CreateArt("Keyart", rect, ArtLibrary.SpriteKind.KeyartMenu,
                    new Color(0.85f, 0.88f, 0.94f, 1f));
                keyart.SetAsFirstSibling();
            }
            var shade = RuntimeMenuFactory.CreatePanel("Shade", rect, new Color(0.012f, 0.02f, 0.035f, 0.86f));
            var srect = shade.rectTransform;
            srect.anchorMin = new Vector2(0f, 0f);
            srect.anchorMax = new Vector2(0.62f, 1f);
            srect.offsetMin = Vector2.zero;
            srect.offsetMax = Vector2.zero;

            var accentEdge = RuntimeMenuFactory.CreatePanel("AccentEdge", rect, new Color(0.30f, 0.85f, 0.95f, 0.5f));
            var erect = accentEdge.rectTransform;
            erect.anchorMin = erect.anchorMax = new Vector2(0.62f, 0.5f);
            erect.pivot = new Vector2(1f, 0.5f);
            erect.sizeDelta = new Vector2(4f, 900f);

            // ---- title block ----
            var title = RuntimeMenuFactory.CreateText("Title", rect, "CROSSROADS", 104, HudTheme.TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.offsetMin = new Vector2(110f, -236f);
            title.rectTransform.offsetMax = new Vector2(1100f, -96f);

            var tagline = RuntimeMenuFactory.CreateText("Tagline", rect, "every choice rewires the city", 34,
                HudTheme.Accent, TextAnchor.MiddleLeft, FontStyle.Italic);
            tagline.rectTransform.anchorMin = tagline.rectTransform.anchorMax = new Vector2(0f, 1f);
            tagline.rectTransform.pivot = new Vector2(0f, 1f);
            tagline.rectTransform.offsetMin = new Vector2(116f, -300f);
            tagline.rectTransform.offsetMax = new Vector2(900f, -240f);

            _saveStatus = RuntimeMenuFactory.CreateText("SaveStatus", rect, "", 26, HudTheme.TextDim,
                TextAnchor.MiddleLeft);
            _saveStatus.rectTransform.anchorMin = _saveStatus.rectTransform.anchorMax = new Vector2(0f, 1f);
            _saveStatus.rectTransform.pivot = new Vector2(0f, 1f);
            _saveStatus.rectTransform.offsetMin = new Vector2(116f, -352f);
            _saveStatus.rectTransform.offsetMax = new Vector2(900f, -306f);

            // ---- buttons ----
            _continueButton = MenuButton(rect, "Continue", "CONTINUE", 0, new Color(0.10f, 0.42f, 0.48f, 0.95f));
            _continueButton.GetComponent<Button>().onClick.AddListener(OnContinue);

            var newGame = MenuButton(rect, "NewGame", "NEW GAME", 1, new Color(0.13f, 0.19f, 0.26f, 0.95f));
            newGame.GetComponent<Button>().onClick.AddListener(OnNewGamePressed);

            var settings = MenuButton(rect, "Settings", "SETTINGS", 2, new Color(0.13f, 0.19f, 0.26f, 0.95f));
            settings.GetComponent<Button>().onClick.AddListener(OnSettingsPressed);

            var credits = MenuButton(rect, "Credits", "CREDITS", 3, new Color(0.13f, 0.19f, 0.26f, 0.95f));
            credits.GetComponent<Button>().onClick.AddListener(OnCreditsPressed);

            // story introduction surface (built once, driven by New Game)
            _intro = IntroCinematicUI.Attach(parent);

            var quit = MenuButton(rect, "Quit", "QUIT", 4, new Color(0.24f, 0.10f, 0.09f, 0.95f));
            quit.GetComponent<Button>().onClick.AddListener(OnQuitPressed);

            var version = RuntimeMenuFactory.CreateText("Version", rect, "prototype \u00B7 visual pass", 22,
                HudTheme.TextDim, TextAnchor.LowerRight);
            version.rectTransform.anchorMin = version.rectTransform.anchorMax = new Vector2(1f, 0f);
            version.rectTransform.pivot = new Vector2(1f, 0f);
            version.rectTransform.offsetMin = new Vector2(-360f, 22f);
            version.rectTransform.offsetMax = new Vector2(-44f, 56f);

            // ---- confirm card (new game) ----
            _confirmCard = BuildCard(rect, "ConfirmCard",
                "BEGIN A NEW LIFE?", "Your current run - choices, powers, the city's memory - will be overwritten.",
                "START OVER", "KEEP PLAYING");
            WireCardAction(_confirmCard, "Confirm", OnNewGameConfirmed);
            WireCardAction(_confirmCard, "KeepPlaying", OnCancelCard);

            // ---- credits card ----
            _creditsCard = BuildCard(rect, "CreditsCard",
                "CROSSROADS", "A decision-based life/action game.\n\nEvery character, hall and echo is original to this project.\nBuilt with Unity 6 + URP.",
                "CLOSE", null);
            WireCardAction(_creditsCard, "KeepPlaying", OnCancelCard);

            _confirmCard.SetActive(false);
            _creditsCard.SetActive(false);
            _root.SetActive(false);
        }

        private static GameObject MenuButton(RectTransform parent, string name, string label, int index, Color bg)
        {
            var btn = RuntimeMenuFactory.CreateButton(name, parent, label, 38, bg, HudTheme.TextMain);
            var r = ((Image)btn.targetGraphic).rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            float top = -400f - index * 118f;
            r.offsetMin = new Vector2(110f, top - 96f);
            r.offsetMax = new Vector2(110f + 520f, top);
            return btn.gameObject;
        }

        /// <summary>A centred glass card with title, body and one or two actions.</summary>
        private static GameObject BuildCard(RectTransform parent, string name, string title, string body,
            string confirmLabel, string cancelLabel)
        {
            var card = RuntimeMenuFactory.CreatePanel(name, parent, HudTheme.GlassDeep);
            var rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(860f, 520f);
            rect.anchoredPosition = Vector2.zero;

            var t = RuntimeMenuFactory.CreateText("Title", rect, title, 48, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            t.rectTransform.pivot = new Vector2(0.5f, 1f);
            t.rectTransform.offsetMin = new Vector2(-380f, -110f);
            t.rectTransform.offsetMax = new Vector2(380f, -40f);

            var b = RuntimeMenuFactory.CreateText("Body", rect, body, 30, HudTheme.TextDim,
                TextAnchor.UpperCenter);
            b.rectTransform.anchorMin = b.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            b.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            b.rectTransform.offsetMin = new Vector2(-360f, -60f);
            b.rectTransform.offsetMax = new Vector2(360f, 100f);

            var confirm = RuntimeMenuFactory.CreateButton("Confirm", rect, confirmLabel, 34,
                new Color(0.10f, 0.42f, 0.48f, 0.95f), HudTheme.TextMain);
            var crect = ((Image)confirm.targetGraphic).rectTransform;
            crect.anchorMin = crect.anchorMax = new Vector2(0.5f, 0f);
            crect.pivot = new Vector2(0.5f, 0f);
            crect.sizeDelta = new Vector2(360f, 104f);
            crect.anchoredPosition = new Vector2(-200f, 46f);
            confirm.onClick.AddListener(delegate { confirm.transform.parent.gameObject.SetActive(false); });

            GameObject cardGo = card.gameObject;
            if (!string.IsNullOrEmpty(cancelLabel))
            {
                var cancel = RuntimeMenuFactory.CreateButton("KeepPlaying", rect, cancelLabel, 34,
                    new Color(0.13f, 0.19f, 0.26f, 0.95f), HudTheme.TextMain);
                var xrect = ((Image)cancel.targetGraphic).rectTransform;
                xrect.anchorMin = xrect.anchorMax = new Vector2(0.5f, 0f);
                xrect.pivot = new Vector2(0.5f, 0f);
                xrect.sizeDelta = new Vector2(360f, 104f);
                xrect.anchoredPosition = new Vector2(200f, 46f);
                cancel.onClick.AddListener(delegate { cardGo.SetActive(false); });
            }
            return cardGo;
        }

        private static void WireCardAction(GameObject card, string buttonName, UnityEngine.Events.UnityAction action)
        {
            var buttons = card.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].name == buttonName) buttons[i].onClick.AddListener(action);
        }

        // ---------------------------------------------------------------- lifecycle
        private void OnEnable()
        {
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void OnStateLoaded(StateLoadedEvent e)
        {
            _hadSave = e.hadSave;
            RefreshSaveStatus();
        }

        private void OnStateReset(StateResetEvent e)
        {
            _hadSave = false;
            RefreshSaveStatus();
        }

        private void RefreshSaveStatus()
        {
            if (_saveStatus == null) return;
            _saveStatus.text = _hadSave
                ? "an autosave remembers your last choice"
                : "a new life waits in the fracture hall";
            if (_continueButton != null) _continueButton.SetActive(_hadSave);
        }

        /// <summary>Sets save presence before the StateLoaded event can arrive (boot order safety).</summary>
        public void SetSavePresence(bool hadSave)
        {
            _hadSave = hadSave;
            RefreshSaveStatus();
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            _root.SetActive(true);
            _group.blocksRaycasts = true;
            InputLock.Set(true, "main menu");
            RefreshSaveStatus();
            if (MenuShown != null) MenuShown(true);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _confirmCard.SetActive(false);
            _creditsCard.SetActive(false);
            _group.blocksRaycasts = false;
            _root.SetActive(false);
            // only release the lock we own (dialogue/pause keep theirs)
            if (InputLock.Active && InputLock.Reason == "main menu") InputLock.Set(false, "main menu");
            if (MenuShown != null) MenuShown(false);
        }

        /// <summary>GameUIBootstrap subscribes: hide the gameplay HUD + touch rig behind the menu.</summary>
        public static event System.Action<bool> MenuShown;

        // ---------------------------------------------------------------- actions
        private void OnContinue()
        {
            Close();
        }

        private IntroCinematicUI _intro;

        private void OnNewGamePressed()
        {
            _confirmCard.SetActive(true);
        }

        private void OnNewGameConfirmed()
        {
            _confirmCard.SetActive(false);
            if (GameServices.IsInitialized) GameServices.ResetRun();
            if (_intro != null)
            {
                // story introduction (art production pass) -> THEN the hall opens
                _intro.Play(delegate
                {
                    EventBus.Publish(new NoticeRequestEvent { text = "A new life begins" });
                    Close();
                });
            }
            else
            {
                EventBus.Publish(new NoticeRequestEvent { text = "A new life begins" });
                Close();
            }
        }

        private void OnCancelCard()
        {
            _confirmCard.SetActive(false);
            _creditsCard.SetActive(false);
        }

        private void OnSettingsPressed()
        {
            var panel = FindFirstObjectByType<SettingsPanelUI>();
            if (panel != null) panel.Open();
        }

        private void OnCreditsPressed()
        {
            _creditsCard.SetActive(true);
        }

        private void OnQuitPressed()
        {
#if UNITY_EDITOR
            Debug.Log("[CROSSROADS] quit requested (editor - ignored)");
#else
            Application.Quit();
#endif
        }
    }
}
