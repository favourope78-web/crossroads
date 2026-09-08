using Crossroads.Gameplay;
using Crossroads.Gameplay.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// SHARED SETTINGS PANEL (VISUAL_TARGET §7): the full live-stepper settings list,
    /// presented identically inside the pause menu and the main menu - one visual
    /// language, one code path. Every change applies LIVE (audio, quality tier, control
    /// rig) and persists immediately to player_settings.json, exactly like the previous
    /// pause-only implementation (same SettingsNudge/ApplySideEffects seams the
    /// mobile-experience tests exercise).
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        private GameObject _panel;
        private readonly Text[] _values = new Text[9];

        public bool IsOpen { get { return _panel != null && _panel.activeSelf; } }

        public static SettingsPanelUI Attach(RectTransform parent)
        {
            var panel = parent.gameObject.AddComponent<SettingsPanelUI>();
            panel.Build(parent);
            return panel;
        }

        private void Build(RectTransform parent)
        {
            _panel = RuntimeMenuFactory.CreatePanel("SettingsPanel", parent, HudTheme.GlassDeep).gameObject;
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(980f, 940f);
            rect.anchoredPosition = Vector2.zero;
            _panel.SetActive(false);

            var frame = RuntimeMenuFactory.CreatePanel("Frame", rect, new Color(0.30f, 0.85f, 0.95f, 0.25f));
            var frect = frame.rectTransform;
            frect.anchorMin = new Vector2(0f, 1f);
            frect.anchorMax = new Vector2(0f, 1f);
            frect.pivot = new Vector2(0f, 1f);
            frect.offsetMin = new Vector2(0f, -6f);
            frect.offsetMax = new Vector2(6f, 0f);

            var title = RuntimeMenuFactory.CreateText("Title", rect, "SETTINGS", 52, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(-400f, -86f);
            title.rectTransform.offsetMax = new Vector2(400f, -26f);

            // ---- steppers: look / camera / controls / audio / video ----
            _values[0] = BuildStepper(rect, "Look sensitivity", delegate { Nudge(SettingId.Sensitivity, -1); }, delegate { Nudge(SettingId.Sensitivity, +1); });
            _values[1] = BuildStepper(rect, "Invert look Y", delegate { Nudge(SettingId.InvertLookY, +1); }, delegate { Nudge(SettingId.InvertLookY, +1); });
            _values[2] = BuildStepper(rect, "Camera distance", delegate { Nudge(SettingId.CameraDistance, -1); }, delegate { Nudge(SettingId.CameraDistance, +1); });
            _values[3] = BuildStepper(rect, "Control size", delegate { Nudge(SettingId.ButtonScale, -1); }, delegate { Nudge(SettingId.ButtonScale, +1); });
            _values[4] = BuildStepper(rect, "Control opacity", delegate { Nudge(SettingId.ControlOpacity, -1); }, delegate { Nudge(SettingId.ControlOpacity, +1); });
            _values[5] = BuildStepper(rect, "Left-handed", delegate { Nudge(SettingId.LeftHanded, +1); }, delegate { Nudge(SettingId.LeftHanded, +1); });
            _values[6] = BuildStepper(rect, "Touch controls", delegate { Nudge(SettingId.TouchVisibility, +1); }, delegate { Nudge(SettingId.TouchVisibility, +1); });
            _values[7] = BuildStepper(rect, "Audio volume", delegate { Nudge(SettingId.Volume, -1); }, delegate { Nudge(SettingId.Volume, +1); });
            _values[8] = BuildStepper(rect, "Graphics quality", delegate { Nudge(SettingId.Quality, -1); }, delegate { Nudge(SettingId.Quality, +1); });

            var close = RuntimeMenuFactory.CreateButton("Close", rect, "CLOSE", 36,
                new Color(0.10f, 0.42f, 0.48f, 0.95f), HudTheme.TextMain);
            var crect = ((Image)close.targetGraphic).rectTransform;
            crect.anchorMin = crect.anchorMax = new Vector2(0.5f, 0f);
            crect.pivot = new Vector2(0.5f, 0f);
            crect.sizeDelta = new Vector2(420f, 110f);
            crect.anchoredPosition = new Vector2(0f, 34f);
            close.onClick.AddListener(Close);

            RefreshValues();
        }

        private Text BuildStepper(RectTransform parent, string label,
            UnityEngine.Events.UnityAction onMinus, UnityEngine.Events.UnityAction onPlus)
        {
            int row = System.Array.IndexOf(_values, null);
            if (row < 0) return null;
            float top = -110f - row * 88f;

            var text = RuntimeMenuFactory.CreateText("Label_" + row, parent, label, 28, HudTheme.TextDim,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
            text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(48f, top);
            text.rectTransform.sizeDelta = new Vector2(390f, 74f);

            // [-] right-most, value centre, [+] beside it (thumb order: minus, plus)
            var minus = RuntimeMenuFactory.CreateButton("Minus_" + row, parent, "\u2013", 36,
                new Color(0.10f, 0.16f, 0.22f, 0.95f), HudTheme.TextMain);
            PinTopRight(minus, -48f, top, 88f, 74f);
            minus.onClick.AddListener(onMinus);

            var plus = RuntimeMenuFactory.CreateButton("Plus_" + row, parent, "+", 36,
                new Color(0.10f, 0.16f, 0.22f, 0.95f), HudTheme.TextMain);
            PinTopRight(plus, -140f, top, 88f, 74f);
            plus.onClick.AddListener(onPlus);

            var value = RuntimeMenuFactory.CreateText("Value_" + row, parent, "", 28, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            value.rectTransform.anchorMin = value.rectTransform.anchorMax = new Vector2(1f, 1f);
            value.rectTransform.pivot = new Vector2(1f, 1f);
            value.rectTransform.anchoredPosition = new Vector2(-250f, top);
            value.rectTransform.sizeDelta = new Vector2(190f, 74f);

            _values[row] = value;
            return value;
        }

        private static void PinTopRight(Button button, float x, float y, float w, float h)
        {
            var r = ((Image)button.targetGraphic).rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        public void Open()
        {
            if (_panel == null) return;
            RefreshValues();
            _panel.SetActive(true);
        }

        public void Close()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>The single settings mutator: pure clamp via SettingsNudge, then refresh/apply/persist.</summary>
        public void Nudge(int settingId, int direction)
        {
            InputSettings s = InputSettingsStore.Current;
            SettingsNudge.Apply(s, settingId, direction);
            RefreshValues();
            ApplySideEffects();
            InputSettingsStore.Save(s); // live-persist: survives an app kill mid-session
        }

        /// <summary>Pushes settings into the systems that cache them (audio, framerate, rig scale/opacity).</summary>
        private void ApplySideEffects()
        {
            InputSettings s = InputSettingsStore.Current;
            AudioListener.volume = s.audioVolume;
            int tier = Mathf.Clamp(s.qualityLevel, 0, QualitySettings.names.Length - 1);
            Application.targetFrameRate = QualityTierApplier.TargetFrameRate(tier);
            if (QualitySettings.GetQualityLevel() != tier) QualitySettings.SetQualityLevel(tier, true);
            QualityTierApplier.ApplyTier(tier);
            var rig = FindFirstObjectByType<MobileControlsUI>();
            if (rig != null) rig.ApplySettings();
        }

        private void RefreshValues()
        {
            InputSettings s = InputSettingsStore.Current;
            if (_values[0] != null) _values[0].text = s.lookSensitivity.ToString("0.0");
            if (_values[1] != null) _values[1].text = s.invertLookY ? "ON" : "OFF";
            if (_values[2] != null) _values[2].text = s.cameraDistance.ToString("0.0") + " m";
            if (_values[3] != null) _values[3].text = s.buttonScale.ToString("0.00") + "\u00D7";
            if (_values[4] != null) _values[4].text = s.controlOpacity.ToString("0.00");
            if (_values[5] != null) _values[5].text = s.leftHanded ? "ON" : "OFF";
            if (_values[6] != null) _values[6].text = s.showTouchControls == 0 ? "Auto" : s.showTouchControls == 1 ? "Always" : "Never";
            if (_values[7] != null) _values[7].text = s.audioVolume.ToString("0.0");
            if (_values[8] != null)
                _values[8].text = s.qualityLevel == 0 ? "Low (30fps)" : s.qualityLevel == 1 ? "Balanced (60fps)" : "High (60fps)";
        }
    }
}
