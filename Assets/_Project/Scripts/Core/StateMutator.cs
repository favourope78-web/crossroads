using System;

namespace Crossroads.Core
{
    /// <summary>
    /// The ONLY write path to GameState (GAME_DESIGN §4.3: "all writes go through
    /// StateMutator so saves, telemetry and the codex see every change in one place").
    /// Every mutation publishes a typed event and logs a one-line trail.
    /// Pure C# - headless-testable.
    /// </summary>
    public class StateMutator
    {
        public readonly GameState State;

        public StateMutator(GameState state)
        {
            State = state ?? new GameState();
        }

        // ------------------------------------------------ flags
        public void SetFlag(string key, string value)
        {
            var e = GameState.FindEntry(State.flags, key);
            if (e == null) { State.flags.Add(new StringEntry(key, value)); }
            else if (e.value == value) { return; }
            else e.value = value;
            EventBus.Publish(new FlagChangedEvent { key = key, value = value });
            StoryLog.Log("[STATE] flag " + key + " = " + value);
        }

        public void ClearFlag(string key)
        {
            var e = GameState.FindEntry(State.flags, key);
            if (e == null) return;
            State.flags.Remove(e);
            EventBus.Publish(new FlagChangedEvent { key = key, value = "" });
            StoryLog.Log("[STATE] flag " + key + " cleared");
        }

        public string GetFlag(string key, string fallback = "") { return State.GetFlag(key, fallback); }
        public bool FlagIs(string key, string value) { return State.HasFlag(key) && State.GetFlag(key) == value; }
        public bool HasFlag(string key) { return State.HasFlag(key); }

        // ------------------------------------------------ world state (the city remembers, §5.2)
        public void SetWorldState(string areaKey, string variantKey)
        {
            var e = GameState.FindEntry(State.worldStates, areaKey);
            if (e == null) State.worldStates.Add(new StringEntry(areaKey, variantKey));
            else if (e.value == variantKey) return;
            else e.value = variantKey;
            EventBus.Publish(new WorldStateChangedEvent { areaKey = areaKey, variantKey = variantKey });
            StoryLog.Log("[STATE] world " + areaKey + " -> " + variantKey);
        }

        public string GetWorldState(string areaKey, string fallback = "") { return State.GetWorldState(areaKey, fallback); }

        // ------------------------------------------------ persistable entity toggles
        public void SetEntity(string key, bool active)
        {
            var e = GameState.FindEntry(State.entities, key);
            if (e == null) State.entities.Add(new StringBoolEntry(key, active));
            else if (e.value == active) return;
            else e.value = active;
            EventBus.Publish(new EntityStateChangedEvent { entityKey = key, active = active });
            StoryLog.Log("[STATE] entity " + key + " = " + (active ? "on" : "off"));
        }

        public bool GetEntity(string key, bool fallback = false) { return State.GetEntity(key, fallback); }

        // ------------------------------------------------ generic vars
        public void SetVar(string key, int value)
        {
            var e = GameState.FindEntry(State.vars, key);
            if (e == null) State.vars.Add(new StringIntEntry(key, value));
            else if (e.value == value) return;
            else e.value = value;
            StoryLog.Log("[STATE] var " + key + " = " + value);
        }

        public void AddVar(string key, int delta)
        {
            SetVar(key, GetVar(key, 0) + delta);
        }

        public int GetVar(string key, int fallback = 0) { return State.GetVar(key, fallback); }

        // ------------------------------------------------ affinity meters (§3.2)
        public int GetAffinity(string line) { return State.GetAffinity(line); }

        public void AddAffinity(string line, int amount)
        {
            if (amount == 0) return;
            string canonical;
            if (!AffinityLine.TryParse(line, out canonical)) return;
            int next = ClampAffinity(GetAffinity(canonical) + amount);
            SetAffinityRaw(canonical, next);
            EventBus.Publish(new AffinityChangedEvent { line = canonical, delta = amount, total = next });
            StoryLog.Log("[STATE] affinity " + canonical + " " + (amount > 0 ? "+" : "") + amount + " -> " + next);
        }

