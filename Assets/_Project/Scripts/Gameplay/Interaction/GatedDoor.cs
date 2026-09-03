using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using UnityEngine;

namespace Crossroads.Prototype
{
    /// <summary>
    /// Area gate: a door whose rules are DATA (GateRuleData - conditions + open/flavor text).
    ///   - First matching rule wins; rules with no conditions act as the fallback.
    ///   - Rule.opens == true  -> door slides + UnlockArea(areaId) once (persisted) + flavor notice.
    ///   - Rule.opens == false -> door stays shut + the rule's text as a notice (locked feedback).
    ///   - When the area is already unlocked (e.g. after restart) the door auto-opens.
    /// Consequences: ACCESSIBLE AREAS (and available interactions) change with state; the
    /// per-path flavor text also proves the branch that granted access.
    /// </summary>
    public class GatedDoor : DoorInteractable
    {
        [Tooltip("Evaluated in order; first match wins.")]
        [SerializeField] protected List<GateRuleData> rules = new List<GateRuleData>();

        [Tooltip("Area id unlocked (and persisted) the first time this gate opens.")]
        [SerializeField] protected string areaId = "";

        [Tooltip("Label override once the gate is passable (prompt).")]
        [SerializeField] protected string unlockedPrompt = "";

        private bool _unlockAnnounced;

        private void Start()
        {
            if (!string.IsNullOrEmpty(areaId) && GameServices.IsInitialized && GameServices.Progress.AreaUnlocked(areaId))
            {
                SetOpen(true, instant: true); // access persisted from a previous session
            }
        }

        public override bool CanInteract(GameObject player)
        {
            if (!base.CanInteract(player)) return false;
            if (_busy) return false;
            // An already-unlocked gate is always usable; a sealed gate stays interactable
            // ONLY when it has rules (so the player gets the "sealed" feedback), else no prompt.
            if (!string.IsNullOrEmpty(areaId) && GameServices.IsInitialized && GameServices.Progress.AreaUnlocked(areaId))
                return true;
            return EvaluateRule() != null;
        }

        public override string PromptText
        {
            get
            {
                if (!string.IsNullOrEmpty(unlockedPrompt) && GameServices.IsInitialized &&
                    GameServices.Progress.AreaUnlocked(areaId)) return unlockedPrompt;
                return base.PromptText;
            }
        }

        public override void OnInteract(GameObject player)
        {
            if (!GameServices.IsInitialized) { SetOpen(!_open); return; }
            if (_open) { SetOpen(false); return; } // already passable - normal toggle

            GateRuleData rule = EvaluateRule();
            if (rule == null || !rule.opens)
            {
                EventBus.Publish(new NoticeRequestEvent
                {
                    text = rule != null ? rule.text : "The gate is sealed."
                });
                return;
            }

            if (!_unlockAnnounced && !string.IsNullOrEmpty(areaId) && !GameServices.Progress.AreaUnlocked(areaId))
            {
                _unlockAnnounced = true; // announce once per session (the unlock itself is idempotent/persisted)
                GameServices.Progress.UnlockArea(areaId);
            }
            EventBus.Publish(new NoticeRequestEvent { text = rule.text });
            SetOpen(true);
        }

        /// <summary>Resolves the gate rules against the live state (pure, testable logic).</summary>
        public GateRuleData EvaluateRule()
        {
            return GateRuleEvaluator.FirstMatch(rules, GameServices.State);
        }
    }
}
