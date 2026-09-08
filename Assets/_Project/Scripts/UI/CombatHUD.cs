using System.Text;
using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Combat feedback HUD (VISUAL_TARGET §6). The player's own health now lives in the
    /// always-on PlayerHUD (top-left); this component owns the fight:
    ///   - tracked enemy plate (top-centre): name + health bar while engaged
    ///   - player status chips (under the PlayerHUD cluster)
    ///   - red edge flash when the player is hit, white flash on hits landed
    ///   - defeat toasts
    /// Fully event-driven; the only per-frame work is two fading flashes.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        private GameObject _enemyRoot;
        private Text _enemyLabel;
        private Image _enemyFill;
        private Image _enemyFlashOverlay;
        private Text _statusLine;
        private Image _hurtVignette;
        private float _playerFlash;
        private float _enemyFlash;
        private string _trackedEnemyId = "";
        private string _trackedEnemyName = "";
        private float _enemyBarUntil;
        private readonly StringBuilder _sb = new StringBuilder(64);

        public static CombatHUD Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<CombatHUD>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            // ---- hurt vignette (fullscreen red edge flash on player damage) ----
            var vigGo = new GameObject("HurtFlash");
            var vrect = vigGo.AddComponent<RectTransform>();
            vrect.SetParent(parent, false);
            vrect.anchorMin = Vector2.zero;
            vrect.anchorMax = Vector2.one;
            vrect.offsetMin = Vector2.zero;
            vrect.offsetMax = Vector2.zero;
            _hurtVignette = vigGo.AddComponent<Image>();
            _hurtVignette.color = new Color(0.55f, 0.08f, 0.05f, 0f);
            _hurtVignette.raycastTarget = false;

            // ---- player status chips (top-left, under the PlayerHUD cluster) ----
            _statusLine = RuntimeMenuFactory.CreateText("Statuses", parent, "", 26, HudTheme.Tide, TextAnchor.UpperLeft);
            var srect = _statusLine.rectTransform;
            srect.anchorMin = srect.anchorMax = new Vector2(0f, 1f);
            srect.pivot = new Vector2(0f, 1f);
            srect.offsetMin = new Vector2(60f, -262f);
            srect.offsetMax = new Vector2(640f, -214f);
            _statusLine.raycastTarget = false;

            // ---- enemy plate (top-centre, appears while engaged) ----
            _enemyRoot = RuntimeMenuFactory.CreatePanel("EnemyBar", parent, new Color(0.04f, 0.06f, 0.09f, 0.9f)).gameObject;
            var erect = _enemyRoot.GetComponent<RectTransform>();
            erect.anchorMin = erect.anchorMax = new Vector2(0.5f, 1f);
            erect.pivot = new Vector2(0.5f, 1f);
            erect.offsetMin = new Vector2(-360f, -132f);
            erect.offsetMax = new Vector2(360f, -46f);

            var flash = RuntimeMenuFactory.CreatePanel("Flash", erect, new Color(1f, 1f, 1f, 0f));
            _enemyFlashOverlay = flash;
            var flrect = flash.rectTransform;
            flrect.anchorMin = Vector2.zero;
            flrect.anchorMax = Vector2.one;
            flrect.offsetMin = Vector2.zero;
            flrect.offsetMax = Vector2.zero;
            flash.raycastTarget = false;

            _enemyLabel = RuntimeMenuFactory.CreateText("Name", erect, "", 30, HudTheme.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
            var elrect = _enemyLabel.rectTransform;
            elrect.anchorMin = elrect.anchorMax = new Vector2(0f, 1f);
            elrect.pivot = new Vector2(0f, 1f);
            elrect.offsetMin = new Vector2(24f, -38f);
            elrect.offsetMax = new Vector2(-24f, -2f);
            _enemyLabel.raycastTarget = false;

            var barBack = RuntimeMenuFactory.CreatePanel("BarBack", erect, new Color(0.03f, 0.045f, 0.07f, 0.95f));
            var brect = barBack.rectTransform;
            brect.anchorMin = brect.anchorMax = new Vector2(0f, 0f);
            brect.pivot = new Vector2(0f, 0f);
            brect.offsetMin = new Vector2(24f, 12f);
            brect.offsetMax = new Vector2(-24f, 30f);
            barBack.raycastTarget = false;

            _enemyFill = RuntimeMenuFactory.CreatePanel("Fill", brect, new Color(0.86f, 0.36f, 0.26f, 0.95f));
            var efrect = _enemyFill.rectTransform;
            efrect.anchorMin = efrect.anchorMax = new Vector2(0f, 0.5f);
            efrect.pivot = new Vector2(0f, 0.5f);
            efrect.sizeDelta = new Vector2(676f, 12f);
            efrect.anchoredPosition = Vector2.zero;
            _enemyFill.type = Image.Type.Filled;
            _enemyFill.fillMethod = Image.FillMethod.Horizontal;
            _enemyFill.raycastTarget = false;

            _enemyRoot.SetActive(false);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Subscribe<CombatantHealedEvent>(OnHealed);
            EventBus.Subscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Subscribe<StatusChangedEvent>(OnStatusChanged);
            EventBus.Subscribe<EnemyStateChangedEvent>(OnEnemyStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Unsubscribe<CombatantHealedEvent>(OnHealed);
            EventBus.Unsubscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Unsubscribe<StatusChangedEvent>(OnStatusChanged);
            EventBus.Unsubscribe<EnemyStateChangedEvent>(OnEnemyStateChanged);
        }

        // ---------------------------------------------------------------- events
        private void OnDamaged(CombatantDamagedEvent e)
        {
            if (e.isPlayer)
            {
                _playerFlash = 0.5f;
            }
            else
            {
                _enemyFlash = 0.3f;
                TrackEnemy(e.enemyId, e.displayName);
                RefreshEnemyBar(e.remainingHealth, e.maxHealth);
            }
        }

        private void OnHealed(CombatantHealedEvent e)
        {
            // healing feedback rides the floating numbers (DamageNumberUI); nothing here
        }

        private void OnDefeated(CombatantDefeatedEvent e)
        {
            if (e.isPlayer)
            {
                _playerFlash = 1.2f;
                EventBus.Publish(new NoticeRequestEvent { text = "You fall - the hall lends you its floor, then its strength" });
            }
            else
            {
                _enemyFlash = 0.6f;
                if (_trackedEnemyId == e.enemyId || string.IsNullOrEmpty(_trackedEnemyId))
                {
                    _trackedEnemyId = e.enemyId;
                    _trackedEnemyName = e.displayName;
                    RefreshEnemyBar(0f, 1f);
                    _enemyBarUntil = Time.unscaledTime + 2.5f;
                }
            }
        }

        private void OnStatusChanged(StatusChangedEvent e)
        {
            if (!e.isPlayer) return;
            RefreshStatusChips();
        }

        private void OnEnemyStateChanged(EnemyStateChangedEvent e)
        {
            if (e.state == EnemyState.Dormant || e.state == EnemyState.Idle) return;
            TrackEnemy(e.enemyId, "");
            if (e.state == EnemyState.Defeat) RefreshEnemyBar(0f, 1f);
            _enemyBarUntil = Time.unscaledTime + 6f;
        }

        private void TrackEnemy(string enemyId, string displayName)
        {
            if (_trackedEnemyId != enemyId)
            {
                _trackedEnemyId = enemyId;
                _trackedEnemyName = displayName;
            }
            else if (!string.IsNullOrEmpty(displayName)) _trackedEnemyName = displayName;
            if (!string.IsNullOrEmpty(_trackedEnemyName)) _enemyLabel.text = _trackedEnemyName.ToUpperInvariant();
            _enemyRoot.SetActive(true);
            _enemyBarUntil = Time.unscaledTime + 6f;
        }

        // ---------------------------------------------------------------- refresh (event-driven)
        private void RefreshEnemyBar(float hp, float maxHp)
        {
            float frac = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
            _enemyFill.fillAmount = frac;
            _enemyRoot.SetActive(true);
        }

        private void RefreshStatusChips()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var controller = player != null ? player.GetComponent<PlayerCombatController>() : null;
            if (controller == null || controller.Combatant == null)
            {
                _statusLine.text = "";
                return;
            }
            _sb.Length = 0;
            var statuses = controller.Combatant.Statuses;
            for (int i = 0; i < statuses.Count; i++)
            {
                if (statuses[i] == null || statuses[i].definition == null) continue;
                if (_sb.Length > 0) _sb.Append("  \u00B7  ");
                _sb.Append(statuses[i].definition.id.ToUpperInvariant());
            }
            _statusLine.text = _sb.ToString();
        }

        private void Update()
        {
            // hurt vignette fade
            if (_playerFlash > 0f)
            {
                _playerFlash -= Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(_playerFlash) * 0.32f;
                Color c = _hurtVignette.color;
                c.a = a;
                _hurtVignette.color = c;
            }
            else if (_hurtVignette.color.a > 0f)
            {
                Color c = _hurtVignette.color;
                c.a = Mathf.Max(0f, c.a - Time.unscaledDeltaTime * 1.6f);
                _hurtVignette.color = c;
            }

            // enemy hit flash
            if (_enemyFlash > 0f)
            {
                _enemyFlash -= Time.unscaledDeltaTime;
                Color c = _enemyFlashOverlay.color;
                c.a = Mathf.Clamp01(_enemyFlash) * 0.35f;
                _enemyFlashOverlay.color = c;
            }

            // enemy plate auto-hide after the fight goes quiet
            if (_enemyRoot.activeSelf && Time.unscaledTime > _enemyBarUntil)
                _enemyRoot.SetActive(false);
        }
    }
}
