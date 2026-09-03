using System.Collections.Generic;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Evaluates the condition whitelist (GAME_DESIGN §4.2) against the live state.
    /// Empty/missing condition lists are always true. Pure C# - unit-testable.
    /// </summary>
    public static class ConditionEvaluator
    {
        public static bool Evaluate(List<DecisionConditionData> conditions, StateMutator state)
        {
            if (conditions == null || conditions.Count == 0) return true;
            if (state == null) return false;

            for (int i = 0; i < conditions.Count; i++)
            {
                DecisionConditionData c = conditions[i];
                if (c == null) continue;
                if (!EvaluateOne(c, state)) return false;
            }
            return true;
        }

        private static bool EvaluateOne(DecisionConditionData c, StateMutator state)
        {
            switch (c.type)
            {
                case ConditionType.FlagIs:
                    return state.FlagIs(c.key, c.value);

                case ConditionType.FlagIsNot:
                    return !state.HasFlag(c.key) || state.GetFlag(c.key) != c.value;

                case ConditionType.FlagMissing:
                    return !state.HasFlag(c.key);

                case ConditionType.VarAtLeast:
                    return state.GetVar(c.key, 0) >= c.amount;

                case ConditionType.AffinityAtLeast:
                    return state.GetAffinity(c.key) >= c.amount;

                case ConditionType.BondAtLeast:
                    return state.GetBond(c.key) >= c.amount;

                case ConditionType.DecisionWas:
                    // empty value = "resolved at all" (any option)
                    if (string.IsNullOrEmpty(c.value)) return state.HasDecision(c.key);
                    return state.DecisionOption(c.key) == c.value;

                case ConditionType.DecisionNotMade:
                    return !state.HasDecision(c.key);

                case ConditionType.CodexOwned:
                    return state.HasCodex(c.key);

                default:
                    return true;
            }
        }
    }
}
