using System;
using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Prototype;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>Data-driven visual variant for a gate (echo color per drive).</summary>
    [Serializable]
    public class GateVariantBinding
    {
        public List<DecisionConditionData> conditions = new List<DecisionConditionData>();
        public Material material;
    }

    /// <summary>
    /// Interactable area gate (energy seal) whose state is pure data:
    ///   rules (GateRuleData) decide open/closed + flavor; first match wins;
    ///   conditions can be ANY state (drive flag, ability owned, item held, reputation...).
    /// Consequences delivered here: ACCESSIBLE AREAS + available interactions.
    ///   - open  -> area unlocked (persisted) once, collider disabled, seal visual hides,
    ///              prompt becomes the unlocked label
    ///   - shut  -> collider blocks, per-rule flavor notice on interact, seal glows in the
    ///              variant color matching the player's current path (data-driven material)
    /// On restart the persisted unlock re-applies (Start + StateLoaded), so access survives.
    /// </summary>
    public class AreaGate : Interactable
    {
        [Tooltip("First matching rule wins; a rule with no conditions is the fallback.")]
        [SerializeField] protected List<GateRuleData> rules = new List<GateRuleData>();

        [Tooltip("Area id unlocked (and persisted) the first time this gate opens.")]
        [SerializeField] protected string areaId = "";

        [Tooltip("Material variants: first match wins (e.g. echo color per drive).")]
        [SerializeField] protected List<GateVariantBinding> variants = new List<GateVariantBinding>();

        [Tooltip("Physical blocker - disabled when the gate is open.")]
        [SerializeField] protected Collider blocker;

        [Tooltip("Visuals hidden when the gate is open (seal plane + pedestal...).")]
        [SerializeField] protected List<GameObject> visuals = new List<GameObject>();

        [Tooltip("Renderer whose material follows the active variant.")]
        [SerializeField] protected Renderer sealRenderer;

        [SerializeField] protected string openPrompt = "Enter";
        [SerializeField] protected string closedPrompt = "Energy Seal";
        [SerializeField] protected string openNotice = "The seal parts.";
        [SerializeField] protected string sealedNotice = "The energy seal shimmers. It does not know you.";

        private bool _open;

        public bool IsOpen { get { return _open; } }

        private void Start()
        {
            Reapply();
            EventBus.Subscribe<StateResetEvent>(OnStateReset);
            EventBus.Subscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Subscribe<DecisionResolvedEvent>(OnStateChanged);
            EventBus.Subscribe<FlagChangedEvent>(OnStateChanged);
            EventBus.Subscribe<EntityStateChangedEvent>(OnStateChanged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<StateResetEvent>(OnStateReset);
            EventBus.Unsubscribe<StateLoadedEvent>(OnStateLoaded);
            EventBus.Unsubscribe<DecisionResolvedEvent>(OnStateChanged);
            EventBus.Unsubscribe<FlagChangedEvent>(OnStateChanged);
            EventBus.Unsubscribe<EntityStateChangedEvent>(OnStateChanged);
        }

        private void OnStateReset(StateResetEvent e) { Reapply(); }
        private void OnStateLoaded(StateLoadedEvent e) { Reapply(); }
        private void OnStateChanged(DecisionResolvedEvent e) { Reapply(); }
        private void OnStateChanged(FlagChangedEvent e) { Reapply(); }
        private void OnStateChanged(EntityStateChangedEvent e) { Reapply(); }

        /// <summary>Re-evaluates state -> open/shut + visuals (pure logic in GateRuleEvaluator).</summary>
        public void Reapply()
        {
            if (!GameServices.IsInitialized)
            {
                ApplyVisual(GateRuleEvaluator.FirstMatch(rules, null), false);
                return;
            }

            bool unlocked = !string.IsNullOrEmpty(areaId) && GameServices.Progress.AreaUnlocked(areaId);
            bool resealed = !string.IsNullOrEmpty(areaId) && GameServices.State.IsAreaClosed(areaId);
            GateRuleData rule = GateRuleEvaluator.FirstMatch(rules, GameServices.State);
            _open = (unlocked || (rule != null && rule.opens)) && !resealed;
            ApplyVisual(rule, _open);
        }

        private void ApplyVisual(GateRuleData rule, bool open)
        {
            if (blocker != null) blocker.enabled = !open;
            if (visuals != null)
                for (int i = 0; i < visuals.Count; i++)
                    if (visuals[i] != null && visuals[i].activeSelf == open) visuals[i].SetActive(!open);

            if (sealRenderer != null && sealRenderer.gameObject.activeSelf)
            {
                GateVariantBinding chosen = null;
                if (GameServices.IsInitialized && variants != null)
                    for (int i = 0; i < variants.Count; i++)
                        if (ConditionEvaluator.Evaluate(variants[i].conditions, GameServices.State)) { chosen = variants[i]; break; }
                if (chosen != null && chosen.material != null) sealRenderer.sharedMaterial = chosen.material;
            }
            StoryLog.Log("[CROSSROADS] Gate " + name + (open ? " OPEN" : " sealed"));
        }

        public override string PromptText
        {
            get { return _open ? openPrompt : closedPrompt; }
        }

        public override bool CanInteract(GameObject player)
        {
            if (!base.CanInteract(player)) return false;
            return GameServices.IsInitialized && (EvaluateRule() != null || _open);
        }

        public override void OnInteract(GameObject player)
        {
            if (!GameServices.IsInitialized) return;
            if (_open)
            {
                EventBus.Publish(new NoticeRequestEvent { text = openNotice });
                return;
            }
            GateRuleData rule = EvaluateRule();
            if (rule == null)
            {
                EventBus.Publish(new NoticeRequestEvent { text = sealedNotice });
                return;
            }
            if (!rule.opens)
            {
                EventBus.Publish(new NoticeRequestEvent { text = string.IsNullOrEmpty(rule.text) ? sealedNotice : rule.text });
                return;
            }

            if (!string.IsNullOrEmpty(areaId) && !GameServices.Progress.AreaUnlocked(areaId))
                GameServices.Progress.UnlockArea(areaId);   // persisted -> access survives restart
            EventBus.Publish(new NoticeRequestEvent { text = rule.opens && !string.IsNullOrEmpty(rule.text) ? rule.text : openNotice });
            Reapply();
        }

        public GateRuleData EvaluateRule()
        {
            return GateRuleEvaluator.FirstMatch(rules, GameServices.State);
        }
    }
}
