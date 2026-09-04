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

        // ------------------------------------------------ campaign (v5)
        /// <summary>Records a resolved story beat. Returns true when newly recorded.</summary>
        public bool MarkCampaignBeat(string beatId)
        {
            if (string.IsNullOrEmpty(beatId) || State.campaignBeats.Contains(beatId)) return false;
            State.campaignBeats.Add(beatId);
            StoryLog.Log("[CAMPAIGN] beat resolved: " + beatId);
            return true;
        }

        /// <summary>Records a taken branch (the run's route through the story). True when new.</summary>
        public bool MarkCampaignBranch(string branchId)
        {
            if (string.IsNullOrEmpty(branchId) || State.campaignBranches.Contains(branchId)) return false;
            State.campaignBranches.Add(branchId);
            StoryLog.Log("[CAMPAIGN] branch taken: " + branchId);
            return true;
        }

        /// <summary>Records a completed chapter. True when newly recorded.</summary>
        public bool MarkCampaignChapter(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId) || State.campaignChapters.Contains(chapterId)) return false;
            State.campaignChapters.Add(chapterId);
            StoryLog.Log("[CAMPAIGN] chapter completed: " + chapterId);
            return true;
        }

        /// <summary>Appends a story journal line (capped ring, oldest dropped). True when kept.</summary>
        public bool AddCampaignJournalLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            if (State.campaignJournal.Contains(line)) return false; // idempotent (beat texts are unique)
            State.campaignJournal.Add(line);
            while (State.campaignJournal.Count > 12) State.campaignJournal.RemoveAt(0);
            return true;
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
            EventBus.Publish(new VarChangedEvent { key = key, value = value });
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

        // ------------------------------------------------ reputation (faction standing, -100..100)
        public int GetReputation(string groupId) { return State.GetReputation(groupId); }

        public void AddReputation(string groupId, int amount)
        {
            if (amount == 0 || string.IsNullOrEmpty(groupId)) return;
            int next = GetReputation(groupId) + amount;
            if (next < -100) next = -100;
            if (next > 100) next = 100;
            SetReputation(groupId, next);
            EventBus.Publish(new ReputationChangedEvent { groupId = groupId, delta = amount, total = next });
            StoryLog.Log("[STATE] reputation " + groupId + " " + (amount > 0 ? "+" : "") + amount + " -> " + next);
        }

        public void SetReputation(string groupId, int amount)
        {
            var e = GameState.FindEntry(State.reputation, groupId);
            if (e == null) State.reputation.Add(new StringIntEntry(groupId, amount));
            else if (e.value == amount) return;
            else e.value = amount;
        }

        // ------------------------------------------------ unlocks (player abilities / powers)
        public bool HasAbility(string abilityId) { return State.HasAbility(abilityId); }

        public void UnlockAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || State.HasAbility(abilityId)) return;
            // a re-unlock may come from a later decision: last write wins over blocking
            var blocked = GameState.FindEntry(State.blockedAbilities, abilityId);
            if (blocked != null) State.blockedAbilities.Remove(blocked);
            State.abilities.Add(new StringEntry(abilityId, "1"));
            EventBus.Publish(new AbilityUnlockedEvent { abilityId = abilityId });
            StoryLog.Log("[STATE] ability unlocked: " + abilityId);
        }

        /// <summary>Excludes an ability by the player's choices (persisted; wins over unlocked).</summary>
        public void BlockAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || State.HasBlockedAbility(abilityId)) return;
            var unlocked = GameState.FindEntry(State.abilities, abilityId);
            if (unlocked != null) State.abilities.Remove(unlocked);
            var levelEntry = GameState.FindEntry(State.abilityLevels, abilityId);
            if (levelEntry != null) State.abilityLevels.Remove(levelEntry);
            State.blockedAbilities.Add(new StringEntry(abilityId, "1"));
            EventBus.Publish(new AbilityBlockedEvent { abilityId = abilityId });
            StoryLog.Log("[STATE] ability blocked: " + abilityId);
        }

        /// <summary>Raises an ability's level (first call levels an unlocked ability to 1+k).</summary>
        public void UpgradeAbility(string abilityId, int levels)
        {
            if (string.IsNullOrEmpty(abilityId) || levels == 0) return;
            if (!State.HasAbility(abilityId)) State.abilities.Add(new StringEntry(abilityId, "1"));
            int current = State.GetAbilityLevel(abilityId, 1);
            SetAbilityLevel(abilityId, current + levels);
        }

        public void SetAbilityLevel(string abilityId, int level)
        {
            if (level < 1) level = 1;
            var e = GameState.FindEntry(State.abilityLevels, abilityId);
            if (e == null) State.abilityLevels.Add(new StringIntEntry(abilityId, level));
            else if (e.value == level) return;
            else e.value = level;
            if (!State.HasAbility(abilityId)) State.abilities.Add(new StringEntry(abilityId, "1"));
            EventBus.Publish(new AbilityLevelChangedEvent { abilityId = abilityId, level = level });
            StoryLog.Log("[STATE] ability level " + abilityId + " -> " + level);
        }

        // ------------------------------------------------ skills (levels)
        public int GetSkill(string skillId) { return State.GetSkill(skillId); }

        public void AddSkillLevel(string skillId, int delta)
        {
            if (delta == 0 || string.IsNullOrEmpty(skillId)) return;
            int next = GetSkill(skillId) + delta;
            SetSkillLevel(skillId, next);
            EventBus.Publish(new SkillChangedEvent { skillId = skillId, delta = delta, level = next });
            StoryLog.Log("[STATE] skill " + skillId + " " + (delta > 0 ? "+" : "") + delta + " -> " + next);
        }

        public void SetSkillLevel(string skillId, int level)
        {
            var e = GameState.FindEntry(State.skills, skillId);
            if (e == null) State.skills.Add(new StringIntEntry(skillId, level));
            else if (e.value == level) return;
            else e.value = level;
        }

        // ------------------------------------------------ resources / items
        public bool HasItem(string itemId) { return State.HasItem(itemId); }
        public int ItemCount(string itemId) { return State.ItemCount(itemId); }

        public void AddItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            State.items.Add(new StringEntry(itemId, "1"));
            EventBus.Publish(new ItemChangedEvent { itemId = itemId, added = true, count = State.ItemCount(itemId) });
            StoryLog.Log("[STATE] item + " + itemId + " (x" + State.ItemCount(itemId) + ")");
        }

        public void RemoveItem(string itemId)
        {
            var e = GameState.FindEntry(State.items, itemId);
            if (e == null) return;
            State.items.Remove(e);
            EventBus.Publish(new ItemChangedEvent { itemId = itemId, added = false, count = State.ItemCount(itemId) });
            StoryLog.Log("[STATE] item - " + itemId);
        }

        // ------------------------------------------------ accessible areas
        public bool IsAreaUnlocked(string areaId) { return State.IsAreaUnlocked(areaId); }

        public void UnlockArea(string areaId)
        {
            if (string.IsNullOrEmpty(areaId) || State.IsAreaUnlocked(areaId)) return;
            State.unlockAreas.Add(new StringEntry(areaId, "1"));
            EventBus.Publish(new AreaUnlockedEvent { areaId = areaId });
            StoryLog.Log("[STATE] area unlocked: " + areaId);
        }

        public void SetCurrentArea(string areaId)
        {
            if (string.IsNullOrEmpty(areaId) || State.currentArea == areaId) return;
            State.currentArea = areaId;
            EventBus.Publish(new AreaChangedEvent { areaId = areaId });
            StoryLog.Log("[STATE] area -> " + areaId);
        }

        /// <summary>Re-seals an area (sweep locks it back down). Persists + notifies.</summary>
        public void CloseArea(string areaId)
        {
            if (string.IsNullOrEmpty(areaId) || State.IsAreaClosed(areaId)) return;
            State.closedAreas.Add(new StringEntry(areaId, "1"));
            EventBus.Publish(new AreaClosedEvent { areaId = areaId });
            StoryLog.Log("[STATE] area closed: " + areaId);
        }

        public void ReopenArea(string areaId)
        {
            var e = GameState.FindEntry(State.closedAreas, areaId);
            if (e == null) return;
            State.closedAreas.Remove(e);
            EventBus.Publish(new AreaReopenedEvent { areaId = areaId });
            StoryLog.Log("[STATE] area reopened: " + areaId);
        }

        public bool IsAreaClosed(string areaId) { return State.IsAreaClosed(areaId); }

        /// <summary>True when the player can be in the area (opened and not re-sealed).</summary>
        public bool IsAreaOpen(string areaId) { return State.IsAreaUnlocked(areaId) && !State.IsAreaClosed(areaId); }

        // ------------------------------------------------ objectives (single write path, §4.3)
        /// <summary>Persists an objective's phase/progress and notifies (ObjectiveChangedEvent).</summary>
        public void UpdateObjective(string objectiveId, ObjectivePhase phase, int progress)
        {
            if (string.IsNullOrEmpty(objectiveId)) return;
            var e = State.GetObjectiveEntry(objectiveId);
            int prevPhase = e != null ? e.phase : (int)ObjectivePhase.Hidden;
            int prevProgress = e != null ? e.progress : 0;
            if (e == null)
            {
                if (phase == ObjectivePhase.Hidden && progress == 0) return; // nothing to store
                State.objectives.Add(new ObjectiveProgressEntry(objectiveId, (int)phase, progress));
            }
            else
            {
                if (e.phase == (int)phase && e.progress == progress) return;
                e.phase = (int)phase;
                e.progress = progress;
            }
            EventBus.Publish(new ObjectiveChangedEvent
            {
                objectiveId = objectiveId,
                phase = phase,
                previousPhase = (ObjectivePhase)prevPhase,
                progress = progress
            });
            StoryLog.Log("[STATE] objective " + objectiveId + " -> " + phase + " (" + progress + ")");
        }

        public ObjectivePhase GetObjectivePhase(string objectiveId)
        {
            return (ObjectivePhase)State.GetObjectivePhase(objectiveId);
        }

        public int GetObjectiveProgress(string objectiveId) { return State.GetObjectiveProgress(objectiveId); }
        public bool ObjectiveWasCompleted(string objectiveId) { return GetObjectivePhase(objectiveId) == ObjectivePhase.Completed; }
        public bool ObjectiveFailed(string objectiveId) { return GetObjectivePhase(objectiveId) == ObjectivePhase.Failed; }
        public bool ObjectiveIsActive(string objectiveId) { return GetObjectivePhase(objectiveId) == ObjectivePhase.Active; }

        // ------------------------------------------------ NPC locations (world state)
        /// <summary>Relocates an NPC by key (MoveNpc effect). Persisted + NpcRelocatedEvent.</summary>
        public void SetNpcLocation(string npcId, string locationKey)
        {
            if (string.IsNullOrEmpty(npcId) || string.IsNullOrEmpty(locationKey)) return;
            var e = GameState.FindEntry(State.npcLocations, npcId);
            if (e == null) State.npcLocations.Add(new StringEntry(npcId, locationKey));
            else if (e.value == locationKey) return;
            else e.value = locationKey;
            EventBus.Publish(new NpcRelocatedEvent { npcId = npcId, locationKey = locationKey });
            StoryLog.Log("[STATE] npc " + npcId + " -> " + locationKey);
        }

        public string GetNpcLocation(string npcId, string fallback = "") { return State.GetNpcLocation(npcId, fallback); }

        // ------------------------------------------------ world interaction unlocks
        /// <summary>Records that a world interaction's conditions passed (persisted, once).</summary>
        public bool UnlockInteraction(string unlockKey, string label = "")
        {
            if (string.IsNullOrEmpty(unlockKey) || State.HasInteractionUnlock(unlockKey)) return false;
            State.interactionUnlocks.Add(new StringEntry(unlockKey, "1"));
            EventBus.Publish(new InteractionUnlockedEvent { unlockKey = unlockKey, label = label });
            StoryLog.Log("[STATE] interaction unlocked: " + unlockKey);
            return true;
        }

        public bool HasInteractionUnlock(string unlockKey) { return State.HasInteractionUnlock(unlockKey); }

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
            State.reputation = saved.reputation;
            State.abilities = saved.abilities;
            State.blockedAbilities = saved.blockedAbilities != null ? saved.blockedAbilities : new System.Collections.Generic.List<StringEntry>();
            State.abilityLevels = saved.abilityLevels != null ? saved.abilityLevels : new System.Collections.Generic.List<StringIntEntry>();
            State.items = saved.items;
            State.skills = saved.skills;
            State.unlockAreas = saved.unlockAreas;
            State.currentArea = saved.currentArea;
            State.decisions = saved.decisions;
            State.codex = saved.codex;
            State.ember = saved.ember; State.tide = saved.tide; State.stone = saved.stone; State.hollow = saved.hollow;
            State.echoBank = saved.echoBank;
            State.objectives = saved.objectives != null ? saved.objectives : new System.Collections.Generic.List<ObjectiveProgressEntry>();
            State.npcLocations = saved.npcLocations != null ? saved.npcLocations : new System.Collections.Generic.List<StringEntry>();
            State.interactionUnlocks = saved.interactionUnlocks != null ? saved.interactionUnlocks : new System.Collections.Generic.List<StringEntry>();
            State.closedAreas = saved.closedAreas != null ? saved.closedAreas : new System.Collections.Generic.List<StringEntry>();
            // campaign route (v5): restore the exact run history - beats, branches, completed
            // chapters and the journal. Without this the route silently re-derives on load and
            // non-re-derivable lines (chapter starts) vanish from the journal.
            State.campaignBeats = saved.campaignBeats != null ? saved.campaignBeats : new System.Collections.Generic.List<string>();
            State.campaignBranches = saved.campaignBranches != null ? saved.campaignBranches : new System.Collections.Generic.List<string>();
            State.campaignChapters = saved.campaignChapters != null ? saved.campaignChapters : new System.Collections.Generic.List<string>();
            State.campaignJournal = saved.campaignJournal != null ? saved.campaignJournal : new System.Collections.Generic.List<string>();
            StoryLog.Log("[STATE] loaded " + saved.decisions.Count + " decision(s) from save");
        }

        public void Reset()
        {
            GameState fresh = new GameState();
            LoadFrom(fresh);
        }
    }
}
