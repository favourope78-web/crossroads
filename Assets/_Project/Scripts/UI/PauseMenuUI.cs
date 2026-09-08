using Crossroads.Core;
using Crossroads.Gameplay;
using Crossroads.Gameplay.Input;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// PAUSE MENU (VISUAL_TARGET §7): RESUME / SETTINGS / SAVE &amp; CLOSE / MAIN MENU.
    /// Pause freezes simulation time (Time.timeScale = 0) so world, combat and cameras
    /// stop together; unscaled UI keeps working. Settings live in the shared
    /// SettingsPanelUI (same panel as the main menu) - one code path, one look.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        private GameObject _panel;
        private Text _saveChip;
        private bool _open;

        public bool IsOpen { get { return _open; } }

        public static PauseMenuUI Attach(RectTransform parent)
        {
            var menu = parent.gameObject.AddComponent<PauseMenuUI>();
            menu.Build(parent);
            return menu;
        }

        private void Build(RectTransform parent)
        {
            _panel = RuntimeMenuFactory.CreatePanel("PausePanel", parent, new Color(0.02f, 0.035f, 0.06f, 0.94f)).gameObject;
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 640f);
            rect.anchoredPosition = Vector2.zero;
            _panel.SetActive(false);

            var edge = RuntimeMenuFactory.CreatePanel("Edge", rect, new Color(0.30f, 0.85f, 0.95f, 0.5f));
            var erect = edge.rectTransform;
            erect.anchorMin = erect.anchorMax = new Vector2(0f, 0.5f);
            erect.pivot = new Vector2(0f, 0.5f);
            erect.sizeDelta = new Vector2(5f, 560f);
            erect.anchoredPosition = Vector2.zero;

            var title = RuntimeMenuFactory.CreateText("Title", rect, "PAUSED", 56, RuntimeMenuFactory.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(-320f, -96f);
            title.rectTransform.offsetMax = new Vector2(320f, -32f);

            _saveChip = RuntimeMenuFactory.CreateText("SaveChip", rect, "", 24, RuntimeMenuFactory.TextDim,
                TextAnchor.MiddleCenter, FontStyle.Italic);
            _saveChip.rectTransform.anchorMin = _saveChip.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _saveChip.rectTransform.pivot = new Vector2(0.5f, 1f);
            _saveChip.rectTransform.offsetMin = new Vector2(-320f, -134f);
            _saveChip.rectTransform.offsetMax = new Vector2(320f, -96f);

            var resume = BuildButton(rect, "Resume", "RESUME", 0, new Color(0.10f, 0.42f, 0.48f, 0.95f));
            resume.onClick.AddListener(Close);

            var settings = BuildButton(rect, "Settings", "SETTINGS", 1, new Color(0.13f, 0.19f, 0.26f, 0.95f));
            settings.onClick.AddListener(OnSettingsPressed);

            var saveBtn = BuildButton(rect, "SaveClose", "SAVE & CLOSE", 2, new Color(0.16f, 0.30f, 0.20f, 0.95f));
            saveBtn.onClick.AddListener(SaveAndClose);

            var menuBtn = BuildButton(rect, "MainMenu", "MAIN MENU", 3, new Color(0.13f, 0.19f, 0.26f, 0.95f));
            menuBtn.onClick.AddListener(OnMainMenuPressed);
        }

        private Button BuildButton(RectTransform parent, string name, string label, int index, Color bg)
        {
            var btn = RuntimeMenuFactory.CreateButton(name, parent, label, 36, bg, RuntimeMenuFactory.TextMain);
            var r = ((Image)btn.targetGraphic).rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.sizeDelta = new Vector2(520f, 104f);
            r.anchoredPosition = new Vector2(0f, 40f + (3 - index) * 122f);
            return btn;
        }

        // ---------------------------------------------------------------- state machine
        public void Open()
        {
            if (_open) return;
            _open = true;
            RefreshSaveChip();
            _panel.SetActive(true);
            Time.timeScale = 0f;              // freeze world + combat + cameras together
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _panel.SetActive(false);
            Time.timeScale = 1f;
        }

        private void RefreshSaveChip()
        {
            if (_saveChip == null) return;
            _saveChip.text = GameServices.IsInitialized && GameServices.Save != null && GameServices.Save.Exists()
                ? "autosave ready - your choices are remembered"
                : "no autosave yet";
        }

        private void SaveAndClose()
        {
            if (GameServices.Progress != null) GameServices.PersistNow(autosaveMirror: true); // progress save
            Close();
        }

        private void OnSettingsPressed()
        {
            var panel = FindFirstObjectByType<SettingsPanelUI>();
            if (panel != null) panel.Open();
        }

        private void OnMainMenuPressed()
        {
            // quit-to-menu semantics: stay paused underneath, main menu takes the screen;
            // CONTINUE from the menu resumes exactly like RESUME would
            Close();
            var menu = FindFirstObjectByType<MainMenuUI>();
            if (menu != null) menu.Open();
        }

        private void OnDestroy()
        {
            // never leave the game frozen if the menu is destroyed mid-pause
            if (_open) Time.timeScale = 1f;
        }
    }
}
