using UnityEngine;
using Crossroads.Core;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Runtime VFX director (art production pass - ART_GAPS particle feedback). Presentation
    /// only: it WATCHES the existing event traffic and never writes back -
    ///
    ///   AbilityUsedEvent        -> line-coloured burst at the player (ember/tide/stone/hollow)
    ///   CombatantDamagedEvent   -> warm hit sparks (crit-sized on big hits)
    ///   (always on)             -> hall dust motes drifting around the camera
    ///
    /// One pool of 64 pooled quads (see VfxMath), built once at Start, no runtime
    /// allocations. Quads use URP/Unlit (Sprites/Default fallback) tinted per particle
    /// via MaterialPropertyBlocks; if no shader can be found (headless tests, stripped
    /// builds) the director disables itself instead of rendering error magenta.
    /// </summary>
    public class VfxDirector : MonoBehaviour
    {
        private struct Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float TotalLife;
            public float BaseSize;
            public Color Color;
            public bool Alive;
        }

        private readonly Particle[] _pool = new Particle[VfxMath.PoolSize];
        private Transform[] _quads;
        private Renderer[] _renderers;
        private MaterialPropertyBlock[] _blocks;
        private readonly int[] _cursor = new int[4];
        private float _dustTimer;
        private bool _failed;

        private void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) { _failed = true; enabled = false; return; }
            var material = new Material(shader);
            material.enableInstancing = true;

            _quads = new Transform[VfxMath.PoolSize];
            _renderers = new Renderer[VfxMath.PoolSize];
            _blocks = new MaterialPropertyBlock[VfxMath.PoolSize];
            for (int i = 0; i < VfxMath.PoolSize; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "VfxQuad" + i;
                var col = quad.GetComponent<Collider>();
                if (col != null) col.enabled = false;
                quad.transform.SetParent(transform, false);
                quad.transform.localScale = Vector3.zero;
                _quads[i] = quad.transform;
                _blocks[i] = new MaterialPropertyBlock();
                _renderers[i] = quad.GetComponent<Renderer>();
                if (_renderers[i] != null) _renderers[i].sharedMaterial = material;
            }

            EventBus.Subscribe<AbilityUsedEvent>(OnAbilityUsed);
            EventBus.Subscribe<CombatantDamagedEvent>(OnDamaged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<AbilityUsedEvent>(OnAbilityUsed);
            EventBus.Unsubscribe<CombatantDamagedEvent>(OnDamaged);
        }

        // ---------------------------------------------------------------- events -> bursts
        private void OnAbilityUsed(AbilityUsedEvent e)
        {
            Burst(0, VfxMath.AbilityCount, OriginNearPlayer(), VfxMath.ColorForLine(LineOfAbility(e.abilityId)), 0.7f, 2.6f);
        }

        private void OnDamaged(CombatantDamagedEvent e)
        {
            if (e.amount <= 0f) return; // dodged/immune: no sparks
            bool big = e.amount >= 25f;
            Burst(1, big ? VfxMath.HitCount : VfxMath.HitCount / 2,
                OriginNearPlayer(), new Color(1f, 0.72f, 0.32f, 1f), big ? 0.45f : 0.3f, big ? 3.4f : 2.2f);
        }

        /// <summary>Ability id -> element line (deterministic string mapping; unknown ids read as hollow).</summary>
        public static string LineOfAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return "hollow";
            if (abilityId.Contains("ember") || abilityId.Contains("cinder") || abilityId.Contains("phoenix")) return "ember";
            if (abilityId.Contains("tide") || abilityId.Contains("riptide")) return "tide";
            if (abilityId.Contains("stone") || abilityId.Contains("tremor") || abilityId.Contains("bulwark")) return "stone";
            return "hollow";
        }

        private Vector3 OriginNearPlayer()
        {
            var player = FindFirstObjectByType<PlayerPrototypeController>();
            if (player != null) return player.transform.position + Vector3.up * 1.2f;
            var cam = Camera.main;
            if (cam != null) return cam.transform.position + cam.transform.forward * 2.5f;
            return transform.position;
        }

        // ---------------------------------------------------------------- pool
        private void Burst(int layer, int count, Vector3 origin, Color color, float size, float speed)
        {
            int start, n;
            VfxMath.LayerRange(layer, out start, out n);
            count = Mathf.Min(count, n);
            int salt = Random.Range(0, 10000);
            for (int k = 0; k < count; k++)
            {
                int slot = start + (_cursor[layer] - start + k) % n;
                Vector3 v;
                VfxMath.BurstVelocity(k, salt, speed, out v);
                _pool[slot].Position = origin;
                _pool[slot].Velocity = v;
                _pool[slot].TotalLife = 0.6f + 0.5f * ((k * 7u % 10u) / 10f);
                _pool[slot].Life = _pool[slot].TotalLife;
                _pool[slot].BaseSize = size * (0.7f + 0.6f * ((k * 13u % 10u) / 10f));
                _pool[slot].Color = color;
                _pool[slot].Alive = true;
            }
            _cursor[layer] = VfxMath.NextCursor(_cursor[layer], layer);
        }

        private void Update()
        {
            if (_failed) return;
            float dt = Time.deltaTime;

            _dustTimer -= dt;
            if (_dustTimer <= 0f)
            {
                _dustTimer = 0.4f;
                SpawnOneDust();
            }

            for (int i = 0; i < VfxMath.PoolSize; i++)
            {
                var p = _pool[i];
                if (p.Alive)
                {
                    float gravity = VfxMath.LayerOfSlot(i) == 2 ? 0f : -3.2f;
                    Vector3 pos = p.Position;
                    Vector3 vel = p.Velocity;
                    float life = p.Life;
                    if (VfxMath.Step(ref pos, ref vel, ref life, dt, gravity))
                    {
                        p.Position = pos;
                        p.Velocity = vel;
                        p.Life = life;
                    }
                    else
                    {
                        p.Alive = false;
                    }
                    _pool[i] = p;
                }
                Transform t = _quads != null && i < _quads.Length ? _quads[i] : null;
                if (t == null) continue;
                if (!p.Alive)
                {
                    t.localScale = Vector3.zero;
                    continue;
                }
                float c = 1f - Mathf.Clamp01(p.Life / Mathf.Max(0.0001f, p.TotalLife));
                t.position = p.Position;
                t.localScale = Vector3.one * (p.BaseSize * VfxMath.SizeOverLife(c));
                if (_renderers != null && _renderers[i] != null && _blocks[i] != null)
                {
                    var col = p.Color;
                    col.a = VfxMath.AlphaOverLife(c);
                    _blocks[i].SetColor("_BaseColor", col);
                    _renderers[i].SetPropertyBlock(_blocks[i]);
                }
            }
        }

        private void SpawnOneDust()
        {
            int start, n;
            VfxMath.LayerRange(2, out start, out n);
            int slot = start + (_cursor[2] - start) % n;
            var cam = Camera.main;
            Vector3 basePos = cam != null
                ? cam.transform.position + cam.transform.forward * 5f
                : transform.position;
            var p = _pool[slot];
            p.Position = basePos + new Vector3(Random.Range(-3f, 3f), Random.Range(-1f, 2.5f), Random.Range(-3f, 3f));
            p.Velocity = VfxMath.DustVelocity(slot);
            p.TotalLife = 5f;
            p.Life = 5f;
            p.BaseSize = 0.05f;
            p.Color = new Color(0.85f, 0.9f, 1f, 0.35f);
            p.Alive = true;
            _pool[slot] = p;
            _cursor[2] = VfxMath.NextCursor(_cursor[2], 2);
        }
    }
}
