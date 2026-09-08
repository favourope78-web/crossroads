using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// ABILITY HOTBAR (VISUAL_TARGET §4/§9): the owned powers as round touch buttons in
    /// the bottom-right action cluster - one tap fires the ability exactly like the power
    /// sheet does (same AbilityManager path, same toasts). Line-coloured ring, glyph,
    /// radial cooldown sweep + remaining seconds. Only OWNED + UNLOCKED lines appear
    /// (locked lines are hidden - the player never sees what they don't have).
    /// Cooldown text ticks at 4 Hz and only while any button is actually cooling.
    /// </summary>
    public class AbilityHotbarHUD : MonoBehaviour
    {
        private const int MaxButtons = 3;
        private const float CooldownTick = 0.25f;

        private readonly GameObject[] _roots = new GameObject[MaxButtons];
        private readonly Image[] _rings = new Image[MaxButtons];
        private readonly Image[] _sweeps = new Image[MaxButtons];
        private readonly Text[] _glyphs = new Text[MaxButtons];
        private readonly Text[] _timers = new Text[MaxButtons];
        private readonly string[] _ids = new string[MaxButtons];
        private readonly Color[] _lineColors = new Color[MaxButtons];
        private int _count;
        private float _nextTick;
        private bool _anyCooling;

        public static AbilityHotbarHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<AbilityHotbarHUD>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            for (int i = 0; i < MaxButtons; i++)
            {
                var go = new GameObject("Ability" + i);
                _roots[i] = go;
                var rect = go.AddComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                // arc over the ATK/DODGE cluster: rightmost is lowest
                float x = -(56f + i * 158f);
                float y = 470f + (i % 2 == 0 ? 0f : 44f);
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(HudTheme.AbilityButton, HudTheme.AbilityButton);

                var back = go.AddComponent<Image>();
                back.sprite = UiShapes.Circle;
                back.color = new Color(0.05f, 0.075f, 0.11f, 0.88f);

                var ringGo = new GameObject("Ring");
                var rrect = ringGo.AddComponent<RectTransform>();
                rrect.SetParent(rect, false);
                Stretch(rrect);
                _rings[i] = ringGo.AddComponent<Image>();
                _rings[i].sprite = UiShapes.Ring;
                _rings[i].color = HudTheme.Accent;
                _rings[i].raycastTarget = false;

                // cooldown sweep: a dark disc that drains radially
                var sweepGo = new GameObject("Sweep");
                var srect = sweepGo.AddComponent<RectTransform>();
                srect.SetParent(rect, false);
                Stretch(srect);
                _sweeps[i] = sweepGo.AddComponent<Image>();
                _sweeps[i].sprite = UiShapes.Circle;
                _sweeps[i].color = new Color(0.01f, 0.02f, 0.03f, 0.78f);
                _sweeps[i].type = Image.Type.Filled;
                _sweeps[i].fillMethod = Image.FillMethod.Radial360;
                _sweeps[i].fillClockwise = true;
                _sweeps[i].fillOrigin = 2; // top
                _sweeps[i].fillAmount = 0f;
                _sweeps[i].raycastTarget = false;

                _glyphs[i] = RuntimeMenuFactory.CreateText("Glyph", rect, "\u2726", 44, HudTheme.TextMain,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                Stretch(_glyphs[i].rectTransform);
                _glyphs[i].raycastTarget = false;

                _timers[i] = RuntimeMenuFactory.CreateText("Timer", rect, "", 30, HudTheme.TextMain,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                Stretch(_timers[i].rectTransform);
                _timers[i].raycastTarget = false;

                var button = go.AddComponent<Button>();
                button.targetGraphic = back;
                var colors = button.colors;
                colors.highlightedColor = new Color(0.30f, 0.85f, 0.95f, 0.35f);
                colors.pressedColor = new Color(0.30f, 0.85f, 0.95f, 0.55f);
                button.colors = colors;
                int index = i;
                button.onClick.AddListener(delegate { OnPressed(index); });

                go.SetActive(false);
            }
            Refresh();
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Subscribe<AbilityLevelChangedEvent>(OnAbilityLevelChanged);
            EventBus.Subscribe<AbilityBlockedEvent>(OnAbilityBlocked);
            EventBus.Subscribe<AbilityUsedEvent>(OnAbilityUsed);
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Unsubscribe<AbilityLevelChangedEvent>(OnAbilityLevelChanged);
            EventBus.Unsubscribe<AbilityBlockedEvent>(OnAbilityBlocked);
            EventBus.Unsubscribe<AbilityUsedEvent>(OnAbilityUsed);
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
        }

        private void OnAbilityUnlocked(AbilityUnlockedEvent e) { Refresh(); }
        private void OnAbilityLevelChanged(AbilityLevelChangedEvent e) { Refresh(); }
        private void OnAbilityBlocked(AbilityBlockedEvent e) { Refresh(); }
        private void OnAbilityUsed(AbilityUsedEvent e) { _anyCooling = true; _nextTick = 0f; }
        private void OnStateLoaded(StateLoadedEvent e) { Refresh(); }
        private void OnStateReset(StateResetEvent e) { Refresh(); }

        private void Update()
        {
            if (!_anyCooling || Time.time < _nextTick) return;
            _nextTick = Time.time + CooldownTick;
            RefreshCooldowns();
        }

        // ---------------------------------------------------------------- refresh
        public void Refresh()
        {
            AbilityManager mgr = GameServices.Abilities;
            if (mgr == null) return;
            List<AbilityRowView> rows = AbilitySheetModel.Build(mgr);
            HotbarModel.Compose(rows, _ids, _lineColors, _glyphTexts, MaxButtons);
            int count = HotbarModel.LastComposedCount;

            for (int i = 0; i < MaxButtons; i++)
            {
                bool show = i < count;
                _roots[i].SetActive(show);
                if (!show) continue;
                _glyphs[i].text = _glyphTexts[i];
                _rings[i].color = new Color(_lineColors[i].r, _lineColors[i].g, _lineColors[i].b, 0.9f);
            }
            RefreshCooldowns();
        }

        private readonly string[] _glyphTexts = new string[MaxButtons];

        private void RefreshCooldowns()
        {
            AbilityManager mgr = GameServices.Abilities;
            if (mgr == null) return;
            bool anyCooling = false;
            for (int i = 0; i < MaxButtons; i++)
            {
                if (!_roots[i].activeSelf || string.IsNullOrEmpty(_ids[i])) continue;
                float remaining = mgr.CooldownRemaining(_ids[i]);
                AbilityLevelData row = mgr.CurrentRow(_ids[i]);
                float total = row != null ? row.cooldown : 0f;
                if (remaining > 0.01f)
                {
                    anyCooling = true;
                    float frac = total > 0.01f ? Mathf.Clamp01(remaining / total) : 1f;
                    _sweeps[i].fillAmount = frac;
                    _timers[i].text = HotbarModel.CooldownText(remaining);
                    _timers[i].enabled = true;
                }
                else
                {
                    _sweeps[i].fillAmount = 0f;
                    _timers[i].text = "";
                    _timers[i].enabled = false;
                }
            }
            _anyCooling = anyCooling;
        }

        // ---------------------------------------------------------------- activation
        private void OnPressed(int index)
        {
            if (index < 0 || index >= MaxButtons) return;
            string abilityId = _ids[index];
            if (string.IsNullOrEmpty(abilityId) || InputLock.Active) return;
            AbilityManager mgr = GameServices.Abilities;
            if (mgr == null) return;

            AbilityDefinitionData def = mgr.Find(abilityId);
            string name = def != null ? def.name : abilityId;
            switch (mgr.Activate(abilityId))
            {
                case AbilityActivation.Ok:
                    EventBus.Publish(new NoticeRequestEvent { text = name + " \u2014 the echo answers" });
                    _anyCooling = true;
                    _nextTick = 0f;
                    break;
                case AbilityActivation.CoolingDown:
                    EventBus.Publish(new NoticeRequestEvent { text = name + " is still recharging (" + HotbarModel.CooldownText(mgr.CooldownRemaining(abilityId)) + "s)" });
                    break;
                case AbilityActivation.Blocked:
                    EventBus.Publish(new NoticeRequestEvent { text = name + " was given back to the hall" });
                    break;
                case AbilityActivation.NotEnoughEnergy:
                    EventBus.Publish(new NoticeRequestEvent { text = "Not enough echoes for " + name });
                    break;
                default:
                    EventBus.Publish(new NoticeRequestEvent { text = name + " is not bound to you yet" });
                    break;
            }
        }
    }

    /// <summary>Pure hotbar derivation (headless-testable): owned+unlocked powers -> buttons.</summary>
    public static class HotbarModel
    {
        private static int _lastCount;

        public static int LastComposedCount { get { return _lastCount; } }

        /// <summary>
        /// Fills parallel button arrays from ability rows: only Unlocked rows, capped at
        /// maxButtons, ordered by definition. Glyph = first character of the ability name.
        /// </summary>
        public static void Compose(List<AbilityRowView> rows, string[] ids, Color[] lineColors, string[] glyphs, int maxButtons)
        {
            _lastCount = 0;
            if (rows == null || ids == null) return;
            int n = Mathf.Min(maxButtons, ids.Length);
            for (int i = 0; i < rows.Count && _lastCount < n; i++)
            {
                AbilityRowView row = rows[i];
                if (row == null || row.access != AbilityAccessState.Unlocked) continue;
                ids[_lastCount] = row.abilityId;
                if (lineColors != null && _lastCount < lineColors.Length)
                    lineColors[_lastCount] = HudTheme.LineColor(row.line);
                if (glyphs != null && _lastCount < glyphs.Length)
                    glyphs[_lastCount] = string.IsNullOrEmpty(row.name) ? "\u2726" : row.name.Substring(0, 1).ToUpperInvariant();
                _lastCount++;
            }
            for (int i = _lastCount; i < n; i++) ids[i] = "";
        }

        /// <summary>Cooldown label: whole seconds, ceiling ("3s"), sub-second shows "1s".</summary>
        public static string CooldownText(float seconds)
        {
            if (seconds <= 0f) return "";
            return ((int)(seconds + 0.999f)).ToString();
        }
    }
}
