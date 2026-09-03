using System.Collections.Generic;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Builds the brief "what changed" indication shown after every choice (design:
    /// clear but short - affinity glyph hint AFTER selection, no spoilers before).
    /// All labels come from the data-driven ProgressionIndex - no hardcoded names.
    /// Pure C# - headless-testable.
    /// </summary>
    public static class EffectNotices
    {
        public static List<ChangeNotice> Build(List<DecisionEffectData> effects, StateMutator state, ProgressionIndex index)
        {
            var notices = new List<ChangeNotice>();
            if (effects == null) return notices;

            for (int i = 0; i < effects.Count; i++)
            {
                DecisionEffectData e = effects[i];
                if (e == null) continue;

                string text = "", category = "";
                switch (e.type)
                {
                    case EffectType.AddAffinity:
                        category = "affinity";
                        text = AffinityLine.DisplayName(e.key) + " " + Signed(e.amount) + " (" + state.GetAffinity(e.key) + ")";
                        break;
                    case EffectType.SetAffinity:
                        category = "affinity";
                        text = AffinityLine.DisplayName(e.key) + " = " + state.GetAffinity(e.key);
                        break;
                    case EffectType.AddBond:
                        category = "bond";
                        text = NameIf(index, e.key) + " " + Signed(e.amount) + " (" + state.GetBond(e.key) + ")";
                        break;
                    case EffectType.AddReputation:
                        category = "rep";
                        text = index != null ? index.ReputationName(e.key) : e.key;
                        text += " " + Signed(e.amount) + " (" + state.GetReputation(e.key) + ")";
                        break;
                    case EffectType.SetReputation:
                        category = "rep";
                        text = (index != null ? index.ReputationName(e.key) : e.key) + " = " + state.GetReputation(e.key);
                        break;
                    case EffectType.UnlockAbility:
                        category = "ability";
                        text = "Ability: " + (index != null ? index.AbilityName(e.key) : e.key);
                        break;
                    case EffectType.UpgradeAbility:
                        category = "ability";
                        text = (index != null ? index.AbilityName(e.key) : e.key) + " -> Level " + state.State.GetAbilityLevel(e.key, 1);
                        break;
                    case EffectType.BlockAbility:
                        category = "ability";
                        text = (index != null ? index.AbilityName(e.key) : e.key) + " sealed by your choice";
                        break;
                    case EffectType.AddSkillLevel:
                        category = "skill";
                        text = "Skill: " + (index != null ? index.SkillName(e.key) : e.key) + " " + (e.amount > 0 ? "+" + e.amount : e.amount.ToString()) + " (" + state.GetSkill(e.key) + ")";
                        break;
                    case EffectType.AddItem:
                        category = "item";
                        text = "Item: " + (index != null ? index.ItemName(e.key) : e.key) + " x" + state.ItemCount(e.key);
                        break;
                    case EffectType.RemoveItem:
                        category = "item";
                        text = "Item lost: " + (index != null ? index.ItemName(e.key) : e.key);
                        break;
                    case EffectType.UnlockArea:
                        category = "area";
                        text = "Area open: " + (index != null ? index.AreaName(e.key) : e.key);
                        break;
                    case EffectType.GrantEchoes:
                        category = "resource";
                        text = "Echoes +" + e.amount + " (" + state.State.echoBank + ")";
                        break;
                    case EffectType.SetFlag:
                        if (e.value == "1" || e.value == "true")
                        {
                            category = "flag";
                            text = "Flag: " + e.key;
                        }
                        break;
                    case EffectType.AddCodex:
                        category = "codex";
                        text = "Codex entry added";
                        break;
                    case EffectType.SpawnEntity:
                        category = "flag";
                        text = e.value == "1" ? "Presence stirs: " + e.key : "Presence fades: " + e.key;
                        break;
                }
                if (!string.IsNullOrEmpty(text)) notices.Add(new ChangeNotice(category, text));
            }
            return notices;
        }

        private static string NameIf(ProgressionIndex index, string id)
        {
            return index != null ? index.NpcName(id) : id;
        }

        private static string Signed(int v) { return v > 0 ? "+" + v : v.ToString(); }
    }
}
