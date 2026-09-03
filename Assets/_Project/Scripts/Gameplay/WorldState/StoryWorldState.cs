using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>Maps a persistable entity key to a scene object (world-state consequence, §5.2).</summary>
    [Serializable]
    public class EntityBinding
    {
        public string key = "";
        public GameObject target;
    }

    /// <summary>Maps a district variant key to a dressing object (the "city remembers" system).</summary>
    [Serializable]
    public class AreaVariantBinding
    {
        public string area = "";
        public string variant = "";
        public GameObject target;
    }

    /// <summary>
    /// Applies persisted world state to the scene (GAME_DESIGN §5.2 "the city remembers").
    /// - On start (after the save is loaded): activates exactly the consequence objects the
    ///   saved decisions produced -> proof of persistence after restart.
    /// - On runtime EntityStateChangedEvent (a choice just made): toggles immediately.
    /// Consequences are NOT cosmetic-only: the same keys/flags feed ConditionEvaluator, so
    /// later encounters can gate dialogue, spawns, paths and endings on them.
    /// </summary>
    public class StoryWorldState : MonoBehaviour
    {
        [SerializeField] private List<EntityBinding> entities = new List<EntityBinding>();
        [SerializeField] private List<AreaVariantBinding> areaVariants = new List<AreaVariantBinding>();

        private void Start()
        {
            ApplyFromState();
            EventBus.Subscribe<EntityStateChangedEvent>(OnEntityStateChanged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<EntityStateChangedEvent>(OnEntityStateChanged);
        }

        /// <summary>Replays the persisted state (called at boot after GameServices loaded the save).</summary>
        public void ApplyFromState()
        {
            if (!GameServices.IsInitialized || GameServices.State == null) return;

            for (int i = 0; i < entities.Count; i++)
            {
                EntityBinding b = entities[i];
                if (b == null || b.target == null) continue;
                bool active = GameServices.State.GetEntity(b.key, false);
                if (b.target.activeSelf != active) b.target.SetActive(active);
            }

            for (int i = 0; i < areaVariants.Count; i++)
            {
                AreaVariantBinding v = areaVariants[i];
                if (v == null || v.target == null) continue;
                bool active = GameServices.State.GetWorldState(v.area) == v.variant;
                if (v.target.activeSelf != active) v.target.SetActive(active);
            }
            StoryLog.Log("[CROSSROADS] World state applied (" + entities.Count + " entity bindings)");
        }

        private void OnEntityStateChanged(EntityStateChangedEvent e)
        {
            SetEntity(e.entityKey, e.active);
        }

        public void SetEntity(string key, bool active)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                EntityBinding b = entities[i];
                if (b != null && b.target != null && b.key == key && b.target.activeSelf != active)
                    b.target.SetActive(active);
            }
        }

        /// <summary>Switches a district dressing variant (area variant sets are exclusive).</summary>
        public void SetAreaVariant(string area, string variant)
        {
            for (int i = 0; i < areaVariants.Count; i++)
            {
                AreaVariantBinding v = areaVariants[i];
                if (v == null || v.target == null || v.area != area) continue;
                bool active = v.variant == variant;
                if (v.target.activeSelf != active) v.target.SetActive(active);
            }
        }
    }
}