        public void SetAffinity(string line, int amount)
        {
            string canonical;
            if (!AffinityLine.TryParse(line, out canonical)) return;
            SetAffinityRaw(canonical, ClampAffinity(amount));
        }

        private void SetAffinityRaw(string canonical, int value)
        {
            switch (canonical)
            {
                case "ember": State.ember = value; break;
                case "tide": State.tide = value; break;
                case "stone": State.stone = value; break;
                case "hollow": State.hollow = value; break;
            }
        }

        private static int ClampAffinity(int v) { return v < 0 ? 0 : (v > 100 ? 100 : v); }

        // ------------------------------------------------ bonds (NPC relationships, §9.1)
        public int GetBond(string npcId) { return State.GetBond(npcId); }

        public void AddBond(string npcId, int amount)
        {
            if (amount == 0 || string.IsNullOrEmpty(npcId)) return;
            int next = GetBond(npcId) + amount;
            if (next < -100) next = -100;
            if (next > 100) next = 100;
            var e = GameState.FindEntry(State.bonds, npcId);
            if (e == null) State.bonds.Add(new StringIntEntry(npcId, next));
            else e.value = next;
            EventBus.Publish(new BondChangedEvent { npcId = npcId, delta = amount, total = next });
            StoryLog.Log("[STATE] bond " + npcId + " " + (amount > 0 ? "+" : "") + amount + " -> " + next);
        }

        // ------------------------------------------------ codex
        public void AddCodex(string codexEntryId)
        {
            if (string.IsNullOrEmpty(codexEntryId) || State.HasCodex(codexEntryId)) return;
            State.codex.Add(codexEntryId);
            StoryLog.Log("[STATE] codex + " + codexEntryId);
        }

        public bool HasCodex(string codexEntryId) { return State.HasCodex(codexEntryId); }

        // ------------------------------------------------ decisions (persistent history)
        public void RecordDecision(string decisionId, string optionId, string summary)
        {
            if (string.IsNullOrEmpty(decisionId)) return;
            var existing = State.GetDecision(decisionId);
            if (existing != null) State.decisions.Remove(existing);
            State.decisions.Add(new ResolvedDecisionEntry(decisionId, optionId, summary));
            StoryLog.Log("[STATE] decision " + decisionId + " -> " + optionId);
        }

        public bool HasDecision(string decisionId) { return State.HasDecision(decisionId); }
        public string DecisionOption(string decisionId) { return State.DecisionOption(decisionId); }
        public ResolvedDecisionEntry GetDecision(string decisionId) { return State.GetDecision(decisionId); }

        // ------------------------------------------------ echoes currency (§3.3)
        public void GrantEchoes(int amount)
        {
            State.echoBank += amount;
            StoryLog.Log("[STATE] echoes + " + amount + " (total " + State.echoBank + ")");
        }

        /// <summary>Replaces this mutator's state contents with a loaded save (used at boot).</summary>
        public void LoadFrom(GameState saved)
        {
            if (saved == null) return;
            State.chapterId = saved.chapterId;
            State.levelId = saved.levelId;
            State.flags = saved.flags;
            State.worldStates = saved.worldStates;
            State.entities = saved.entities;
            State.vars = saved.vars;
            State.bonds = saved.bonds;
            State.decisions = saved.decisions;
            State.codex = saved.codex;
            State.ember = saved.ember; State.tide = saved.tide; State.stone = saved.stone; State.hollow = saved.hollow;
            State.echoBank = saved.echoBank;
            StoryLog.Log("[STATE] loaded " + saved.decisions.Count + " decision(s) from save");
        }

        public void Reset()
        {
            GameState fresh = new GameState();
            LoadFrom(fresh);
        }
    }
}
