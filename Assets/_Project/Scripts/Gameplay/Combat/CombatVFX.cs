using System.Collections.Generic;
using Crossroads.Core;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Lightweight combat / story VFX director (production polish pass). Everything is a
    /// pooled unlit primitive driven from the EventBus - no particle assets to author, no
    /// per-hit allocations after warm-up, one material per palette line (SRP-batcher friendly):
    ///
    ///   hit spark     CombatantDamagedEvent (enemy hit)   burst of 6 shards at the target
    ///   player hurt   CombatantDamagedEvent (player)      crimson ring pulse at Ari's feet
    ///   dodge         PlayerActionEvent.Dodge             cyan after-image streak
    ///   enemy defeat  CombatantDefeatedEvent (enemy)      rising ember motes + ground ring
    ///   enemy alert   EnemyStateChangedEvent.Alert        short cyan "!" pillar above the enemy
    ///   windup        EnemyStateChangedEvent.AttackWindup expanding warning ring (read the arc)
    ///   ability       handled by AbilityPulseVFX (kept) - this adds a vertical beam
    ///   decision      DecisionResolvedEvent               line-coloured pillar at the player
    ///
    /// Budget: 48 pooled shards max (each a 12-tri cube), all disabled when idle; the Update
    /// loop only iterates live shards. Positions come from the scene registry (CombatDirector
    /// enemies by id / the Player tag) - the events themselves carry no transforms by design.
    /// </summary>
    public class CombatVFX : MonoBehaviour
    {
        private const int PoolSize = 48;

        private struct Shard
        {
            public Transform t;
            public Renderer r;
            public Vector3 vel;
            public float age, life;
            public Vector3 scale0;
            public bool live;
            public int palette;
            public float gravity;
        }

        private static readonly Color Ember = new Color(0.95f, 0.38f, 0.22f, 1f);
        private static readonly Color Tide = new Color(0.25f, 0.80f, 0.85f, 1f);
        private static readonly Color Stone = new Color(0.85f, 0.68f, 0.32f, 1f);
        private static readonly Color Hollow = new Color(0.45f, 0.18f, 0.60f, 1f);
        private static readonly Color Crimson = new Color(0.90f, 0.12f, 0.20f, 1f);
        private static readonly Color Spark = new Color(1.0f, 0.92f, 0.70f, 1f);
        private static readonly Color[] Palette = { Ember, Tide, Stone, Hollow, Crimson, Spark };

        private readonly Shard[] _pool = new Shard[PoolSize];
        private Material[] _mats;
        private int _liveCount;
        private Transform _player;
        private float _nextPlayerSearch;
        private readonly Dictionary<string, Transform> _enemyById = new Dictionary<string, Transform>(32);

        public int LiveShards { get { return _liveCount; } }

        // ---------------------------------------------------------------- lifecycle
        private void Awake()
        {
            _mats = new Material[Palette.Length];
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            for (int i = 0; i < Palette.Length; i++)
            {
                if (shader == null) break;
                var m = new Material(shader);
                m.SetColor("_BaseColor", Palette[i]);
                m.color = Palette[i];
                m.enableInstancing = true;
                _mats[i] = m;
            }
            for (int i = 0; i < PoolSize; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "VFX_Shard";
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                go.transform.SetParent(transform, false);
                var r = go.GetComponent<Renderer>();
                if (r != null) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
                go.SetActive(false);
                _pool[i].t = go.transform;
                _pool[i].r = r;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Subscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Subscribe<EnemyStateChangedEvent>(OnEnemyState);
            EventBus.Subscribe<PlayerActionEvent>(OnPlayerAction);
            EventBus.Subscribe<DecisionResolvedEvent>(OnDecision);
            EventBus.Subscribe<AbilityUsedEvent>(OnAbility);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CombatantDamagedEvent>(OnDamaged);
            EventBus.Unsubscribe<CombatantDefeatedEvent>(OnDefeated);
            EventBus.Unsubscribe<EnemyStateChangedEvent>(OnEnemyState);
            EventBus.Unsubscribe<PlayerActionEvent>(OnPlayerAction);
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnDecision);
            EventBus.Unsubscribe<AbilityUsedEvent>(OnAbility);
        }

        // ---------------------------------------------------------------- lookups
        private Transform Player()
        {
            if (_player != null) return _player;
            if (Time.time < _nextPlayerSearch) return null;
            _nextPlayerSearch = Time.time + 1f;
            var go = GameObject.FindGameObjectWithTag("Player");
            _player = go != null ? go.transform : null;
            return _player;
        }

        private Transform Enemy(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId)) return null;
            Transform t;
            if (_enemyById.TryGetValue(enemyId, out t) && t != null) return t;
            IReadOnlyList<EnemyAgent> live = CombatDirector.LiveEnemies;
            for (int i = 0; i < live.Count; i++)
            {
                var a = live[i];
                if (a != null && a.EnemyId == enemyId) { _enemyById[enemyId] = a.transform; return a.transform; }
            }
            return null;
        }

        /// <summary>Palette index for a damage type / ability line (mirrors AbilityPulseVFX).</summary>
        public static int PaletteFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return 5;
            if (key.StartsWith("ember")) return 0;
            if (key.StartsWith("tide")) return 1;
            if (key.StartsWith("stone")) return 2;
            if (key.StartsWith("hollow") || key.StartsWith("echo")) return 3;
            return 5; // kinetic / unknown -> neutral spark
        }

        // ---------------------------------------------------------------- handlers
        private void OnDamaged(CombatantDamagedEvent e)
        {
            if (e.isPlayer)
            {
                Transform p = Player();
                if (p == null) return;
                if (e.dodged) Streak(p.position + Vector3.up * 0.9f, -p.forward, 1, 4);
                else Ring(p.position + Vector3.up * 0.08f, 4, 1.4f, 0.35f);
                return;
            }
            Transform t = Enemy(e.enemyId);
            if (t == null) return;
            Vector3 at = t.position + Vector3.up * 1.1f;
            if (e.dodged) { Streak(at, t.right, 1, 3); return; }
            Burst(at, e.amount >= 20f ? 8 : 6, 5, 2.6f, 0.34f);
            Burst(at, 3, PaletteFor(e.damageType.ToString().ToLowerInvariant()), 1.6f, 0.45f);
        }

        private void OnDefeated(CombatantDefeatedEvent e)
        {
            if (e.isPlayer) return;
            Transform t = Enemy(e.enemyId);
            if (t == null) return;
            Ring(t.position + Vector3.up * 0.05f, 3, 2.2f, 0.7f);
            Motes(t.position + Vector3.up * 0.6f, 10, 0, 1.1f);
        }

        private void OnEnemyState(EnemyStateChangedEvent e)
        {
            Transform t = Enemy(e.enemyId);
            if (t == null) return;
            if (e.state == EnemyState.Alert) Pillar(t.position + Vector3.up * 2.6f, 1, 0.5f, 0.35f);
            else if (e.state == EnemyState.AttackWindup) Ring(t.position + Vector3.up * 0.05f, 4, 1.9f, 0.45f);
        }

        private void OnPlayerAction(PlayerActionEvent e)
        {
            Transform p = Player();
            if (p == null) return;
            switch (e.action)
            {
                case PlayerAction.Dodge: Streak(p.position + Vector3.up * 0.9f, -p.forward, 1, 5); break;
                case PlayerAction.Attack:
                    Burst(p.position + Vector3.up * 1.2f + p.forward * 0.9f, e.connected ? 4 : 2, 5, 1.8f, 0.22f);
                    break;
                case PlayerAction.Respawn: Pillar(p.position, 1, 2.4f, 0.9f); break;
            }
        }

        private void OnDecision(DecisionResolvedEvent e)
        {
            Transform p = Player();
            if (p == null) return;
            int pal = PaletteFor(e.optionId);
            Pillar(p.position + Vector3.up * 1.2f, pal, 2.6f, 1.0f);
            Motes(p.position + Vector3.up * 0.4f, 8, pal, 1.2f);
        }

        private void OnAbility(AbilityUsedEvent e)
        {
            Transform p = Player();
            if (p == null) return;
            int pal = PaletteFor(e.abilityId);
            Pillar(p.position + Vector3.up * 1.0f, pal, 2.0f, 0.55f);
            Motes(p.position + Vector3.up * 0.3f, 6, pal, Mathf.Clamp(e.duration > 0f ? e.duration * 0.3f : 0.8f, 0.5f, 1.5f));
        }

        // ---------------------------------------------------------------- emitters
        private int Acquire()
        {
            for (int i = 0; i < PoolSize; i++) if (!_pool[i].live) return i;
            // steal the oldest
            int oldest = 0; float best = -1f;
            for (int i = 0; i < PoolSize; i++) if (_pool[i].age / Mathf.Max(_pool[i].life, 0.01f) > best) { best = _pool[i].age / Mathf.Max(_pool[i].life, 0.01f); oldest = i; }
            return oldest;
        }

        private void Spawn(Vector3 pos, Vector3 vel, Vector3 scale, float life, int palette, float gravity)
        {
            int i = Acquire();
            ref Shard s = ref _pool[i];
            if (s.t == null) return;
            if (!s.live) _liveCount++;
            s.live = true;
            s.age = 0f; s.life = life; s.vel = vel; s.scale0 = scale; s.palette = palette; s.gravity = gravity;
            s.t.position = pos;
            s.t.localScale = scale;
            s.t.rotation = Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f);
            if (s.r != null && _mats != null && _mats[palette] != null) s.r.sharedMaterial = _mats[palette];
            s.t.gameObject.SetActive(true);
        }

        /// <summary>Radial shard burst (hit sparks).</summary>
        public void Burst(Vector3 at, int count, int palette, float speed, float life)
        {
            for (int k = 0; k < count; k++)
            {
                Vector3 dir = new Vector3(Random.value * 2f - 1f, Random.value * 1.2f, Random.value * 2f - 1f).normalized;
                Spawn(at, dir * speed * (0.6f + Random.value * 0.6f), new Vector3(0.05f, 0.05f, 0.22f), life, palette, 6f);
            }
        }

        /// <summary>Slow upward motes (defeat / decision / ability afterglow).</summary>
        public void Motes(Vector3 at, int count, int palette, float life)
        {
            for (int k = 0; k < count; k++)
            {
                Vector3 off = new Vector3(Random.value * 1.2f - 0.6f, Random.value * 0.6f, Random.value * 1.2f - 0.6f);
                Spawn(at + off, new Vector3(0f, 0.9f + Random.value * 0.8f, 0f), new Vector3(0.07f, 0.07f, 0.07f), life * (0.7f + Random.value * 0.6f), palette, -0.4f);
            }
        }

        /// <summary>Flat expanding ring made of 12 shards (telegraph / hurt pulse / defeat).</summary>
        public void Ring(Vector3 at, int palette, float radius, float life)
        {
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Spawn(at + dir * 0.2f, dir * (radius / Mathf.Max(life, 0.05f)), new Vector3(0.18f, 0.03f, 0.08f), life, palette, 0f);
            }
        }

        /// <summary>Vertical beam of stacked shards (alert marker / decision pillar).</summary>
        public void Pillar(Vector3 at, int palette, float height, float life)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(height * 3f), 2, 8);
            for (int k = 0; k < n; k++)
                Spawn(at + Vector3.up * (k * height / n), new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, height / n * 0.9f, 0.12f), life * (0.6f + 0.4f * k / n), palette, 0f);
        }

        /// <summary>After-image streak along a direction (dodge).</summary>
        public void Streak(Vector3 at, Vector3 dir, int palette, int count)
        {
            for (int k = 0; k < count; k++)
                Spawn(at + dir * (k * 0.25f), dir * 1.5f, new Vector3(0.25f, 0.9f, 0.08f), 0.28f + k * 0.04f, palette, 0f);
        }

        private void Update()
        {
            if (_liveCount == 0) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < PoolSize; i++)
            {
                ref Shard s = ref _pool[i];
                if (!s.live) continue;
                s.age += dt;
                if (s.age >= s.life || s.t == null)
                {
                    s.live = false; _liveCount--;
                    if (s.t != null) s.t.gameObject.SetActive(false);
                    continue;
                }
                s.vel.y -= s.gravity * dt;
                s.t.position += s.vel * dt;
                float k = 1f - s.age / s.life;
                s.t.localScale = s.scale0 * (0.35f + 0.65f * k);
            }
        }
    }
}
