using System.Text;
using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// Combat feedback HUD (task: health display, damage feedback, ability feedback,
    /// enemy state feedback, defeat feedback). Fully event-driven (refreshes on combat
    /// events only - no per-frame work beyond a damage-flash fade):
    ///   - player health bar (bottom-left, above the INTERACT zone) + status chips
    ///   - ATTACK + DODGE touch buttons (bottom-right, under POWERS; >=88dp targets)
    ///   - enemy name / state / health bar (top-center) while an enemy is engaged
    ///   - red flash on player damage, white flash on hits landed, toasts on defeats
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        private Text _hpLabel;
        private Image _hpFill;
        private Image _hpPanel;
        private Text _statusLine;
        private GameObject _enemyRoot;
        private Text _enemyLabel;
        private Text _enemyState;
        private Image _enemyFill;
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
            // ---- player health (bottom-left, above the INTERACT zone) ----
            _hpPanel = RuntimeMenuFactory.CreatePanel("PlayerHealth", parent, RuntimeMenuFactory.Panel);
            var hrect = _hpPanel.rectTransform;
            hrect.anchorMin = new Vector2(0f, 0f);
            hrect.anchorMax = new Vector2(0f, 0f);
            hrect.pivot = new Vector2(0f, 0f);
            hrect.offsetMin = new Vector2(60f, 24f);
            hrect.offsetMax = new Vector2(60f + 520f, 24f + 104f);

            _hpFill = RuntimeMenuFactory.CreatePanel("Fill", hrect, new Color(0.32f, 0.78f, 0.55f, 0.95f));
            var frect = _hpFill.rectTransform;
            frect.anchorMin = new Vector2(0f, 0f);
            frect.anchorMax = new Vector2(1f, 1f);
            frect.offsetMin = new Vector2(10f, 10f);
            frect.offsetMax = new Vector2(-10f, -10f);

            _hpLabel = RuntimeMenuFactory.CreateText("Label", hrect, "ARI  100/100", 34, RuntimeMenuFactory.TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            RuntimeMenuFactory.Stretch(_hpLabel.rectTransform, 12f, 12f, 8f, 8f);

            _statusLine = RuntimeMenuFactory.CreateText("Statuses", hrect, "", 26, RuntimeMenuFactory.Tide, TextAnchor.UpperLeft);
            var srect = _statusLine.rectTransform;
            srect.anchorMin = new Vector2(0f, 1f);
            srect.anchorMax = new Vector2(1f, 1f);
            srect.pivot = new Vector2(0.5f, 1f);
            srect.offsetMin = new Vector2(14f, -78f);
            srect.offsetMax = new Vector2(-14f, -2f);

            // ---- ATTACK + DODGE (bottom-right, below POWERS) ----
            var attack = RuntimeMenuFactory.CreateButton("AttackButton", parent, "ATTACK", 38,
                new Color(0.16f, 0.24f, 0.30f, 0.95f), RuntimeMenuFactory.TextMain);
            var arect = ((Image)attack.targetGraphic).rectTransform;
            arect.anchorMin = new Vector2(1f, 0f);
            arect.anchorMax = new Vector2(1f, 0f);
            arect.pivot = new Vector2(1f, 0f);
            arect.offsetMin = new Vector2(-330f, 24f);
            arect.offsetMax = new Vector2(-90f, 134f);
            attack.onClick.AddListener(OnAttackPressed);

            var dodge = RuntimeMenuFactory.CreateButton("DodgeButton", parent, "DODGE", 32,
                new Color(0.13f, 0.20f, 0.26f, 0.95f), RuntimeMenuFactory.TextMain);
            var drect = ((Image)dodge.targetGraphic).rectTransform;
            drect.anchorMin = new Vector2(1f, 0f);
            drect.anchorMax = new Vector2(1f, 0f);
            drect.pivot = new Vector2(1f, 0f);
            drect.offsetMin = new Vector2(-470f, 34f);
            drect.offsetMax = new Vector2(-350f, 124f);
            dodge.onClick.AddListener(OnDodgePressed);

            // ---- enemy bar (top-center, appears while engaged) ----
            _enemyRoot = RuntimeMenuFactory.CreatePanel("EnemyBar", parent, RuntimeMenuFactory.Panel).gameObject;
            var erect = _enemyRoot.GetComponent<RectTransform>();
            erect.anchorMin = new Vector2(0.5f, 1f);
            erect.anchorMax = new Vector2(0.5f, 1f);
            erect.pivot = new Vector2(0.5f, 1f);
            erect.offsetMin = new Vector2(-350f, -150f);
            erect.offsetMax = new Vector2(350f, -60f);

            _enemyFill = RuntimeMenuFactory.CreatePanel("Fill", erect, new Color(0.86f, 0.36f, 0.26f, 0.95f));
            var efrect = _enemyFill.rectTransform;
            efrect.anchorMin = new Vector2(0f, 0f);
            efrect.anchorMax = new Vector2(1f, 1f);
            efrect.offsetMin = new Vector2(8f, 8f);
            efrect.offsetMax = new Vector2(-8f, -44f);

            _enemyLabel = RuntimeMenuFactory.CreateText("Name", erect, "", 30, RuntimeMenuFactory.TextMain, TextAnchor.UpperLeft, FontStyle.Bold);
            var elrect = _enemyLabel.rectTransform;
            elrect.anchorMin = new Vector2(0f, 1f);
            elrect.anchorMax = new Vector2(1f, 1f);
            elrect.pivot = new Vector2(0.5f, 1f);
            elrect.offsetMin = new Vector2(16f, -40f);
            elrect.offsetMax = new Vector2(-16f, -2f);

            _enemyState = RuntimeMenuFactory.CreateText("State", erect, "", 26, RuntimeMenuFactory.Stone, TextAnchor.LowerLeft);
            var esrect = _enemyState.rectTransform;
            esrect.anchorMin = new Vector2(0f, 0f);
            esrect.anchorMax = new Vector2(1f, 0f);
            esrect.pivot = new Vector2(0.5f, 0f);
            esrect.offsetMin = new Vector2(16f, 6f);
            esrect.offsetMax = new Vector2(-16f, 40f);

            _enemyRoot.SetActive(false);
            RefreshPlayerBar(1f, 1f, 0f);
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
                RefreshPlayerBar(e.remainingHealth, e.maxHealth, e.amount);
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
            if (e.isPlayer) RefreshPlayerBar(e.remainingHealth, e.maxHealth, 0f);
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
                    _enemyState.text = "destroyed";
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
            _enemyState.text = e.stateLabel;
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
        private void RefreshPlayerBar(float hp, float maxHp, float lastHit)
        {
            float frac = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 1f;
            _hpFill.rectTransform.offsetMax = new Vector2(-10f - (1f - frac) * 500f, -10f);
            _hpFill.color = frac > 0.5f ? new Color(0.32f, 0.78f, 0.55f, 0.95f)
                : frac > 0.25f ? new Color(0.85f, 0.68f, 0.32f, 0.95f)
                : new Color(0.86f, 0.36f, 0.26f, 0.95f);
            _sb.Length = 0;
            _sb.Append("ARI  ").Append(Mathf.CeilToInt(hp)).Append('/').Append(Mathf.CeilToInt(maxHp));
            if (lastHit > 0f) _sb.Append("   -").Append(Mathf.CeilToInt(lastHit));
            _hpLabel.text = _sb.ToString();
        }

        private void RefreshEnemyBar(float hp, float maxHp)
        {
            float frac = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
            _enemyFill.rectTransform.offsetMax = new Vector2(-8f - (1f - frac) * 676f, -44f);
            _enemyRoot.SetActive(true);
        }

        private void RefreshStatusChips()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var controller = player != null ? player.GetComponent<PlayerCombatController>() : null;
            var combatant = controller != null ? controller.Combatant : null;
            if (combatant == null) { _statusLine.text = ""; return; }
            _sb.Length = 0;
            var statuses = combatant.Statuses;
            for (int i = 0; i < statuses.Count; i++)
            {
                if (_sb.Length > 0) _sb.Append(" · ");
                _sb.Append(statuses[i].definition.name);
            }
            _statusLine.text = _sb.ToString();
        }

        // ---------------------------------------------------------------- buttons + fade
        private void OnAttackPressed()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var controller = player.GetComponent<PlayerCombatController>();
            if (controller != null) controller.TryAttack();
        }

        private void OnDodgePressed()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            var controller = player.GetComponent<PlayerCombatController>();
            if (controller != null) controller.TryDodge();
        }

        private void Update()
        {
            // flash fades + enemy-bar auto-hide only (no allocations)
            if (_playerFlash > 0f)
            {
                _playerFlash -= Time.unscaledDeltaTime;
                _hpPanel.color = _playerFlash > 0f
                    ? new Color(0.35f, 0.08f, 0.06f, 0.92f)
                    : RuntimeMenuFactory.Panel;
            }
            if (_enemyRoot.activeSelf && Time.unscaledTime > _enemyBarUntil && _enemyFlash <= 0f)
                _enemyRoot.SetActive(false);
            if (_enemyFlash > 0f) _enemyFlash -= Time.unscaledDeltaTime;
        }
    }
}
