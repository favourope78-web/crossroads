using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Mobile-friendly power sheet (GAME_DESIGN §8.1 layout, right side - the left is the
    /// virtual stick + INTERACT zone): one big [POWERS] toggle (>= 88dp) and a slide-up
    /// sheet listing every known ability with its live state - locked (with the unlock
    /// hint), sealed (blocked by a choice), or Lv X with cooldown. Tapping an available
    /// ability activates it through the data-driven AbilityManager; the world effect
    /// (AbilityPulseVFX) and NPCs react to the raised event.
    /// No per-frame work while the sheet is closed (cooldowns tick only when visible;
    /// they are computed on demand from an injected clock - nothing scans per frame).
    /// </summary>
    public class AbilityHUD : MonoBehaviour
    {
        private GameObject _toggleRoot;
        private GameObject _sheetRoot;
        private Text _toggleLabel;
        private Text _title;
        private readonly List<GameObject> _rowRoots = new List<GameObject>();
        private readonly List<Text> _rowStateTexts = new List<Text>();
        private readonly List<Text> _rowNameTexts = new List<Text>();
        private float _nextCooldownTick;

        public static AbilityHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<AbilityHUD>();
            hud.Build(parent);
            return hud;
        }

        // ---------------------------------------------------------------- construction
        private void Build(RectTransform parent)
        {
            // [POWERS] toggle - bottom-right, mirrored geometry of the INTERACT button
            var togglePanel = RuntimeMenuFactory.CreatePanel("PowersButton", parent, RuntimeMenuFactory.Panel);
            _toggleRoot = togglePanel.gameObject;
            var trect = togglePanel.rectTransform;
            trect.anchorMin = new Vector2(1f, 0f);
            trect.anchorMax = new Vector2(1f, 0f);
            trect.pivot = new Vector2(1f, 0f);
            trect.offsetMin = new Vector2(-380f, 150f);
            trect.offsetMax = new Vector2(-60f, 260f);

            var tbtn = togglePanel.gameObject.AddComponent<Button>();
            tbtn.targetGraphic = togglePanel;
            var tcolors = tbtn.colors;
            tcolors.highlightedColor = new Color(RuntimeMenuFactory.Accent.r, RuntimeMenuFactory.Accent.g, RuntimeMenuFactory.Accent.b, 0.5f);
            tcolors.pressedColor = new Color(RuntimeMenuFactory.Accent.r * 0.7f, RuntimeMenuFactory.Accent.g * 0.7f, RuntimeMenuFactory.Accent.b * 0.7f, 1f);
            tbtn.colors = tcolors;
            tbtn.onClick.AddListener(ToggleSheet);

            _toggleLabel = RuntimeMenuFactory.CreateText("Label", trect, "POWERS", 38, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_toggleLabel.rectTransform, 20f, 20f, 12f, 12f);

            // sheet
            var sheetPanel = RuntimeMenuFactory.CreatePanel("PowersSheet", parent, RuntimeMenuFactory.PanelSoft);
            _sheetRoot = sheetPanel.gameObject;
            var srect = sheetPanel.rectTransform;
            srect.anchorMin = new Vector2(1f, 0f);
            srect.anchorMax = new Vector2(1f, 0f);
            srect.pivot = new Vector2(1f, 0f);
            srect.offsetMin = new Vector2(-580f, 290f);
            srect.offsetMax = new Vector2(-60f, 680f);

            _title = RuntimeMenuFactory.CreateText("Title", srect, "POWERS", 34, RuntimeMenuFactory.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            var trect2 = _title.rectTransform;
            trect2.anchorMin = new Vector2(0f, 1f);
            trect2.anchorMax = new Vector2(1f, 1f);
            trect2.pivot = new Vector2(0.5f, 1f);
            trect2.offsetMin = new Vector2(24f, -64f);
            trect2.offsetMax = new Vector2(-24f, -14f);

            BuildRows(srect);
            _sheetRoot.SetActive(false);
        }

        private void BuildRows(RectTransform parent)
        {
            // three known abilities; rows rebuilt from definitions (data-driven count)
            AbilityManager mgr = GameServices.Abilities;
            List<AbilityDefinitionData> defs = mgr != null ? mgr.Definitions : null;
            int count = defs != null ? defs.Count : 0;
            for (int i = 0; i < count; i++)
            {
                AbilityDefinitionData def = defs[i];
                if (def == null) continue;

                int index = _rowRoots.Count;
                var rowPanel = RuntimeMenuFactory.CreatePanel("Row_" + def.id, parent, PanelColour(def.line));
                var rrect = rowPanel.rectTransform;
                rrect.anchorMin = new Vector2(0f, 1f);
                rrect.anchorMax = new Vector2(1f, 1f);
                rrect.pivot = new Vector2(0.5f, 1f);
                float top = -90f - index * 128f;
                rrect.offsetMin = new Vector2(20f, top - 112f);
                rrect.offsetMax = new Vector2(-20f, top);

                var btn = rowPanel.gameObject.AddComponent<Button>();
                btn.targetGraphic = rowPanel;
                var colors = btn.colors;
                colors.highlightedColor = new Color(RuntimeMenuFactory.Accent.r, RuntimeMenuFactory.Accent.g, RuntimeMenuFactory.Accent.b, 0.45f);
                btn.colors = colors;
                string abilityId = def.id;
                btn.onClick.AddListener(delegate { OnRowPressed(abilityId); });

                var nameText = RuntimeMenuFactory.CreateText("Name", rrect, def.name, 34, RuntimeMenuFactory.TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
                var nrect = nameText.rectTransform;
                nrect.anchorMin = new Vector2(0f, 1f);
                nrect.anchorMax = new Vector2(1f, 1f);
                nrect.pivot = new Vector2(0.5f, 1f);
                nrect.offsetMin = new Vector2(24f, -52f);
                nrect.offsetMax = new Vector2(-24f, -14f);

                var stateText = RuntimeMenuFactory.CreateText("State", rrect, "", 26, RuntimeMenuFactory.TextDim, TextAnchor.MiddleLeft);
                var srect2 = stateText.rectTransform;
                srect2.anchorMin = new Vector2(0f, 0f);
                srect2.anchorMax = new Vector2(1f, 1f);
                srect2.pivot = new Vector2(0.5f, 0.5f);
                srect2.offsetMin = new Vector2(24f, 8f);
                srect2.offsetMax = new Vector2(-24f, -46f);

                _rowRoots.Add(rowPanel.gameObject);
                _rowNameTexts.Add(nameText);
                _rowStateTexts.Add(stateText);
            }
        }

        private static Color PanelColour(string line)
        {
            // per-line tinted row panels (sanctioned palette: line colours as trim only)
            switch ((line ?? "").ToLowerInvariant())
            {
                case "ember": return new Color(RuntimeMenuFactory.Panel.r + 0.06f, RuntimeMenuFactory.Panel.g, RuntimeMenuFactory.Panel.b, 0.92f);
                case "tide": return new Color(RuntimeMenuFactory.Panel.r, RuntimeMenuFactory.Panel.g + 0.04f, RuntimeMenuFactory.Panel.b + 0.04f, 0.92f);
                case "stone": return new Color(RuntimeMenuFactory.Panel.r + 0.03f, RuntimeMenuFactory.Panel.g + 0.02f, RuntimeMenuFactory.Panel.b, 0.92f);
                default: return RuntimeMenuFactory.PanelSoft;
            }
        }

        // ---------------------------------------------------------------- events
        private void OnEnable()
        {
            EventBus.Subscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Subscribe<AbilityLevelChangedEvent>(OnAbilityLevelChanged);
            EventBus.Subscribe<AbilityBlockedEvent>(OnAbilityBlocked);
            EventBus.Subscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Subscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AbilityUnlockedEvent>(OnAbilityUnlocked);
            EventBus.Unsubscribe<AbilityLevelChangedEvent>(OnAbilityLevelChanged);
            EventBus.Unsubscribe<AbilityBlockedEvent>(OnAbilityBlocked);
            EventBus.Unsubscribe<DialogueStartedEvent>(OnDialogueStarted);
            EventBus.Unsubscribe<DialogueEndedEvent>(OnDialogueEnded);
        }

        private void OnAbilityUnlocked(AbilityUnlockedEvent e) { Refresh(); }
        private void OnAbilityLevelChanged(AbilityLevelChangedEvent e) { Refresh(); }
        private void OnAbilityBlocked(AbilityBlockedEvent e) { Refresh(); }
        private void OnDialogueStarted(DialogueStartedEvent e) { _sheetRoot.SetActive(false); }
        private void OnDialogueEnded(DialogueEndedEvent e) { /* player re-opens manually */ }

        private void ToggleSheet()
        {
            bool open = !_sheetRoot.activeSelf;
            _sheetRoot.SetActive(open);
            if (open)
            {
                Refresh();
                _nextCooldownTick = 0f; // force first tick this frame
            }
        }

        private void Update()
        {
            // cooldown labels tick ONLY while the sheet is open (no idle per-frame work)
            if (!_sheetRoot.activeSelf || Time.time < _nextCooldownTick) return;
            _nextCooldownTick = Time.time + 0.25f;
            RefreshCooldowns();
        }

        // ---------------------------------------------------------------- refresh
        public void Refresh()
        {
            AbilityManager mgr = GameServices.Abilities;
            if (mgr == null) return;
            List<AbilityRowView> rows = AbilitySheetModel.Build(mgr);
            int n = Mathf.Min(rows.Count, _rowRoots.Count);
            for (int i = 0; i < n; i++)
            {
                // task: show only what the player OWNS - locked lines are hidden entirely
                // (blocked lines stay visible: the player owns them, a decision sealed them)
                _rowRoots[i].SetActive(rows[i].access != AbilityAccessState.Locked);
                _rowNameTexts[i].text = rows[i].name;
                _rowStateTexts[i].text = rows[i].stateText;
                _rowStateTexts[i].color = rows[i].access == AbilityAccessState.Unlocked
                    ? RuntimeMenuFactory.Tide
                    : (rows[i].access == AbilityAccessState.Blocked ? RuntimeMenuFactory.Stone : RuntimeMenuFactory.TextDim);
            }
        }

        private void RefreshCooldowns()
        {
            AbilityManager mgr = GameServices.Abilities;
            if (mgr == null) return;
            List<AbilityRowView> rows = AbilitySheetModel.Build(mgr);
            int n = Mathf.Min(rows.Count, _rowStateTexts.Count);
            for (int i = 0; i < n; i++)
            {
                if (rows[i].access == AbilityAccessState.Unlocked)
                    _rowStateTexts[i].text = rows[i].stateText;
            }
        }

        // ---------------------------------------------------------------- activation
        private void OnRowPressed(string abilityId)
        {
            if (InputLock.Active) return;
            AbilityManager mgr = GameServices.Abilities;
            if (mgr == null) return;

            AbilityDefinitionData def = mgr.Find(abilityId);
            string name = def != null ? def.name : abilityId;
            switch (mgr.Activate(abilityId))
            {
                case AbilityActivation.Ok:
                    EventBus.Publish(new NoticeRequestEvent { text = name + " — the echo answers" });
                    Refresh();
                    break;
                case AbilityActivation.CoolingDown:
                    float rem = mgr.CooldownRemaining(abilityId);
                    EventBus.Publish(new NoticeRequestEvent { text = name + " is still recharging (" + ((int)(rem + 0.99f)) + "s)" });
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
}
