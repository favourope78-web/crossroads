using System.Text;
using Crossroads.Core;
using Crossroads.Gameplay;
using Crossroads.Narrative;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// THE player HUD (VISUAL_TARGET §4): top-left cluster that answers "who am I, how am
    /// I doing, where am I" in one glance -
    ///   portrait ring + name, HEALTH bar (delayed-damage ghost fill), echoes chip,
    ///   area chip on arrival (fades), autosave pip.
    /// Replaces the old always-on StateHUD stat dump as the normal player's health surface
    /// (the stat dump itself is dev-gated now). Fully event-driven; the only per-frame
    /// work is the damage-ghost ease and chip fades (two lerps, no allocations).
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        private GameObject _root;
        private Image _hpFill;
        private Image _hpGhost;
        private Text _hpLabel;
        private Text _echoLabel;
        private Text _areaLabel;
        private GameObject _areaChip;
        private GameObject _savePip;
        private CanvasGroup _areaGroup;
        private CanvasGroup _saveGroup;

        private float _hpFraction = 1f;
        private float _ghostFraction = 1f;
        private float _ghostDelay;
        private float _areaVisibleUntil;
        private float _saveVisibleUntil;
        private Transform _player;
        private float _nextPlayerSearch;
        private readonly StringBuilder _sb = new StringBuilder(24);

        public static PlayerHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<PlayerHUD>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            var go = new GameObject("PlayerHUD");
            _root = go;
            var rect = go.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(HudTheme.Corner, -HudTheme.Corner);
            rect.sizeDelta = new Vector2(560f, 210f);

            // ---- portrait ring (glass circle + accent ring + initial) ----
            var portrait = new GameObject("Portrait");
            var prect = portrait.AddComponent<RectTransform>();
            prect.SetParent(rect, false);
            prect.anchorMin = prect.anchorMax = new Vector2(0f, 1f);
            prect.pivot = new Vector2(0.5f, 0.5f);
            prect.anchoredPosition = new Vector2(62f, -62f);
            prect.sizeDelta = new Vector2(104f, 104f);
            AddShape(portrait, UiShapes.Circle, new Color(0.05f, 0.075f, 0.11f, 0.95f), 104f);
            AddShape(portrait, UiShapes.Ring, HudTheme.Stroke, 112f);
            var initial = RuntimeMenuFactory.CreateText("Initial", prect, "A", 46, HudTheme.Accent,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(initial.rectTransform);
            initial.raycastTarget = false;

            var name = RuntimeMenuFactory.CreateText("Name", rect, "ARI", 30, HudTheme.TextMain,
                TextAnchor.LowerLeft, FontStyle.Bold);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, 1f);
            name.rectTransform.pivot = new Vector2(0f, 1f);
            name.rectTransform.offsetMin = new Vector2(130f, -104f);
            name.rectTransform.offsetMax = new Vector2(300f, -52f);

            // ---- health bar (fill + delayed ghost) ----
            var hpBack = new GameObject("HpBack");
            var hrect = hpBack.AddComponent<RectTransform>();
            hrect.SetParent(rect, false);
            hrect.anchorMin = hrect.anchorMax = new Vector2(0f, 1f);
            hrect.pivot = new Vector2(0f, 1f);
            hrect.anchoredPosition = new Vector2(130f, -116f);
            hrect.sizeDelta = new Vector2(360f, 36f);
            AddShape(hpBack, UiShapes.Pill, new Color(0.03f, 0.05f, 0.075f, 0.92f), new Vector2(360f, 36f));

            _hpGhost = AddShape(hpBack, UiShapes.Pill, new Color(0.9f, 0.45f, 0.3f, 0.55f), new Vector2(348f, 24f), new Vector2(6f, -6f));
            _hpFill = AddShape(hpBack, UiShapes.Pill, HudTheme.Good, new Vector2(348f, 24f), new Vector2(6f, -6f));
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = 0;

            _hpLabel = RuntimeMenuFactory.CreateText("HpLabel", hrect, "100/100", 24, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(_hpLabel.rectTransform);
            _hpLabel.raycastTarget = false;

            // ---- echoes chip (currency) ----
            var echoes = new GameObject("Echoes");
            var erect = echoes.AddComponent<RectTransform>();
            erect.SetParent(rect, false);
            erect.anchorMin = erect.anchorMax = new Vector2(0f, 0f);
            erect.pivot = new Vector2(0f, 0f);
            erect.anchoredPosition = new Vector2(130f, 34f);
            erect.sizeDelta = new Vector2(210f, 54f);
            AddShape(echoes, UiShapes.Pill, HudTheme.GlassSoft, new Vector2(210f, 54f));
            _echoLabel = RuntimeMenuFactory.CreateText("EchoLabel", erect, "\u25C8 0", 26, HudTheme.Stone,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(_echoLabel.rectTransform);
            _echoLabel.raycastTarget = false;

            // ---- autosave pip ----
            var pip = new GameObject("SavePip");
            var pipRect = pip.AddComponent<RectTransform>();
            pipRect.SetParent(rect, false);
            pipRect.anchorMin = pipRect.anchorMax = new Vector2(1f, 0f);
            pipRect.pivot = new Vector2(1f, 0.5f);
            pipRect.anchoredPosition = new Vector2(-16f, 27f);
            pipRect.sizeDelta = new Vector2(30f, 30f);
            _saveGroup = pip.AddComponent<CanvasGroup>();
            _saveGroup.alpha = 0f;
            AddShape(pip, UiShapes.Circle, HudTheme.Good, 26f);
            _savePip = pip;

            // ---- area chip (arrives on area/location change, fades) ----
            var chip = new GameObject("AreaChip");
            var crect = chip.AddComponent<RectTransform>();
            crect.SetParent(rect, false);
            crect.anchorMin = crect.anchorMax = new Vector2(0f, 0f);
            crect.pivot = new Vector2(0f, 0f);
            crect.anchoredPosition = new Vector2(0f, -66f);
            crect.sizeDelta = new Vector2(430f, 58f);
            AddShape(chip, UiShapes.Pill, new Color(0.035f, 0.055f, 0.085f, 0.88f), new Vector2(430f, 58f));
            _areaGroup = chip.AddComponent<CanvasGroup>();
            _areaGroup.alpha = 0f;
            _areaLabel = RuntimeMenuFactory.CreateText("AreaLabel", crect, "", 28, HudTheme.TextMain,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(_areaLabel.rectTransform);
            _areaLabel.raycastTarget = false;
            _areaChip = chip;

            ApplyHealth(1f, 1f);
            RefreshEchoes();
        }

        private static Image AddShape(GameObject parent, Sprite sprite, Color color, float size)
        {
            return AddShape(parent, sprite, color, new Vector2(size, size), Vector2.zero);
        }

        private static Image AddShape(GameObject parent, Sprite sprite, Color color, Vector2 size)
        {
            return AddShape(parent, sprite, color, size, Vector2.zero);
        }

        private static Image AddShape(GameObject parent, Sprite sprite, Color color, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(sprite == UiShapes.Pill ? "PillShape" : "Shape");
            var r = go.AddComponent<RectTransform>();
            r.SetParent(parent.transform, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- events
        private void OnEnable()
        {
            EventBus.Subscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Subscribe<CombatantHealedEvent>(OnHealed);
            EventBus.Subscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Subscribe<ItemChangedEvent>(OnItemChanged);
            EventBus.Subscribe<AreaChangedEvent>(OnAreaChanged);
            EventBus.Subscribe<LocationArrivedEvent>(OnLocationArrived);
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
            EventBus.Subscribe<SaveCompletedEvent>(OnSaved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Unsubscribe<CombatantHealedEvent>(OnHealed);
            EventBus.Unsubscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Unsubscribe<ItemChangedEvent>(OnItemChanged);
            EventBus.Unsubscribe<AreaChangedEvent>(OnAreaChanged);
            EventBus.Unsubscribe<LocationArrivedEvent>(OnLocationArrived);
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
            EventBus.Unsubscribe<SaveCompletedEvent>(OnSaved);
        }

        private void OnDamaged(CombatantDamagedEvent e)
        {
            if (!e.isPlayer) return;
            ApplyHealth(e.remainingHealth, e.maxHealth);
            _ghostDelay = 0.55f;
        }

        private void OnHealed(CombatantHealedEvent e)
        {
            if (!e.isPlayer) return;
            ApplyHealth(e.remainingHealth, e.maxHealth);
        }

        private void OnDefeated(CombatantDefeatedEvent e)
        {
            if (!e.isPlayer) return;
            ApplyHealth(0f, 1f);
        }

        private void OnItemChanged(ItemChangedEvent e) { RefreshEchoes(); }

        private void OnAreaChanged(AreaChangedEvent e) { ShowArea(PrettyArea(e.areaId)); }

        private void OnLocationArrived(LocationArrivedEvent e)
        {
            if (!string.IsNullOrEmpty(e.name)) ShowArea(e.name.ToUpperInvariant());
        }

        private void OnStateLoaded(StateLoadedEvent e)
        {
            _hpFraction = -1f; // force re-read from the combat controller next Update
            RefreshEchoes();
        }

        private void OnStateReset(StateResetEvent e)
        {
            _hpFraction = -1f;
            _ghostFraction = 1f;
            RefreshEchoes();
        }

        private void OnSaved(SaveCompletedEvent e)
        {
            _saveVisibleUntil = Time.unscaledTime + (e.ok ? 1.6f : 2.6f);
        }

        // ---------------------------------------------------------------- frame
        private void Update()
        {
            // initial HP pull (once the player exists; then purely event-driven)
            if (_hpFraction < 0f)
            {
                if (Time.time >= _nextPlayerSearch)
                {
                    _nextPlayerSearch = Time.time + 0.5f;
                    var controller = FindPlayerCombat();
                    if (controller != null)
                    {
                        float hp = controller.HealthFraction;
                        ApplyHealth(hp, 1f);
                        _ghostFraction = hp;
                    }
                }
            }

            // delayed damage ghost: holds, then eases toward the real value
            if (_ghostDelay > 0f) _ghostDelay -= Time.deltaTime;
            else if (_ghostFraction > _hpFraction + 0.001f)
            {
                _ghostFraction = Mathf.MoveTowards(_ghostFraction, _hpFraction, Time.deltaTime * 0.55f);
                _hpGhost.fillAmount = _ghostFraction;
            }

            // chip fades (unscaled - they must finish through pauses)
            if (_areaGroup != null)
            {
                float until = _areaVisibleUntil - Time.unscaledTime;
                _areaGroup.alpha = until > 0f ? Mathf.Clamp01(until / 0.6f) : 0f;
                if (until <= -0.1f && _areaChip.activeSelf) _areaChip.SetActive(false);
            }
            if (_saveGroup != null)
            {
                float until = _saveVisibleUntil - Time.unscaledTime;
                _saveGroup.alpha = until > 0f ? Mathf.Clamp01(until / 0.4f) : 0f;
            }
        }

        // ---------------------------------------------------------------- helpers
        private void ApplyHealth(float hp, float maxHp)
        {
            float frac = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 1f;
            _hpFraction = frac;
            if (_ghostFraction < frac) // heals move the ghost immediately
            {
                _ghostFraction = frac;
            }
            _hpFill.fillAmount = frac;
            _hpFill.color = HudTheme.HealthFill(frac);
            _hpGhost.fillAmount = _ghostFraction;
            _hpLabel.text = HpText(hp < 0f ? 0f : hp, maxHp);
        }

        private void RefreshEchoes()
        {
            int echoes = 0;
            if (GameServices.IsInitialized && GameServices.State != null) echoes = GameServices.State.State.echoBank;
            _sb.Length = 0;
            _sb.Append("\u25C8 ").Append(echoes);
            _echoLabel.text = _sb.ToString();
        }

        private void ShowArea(string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            _areaLabel.text = label;
            _areaChip.SetActive(true);
            _areaVisibleUntil = Time.unscaledTime + 3.4f;
        }

        private static string PrettyArea(string areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return "";
            var sb = new System.Text.StringBuilder(areaId.Length + 4);
            bool upper = true;
            for (int i = 0; i < areaId.Length; i++)
            {
                char c = areaId[i];
                if (c == '_' || c == '-')
                {
                    sb.Append(' ');
                    upper = true;
                }
                else sb.Append(upper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                upper = false;
            }
            return sb.ToString();
        }

        private static PlayerCombatController FindPlayerCombat()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<PlayerCombatController>() : null;
        }

        /// <summary>Pure label formatting (headless-testable): 98/100.</summary>
        public static string HpText(float hp, float maxHp)
        {
            int h = Mathf.CeilToInt(Mathf.Max(0f, hp));
            int m = Mathf.CeilToInt(Mathf.Max(0f, maxHp));
            return h + "/" + m;
        }
    }
}
