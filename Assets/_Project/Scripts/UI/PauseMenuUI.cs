using Crossroads.Core;
using Crossroads.Gameplay.Input;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Pause + settings overlay (task 8): Resume, look sensitivity, camera distance, audio
    /// volume, graphics quality. Settings apply LIVE and persist immediately to
    /// player_settings.json (own file - never bumps the save schema). Pause freezes
    /// simulation time (Time.timeScale = 0) so world, combat and cameras stop together;
    /// unscaled UI keeps working. Small -/+ steppers instead of sliders: more reliable
    /// touch targets, no extra dependencies.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        public enum Setting { Sensitivity, CameraDistance, Volume, Quality }

        private GameObject _panel;
        private Text _sensValue;
        private Text _distValue;
        private Text _volValue;
        private Text _qualityValue;
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
            _panel = RuntimeMenuFactory.CreatePanel("PausePanel", parent, new Color(0.03f, 0.05f, 0.08f, 0.96f)).gameObject;
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 860f);
            rect.anchoredPosition = Vector2.zero;
            _panel.SetActive(false);

            var title = RuntimeMenuFactory.CreateText("Title", rect, "PAUSED", 56, RuntimeMenuFactory.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(title.rectTransform, 0f, 0f, 640f, 20f);

            var resume = RuntimeMenuFactory.CreateButton("Resume", rect, "RESUME", 40,
                new Color(0.10f, 0.42f, 0.48f, 0.95f), RuntimeMenuFactory.TextMain);
            var rrect = ((Image)resume.targetGraphic).rectTransform;
            rrect.anchorMin = rrect.anchorMax = new Vector2(0.5f, 0f);
            rrect.pivot = new Vector2(0.5f, 0f);
            rrect.sizeDelta = new Vector2(520f, 130f);
            rrect.anchoredPosition = new Vector2(0f, 40f);
            resume.onClick.AddListener(Close);

            _sensValue = BuildStepper(rect, 0, "Look sensitivity",
                delegate { Nudge(Setting.Sensitivity, -1); }, delegate { Nudge(Setting.Sensitivity, +1); });
            _distValue = BuildStepper(rect, 1, "Camera distance",
                delegate { Nudge(Setting.CameraDistance, -1); }, delegate { Nudge(Setting.CameraDistance, +1); });
            _volValue = BuildStepper(rect, 2, "Audio volume",
                delegate { Nudge(Setting.Volume, -1); }, delegate { Nudge(Setting.Volume, +1); });
            _qualityValue = BuildStepper(rect, 3, "Graphics quality",
                delegate { Nudge(Setting.Quality, -1); }, delegate { Nudge(Setting.Quality, +1); });

            var saveBtn = RuntimeMenuFactory.CreateButton("SaveClose", rect, "SAVE & CLOSE", 32,
                new Color(0.16f, 0.30f, 0.20f, 0.95f), RuntimeMenuFactory.TextMain);
            var srect = ((Image)saveBtn.targetGraphic).rectTransform;
            srect.anchorMin = srect.anchorMax = new Vector2(0.5f, 0f);
            srect.pivot = new Vector2(0.5f, 0f);
            srect.sizeDelta = new Vector2(520f, 100f);
            srect.anchoredPosition = new Vector2(0f, 190f);
            saveBtn.onClick.AddListener(SaveAndClose);

            RefreshValues();
        }

        private Text BuildStepper(RectTransform parent, int row, string label,
            UnityEngine.Events.UnityAction onMinus, UnityEngine.Events.UnityAction onPlus)
        {
            float top = -120f - row * 118f;
            var text = RuntimeMenuFactory.CreateText("Label_" + row, parent, label, 30, RuntimeMenuFactory.TextDim,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(text.rectTransform, 40f, 430f, top, top - 92f);

            var minus = RuntimeMenuFactory.CreateButton("Minus_" + row, parent, "-", 40,
                new Color(0.10f, 0.16f, 0.22f, 0.95f), RuntimeMenuFactory.TextMain);
            var mrect = ((Image)minus.targetGraphic).rectTransform;
            mrect.anchorMin = mrect.anchorMax = new Vector2(1f, 1f);
            mrect.pivot = new Vector2(1f, 1f);
            mrect.sizeDelta = new Vector2(96f, 92f);
            mrect.anchoredPosition = new Vector2(-300f, top);
            minus.onClick.AddListener(onMinus);

            var value = RuntimeMenuFactory.CreateText("Value_" + row, parent, "", 30, RuntimeMenuFactory.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(value.rectTransform, -260f, 160f, top, top - 92f);

            var plus = RuntimeMenuFactory.CreateButton("Plus_" + row, parent, "+", 40,
                new Color(0.10f, 0.16f, 0.22f, 0.95f), RuntimeMenuFactory.TextMain);
            var prect = ((Image)plus.targetGraphic).rectTransform;
            prect.anchorMin = prect.anchorMax = new Vector2(1f, 1f);
            prect.pivot = new Vector2(1f, 1f);
            prect.sizeDelta = new Vector2(96f, 92f);
            prect.anchoredPosition = new Vector2(-40f, top);
            plus.onClick.AddListener(onPlus);

            return value;
        }

        // ---------------------------------------------------------------- state machine
        public void Open()
        {
            if (_open) return;
            _open = true;
            RefreshValues();
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

        private void SaveAndClose()
        {
            ApplySideEffects();
            InputSettingsStore.Save(InputSettingsStore.Current); // settings file
            if (GameServices.Progress != null) GameServices.PersistNow(autosaveMirror: true); // progress save
            Close();
        }

        /// <summary>The single settings mutator: pure clamp via SettingsNudge, then refresh/apply/persist.</summary>
        public void Nudge(Setting setting, int direction)
        {
            InputSettings s = InputSettingsStore.Current;
            SettingsNudge.Apply(s, (int)setting, direction);
            RefreshValues();
            ApplySideEffects();
            InputSettingsStore.Save(s); // live-persist: survives an app kill mid-session
        }

        /// <summary>Pushes settings into the systems that cache them (audio, framerate, rig scale/opacity).</summary>
        private void ApplySideEffects()
        {
            InputSettings s = InputSettingsStore.Current;
            AudioListener.volume = s.audioVolume;
            Application.targetFrameRate = s.qualityLevel == 0 ? 30 : 60; // Low caps battery burn
            var rig = FindFirstObjectByType<MobileControlsUI>();
            if (rig != null) rig.ApplySettings();
        }

        private void RefreshValues()
        {
            InputSettings s = InputSettingsStore.Current;
            if (_sensValue != null) _sensValue.text = s.lookSensitivity.ToString("0.0");
            if (_distValue != null) _distValue.text = s.cameraDistance.ToString("0.0") + " m";
            if (_volValue != null) _volValue.text = s.audioVolume.ToString("0.0");
            if (_qualityValue != null)
                _qualityValue.text = s.qualityLevel == 0 ? "Low (30fps)" : s.qualityLevel == 1 ? "Balanced (60fps)" : "High (60fps)";
        }

        private void OnDestroy()
        {
            // never leave the game frozen if the menu is destroyed mid-pause
            if (_open) Time.timeScale = 1f;
        }
    }
}
