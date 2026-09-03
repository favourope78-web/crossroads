using System.Collections.Generic;
using Crossroads.Narrative;

namespace Crossroads.UI
{
    /// <summary>One rendered row of the ability sheet, ready for display.</summary>
    public class AbilityRowView
    {
        public string abilityId = "";
        public string name = "";
        public string line = "";
        public string description = "";
        public string stateText = "";    // short line under the name
        public int level;
        public int maxLevel;
        public float cooldownRemaining;
        public AbilityAccessState access;
        public bool canActivateNow;
    }

    /// <summary>
    /// Pure snapshot builder for the power sheet - no Unity types. Turns the data-driven
    /// AbilityManager (definitions + persisted progress + live cooldowns) into display rows,
    /// so every UI rule (labels, states, activation availability) is headless-testable.
    /// </summary>
    public static class AbilitySheetModel
    {
        public static List<AbilityRowView> Build(AbilityManager manager)
        {
            var rows = new List<AbilityRowView>();
            if (manager == null) return rows;

            List<AbilityDefinitionData> defs = manager.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                AbilityDefinitionData def = defs[i];
                if (def == null) continue;
                rows.Add(BuildRow(manager, def));
            }
            return rows;
        }

        public static AbilityRowView BuildRow(AbilityManager manager, AbilityDefinitionData def)
        {
            var row = new AbilityRowView
            {
                abilityId = def.id,
                name = def.name,
                line = def.line,
                description = def.description,
                access = manager.AccessState(def.id),
                level = manager.Level(def.id),
                maxLevel = manager.MaxLevel(def.id),
                cooldownRemaining = manager.CooldownRemaining(def.id)
            };

            switch (row.access)
            {
                case AbilityAccessState.Blocked:
                    row.stateText = "SEALED - given back to the hall";
                    row.canActivateNow = false;
                    break;
                case AbilityAccessState.Unlocked:
                    if (row.cooldownRemaining > 0f)
                        row.stateText = "Lv " + row.level + " · recharging " + Clean(row.cooldownRemaining) + "s";
                    else
                        row.stateText = "Lv " + row.level + (row.level < row.maxLevel ? " · READY" : " · READY (MAX)");
                    row.canActivateNow = row.cooldownRemaining <= 0f;
                    break;
                default:
                    row.stateText = "LOCKED - " + def.unlockHint;
                    row.canActivateNow = false;
                    break;
            }
            return row;
        }

        private static string Clean(float seconds)
        {
            int s = (int)(seconds + 0.999f);
            return s.ToString();
        }
    }
}
