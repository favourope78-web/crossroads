using System.Collections.Generic;
using System.Text;
using Crossroads.Core;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Tracks the player's CURRENT state (task: "GameStateManager"). Single façade over the
    /// authoritative GameState/StateMutator - every read is the live value, every write goes
    /// through the mutator (and therefore saves, telemetry and the event bus). Exposes the
    /// data-driven player attributes with their display names:
    ///   reputation (faction standing) · relationships (bond tiers) · resources (items + echoes)
    ///   skills · unlocks (abilities, areas) · story flags · decisions · current area.
    /// Future encounters check previous state here (HasDecision / DecisionOption / FlagIs /
    /// Reputation / BondTier / HasAbility / HasItem / Skill / AreaUnlocked).
    /// Pure C# - headless-testable.
    /// </summary>
    public class GameStateManager
    {
        public readonly StateMutator State;
        public readonly ProgressionIndex Index;

        public GameStateManager(StateMutator state, IEncounterSource content)
        {
            State = state;
            Index = new ProgressionIndex(content != null ? content.Content : null);
        }

        // ------------------------------------------------ story flags + decisions
        public bool HasFlag(string key) { return State.HasFlag(key); }
        public bool FlagIs(string key, string value) { return State.FlagIs(key, value); }
        public string GetFlag(string key, string fallback = "") { return State.GetFlag(key, fallback); }
        public bool HasDecision(string decisionId) { return State.HasDecision(decisionId); }
        public string DecisionOption(string decisionId) { return State.DecisionOption(decisionId); }

        // ------------------------------------------------ areas (accessible + current)
        public string CurrentArea { get { return State.State.CurrentArea; } }
        public bool AreaUnlocked(string areaId) { return State.IsAreaUnlocked(areaId); }
        public void UnlockArea(string areaId) { State.UnlockArea(areaId); }
        public void SetCurrentArea(string areaId) { State.SetCurrentArea(areaId); }

        // ------------------------------------------------ reputation
        public int Reputation(string groupId) { return State.GetReputation(groupId); }
        public void AddReputation(string groupId, int delta) { State.AddReputation(groupId, delta); }

        /// <summary>Only non-zero reputation groups (id, name, value).</summary>
        public List<Triplet> Reputations()
        {
            var list = new List<Triplet>();
            var state = State.State;
            for (int i = 0; i < state.reputation.Count; i++)
            {
                if (state.reputation[i] == null || state.reputation[i].value == 0) continue;
                list.Add(new Triplet(state.reputation[i].key, Index.ReputationName(state.reputation[i].key), state.reputation[i].value));
            }
            return list;
        }

        // ------------------------------------------------ relationships (bond tiers §9.1)
        public int Bond(string npcId) { return State.GetBond(npcId); }
        public void AddBond(string npcId, int delta) { State.AddBond(npcId, delta); }

        public string BondTier(string npcId)
        {
            int b = Bond(npcId);
            if (b <= -50) return "Hostile";
            if (b < 0) return "Wary";
            if (b == 0) return "New";
            if (b < 50) return "Warm";
            if (b < 80) return "Bonded";
            return "Kin";
        }

        // ------------------------------------------------ unlocks (abilities) + skills
        public bool HasAbility(string abilityId) { return State.HasAbility(abilityId); }
        public void GrantAbility(string abilityId) { State.UnlockAbility(abilityId); }
        public List<string> Abilities() { return State.State.abilities.ConvertAll(a => a.key); }

        public int Skill(string skillId) { return State.GetSkill(skillId); }
        public void AddSkill(string skillId, int delta) { State.AddSkillLevel(skillId, delta); }

        // ------------------------------------------------ resources
        public bool HasItem(string itemId) { return State.HasItem(itemId); }
        public int ItemCount(string itemId) { return State.ItemCount(itemId); }
        public void AddItem(string itemId) { State.AddItem(itemId); }
        public void RemoveItem(string itemId) { State.RemoveItem(itemId); }
        public int Echoes { get { return State.State.echoBank; } }

        // ------------------------------------------------ player card (HUD status lines)
        /// <summary>Compact, UI-ready snapshot of the player's current state.</summary>
        public List<string> StatusLines()
        {
            var lines = new List<string>();

            lines.Add("Ember " + State.State.ember + " · Tide " + State.State.tide + " · Stone " + State.State.stone);

            var reps = Reputations();
            if (reps.Count > 0)
            {
                var sb = new StringBuilder("Standing  ");
                for (int i = 0; i < reps.Count; i++)
                    sb.Append(reps[i].name).Append(' ').Append(Signed(reps[i].value)).Append(i < reps.Count - 1 ? "  " : "");
                lines.Add(sb.ToString());
            }
            else lines.Add("Standing  unknown");

            lines.Add("Bonds  " + BondLine("mara") + "  ·  " + BondLine("sera"));

            var abilities = Abilities();
            lines.Add(abilities.Count > 0
                ? "Power  " + string.Join(" · ", abilities.ConvertAll(a => Index.AbilityName(a)).ToArray())
                : "Power  none");

            lines.Add("Skill  " + Index.SkillName("echo_attunement") + " " + Skill("echo_attunement"));

            lines.Add(State.HasItem("echo_shard")
                ? "Owns  " + Index.ItemName("echo_shard") + " ×" + ItemCount("echo_shard") + " · Echoes " + Echoes
                : "Owns  Echoes " + Echoes);

            int resolved = State.State.decisions != null ? State.State.decisions.Count : 0;
            lines.Add("Decisions  " + resolved + " recorded");

            lines.Add("Area  " + Index.AreaName(CurrentArea) + (AreaUnlocked("annex") ? "  ·  North Annex open" : ""));
            return lines;
        }

        private string BondLine(string npcId)
        {
            int b = Bond(npcId);
            return Index.NpcName(npcId) + " " + BondTier(npcId) + " (" + Signed(b) + ")";
        }

        private static string Signed(int v) { return v > 0 ? "+" + v : v.ToString(); }

        /// <summary>One-line debug description of the run (logs/tests).</summary>
        public string Describe()
        {
            return "[player] area=" + CurrentArea
                + " decisions=" + State.State.decisions.Count
                + " abilities=" + State.State.abilities.Count
                + " items=" + State.State.items.Count
                + " rep=" + State.State.reputation.Count;
        }
    }

    /// <summary>Triplet helper (id, display name, value) for status/tests.</summary>
    public class Triplet
    {
        public string id;
        public string name;
        public int value;
        public Triplet(string id, string name, int value) { this.id = id; this.name = name; this.value = value; }
    }
}
