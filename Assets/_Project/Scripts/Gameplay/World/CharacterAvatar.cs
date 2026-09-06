using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// The canonical character body of an NPC / enemy (release pass, CHARACTER_REFERENCE §2
    /// "one character = one canonical mesh reused everywhere").
    ///
    /// Owns: the avatar prefab instance (Assets/_Project/Prefabs/Characters/&lt;Name&gt;.prefab, a
    /// Humanoid model + the shared Character_Controller), the Animator parameter vocabulary the
    /// whole cast shares (Speed / Talking / Attack / Dodge / Hit / Defeat / Alert) and the
    /// distance LOD that keeps 60+ characters affordable on a mid-range phone:
    ///
    ///   tier 0  (&lt; nearDistance)      full: animator every frame, shadows on, brain full rate
    ///   tier 1  (&lt; farDistance)       reduced: animator 1/2 rate, no shadow casting
    ///   tier 2  (&gt;= farDistance)      dormant: animator disabled (pose frozen), renderer culled
    ///                                  by the camera anyway; brain 1/4 rate (owner decides)
    ///   hidden  (owner inactive)       nothing runs
    ///
    /// The owner (NpcAgent / EnemyAgent) calls Tick(distance) from its own Update so the LOD
    /// decision costs one distance it already computed. No allocations after Spawn().
    /// </summary>
    public class CharacterAvatar
    {
        public const float DefaultNearDistance = 14f;
        public const float DefaultFarDistance = 32f;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int TalkingHash = Animator.StringToHash("Talking");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int DodgeHash = Animator.StringToHash("Dodge");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DefeatHash = Animator.StringToHash("Defeat");
        private static readonly int AlertHash = Animator.StringToHash("Alert");

        private readonly Transform _owner;
        private GameObject _instance;
        private Animator _animator;
        private Renderer[] _renderers;
        private int _tier = -1;
        private int _frame;
        private float _speed;
        private bool _talking;
        private bool _defeated;

        public float nearDistance = DefaultNearDistance;
        public float farDistance = DefaultFarDistance;

        /// <summary>0 full, 1 reduced, 2 dormant; -1 before the first Tick.</summary>
        public int Tier { get { return _tier; } }
        public bool HasBody { get { return _instance != null; } }
        public float Speed { get { return _speed; } }
        public bool Talking { get { return _talking; } }
        public bool Defeated { get { return _defeated; } }
        public Animator Animator { get { return _animator; } }

        public CharacterAvatar(Transform owner)
        {
            _owner = owner;
        }

        /// <summary>Instantiates the canonical prefab under the owner and hides the placeholder
        /// primitives (Body/Head/Hair/... children the scene generator authored as fallbacks).</summary>
        public void Spawn(GameObject prefab, string label, Renderer keepRenderer)
        {
            if (prefab == null || _owner == null) return;
            if (_instance == null)
            {
                _instance = (GameObject)Object.Instantiate(prefab);
                _instance.name = "Avatar_" + label;
                Transform t = _instance.transform;
                t.SetParent(_owner, false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                _animator = _instance.GetComponent<Animator>();
                if (_animator == null) _animator = _instance.GetComponentInChildren<Animator>();
                _renderers = _instance.GetComponentsInChildren<Renderer>(true);
            }
            // the primitive stand-in becomes invisible but stays in the hierarchy: the owner's
            // bodyRenderer still receives material variants / hit flashes (cheap, invisible) and
            // headless tests keep their references
            Renderer[] all = _owner.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r == null || IsMine(r)) continue;
                r.enabled = false;
            }
            if (keepRenderer != null) keepRenderer.enabled = false;
        }

        private bool IsMine(Renderer r)
        {
            if (_renderers == null) return false;
            for (int i = 0; i < _renderers.Length; i++) if (_renderers[i] == r) return true;
            return false;
        }

        // ---------------------------------------------------------------- animation vocabulary
        public void SetSpeed(float normalized)
        {
            _speed = normalized < 0f ? 0f : (normalized > 1f ? 1f : normalized);
        }

        public void SetTalking(bool talking)
        {
            if (_talking == talking) return;
            _talking = talking;
            if (_animator != null && _tier != 2) _animator.SetBool(TalkingHash, talking);
        }

        public void Attack() { Trigger(AttackHash); }
        public void Dodge() { Trigger(DodgeHash); }
        public void Hit() { Trigger(HitHash); }
        public void Alert() { Trigger(AlertHash); }

        public void Defeat()
        {
            if (_defeated) return;
            _defeated = true;
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.SetTrigger(DefeatHash);
            }
        }

        private void Trigger(int hash)
        {
            if (_animator == null || _defeated) return;
            if (!_animator.enabled) _animator.enabled = true; // a dormant body that gets hit wakes up
            _animator.SetTrigger(hash);
        }

        // ---------------------------------------------------------------- LOD
        /// <summary>Call once per owner Update with the owner-to-player distance. Returns the tier.</summary>
        public int Tick(float distanceToPlayer)
        {
            int tier = distanceToPlayer < nearDistance ? 0 : (distanceToPlayer < farDistance ? 1 : 2);
            if (tier != _tier) ApplyTier(tier);
            _frame++;
            if (_animator != null && _animator.enabled && !_defeated)
            {
                // tier 1: push parameters every other frame (the Animator itself is culled by
                // Unity when off-screen - CullUpdateTransforms - this halves the on-screen cost)
                if (tier == 0 || (_frame & 1) == 0)
                    _animator.SetFloat(SpeedHash, _speed, 0.15f, Time.deltaTime);
            }
            return tier;
        }

        private void ApplyTier(int tier)
        {
            _tier = tier;
            if (_renderers != null)
            {
                var mode = tier == 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].shadowCastingMode = mode;
            }
            if (_animator != null)
            {
                bool run = tier != 2 || _defeated;
                if (_animator.enabled != run) _animator.enabled = run;
                if (run) _animator.SetBool(TalkingHash, _talking);
            }
        }

        /// <summary>Static helper so a pure decision can be tested headlessly.</summary>
        public static int TierFor(float distance, float near, float far)
        {
            return distance < near ? 0 : (distance < far ? 1 : 2);
        }

        /// <summary>Brain update divisor per tier: 1 (every frame), 2, 4.</summary>
        public static int BrainDivisor(int tier)
        {
            return tier <= 0 ? 1 : (tier == 1 ? 2 : 4);
        }
    }
}
