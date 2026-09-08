using Crossroads.Core;
using Crossroads.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Crossroads.UI
{
    /// <summary>
    /// FLOATING COMBAT TEXT (VISUAL_TARGET §6): pooled damage numbers that rise and fade
    /// where the hit happened. Player damage = red, enemy damage = white, heavy hits =
    /// gold + larger. Fixed pool of 14 texts reused forever (zero runtime allocation),
    /// projected from world to screen each frame only while at least one is alive.
    /// No camera / no player (headless tests): everything no-ops safely.
    /// </summary>
    public class DamageNumberUI : MonoBehaviour
    {
        private const int PoolSize = 14;
        private const float LifeSeconds = 0.9f;

        private class Slot
        {
            public Text label;
            public RectTransform rect;
            public Vector3 worldPos;
            public float age;
            public bool alive;
            public float rise;
        }

        private readonly Slot[] _slots = new Slot[PoolSize];
        private int _cursor;
        private Transform _source; // parent canvas transform (screen-space anchors)

        public static DamageNumberUI Attach(RectTransform parent)
        {
            var hud = parent.gameObject.AddComponent<DamageNumberUI>();
            hud.Build(parent);
            return hud;
        }

        private void Build(RectTransform parent)
        {
            _source = parent;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Dmg" + i);
                var rect = go.AddComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(220f, 64f);
                var label = RuntimeMenuFactory.CreateText("Text", rect, "", 44, HudTheme.TextMain,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.raycastTarget = false;
                go.SetActive(false);
                _slots[i] = new Slot { label = label, rect = rect };
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Subscribe<CombatantHealedEvent>(OnHealed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Unsubscribe<CombatantHealedEvent>(OnHealed);
        }

        private void OnDamaged(CombatantDamagedEvent e)
        {
            if (e.amount <= 0f) return;
            Vector3 pos = e.isPlayer ? PlayerHead() : EnemyPosition(e.enemyId, e.combatantId);
            Spawn(pos, "-" + Mathf.CeilToInt(e.amount), e.isPlayer ? HudTheme.Bad : (e.amount >= 20f ? HudTheme.Stone : HudTheme.TextMain), e.amount >= 20f ? 1.25f : 1f);
        }

        private void OnHealed(CombatantHealedEvent e)
        {
            if (!e.isPlayer || e.amount <= 0f) return;
            Spawn(PlayerHead(), "+" + Mathf.CeilToInt(e.amount), HudTheme.Good, 1f);
        }

        /// <summary>World position above the player's head (tag lookup cached).</summary>
        private Vector3 PlayerHead()
        {
            if (_player == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return Vector3.zero;
                _player = player.transform;
            }
            return _player.position + Vector3.up * 1.6f;
        }

        /// <summary>World position of the hit enemy (registry lookup; falls back to the player).</summary>
        private Vector3 EnemyPosition(string enemyId, string combatantId)
        {
            var enemies = CombatDirector.LiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent a = enemies[i];
                if (a == null) continue;
                if (a.EnemyId == enemyId || a.GetInstanceID().ToString() == combatantId)
                    return a.transform.position + Vector3.up * 1.5f;
            }
            return PlayerHead() + Vector3.up * 0.4f;
        }

        private Transform _player;

        private void Spawn(Vector3 worldPos, string text, Color color, float scale)
        {
            Slot slot = null;
            for (int i = 0; i < PoolSize; i++)
            {
                int idx = (_cursor + i) % PoolSize;
                if (!_slots[idx].alive) { slot = _slots[idx]; _cursor = (idx + 1) % PoolSize; break; }
            }
            if (slot == null)
            {
                slot = _slots[_cursor]; // pool exhausted: recycle the oldest-ish slot
                _cursor = (_cursor + 1) % PoolSize;
            }
            slot.worldPos = worldPos;
            slot.age = 0f;
            slot.alive = true;
            slot.rise = 0f;
            slot.label.text = text;
            slot.label.color = color;
            slot.label.fontSize = Mathf.RoundToInt(44f * scale);
            slot.rect.gameObject.SetActive(true);
        }

        private void Update()
        {
            var cam = Camera.main;
            for (int i = 0; i < PoolSize; i++)
            {
                Slot s = _slots[i];
                if (!s.alive) continue;
                s.age += Time.unscaledDeltaTime;
                if (s.age >= LifeSeconds)
                {
                    s.alive = false;
                    s.rect.gameObject.SetActive(false);
                    continue;
                }
                if (cam == null) continue;
                Vector3 screen = cam.WorldToScreenPoint(s.worldPos);
                float k = s.age / LifeSeconds;
                s.rise = 30f + 90f * k;                                       // rise over life
                float jitterX = Mathf.Sin(i * 2.4f) * 26f;                    // spread stacked hits
                s.rect.anchoredPosition = new Vector2(
                    screen.x - 960f + jitterX,                                 // canvas-centre coordinates
                    screen.y - 540f + s.rise);
                Color c = s.label.color;
                c.a = k < 0.6f ? 1f : Mathf.Clamp01(1f - (k - 0.6f) / 0.4f);  // fade only in the last 40 %
                s.label.color = c;
            }
        }

        /// <summary>Headless/tests: how many pool slots are live right now.</summary>
        public int LiveSlots
        {
            get
            {
                int n = 0;
                for (int i = 0; i < PoolSize; i++) if (_slots[i] != null && _slots[i].alive) n++;
                return n;
            }
        }
    }
}
