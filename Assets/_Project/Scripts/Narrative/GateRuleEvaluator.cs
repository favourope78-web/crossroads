using System.Collections.Generic;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Pure rule resolution for area gates / conditional interactions (data-driven):
    /// the first rule whose conditions pass wins; a rule with no conditions is the
    /// fallback. Headless-testable - the MonoBehaviour gate just applies the result.
    /// </summary>
    public static class GateRuleEvaluator
    {
        /// <summary>First matching rule, or null when no rule matches.</summary>
        public static GateRuleData FirstMatch(List<GateRuleData> rules, StateMutator state)
        {
            if (rules == null || state == null) return null;
            for (int i = 0; i < rules.Count; i++)
            {
                GateRuleData r = rules[i];
                if (r == null) continue;
                if (ConditionEvaluator.Evaluate(r.conditions, state)) return r;
            }
            return null;
        }
    }
}
