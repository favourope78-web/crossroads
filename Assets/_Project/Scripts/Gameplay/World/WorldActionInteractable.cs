using System.Collections.Generic;
using Crossroads.Core;
using Crossroads.Narrative;
using Crossroads.Prototype;
using UnityEngine;

namespace Crossroads.Gameplay
{
    /// <summary>
    /// The reusable data-driven WORLD ACTION (the objective system's hands):
    /// an Interactable whose availability is a condition list (ANY state: decision,
    /// flag, item, ability, objective, world variant...) and whose use applies a
    /// data effect list through EffectApplier - the same single write path decisions use.
    ///
    /// Consequences this enables (all data, no code):
    ///   - ability-dependent interactions: put AbilityOwned in conditions and a player
    ///     without the power gets lockedNotice instead of the effect (task: at least one
    ///     interaction only available through an ability)
    ///   - repeatable counters: useCountVar + maxUses (objective progress reads the var)
    ///   - one-shot world changes: consumeEntityKey hides the object after the final use
    ///   - objective completion: effects set the flags/vars objectives complete on
    /// </summary>
    public class WorldActionInteractable : Interactable
    {
        [Tooltip("All must pass to actually use it (ability, item, flag, objective, world state...).")]
        [SerializeField] private List<DecisionConditionData> conditions = new List<DecisionConditionData>();

        [Tooltip("Toast shown when the player tries but the conditions do not pass.")]
        [SerializeField] private string lockedNotice = "Nothing happens.";

        [Tooltip("Effects applied per successful use (EffectApplier - single write path).")]
        [SerializeField] private List<DecisionEffectData> perUseEffects = new List<DecisionEffectData>();

        [Tooltip("Progress var incremented by uses (0/1 = one-shot). Objective counters read it.")]
        [SerializeField] private string useCountVar = "";

        [Tooltip("Maximum uses before the action is spent (each use = +1 on useCountVar).")]
        [SerializeField] private int maxUses = 1;

        [Tooltip("Entity key hidden when the action is spent (the crate you emptied...).")]
        [SerializeField] private string consumeEntityKey = "";

        [SerializeField] private string useNotice = "";
        [SerializeField] private string spentNotice = "There is nothing left to do here.";
        [SerializeField] private bool hidePromptWhenSpent = true;

        private bool _spent;

        public bool ConditionsPass
        {
            get
            {
                return GameServices.IsInitialized &&
                       ConditionEvaluator.Evaluate(conditions, GameServices.State);
            }
        }

        public int UsesSoFar
        {
            get
            {
                if (string.IsNullOrEmpty(useCountVar) || !GameServices.IsInitialized) return _spent ? maxUses : 0;
                return GameServices.State.GetVar(useCountVar, 0);
            }
        }

        public bool Spent { get { return maxUses > 0 && UsesSoFar >= maxUses; } }

        public override string PromptText
        {
            get
            {
                if (Spent) return !string.IsNullOrEmpty(spentLabel) ? spentLabel : Label;
                return !ConditionsPass && !string.IsNullOrEmpty(lockedLabel) ? lockedLabel : Label;
            }
        }

        [SerializeField] private string lockedLabel = "";
        [SerializeField] private string spentLabel = "";

        public override bool CanInteract(GameObject player)
        {
            if (!base.CanInteract(player)) return false;
            if (hidePromptWhenSpent && Spent) return false;
            return true;
        }

        public override void OnInteract(GameObject player)
        {
            if (!GameServices.IsInitialized) return;

            if (Spent)
            {
                EventBus.Publish(new NoticeRequestEvent { text = spentNotice });
                return;
            }

            if (!ConditionsPass)
            {
                // Feedback over silence: the world explains WHY this is not available
                // (e.g. "the beacon does not answer your empty hands" without ember).
                EventBus.Publish(new NoticeRequestEvent { text = lockedNotice });
                StoryLog.Log("[CROSSROADS] World action locked: " + name);
                return;
            }

            // 1. apply the data effects through the single write path
            EffectApplier.Apply(perUseEffects, GameServices.State);

            // 2. count the use (persisted var -> objective counters react via event)
            if (!string.IsNullOrEmpty(useCountVar) && maxUses > 0)
                GameServices.State.AddVar(useCountVar, 1);

            // 3. feedback
            string notice = useNotice;
            if (string.IsNullOrEmpty(notice) && GameServices.Progress != null)
            {
                var notices = EffectNotices.Build(perUseEffects, GameServices.State, GameServices.Progress.Index);
                if (notices.Count > 0) notice = notices[0].text;
            }
            if (!string.IsNullOrEmpty(notice))
                EventBus.Publish(new NoticeRequestEvent { text = notice });

            // 4. spend: hide the consumed object when the final use happened
            if (maxUses > 0 && UsesSoFar >= maxUses)
            {
                _spent = true;
                if (!string.IsNullOrEmpty(consumeEntityKey))
                    GameServices.State.SetEntity(consumeEntityKey, false);
            }
            StoryLog.Log("[CROSSROADS] World action used: " + name + " (" + UsesSoFar + "/" + maxUses + ")");
        }
    }
}
