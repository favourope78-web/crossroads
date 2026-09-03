using System.Collections.Generic;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Authoring helpers + the PROTOTYPE content for the consequence/progression phase:
    ///   1. c1_hall_first_light  - Mara, the first decision (A/B/C -> Ember/Tide/Stone).
    ///      Rewarded with reputation, an ability, a skill level, world state and entities.
    ///   2. c1_hall_shard        - the Fracture Shard in the East Annex (area access is
    ///      gated by the first decision's ability). Per-path lines + take/leave decision.
    ///   3. c1_hall_sera         - Sera at the east door: behavior/dialogue differ per first
    ///      choice; one choice option is ONLY available on the Tide path, another ONLY if
    ///      you carry the shard (future choices gated on previous decisions/state).
    ///
    /// All content is data (mirrored in content JSON + the authored asset); the runner,
    /// evaluators and GameStateManager never hardcode a branch.
    /// </summary>
    public static class StoryContentBuilder
    {
        // ids ------------------------------------------------------------------
        public const string EncounterFirstLight = "c1_hall_first_light";
        public const string DecisionFirstLight = "dec_c1_hall_first_light";
        public const string GraphFirstLight = "g_c1_hall_first_light";

        public const string EncounterShard = "c1_hall_shard";
        public const string DecisionShard = "dec_east_shard";
        public const string GraphShard = "g_c1_hall_shard";

        public const string EncounterSera = "c1_hall_sera";
        public const string DecisionSera = "dec_sera_lookout";
        public const string GraphSera = "g_c1_hall_sera";

        public const string DriveFlag = "c1_hall_drive";
        public const string AreaHall = "hall";
        public const string AreaAnnex = "east_annex";

        public const string AbilityEmber = "ember_pulse";
        public const string AbilityTide = "tide_mend";
        public const string AbilityStone = "stone_ward";
        public const string SkillAttunement = "echo_attunement";
        public const string ItemShard = "echo_shard";

        public static StoryContentData CreateFirstLightContent()
        {
            var content = new StoryContentData();

            // ---------------------------------------------------------------- progression data
            content.progression.abilities.AddRange(new List<AbilityDefinitionData>
            {
                new AbilityDefinitionData { id = AbilityEmber, name = "Ember Pulse", line = "ember",
                    description = "The first echo. Heat answers your will. (Gates: hall energy seals.)" },
                new AbilityDefinitionData { id = AbilityTide, name = "Tide Mend", line = "tide",
                    description = "The first echo. The hall breathes easier around you. (Gates: hall energy seals.)" },
                new AbilityDefinitionData { id = AbilityStone, name = "Stone Ward", line = "stone",
                    description = "The first echo. Stillness bends around you. (Gates: hall energy seals.)" }
            });
            content.progression.skills.Add(new SkillDefinitionData { id = SkillAttunement, name = "Echo Attunement", maxLevel = 3 });
            content.progression.items.Add(new ItemDefinitionData { id = ItemShard, name = "Fracture Shard",
                description = "A cold-warm sliver of the Fracture light. It held still for you." });
            content.progression.reputationGroups.AddRange(new List<ReputationGroupData>
            {
                new ReputationGroupData { id = "choir", name = "The Choir" },
                new ReputationGroupData { id = "folk", name = "People of Vessa" },
                new ReputationGroupData { id = "wards", name = "The Wardens" }
            });
            content.progression.areas.AddRange(new List<AreaDefinitionData>
            {
                new AreaDefinitionData { id = AreaHall, name = "Fracture Hall" },
                new AreaDefinitionData { id = AreaAnnex, name = "East Annex" }
            });

            // ---------------------------------------------------------------- decision 1 (the branch)
            content.decisions.Add(new DecisionNodeData
            {
                id = DecisionFirstLight,
                promptText = "The Fracture light hovers over the hall. It is waiting. How do you answer it?",
                codexEntryId = "c1_echo_first_light",
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData
                    {
                        id = "ember_reach",
                        text = "Take it. Reach the light before the Choir does.",
                        afterText = "Ari's palms open. The light pours in hot and red - and every wall in the hall suddenly looks like a door you could break.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = DriveFlag, value = "ember" },
                            new DecisionEffectData { type = EffectType.AddAffinity, key = "ember", amount = 10 },
                            new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 5 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "choir", amount = -10 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 5 },
                            new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "ember" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "ember_marker", value = "1" },
                            new DecisionEffectData { type = EffectType.UnlockAbility, key = AbilityEmber },
                            new DecisionEffectData { type = EffectType.AddSkillLevel, key = SkillAttunement, amount = 1 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_echo_ember" },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 15 }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "tide_clear",
                        text = "Get the others out. The light can wait.",
                        afterText = "You pull the twins clear, and the light settles where they were - soft. Like it is learning to watch you breathe.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = DriveFlag, value = "tide" },
                            new DecisionEffectData { type = EffectType.AddAffinity, key = "tide", amount = 10 },
                            new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 10 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 8 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "choir", amount = -3 },
                            new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "tide" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "tide_marker", value = "1" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "tide_bystanders", value = "1" },
                            new DecisionEffectData { type = EffectType.UnlockAbility, key = AbilityTide },
                            new DecisionEffectData { type = EffectType.AddSkillLevel, key = SkillAttunement, amount = 1 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_echo_tide" },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 20 }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "stone_still",
                        text = "Stay still. Watch. Don't let the Choir see you move.",
                        afterText = "You go quiet as the wall. The light circles once - and passes. It remembers patience.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = DriveFlag, value = "stone" },
                            new DecisionEffectData { type = EffectType.AddAffinity, key = "stone", amount = 10 },
                            new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 3 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "wards", amount = 8 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 2 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "choir", amount = -2 },
                            new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "stone" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "stone_marker", value = "1" },
                            new DecisionEffectData { type = EffectType.UnlockAbility, key = AbilityStone },
                            new DecisionEffectData { type = EffectType.AddSkillLevel, key = SkillAttunement, amount = 1 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_echo_stone" },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 15 }
                        }
                    }
                }
            });

            // ---------------------------------------------------------------- graph 1 (Mara)
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphFirstLight,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "start", speaker = "", text = "", branchPrefix = "opener" },
                    new DialogueNodeData { id = "opener_fresh", speaker = "Mara",
                        text = "The light came through the ceiling - did you see it? Everyone's running. The Choir guards are sealing the doors.",
                        nextId = "opener_fresh2",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionNotMade, key = DecisionFirstLight } } },
                    new DialogueNodeData { id = "opener_fresh2", speaker = "Mara",
                        text = "But it looked at you, Ari. I saw it. That's not nothing.",
                        nextId = "opener_fresh3" },
                    new DialogueNodeData { id = "opener_fresh3", speaker = "Mara",
                        text = "We can't both stand here. You decide - I'll follow you.",
                        nextId = "decide" },
                    new DialogueNodeData { id = "opener_again", speaker = "Mara",
                        text = "You're still standing. Still the one the light watches. What now?",
                        nextId = "decide",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "" } } },

                    new DialogueNodeData { id = "decide", speaker = "", text = "", decisionId = DecisionFirstLight, branchPrefix = "after" },

                    new DialogueNodeData { id = "after_ember", speaker = "Mara",
                        text = "When you moved, the columns burned red. The Choir saw. They're coming for you now, Ari.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "ember" } } },
                    new DialogueNodeData { id = "after_tide", speaker = "Mara",
                        text = "You pulled the twins clear - they'll make it. I've got your back. We move together from here.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" } } },
                    new DialogueNodeData { id = "after_stone", speaker = "Mara",
                        text = "The guards walked right past us. You're scaring me a little, Ari. That's the third time.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "stone" } } },
                    new DialogueNodeData { id = "after", speaker = "Mara",
                        text = "...Okay. That's what we do, then.",
                        nextId = "end" },
                    new DialogueNodeData { id = "end", speaker = "", text = "", end = true }
                }
            });

            // ---------------------------------------------------------------- decision 2 (shard)
            content.decisions.Add(new DecisionNodeData
            {
                id = DecisionShard,
                promptText = "The shard hums against your hand. The hall shifts its weight, waiting.",
                codexEntryId = "c1_shard",
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData
                    {
                        id = "take",
                        text = "Take it. It hums when you touch it.",
                        afterText = "Cold... no, warm. The shard settles against your palm like it has been waiting for you.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.AddItem, key = ItemShard },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 25 },
                            new DecisionEffectData { type = EffectType.AddSkillLevel, key = SkillAttunement, amount = 1 },
                            new DecisionEffectData { type = EffectType.SetFlag, key = "shard_taken", value = "1" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "echo_shard", value = "0" },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shard" },
                            new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 3 }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "leave",
                        text = "Leave it. Maybe the hall needs it more.",
                        afterText = "You step away. The shard dims, and for a second the hall seems to breathe with relief.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = "shard_left", value = "1" },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 2 },
                            new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 2 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shard_left" }
                        }
                    }
                }
            });

            // ---------------------------------------------------------------- graph 2 (shard)
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphShard,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "start", speaker = "", text = "", branchPrefix = "shard_line" },
                    new DialogueNodeData { id = "shard_line_ember", speaker = "The Shard",
                        text = "It is still warm where the light fell. It tilts toward you - like a compass stuck on your name.",
                        nextId = "shard_offer",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "ember" },
                            new DecisionConditionData { type = ConditionType.DecisionNotMade, key = DecisionShard }
                        } },
                    new DialogueNodeData { id = "shard_line_tide", speaker = "The Shard",
                        text = "It hums low, like a held breath. You feel the twins' fear in it - and their relief.",
                        nextId = "shard_offer",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" },
                            new DecisionConditionData { type = ConditionType.DecisionNotMade, key = DecisionShard }
                        } },
                    new DialogueNodeData { id = "shard_line_stone", speaker = "The Shard",
                        text = "It does not move. It waits, the way you waited. It watches you measure it.",
                        nextId = "shard_offer",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "stone" },
                            new DecisionConditionData { type = ConditionType.DecisionNotMade, key = DecisionShard }
                        } },
                    new DialogueNodeData { id = "shard_line_left", speaker = "The Shard",
                        text = "You left it before. It still hums - quieter. The choice stays yours.",
                        nextId = "shard_left_end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShard, value = "leave" } } },
                    new DialogueNodeData { id = "shard_left_end", speaker = "The Shard",
                        text = "The shard pulses once - acknowledgement - and dims.", end = true },
                    new DialogueNodeData { id = "shard_line_done", speaker = "The Shard",
                        text = "The shard is quiet now. It chose you once.", end = true,
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShard, value = "take" } } },

                    new DialogueNodeData { id = "shard_offer", speaker = "", text = "", decisionId = DecisionShard, branchPrefix = "shard_after" },
                    new DialogueNodeData { id = "shard_after_take", speaker = "", text = "", end = true,
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShard, value = "take" } } },
                    new DialogueNodeData { id = "shard_after_leave", speaker = "", text = "", end = true,
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShard, value = "leave" } } }
                }
            });

            // ---------------------------------------------------------------- decision 3 (Sera)
            content.decisions.Add(new DecisionNodeData
            {
                id = DecisionSera,
                promptText = "Sera watches the hall. What do you say?",
                codexEntryId = "",
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData
                    {
                        id = "lookout",
                        text = "I'd feel better with an eye on that north seal.",
                        afterText = "Sera nods once and takes up a spot by the north wall, eyes on the seal.",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" } },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.AddBond, key = "sera", amount = 10 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 3 },
                            new DecisionEffectData { type = EffectType.SetFlag, key = "sera_watch", value = "1" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "sera_lamp", value = "1" },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_sera_lookout" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "shard_show",
                        text = "Show her what you found in the annex.",
                        afterText = "Sera traces its edge. 'They'll want this. Keep it hidden, and keep moving.'",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ItemHeld, key = ItemShard } },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.AddBond, key = "sera", amount = 5 },
                            new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 2 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_archivist_hint" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "keep_low",
                        text = "Keep your head down, Sera.",
                        afterText = "Sera melts back into the shadows. You almost lose her.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.AddBond, key = "sera", amount = 3 },
                            new DecisionEffectData { type = EffectType.SetFlag, key = "sera_low", value = "1" }
                        }
                    }
                }
            });

            // ---------------------------------------------------------------- graph 3 (Sera)
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphSera,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "start", speaker = "", text = "", branchPrefix = "sera_line" },
                    new DialogueNodeData { id = "sera_line_ember", speaker = "Sera",
                        text = "You're the one who reached for it. The Choir has your scent now. I'd stay off the street.",
                        nextId = "sera_offer",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "ember" } } },
                    new DialogueNodeData { id = "sera_line_tide", speaker = "Sera",
                        text = "You got my sisters out. I owe you. If you need an eye on that north seal - I'm your lookout.",
                        nextId = "sera_offer",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" } } },
                    new DialogueNodeData { id = "sera_line_stone", speaker = "Sera",
                        text = "You went still as the wall and the sweep passed right through. I don't know if that's luck - or something else.",
                        nextId = "sera_offer",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "stone" } } },
                    new DialogueNodeData { id = "sera_line_default", speaker = "Sera",
                        text = "Careful. The Choir just swept the hall again. Something in there is looking at you.",
                        nextId = "sera_offer" },

                    new DialogueNodeData { id = "sera_offer", speaker = "", text = "", decisionId = DecisionSera, branchPrefix = "sera_after" },

                    new DialogueNodeData { id = "sera_after_lookout", speaker = "Sera",
                        text = "I'll be here. Nothing crosses that seal without me knowing it.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionSera, value = "lookout" } } },
                    new DialogueNodeData { id = "sera_after_shard_show", speaker = "Sera",
                        text = "A shard from the light... The Archivist would pay to study that. Stay careful, Ari.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionSera, value = "shard_show" } } },
                    new DialogueNodeData { id = "sera_after_keep_low", speaker = "Sera",
                        text = "...Yeah. Low is good. Low is what keeps you breathing.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionSera, value = "keep_low" } } },
                    new DialogueNodeData { id = "end", speaker = "", text = "", end = true }
                }
            });

            // ---------------------------------------------------------------- encounters
            content.encounters.AddRange(new List<EncounterDefinitionData>
            {
                new EncounterDefinitionData { id = EncounterFirstLight, npcName = "Mara", graphId = GraphFirstLight, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterShard, npcName = "The Shard", graphId = GraphShard, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterSera, npcName = "Sera", graphId = GraphSera, startNodeId = "start" }
            });

            return content;
        }
    }
}
