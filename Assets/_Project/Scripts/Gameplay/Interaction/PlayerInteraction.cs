using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Prototype;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// Reusable proximity-interaction system (GAME_DESIGN §8.3): scans cached Interactables,
    /// picks the nearest valid target by the design's rules, raises InteractPromptEvent for
    /// the UI's contextual [INTERACT] button and drives door/NPC/object interactions.
    /// Replaces the prototype's IMGUI InteractInput; doors and inspectables keep working.
    /// Input: the on-screen INTERACT button (mobile) + E key (editor/gamepad-lite).
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        private static readonly List<ProximityTarget> TargetBuffer = new List<ProximityTarget>(32);

        [SerializeField] private float scanInterval = 0.2f;
        [SerializeField] private float cacheRefreshInterval = 1.0f;

        private Interactable _current;
        private Interactable _lastPublished;
        private List<Interactable> _cache = new List<Interactable>();
        private float _nextScan;
        private float _nextCacheRefresh;

        public Interactable Current { get { return _current; } }

        private void Start()
        {
            RefreshCache();
            _nextScan = 0f;
            _nextCacheRefresh = 0f;
        }

        private void OnEnable()
        {
            // World-state consequences may spawn/activate new interactables mid-run
            EventBus.Subscribe<EntityStateChangedEvent>(OnEntityStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EntityStateChangedEvent>(OnEntityStateChanged);
        }

        private void OnEntityStateChanged(EntityStateChangedEvent e)
        {
            _nextCacheRefresh = 0f; // refresh on next frame
        }

        /// <summary>Rescans the scene for interactables (cheap, rare - cache invalidation point).</summary>
        public void RefreshCache()
        {
            _cache = new List<Interactable>(FindObjectsByType<Interactable>(FindObjectsSortMode.None));
            _nextCacheRefresh = Time.time + cacheRefreshInterval;
        }

        /// <summary>Registers a dynamically spawned interactable (or re-scans if null).</summary>
        public void Register(Interactable interactable)
        {
            if (interactable == null) { RefreshCache(); return; }
            if (!_cache.Contains(interactable)) _cache.Add(interactable);
        }

        public void Unregister(Interactable interactable)
        {
            _cache.Remove(interactable);
        }

        private void Update()
        {
            // Respect the decision/dialogue lock (GAME_DESIGN §4.5) and keep movement gated too
            if (InputLock.Active)
            {
                SetAndPublish(null);
                return;
            }

            if (Time.time >= _nextCacheRefresh) RefreshCache();
            if (Time.time < _nextScan) return;
            _nextScan = Time.time + scanInterval;

            UpdateTargets();
            BuildTargets(); // fills TargetBuffer parallel to _cache
            ProximityTarget pick = ProximitySelector.Pick(ToPoint3(transform.position), TargetBuffer);
            Interactable target = pick != null ? _cache[TargetBuffer.IndexOf(pick)] : null;

            SetAndPublish(target);

            if (target != null && ConsumeInteractKey())
            {
                Interact();
            }
        }

        private void UpdateTargets()
        {
            // drop destroyed entries lazily
            for (int i = _cache.Count - 1; i >= 0; i--)
                if (_cache[i] == null) _cache.RemoveAt(i);
        }

        private List<ProximityTarget> BuildTargets()
        {
            TargetBuffer.Clear();
            for (int i = 0; i < _cache.Count; i++)
            {
                Interactable it = _cache[i];
                if (it == null || !it.CanInteract(gameObject)) continue;
                TargetBuffer.Add(new ProximityTarget(
                    it.name, ToPoint3(it.transform.position), it.Radius, it.Priority));
            }
            return TargetBuffer;
        }

        private void SetAndPublish(Interactable target)
        {
            if (ReferenceEquals(target, _lastPublished)) return;
            _lastPublished = target;
            _current = target;
            EventBus.Publish(new InteractPromptEvent
            {
                visible = target != null,
                label = target != null ? target.PromptText : "",
                interactableId = target != null ? target.name : "",
                priority = target != null ? target.Priority : 0f
            });
        }

        private static bool ConsumeInteractKey()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E)) return true;
#endif
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame) return true;
#endif
            return false;
        }

        /// <summary>Called by the mobile INTERACT button (and by tests).</summary>
        public void Interact()
        {
            if (_current == null || InputLock.Active) return;
            if (!_current.CanInteract(gameObject)) return;
            _current.OnInteract(gameObject);
        }

        internal static Point3 ToPoint3(Vector3 v)
        {
            return new Point3(v.x, v.y, v.z);
        }
    }
}
