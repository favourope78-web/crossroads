using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>Data-driven NPC mood variant: conditions -> material + display title.</summary>
    [Serializable]
    public class NpcVariantBinding
    {
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
        public Material bodyMaterial;
        public string title = "";
    }

    /// <summary>
    /// FateStateDriver v1 (GAME_DESIGN §9.2: one character = one prefab + a state driver
    /// that reads flags/bonds at load and selects behaviour). Consequences visible live:
    ///   - on Start: applies the variant matching the CURRENT state (restart persistence)
    ///   - on state events (bond/flag/decision/save load): re-applies immediately,
    ///     so the moment a choice resolves, the NPC's look/title changes.
    /// Data-driven: variants are condition -> (material, title) pairs in the inspector;
    /// the dialogue graph supplies the actual spoken lines per state.
    /// </summary>
    public class NpcFateDriver : MonoBehaviour
    {
        [SerializeField] private List<NpcVariantBinding> variants = new List<NpcVariantBinding>();
        [SerializeField] private string baseTitle = "";
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Renderer bodyRenderer;

        public string CurrentTitle { get; private set; }

        private void Start()
        {
            Apply();
            EventBus.Subscribe<BondChangedEvent>(OnBondChanged);
            EventBus.Subscribe<AffinityChangedEvent>(OnStateEvent);
            EventBus.Subscribe<FlagChangedEvent>(OnStateEvent);
            EventBus.Subscribe<DecisionResolvedEvent>(OnStateEvent);
            EventBus.Subscribe<StateLoadedEvent>(OnStateEvent);
            EventBus.Subscribe<StateResetEvent>(OnStateEvent);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<BondChangedEvent>(OnBondChanged);
            EventBus.Unsubscribe<AffinityChangedEvent>(OnStateEvent);
            EventBus.Unsubscribe<FlagChangedEvent>(OnStateEvent);
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnStateEvent);
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateEvent);
            EventBus.Unsubscribe<StateResetEvent>(OnStateEvent);
        }

        private void OnBondChanged(BondChangedEvent e) { Apply(); }
        private void OnStateEvent(AffinityChangedEvent e) { Apply(); }
        private void OnStateEvent(FlagChangedEvent e) { Apply(); }
        private void OnStateEvent(DecisionResolvedEvent e) { Apply(); }
        private void OnStateEvent(StateLoadedEvent e) { Apply(); }
        private void OnStateEvent(StateResetEvent e) { Apply(); }

        /// <summary>Picks the first variant whose conditions pass and applies it live.</summary>
        public void Apply()
        {
            if (!GameServices.IsInitialized) return;
            NpcVariantBinding chosen = null;
            for (int i = 0; i < variants.Count; i++)
            {
                if (ConditionEvaluator.Evaluate(variants[i].conditions, GameServices.State))
                {
                    chosen = variants[i];
                    break;
                }
            }
            if (chosen == null)
            {
                CurrentTitle = baseTitle;
                if (bodyRenderer != null && baseMaterial != null) bodyRenderer.sharedMaterial = baseMaterial;
                return;
            }
            CurrentTitle = string.IsNullOrEmpty(chosen.title) ? baseTitle : chosen.title;
            if (bodyRenderer != null && chosen.bodyMaterial != null) bodyRenderer.sharedMaterial = chosen.bodyMaterial;
        }
    }
}
