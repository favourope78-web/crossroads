using System.Collections.Generic;
using System.Text;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Applies the effect whitelist (GAME_DESIGN §4.2) through the StateMutator - the single
    /// write path - and returns a human-readable trail for the "after selection" feedback
    /// (§4.5: subtle affinity glyph hint AFTER selection, never before).
    /// Pure C# - unit-testable.
    /// </summary>
    public static class EffectApplier
    {
        public static List<AffinityDelta> Apply(List<DecisionEffectData> effects, StateMutator state)
        {
            var affinityDeltas = new List<AffinityDelta>();
            if (effects == null) return affinityDeltas;

            for (int i = 0; i < effects.Count; i++)
            {
                DecisionEffectData e = effects[i];
                if (e == null) continue;
                ApplyOne(e, state, affinityDeltas);
            }
            return affinityDeltas;
        }

        private static void ApplyOne(DecisionEffectData e, StateMutator state, List<AffinityDelta> deltas)
        {
            switch (e.type)
            {
                case EffectType.SetFlag:
                    state.SetFlag(e.key, e.value);
                    break;
                case EffectType.ClearFlag:
                    state.ClearFlag(e.key);
                    break;
                case EffectType.AddAffinity:
                {
                    state.AddAffinity(e.key, e.amount);
                    int after = state.GetAffinity(e.key);
                    deltas.Add(new AffinityDelta(AffinityLine.DisplayName(e.key), e.amount, after));
                    break;
                }
                case EffectType.SetAffinity:
                    state.SetAffinity(e.key, e.amount);
                    break;
                case EffectType.AddBond:
                    state.AddBond(e.key, e.amount);
                    break;
                case EffectType.SetVar:
                    state.SetVar(e.key, e.amount);
                    break;
                case EffectType.AddVar:
                    state.AddVar(e.key, e.amount);
                    break;
                case EffectType.SetWorldState:
                    state.SetWorldState(e.key, e.value);
                    break;
                case EffectType.SpawnEntity:
                    bool active = e.value == null || e.value == "" || e.value == "1" || e.value.ToLowerInvariant() == "true";
                    state.SetEntity(e.key, active);
                    break;
                case EffectType.AddCodex:
                    state.AddCodex(e.key);
                    break;
                case EffectType.GrantEchoes:
                    state.GrantEchoes(e.amount);
                    break;
            }
        }

        /// <summary>Compact summary e.g. "Ember +10 | bond mara +5 | world hall=ember | codex +1".</summary>
        public static string Summarize(List<DecisionEffectData> effects, StateMutator state)
        {
            if (effects == null || effects.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < effects.Count; i++)
            {
                DecisionEffectData e = effects[i];
                if (e == null) continue;
                if (sb.Length > 0) sb.Append(" | ");
                switch (e.type)
                {
                    case EffectType.SetFlag: sb.Append("flag ").Append(e.key).Append('=').Append(e.value); break;
                    case EffectType.ClearFlag: sb.Append("flag ").Append(e.key).Append(" cleared"); break;
                    case EffectType.AddAffinity: sb.Append(AffinityLine.DisplayName(e.key)).Append(" +").Append(e.amount); break;
                    case EffectType.SetAffinity: sb.Append(AffinityLine.DisplayName(e.key)).Append(" = ").Append(e.amount); break;
                    case EffectType.AddBond: sb.Append("bond ").Append(e.key).Append(" +").Append(e.amount); break;
                    case EffectType.SetVar: sb.Append("var ").Append(e.key).Append('=').Append(e.amount); break;
                    case EffectType.AddVar: sb.Append("var ").Append(e.key).Append(" +").Append(e.amount); break;
                    case EffectType.SetWorldState: sb.Append("world ").Append(e.key).Append('=').Append(e.value); break;
                    case EffectType.SpawnEntity: sb.Append("entity ").Append(e.key).Append(e.value == "1" ? " on" : " off"); break;
                    case EffectType.AddCodex: sb.Append("codex +1"); break;
                    case EffectType.GrantEchoes: sb.Append("echoes +").Append(e.amount); break;
                }
            }
            return sb.ToString();
        }
    }
}
