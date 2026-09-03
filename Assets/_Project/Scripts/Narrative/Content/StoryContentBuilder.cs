using System.Collections.Generic;
using Crossroads.Core;

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

        public const string EncounterShrine = "c1_east_shrine";
        public const string DecisionShrine = "dec_east_shrine";
        public const string GraphShrine = "g_c1_east_shrine";

        public const string NpcMara = "mara";
        public const string NpcSera = "sera";
        public const string EncounterMaraConfide = "c1_hall_mara_confide";
        public const string GraphMaraConfide = "g_c1_hall_mara_confide";
        public const string EncounterSeraShard = "c1_hall_sera_shard";
        public const string GraphSeraShard = "g_c1_hall_sera_shard";

        public static StoryContentData CreateFirstLightContent()
        {
            var content = new StoryContentData();

            // ---------------------------------------------------------------- progression data
            content.progression.abilities.AddRange(new List<AbilityDefinitionData>
            {
                new AbilityDefinitionData
                {
                    id = AbilityEmber, name = "Ember Pulse", line = "ember", category = AbilityCategory.Active,
                    description = "The first echo. Heat answers your will in a red pulse that rolls through water and air alike. (Gates: hall energy seals.)",
                    unlockHint = "Claim the Fracture light in the First Hall.",
                    unlockConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "ember_reach" } },
                    vfxRef = "fx/pulse/ember", sfxRef = "sfx/pulse/ember", echoCostPerLevel = 10,
                    levels = new List<AbilityLevelData>
                    {
                        new AbilityLevelData { level = 1, cooldown = 12f, power = 1f, radius = 3.5f, duration = 1f, energyCost = 0,
                            description = "A restrained burst. The echo answers once, then waits." },
                        new AbilityLevelData { level = 2, cooldown = 9f, power = 1.5f, radius = 4.5f, duration = 1.4f, energyCost = 0,
                            description = "The pulse runs deeper and returns sooner. The hall's heat bends around you." },
                        new AbilityLevelData { level = 3, cooldown = 6f, power = 2.25f, radius = 6f, duration = 1.8f, energyCost = 0,
                            description = "Full bind. The echo answers as fast as your heart and burns twice as wide." }
                    }
                },
                new AbilityDefinitionData
                {
                    id = AbilityTide, name = "Tide Mend", line = "tide", category = AbilityCategory.Active,
                    description = "The first echo. A cool washing swell that soothes what the Fracture has bruised - and settles those who stand too close. (Gates: hall energy seals.)",
                    unlockHint = "Put others first when the light falls in the First Hall.",
                    unlockConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "tide_clear" } },
                    vfxRef = "fx/pulse/tide", sfxRef = "sfx/pulse/tide", echoCostPerLevel = 10,
                    levels = new List<AbilityLevelData>
                    {
                        new AbilityLevelData { level = 1, cooldown = 12f, power = 1f, radius = 3.5f, duration = 1f, energyCost = 0,
                            description = "A soft wash. Frayed edges pull themselves straight." },
                        new AbilityLevelData { level = 2, cooldown = 9f, power = 1.5f, radius = 4.5f, duration = 1.4f, energyCost = 0,
                            description = "The wash carries further and comes sooner. The hall breathes easier around you." },
                        new AbilityLevelData { level = 3, cooldown = 6f, power = 2.25f, radius = 6f, duration = 1.8f, energyCost = 0,
                            description = "Full bind. The tide moves through you like it has always known the way." }
                    }
                },
                new AbilityDefinitionData
                {
                    id = AbilityStone, name = "Stone Ward", line = "stone", category = AbilityCategory.Active,
                    description = "The first echo. A ring of stillness that slows what moves too fast and holds what moves too close. (Gates: hall energy seals.)",
                    unlockHint = "Refuse to move when the light hunts in the First Hall.",
                    unlockConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "stone_still" } },
                    vfxRef = "fx/pulse/stone", sfxRef = "sfx/pulse/stone", echoCostPerLevel = 10,
                    levels = new List<AbilityLevelData>
                    {
                        new AbilityLevelData { level = 1, cooldown = 12f, power = 1f, radius = 3.5f, duration = 1f, energyCost = 0,
                            description = "A quiet ring. The hurry around you remembers how to wait." },
                        new AbilityLevelData { level = 2, cooldown = 9f, power = 1.5f, radius = 4.5f, duration = 1.4f, energyCost = 0,
                            description = "The ring holds wider and re-forms sooner. Stillness bends around you." },
                        new AbilityLevelData { level = 3, cooldown = 6f, power = 2.25f, radius = 6f, duration = 1.8f, energyCost = 0,
                            description = "Full bind. You are where you stand - and the hall knows it." }
                    }
                }
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

            // ---------------------------------------------------------------- decision 4 (Echo Shrine: upgrade / seal)
            content.decisions.Add(new DecisionNodeData
            {
                id = DecisionShrine,
                promptText = "The plinth of light waits. It knows the echo you carry - and it is hungry for more.",
                codexEntryId = "c1_shrine",
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData
                    {
                        id = "deep_ember",
                        text = "Pour echoes in. Deepen the Ember bind.",
                        afterText = "The plinth drinks the red light and gives it back, doubled.",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityEmber },
                            new DecisionConditionData { type = ConditionType.EchoesAtLeast, amount = 10 },
                            new DecisionConditionData { type = ConditionType.AbilityLevelBelow, key = AbilityEmber, amount = 3 }
                        },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.UpgradeAbility, key = AbilityEmber, amount = 1 },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = -10 },
                            new DecisionEffectData { type = EffectType.AddSkillLevel, key = SkillAttunement, amount = 1 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shrine_deep" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "deep_tide",
                        text = "Pour echoes in. Deepen the Tide bind.",
                        afterText = "The plinth drinks the cool light and gives it back, deeper.",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityTide },
                            new DecisionConditionData { type = ConditionType.EchoesAtLeast, amount = 10 },
                            new DecisionConditionData { type = ConditionType.AbilityLevelBelow, key = AbilityTide, amount = 3 }
                        },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.UpgradeAbility, key = AbilityTide, amount = 1 },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = -10 },
                            new DecisionEffectData { type = EffectType.AddSkillLevel, key = SkillAttunement, amount = 1 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shrine_deep" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "deep_stone",
                        text = "Pour echoes in. Deepen the Stone bind.",
                        afterText = "The plinth drinks the pale light and gives it back, heavier.",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityStone },
                            new DecisionConditionData { type = ConditionType.EchoesAtLeast, amount = 10 },
                            new DecisionConditionData { type = ConditionType.AbilityLevelBelow, key = AbilityStone, amount = 3 }
                        },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.UpgradeAbility, key = AbilityStone, amount = 1 },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = -10 },
                            new DecisionEffectData { type = EffectType.AddSkillLevel, key = SkillAttunement, amount = 1 },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shrine_deep" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "seal_ember",
                        text = "Offer the Ember echo. Let the hall take it back.",
                        afterText = "You lay the red echo on the stone. It sinks without a sound - and the hall exhales.",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityEmber }
                        },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.BlockAbility, key = AbilityEmber },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 20 },
                            new DecisionEffectData { type = EffectType.SetFlag, key = "c1_echo_sealed", value = "1" },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shrine_seal" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "seal_tide",
                        text = "Offer the Tide echo. Let the hall take it back.",
                        afterText = "You lay the cool echo on the stone. It sinks without a sound - and the hall exhales.",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityTide }
                        },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.BlockAbility, key = AbilityTide },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 20 },
                            new DecisionEffectData { type = EffectType.SetFlag, key = "c1_echo_sealed", value = "1" },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shrine_seal" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "seal_stone",
                        text = "Offer the Stone echo. Let the hall take it back.",
                        afterText = "You lay the pale echo on the stone. It sinks without a sound - and the hall exhales.",
                        conditions = new List<DecisionConditionData>
                        {
                            new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityStone }
                        },
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.BlockAbility, key = AbilityStone },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 20 },
                            new DecisionEffectData { type = EffectType.SetFlag, key = "c1_echo_sealed", value = "1" },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_shrine_seal" }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "leave",
                        text = "Step back. The shrine can wait.",
                        afterText = "You step back. The plinth dims, patient as stone.",
                        conditions = new List<DecisionConditionData>(),
                        effects = new List<DecisionEffectData>()
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

            // ---------------------------------------------------------------- graph 4 (Mara confide - bond-gated interaction)
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphMaraConfide,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "start", branchPrefix = "confide_line" },
                    new DialogueNodeData { id = "confide_line_tide", speaker = "Mara",
                        text = "You got the twins out. Everyone saw it. I used to think this hall only makes people harder - then you showed up still soft.",
                        nextId = "confide_promise",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" } } },
                    new DialogueNodeData { id = "confide_line_ember", speaker = "Mara",
                        text = "You took the light like it owed you. The whole hall felt it. Just... remember who you were before it, alright?",
                        nextId = "confide_promise",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "ember" } } },
                    new DialogueNodeData { id = "confide_line_stone", speaker = "Mara",
                        text = "You walked through that light like it was nothing. Calm as ever. It's a little unnerving, honestly - in the good way.",
                        nextId = "confide_promise",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "stone" } } },
                    new DialogueNodeData { id = "confide_line_default", speaker = "Mara",
                        text = "It's good to hear your voice. This place gets loud in the quiet hours.",
                        nextId = "confide_promise" },
                    new DialogueNodeData { id = "confide_promise", speaker = "Mara",
                        text = "Promise me you'll keep being the person who helps before he hesitates.",
                        nextId = "end" },
                    new DialogueNodeData { id = "end", speaker = "", text = "", end = true }
                }
            });

            // ---------------------------------------------------------------- graph 5 (Sera shard story - item-gated interaction)
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphSeraShard,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "start", branchPrefix = "shard_story" },
                    new DialogueNodeData { id = "shard_story_tide", speaker = "Sera",
                        text = "It hums low and easy - like the hall is glad you're carrying it. I don't know who chose you for it. I just know it wasn't a mistake.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" } } },
                    new DialogueNodeData { id = "shard_story_ember", speaker = "Sera",
                        text = "Careful with that. Bright things pick owners - and they always want more than they give. ...But it chose you, so maybe it gives back.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "ember" } } },
                    new DialogueNodeData { id = "shard_story_stone", speaker = "Sera",
                        text = "It sits quiet in your hand. Like it already decided to stay. That settles easier than anything else in this hall.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "stone" } } },
                    new DialogueNodeData { id = "shard_story_default", speaker = "Sera",
                        text = "A sliver of the Fracture. Most of us never touch one, ever. You just... carry it. Keep it safe, then.",
                        nextId = "end" },
                    new DialogueNodeData { id = "end", speaker = "", text = "", end = true }
                }
            });

            // ---------------------------------------------------------------- graph 6 (Echo Shrine)
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphShrine,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "start", speaker = "", text = "", branchPrefix = "shrine_line" },
                    new DialogueNodeData { id = "shrine_line_fresh", speaker = "Echo Shrine",
                        text = "A small plinth of fractured light rises from the annex floor. It hums at the frequency of what you carry.",
                        nextId = "shrine_offer",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionNotMade, key = DecisionShrine } } },
                    new DialogueNodeData { id = "shrine_line_again", speaker = "Echo Shrine",
                        text = "The plinth's light settles, then lifts again. It remembers your footprint in the echo.",
                        nextId = "shrine_offer",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "" } } },
                    new DialogueNodeData { id = "shrine_line_default", speaker = "Echo Shrine",
                        text = "The plinth hums, patient.",
                        nextId = "shrine_offer" },
                    new DialogueNodeData { id = "shrine_offer", speaker = "", text = "", decisionId = DecisionShrine, branchPrefix = "shrine_after" },
                    new DialogueNodeData { id = "after_deep_ember", speaker = "Echo Shrine",
                        text = "The red light folds into you. The echo answers deeper now.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "deep_ember" } } },
                    new DialogueNodeData { id = "after_deep_tide", speaker = "Echo Shrine",
                        text = "The cool light folds into you. The echo answers deeper now.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "deep_tide" } } },
                    new DialogueNodeData { id = "after_deep_stone", speaker = "Echo Shrine",
                        text = "The pale light folds into you, heavy and unhurried. The echo answers deeper now.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "deep_stone" } } },
                    new DialogueNodeData { id = "after_seal_ember", speaker = "Echo Shrine",
                        text = "The red echo sinks into the stone. You walk lighter - and the hall no longer rings for you.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "seal_ember" } } },
                    new DialogueNodeData { id = "after_seal_tide", speaker = "Echo Shrine",
                        text = "The cool echo sinks into the stone. You walk lighter - and the hall no longer rings for you.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "seal_tide" } } },
                    new DialogueNodeData { id = "after_seal_stone", speaker = "Echo Shrine",
                        text = "The pale echo sinks into the stone. You walk lighter - and the hall no longer rings for you.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "seal_stone" } } },
                    new DialogueNodeData { id = "after_leave", speaker = "Echo Shrine",
                        text = "You step back. The plinth dims, patient as stone, still humming your name.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionShrine, value = "leave" } } },
                    new DialogueNodeData { id = "end", speaker = "", text = "", end = true }
                }
            });

            // ---------------------------------------------------------------- encounters
            content.encounters.AddRange(new List<EncounterDefinitionData>
            {
                new EncounterDefinitionData { id = EncounterFirstLight, npcName = "Mara", graphId = GraphFirstLight, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterShard, npcName = "The Shard", graphId = GraphShard, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterSera, npcName = "Sera", graphId = GraphSera, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterMaraConfide, npcName = "Mara", graphId = GraphMaraConfide, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterSeraShard, npcName = "Sera", graphId = GraphSeraShard, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterShrine, npcName = "Echo Shrine", graphId = GraphShrine, startNodeId = "start" }
            });

            // ---------------------------------------------------------------- NPC definitions (§9: one character = one data row)
            content.npcs.AddRange(new List<NpcDefinitionData>
            {
                new NpcDefinitionData
                {
                    id = NpcMara, displayName = "Mara", sheetRef = "REF-02",
                    description = "Childhood friend. Bond keeps her near; your choices set her mood.",
                    behaviour = new NpcBehaviourData
                    {
                        personality = NpcPersonality.Friendly, facesPlayer = true,
                        reactRadius = 4.5f, approachDistance = 1.6f, avoidDistance = 0f,
                        talkDistance = 2.0f, moveSpeed = 1.1f, turnSpeed = 6f, usesRoutine = true
                    },
                    states = new List<NpcStateData>
                    {
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.BondAtLeast, key = NpcMara, amount = 8 } },
                            title = "Mara · Warm",
                            moodLine = "Mara's eyes soften. She stays closer now.",
                            approachDistance = 1.3f, avoidDistance = -1f, moveSpeed = -1f, reactRadius = -1f
                        }
                    },
                    interactions = new List<NpcInteractionData>
                    {
                        new NpcInteractionData { id = "confide", label = "Comfort Mara", encounterId = EncounterMaraConfide,
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.BondAtLeast, key = NpcMara, amount = 8 } } },
                        new NpcInteractionData { id = "talk", label = "Talk to Mara", encounterId = EncounterFirstLight }
                    },
                    routine = new List<NpcStopData>
                    {
                        new NpcStopData { position = new Point3(4.5f, 0f, -8f), dwellSeconds = 2.5f },
                        new NpcStopData { position = new Point3(7.2f, 0f, -5.8f), dwellSeconds = 2.5f }
                    }
                },
                new NpcDefinitionData
                {
                    id = NpcSera, displayName = "Sera", sheetRef = "REF-04",
                    description = "A refugee from the lower halls. Wary of the Echo; warms only to proof of kindness.",
                    behaviour = new NpcBehaviourData
                    {
                        personality = NpcPersonality.Wary, facesPlayer = true,
                        reactRadius = 4.5f, approachDistance = 0f, avoidDistance = 2.6f,
                        talkDistance = 2.2f, moveSpeed = 0.9f, turnSpeed = 4f, usesRoutine = true
                    },
                    states = new List<NpcStateData>
                    {
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = "c1_echo_sealed", value = "1" } },
                            title = "Sera · Warded",
                            moodLine = "Sera's eyes rest on your empty hands. 'The hall says you gave it back. I can hear it in the silence you leave.'",
                            approachDistance = 1.8f, avoidDistance = 0f, moveSpeed = 1.0f, reactRadius = -1f
                        },
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.SkillAtLeast, key = SkillAttunement, amount = 2 } },
                            title = "Sera · Attuned",
                            moodLine = "'Your echo rings twice as deep as it did.' She nods, almost approving.",
                            approachDistance = 1.5f, avoidDistance = 0f, moveSpeed = 1.0f, reactRadius = -1f
                        },
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" } },
                            title = "Sera · Grateful",
                            moodLine = "Sera's guard drops. She steps closer, unafraid.",
                            approachDistance = 1.5f, avoidDistance = 0f, moveSpeed = 1.0f, reactRadius = -1f
                        },
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "ember" } },
                            title = "Sera · Watchful",
                            moodLine = "Sera keeps her distance. Your echo burns too bright.",
                            approachDistance = 0f, avoidDistance = 3.4f, moveSpeed = -1f, reactRadius = -1f
                        },
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "stone" } },
                            title = "Sera · Intrigued",
                            moodLine = "Sera studies you sidelong, curious despite herself.",
                            approachDistance = 1.2f, avoidDistance = 0f, moveSpeed = 0.8f, reactRadius = -1f
                        }
                    },
                    interactions = new List<NpcInteractionData>
                    {
                        new NpcInteractionData { id = "talk", label = "Talk to Sera", encounterId = EncounterSera },
                        new NpcInteractionData { id = "show_shard", label = "Show the shard", encounterId = EncounterSeraShard,
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ItemHeld, key = ItemShard } } }
                    },
                    routine = new List<NpcStopData>
                    {
                        new NpcStopData { position = new Point3(16.5f, 0f, 3.2f), dwellSeconds = 2.0f },
                        new NpcStopData { position = new Point3(18.5f, 0f, 2.2f), dwellSeconds = 2.0f }
                    }
                }
            });

            return content;
        }
    }
}
