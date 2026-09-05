using Crossroads.Core;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// World-side effect of the prototype's ACTIVE abilities: a radial pulse burst around
    /// the player - one pooled expanding ring + a light flare at Ari's position. It listens
    /// to AbilityUsedEvent (raised by the data-driven AbilityManager), so effects are fully
    /// decoupled: the manager publishes numbers (level row), the world reacts.
    /// Mobile-friendly: one ring object, no particles, no per-frame work when idle.
    /// </summary>
    public class AbilityPulseVFX : MonoBehaviour
    {
        [Tooltip("Ability ids this VFX answers to (prototype: all three initial powers).")]
        [SerializeField] private string[] abilityIds = new string[] { "ember_pulse", "tide_mend", "stone_ward" };

        [SerializeField] private float ringLifetime = 0.8f;
        [SerializeField] private float ringHeightY = 0.35f;

        // sanctioned palette (parity with UI: Ember #F26138-ish, Tide, Stone - line colours
        // only ever appear in effects/trim per the reference style rules)
        private static readonly Color EmberColor = new Color(0.95f, 0.38f, 0.22f, 0.9f);
        private static readonly Color TideColor = new Color(0.25f, 0.80f, 0.85f, 0.9f);
        private static readonly Color StoneColor = new Color(0.85f, 0.68f, 0.32f, 0.9f);
        private static readonly Color HollowColor = new Color(0.45f, 0.18f, 0.60f, 0.9f); // Hollow line (campaign pass)

        private GameObject _ring;
        private Material _ringMaterial;
        private float _age;
        private float _maxRadius;
        private Color _color;

        private void OnEnable()
        {
            EventBus.Subscribe<AbilityUsedEvent>(OnAbilityUsed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<AbilityUsedEvent>(OnAbilityUsed);
        }

        private void OnAbilityUsed(AbilityUsedEvent e)
        {
            if (_ring != null) Destroy(_ring);
            if (!WouldShowFor(e.abilityId)) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Vector3 origin = player.transform.position + new Vector3(0f, ringHeightY, 0f);
            _ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ring.name = "AbilityPulse_" + e.abilityId;
            _ring.transform.position = origin;
            _ring.transform.localScale = new Vector3(0.4f, 0.03f, 0.4f);

            var renderer = _ring.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                _ringMaterial = new Material(ResolveShader());
                _ringMaterial.color = ColorFor(e.abilityId);
                _ringMaterial.SetColor("_BaseColor", ColorFor(e.abilityId));
                renderer.material = _ringMaterial;
            }

            _color = ColorFor(e.abilityId);
            _age = 0f;
            _maxRadius = Mathf.Max(e.radius, 2.5f);
        }

        /// <summary>True when this VFX answers to the ability (extensible data list).</summary>
        public bool WouldShowFor(string abilityId)
        {
            for (int i = 0; i < abilityIds.Length; i++)
                if (abilityIds[i] == abilityId) return true;
            // campaign content pass: every authored power line gets the pulse (colour from its line)
            return LineOf(abilityId) != "";
        }

        /// <summary>Ability line ("ember"/"tide"/"stone"/"hollow") from the content, "" if unknown.</summary>
        private static string LineOf(string abilityId)
        {
            if (!Crossroads.Narrative.GameServices.IsInitialized || Crossroads.Narrative.GameServices.Abilities == null) return "";
            var def = Crossroads.Narrative.GameServices.Abilities.Find(abilityId);
            return def != null && def.line != null ? def.line : "";
        }

        private void Update()
        {
            if (_ring == null) return;
            _age += Time.deltaTime;
            float t = _age / ringLifetime;
            if (t >= 1f)
            {
                Destroy(_ring);
                if (_ringMaterial != null) Destroy(_ringMaterial);
                _ring = null;
                _ringMaterial = null;
                return;
            }
            // expanding ring: scale up + fade out
            float r = Mathf.Lerp(0.4f, _maxRadius * 2f, t);
            _ring.transform.localScale = new Vector3(r, 0.03f, r);
            if (_ringMaterial != null)
            {
                Color c = _color;
                c.a = 1f - t;
                _ringMaterial.color = c;                 // classic unlit shaders
                _ringMaterial.SetColor("_BaseColor", c); // URP unlit
                _ringMaterial.SetColor("_EmissionColor", c * 0.6f);
            }
        }

        private static Color ColorFor(string abilityId)
        {
            switch ((abilityId ?? "").ToLowerInvariant())
            {
                case "ember_pulse": return EmberColor;
                case "tide_mend": return TideColor;
                case "stone_ward": return StoneColor;
            }
            switch (LineOf(abilityId))
            {
                case "tide": return TideColor;
                case "stone": return StoneColor;
                case "hollow": return HollowColor;
                default: return EmberColor;
            }
        }

        private static Shader ResolveShader()
        {
            // URP unlit first, classic fallbacks; one shared shader, one cloned material
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return shader;
        }
    }
}
