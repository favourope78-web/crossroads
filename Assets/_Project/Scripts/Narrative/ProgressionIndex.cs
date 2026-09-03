using System;
using System.Collections.Generic;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Display-name index over the data-driven progression content (progression section of
    /// the story library). Gameplay/UI ask "what is this called" here - never hardcode names.
    /// Pure C# - headless-testable.
    /// </summary>
    public class ProgressionIndex
    {
        private readonly Dictionary<string, string> _abilityNames = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _skillNames = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _itemNames = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _repNames = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _areaNames = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _npcNames = new Dictionary<string, string> { { "mara", "Mara" }, { "sera", "Sera" } };

        public ProgressionIndex() { }
        public ProgressionIndex(StoryContentData content)
        {
            if (content == null) return;
            ProgressionContentData p = content.progression;
            if (p == null) return;

            if (p.abilities != null)
                for (int i = 0; i < p.abilities.Count; i++)
                    if (p.abilities[i] != null && !string.IsNullOrEmpty(p.abilities[i].id))
                        _abilityNames[p.abilities[i].id] = string.IsNullOrEmpty(p.abilities[i].name) ? p.abilities[i].id : p.abilities[i].name;

            if (p.skills != null)
                for (int i = 0; i < p.skills.Count; i++)
                    if (p.skills[i] != null && !string.IsNullOrEmpty(p.skills[i].id))
                        _skillNames[p.skills[i].id] = string.IsNullOrEmpty(p.skills[i].name) ? p.skills[i].id : p.skills[i].name;

            if (p.items != null)
                for (int i = 0; i < p.items.Count; i++)
                    if (p.items[i] != null && !string.IsNullOrEmpty(p.items[i].id))
                        _itemNames[p.items[i].id] = string.IsNullOrEmpty(p.items[i].name) ? p.items[i].id : p.items[i].name;

            if (p.reputationGroups != null)
                for (int i = 0; i < p.reputationGroups.Count; i++)
                    if (p.reputationGroups[i] != null && !string.IsNullOrEmpty(p.reputationGroups[i].id))
                        _repNames[p.reputationGroups[i].id] = string.IsNullOrEmpty(p.reputationGroups[i].name) ? p.reputationGroups[i].id : p.reputationGroups[i].name;

            if (p.areas != null)
                for (int i = 0; i < p.areas.Count; i++)
                    if (p.areas[i] != null && !string.IsNullOrEmpty(p.areas[i].id))
                        _areaNames[p.areas[i].id] = string.IsNullOrEmpty(p.areas[i].name) ? p.areas[i].id : p.areas[i].name;
        }

        public string AbilityName(string id) { return Lookup(_abilityNames, id); }
        public string SkillName(string id) { return Lookup(_skillNames, id); }
        public string ItemName(string id) { return Lookup(_itemNames, id); }
        public string ReputationName(string id) { return Lookup(_repNames, id); }
        public string AreaName(string id) { return Lookup(_areaNames, id); }

        public string NpcName(string id)
        {
            string name;
            if (!string.IsNullOrEmpty(id) && _npcNames.TryGetValue(id, out name)) return name;
            if (string.IsNullOrEmpty(id)) return "?";
            return char.ToUpperInvariant(id[0]) + id.Substring(1);
        }

        private static string Lookup(Dictionary<string, string> map, string id)
        {
            string v;
            if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out v)) return v;
            return id;
        }
    }
}
