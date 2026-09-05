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
    public static partial class StoryContentBuilder
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
        public const string AreaAnnex = "annex"; // matches the scene gate/trigger id + story_content.json

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

        // objectives / world-interaction phase ----------------------------------
        public const string ObjectiveEmberBeacon = "obj_ember_beacon";
        public const string ObjectiveEmberCache = "obj_ember_cache";
        public const string ObjectiveTideKeepsake = "obj_tide_keepsake";
        public const string ObjectiveTideReport = "obj_tide_report";
        public const string ObjectiveStoneBarricade = "obj_stone_barricade";
        public const string ObjectiveStoneRebuild = "obj_stone_rebuild";

        public const string ItemKeepsake = "twins_keepsake";
        public const string ItemEmberCore = "ember_core";

        public const string FlagBeaconSilenced = "beacon_silenced";
        public const string FlagEmberCacheOpened = "ember_cache_opened";
        public const string FlagKeepsakeFound = "keepsake_found";
        public const string FlagKeepsakeReturned = "keepsake_returned";
        public const string VarBraceCount = "brace_count";
        public const string VarRubbleCount = "rubble_count";
        public const string LocationSeraAnnexGate = "annex_gate";

        public const string EncounterMaraReport = "c1_hall_mara_report";
        public const string GraphMaraReport = "g_c1_hall_mara_report";
        public const string DecisionTideReport = "dec_tide_report";

        // combat phase -----------------------------------------------------------
        public const string ObjectiveWardenHunt = "obj_warden_hunt";
        public const string EnemyChoirWarden = "choir_warden";
        public const string StatusEchoBurn = "echo_burn";
        public const string StatusSuppression = "suppression";
        public const string StatusDodgeGuard = "dodge_guard";
        public const string StatusTideSoothe = "tide_soothe";
        // campaign framework (branching story) ----------------------------------
        public const string EncounterSeraEcho = "camp_sera_echo";
        public const string DecisionSeraEcho = "camp_sera_echo_dec";
        public const string GraphSeraEcho = "g_sera_echo";
        public const string ChapterFirstLight = "ch_first_light";
        public const string ChapterWhispers = "ch_whispers";
        public const string BeatArrival = "beat_arrival";
        public const string FlagPathResolved = "path_resolved";
        public const string FlagChapterOneComplete = "ch1_complete";

        public const string VarWardenDrivenOff = "warden_driven_off";
        public const string VarTimesFelled = "times_felled";
        public const string VarPlayerHp = "player_hp";

        // world expansion (locations) -------------------------------------------
        // Location ids SHARE the area-id namespace (GameState.currentArea/unlockAreas):
        // the scene's AreaTriggers write the same keys, so physical position and the
        // location graph can never desync.
        public const string LocationHall = AreaHall;
        public const string LocationAnnex = AreaAnnex;
        public const string LocationTidewell = AreaTidewell;
        public const string CheckpointHall = "hall_spawn";
        public const string CheckpointAnnex = "annex_spawn";
        public const string CheckpointTidewell = "tidewell_spawn";
        public const string AreaTidewell = "tidewell"; // matches the scene trigger id + story_content.json

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
            content.progression.items.Add(new ItemDefinitionData { id = ItemKeepsake, name = "Twins' Keepsake",
                description = "A stamped tin locket, warm from being held too tight. It belongs to the twins by the east columns." });
            content.progression.items.Add(new ItemDefinitionData { id = ItemEmberCore, name = "Ember Core",
                description = "What the beacon guarded before it forgot your name. It hums with banked heat." });
            content.progression.reputationGroups.AddRange(new List<ReputationGroupData>
            {
                new ReputationGroupData { id = "choir", name = "The Choir" },
                new ReputationGroupData { id = "folk", name = "People of Vessa" },
                new ReputationGroupData { id = "wards", name = "The Wardens" }
            });
            content.progression.areas.AddRange(new List<AreaDefinitionData>
            {
                new AreaDefinitionData { id = AreaHall, name = "Fracture Hall" },
                new AreaDefinitionData { id = AreaAnnex, name = "North Annex" },
                new AreaDefinitionData { id = AreaTidewell, name = "Tidewell Shrine" }
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
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 20 },
                            new DecisionEffectData { type = EffectType.MoveNpc, key = NpcSera, value = AreaTidewell } // she keeps the shrine now
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
                    new DialogueNodeData { id = "confide_line_quiet", speaker = "Mara",
                        text = "The north went quiet an hour ago - the whole hall felt it exhale. Whatever you did up there, it stays done.",
                        nextId = "confide_promise",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagBeaconSilenced, value = "1" } } },
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
                new EncounterDefinitionData { id = EncounterShrine, npcName = "Echo Shrine", graphId = GraphShrine, startNodeId = "start" },
                new EncounterDefinitionData { id = EncounterMaraReport, npcName = "Mara", graphId = GraphMaraReport, startNodeId = "start" }
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
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveTideReport } },
                            title = "Mara · Heartened",
                            moodLine = "Mara stands easier since you told her how it felt.",
                            approachDistance = 1.2f, avoidDistance = -1f, moveSpeed = -1f, reactRadius = -1f
                        },
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
                        new NpcInteractionData { id = "report", label = "Tell her about the twins", encounterId = EncounterMaraReport,
                            conditions = new List<DecisionConditionData>
                            {
                                new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveTideKeepsake },
                                new DecisionConditionData { type = ConditionType.DecisionNotMade, key = DecisionTideReport }
                            } },
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
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveWardenHunt } },
                            title = "Sera · Shieldmate",
                            moodLine = "'You put the Warden down.' Sera looks at you like the hall just changed its mind about you.",
                            approachDistance = 1.3f, avoidDistance = 0f, moveSpeed = 1.0f, reactRadius = -1f
                        },
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveEmberBeacon } },
                            title = "Sera · Vanguard",
                            moodLine = "'The beacon's quiet. First time in days I can hear myself think.' Sera takes her watch by the annex gate.",
                            approachDistance = 1.4f, avoidDistance = 0f, moveSpeed = 1.0f, reactRadius = -1f
                        },
                        new NpcStateData
                        {
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveStoneBarricade } },
                            title = "Sera · Steadied",
                            moodLine = "'It held. You held it.' Sera's shoulders drop an inch.",
                            approachDistance = 1.5f, avoidDistance = 0f, moveSpeed = 1.0f, reactRadius = -1f
                        },
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

            // ================================================================ OBJECTIVE / MISSION SYSTEM
            // Three path objectives (one per First Light decision) + follow-ups, all data.
            // Each demonstrates a different capability:
            //   ember  - ability-gated completion (the beacon only answers ember), follow-up cache
            //   tide   - two-step checklist + NPC-delivered completion via a dialogue decision
            //   stone  - counter progress (0/2), FAILABLE (sealing your echo topples it), recovery follow-up

            content.objectives.AddRange(new List<ObjectiveDefinitionData>
            {
                // ---- EMBER PATH -------------------------------------------------
                new ObjectiveDefinitionData
                {
                    id = ObjectiveEmberBeacon, title = "Silence the Choir Beacon", type = ObjectiveType.Main,
                    areaId = AreaAnnex, description = "The Choir's beacon in the north annex marks everyone the light touched - starting with you. Ember answers heat: make the beacon forget your name.",
                    offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "ember_reach" } },
                    completeConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagBeaconSilenced, value = "1" } },
                    consequences = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.SetWorldState, key = AreaAnnex, value = "quiet" },
                        new DecisionEffectData { type = EffectType.SpawnEntity, key = "ember_cache", value = "1" },
                        new DecisionEffectData { type = EffectType.AddReputation, key = "choir", amount = -10 },
                        new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 5 },
                        new DecisionEffectData { type = EffectType.AddBond, key = "sera", amount = 4 },
                        new DecisionEffectData { type = EffectType.MoveNpc, key = "sera", value = LocationSeraAnnexGate },
                        new DecisionEffectData { type = EffectType.GrantEchoes, amount = 10 },
                        new DecisionEffectData { type = EffectType.AddCodex, key = "c1_beacon_silenced" }
                    },
                    followUps = new List<string> { ObjectiveEmberCache },
                    completionNotice = "Objective complete - the beacon no longer knows your name."
                },
                new ObjectiveDefinitionData
                {
                    id = ObjectiveEmberCache, title = "Claim the Ember Cache", type = ObjectiveType.Side,
                    areaId = AreaAnnex, description = "Where the beacon stood, the hall left a gift for the hand that quieted it. Open the cache.",
                    offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveEmberBeacon } },
                    completeConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagEmberCacheOpened, value = "1" } },
                    consequences = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.AddItem, key = ItemEmberCore },
                        new DecisionEffectData { type = EffectType.GrantEchoes, amount = 15 },
                        new DecisionEffectData { type = EffectType.AddCodex, key = "c1_ember_cache" }
                    },
                    completionNotice = "Objective complete - the hall's gift is yours."
                },

                // ---- TIDE PATH --------------------------------------------------
                new ObjectiveDefinitionData
                {
                    id = ObjectiveTideKeepsake, title = "The Twins' Keepsake", type = ObjectiveType.Main,
                    areaId = AreaHall, giverNpcId = NpcSera, description = "The twins you pulled clear lost their mother's keepsake in the rush. The tide left it glinting by the east columns - find it and bring it back.",
                    offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "tide_clear" } },
                    steps = new List<ObjectiveStepData>
                    {
                        new ObjectiveStepData { text = "Find the keepsake by the east columns",
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagKeepsakeFound, value = "1" } } },
                        new ObjectiveStepData { text = "Bring it back to the twins",
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagKeepsakeReturned, value = "1" } } }
                    },
                    completeConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagKeepsakeReturned, value = "1" } },
                    consequences = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.AddBond, key = "sera", amount = 8 },
                        new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 6 },
                        new DecisionEffectData { type = EffectType.SpawnEntity, key = "tide_bystanders", value = "0" },
                        new DecisionEffectData { type = EffectType.SpawnEntity, key = "tide_calm", value = "1" },
                        new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "twins_blessed" },
                        new DecisionEffectData { type = EffectType.GrantEchoes, amount = 10 },
                        new DecisionEffectData { type = EffectType.AddCodex, key = "c1_twins_keepsake" }
                    },
                    followUps = new List<string> { ObjectiveTideReport },
                    completionNotice = "Objective complete - the twins hold their mother's keepsake again."
                },
                new ObjectiveDefinitionData
                {
                    id = ObjectiveTideReport, title = "Tell Mara What the Light Did", type = ObjectiveType.Side,
                    areaId = AreaHall, giverNpcId = NpcMara, description = "Mara has been watching since the light chose you. She should hear what the twins' gratitude looked like.",
                    offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveTideKeepsake } },
                    completeConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionTideReport, value = "" } },
                    consequences = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 5 },
                        new DecisionEffectData { type = EffectType.AddAffinity, key = "tide", amount = 5 },
                        new DecisionEffectData { type = EffectType.AddCodex, key = "c1_tide_report" }
                    },
                    completionNotice = "Objective complete - Mara knows what the light did."
                },

                // ---- STONE PATH -------------------------------------------------
                new ObjectiveDefinitionData
                {
                    id = ObjectiveStoneBarricade, title = "Steady the North Barricade", type = ObjectiveType.Crisis,
                    areaId = AreaHall, description = "The Choir's next sweep will come through the north passage. The barricade holds twice as long with hands on it - or once, forever, with stillness. Careful: give your echo back to the shrine and the wood remembers gravity.",
                    offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "stone_still" } },
                    counterVar = VarBraceCount, counterTarget = 2, counterText = "Braces set",
                    completeConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.VarAtLeast, key = VarBraceCount, amount = 2 } },
                    failConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = "c1_echo_sealed", value = "1" } },
                    consequences = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.AddReputation, key = "wards", amount = 8 },
                        new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 3 },
                        new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 2 },
                        new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "barricade_held" },
                        new DecisionEffectData { type = EffectType.GrantEchoes, amount = 10 },
                        new DecisionEffectData { type = EffectType.AddCodex, key = "c1_barricade_held" }
                    },
                    failureConsequences = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.SpawnEntity, key = "barricade", value = "0" },
                        new DecisionEffectData { type = EffectType.SpawnEntity, key = "barricade_rubble", value = "1" },
                        new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "barricade_fell" },
                        new DecisionEffectData { type = EffectType.AddReputation, key = "wards", amount = -6 },
                        new DecisionEffectData { type = EffectType.CloseArea, key = AreaAnnex },
                        new DecisionEffectData { type = EffectType.AddCodex, key = "c1_barricade_fell" }
                    },
                    followUps = new List<string> { ObjectiveStoneRebuild },
                    completionNotice = "Objective complete - the north passage will hold.",
                    failureNotice = "Objective failed - you gave your stillness back, and the barricade fell."
                },
                new ObjectiveDefinitionData
                {
                    id = ObjectiveStoneRebuild, title = "Clear the Fallen Barricade", type = ObjectiveType.Recovery,
                    areaId = AreaHall, description = "Stillness was given back, and the wood remembered gravity. Clear the rubble - the passage is needed either way.",
                    offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveFailed, key = ObjectiveStoneBarricade } },
                    counterVar = VarRubbleCount, counterTarget = 2, counterText = "Rubble cleared",
                    completeConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.VarAtLeast, key = VarRubbleCount, amount = 2 } },
                    consequences = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 3 },
                        new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "passage_cleared" },
                        new DecisionEffectData { type = EffectType.ReopenArea, key = AreaAnnex },
                        new DecisionEffectData { type = EffectType.GrantEchoes, amount = 5 }
                    },
                    completionNotice = "Objective complete - the passage is clear again."
                }
            });

            // world interaction unlock registry: what THIS player may touch (persisted,
            // event-synced, path- and ability-dependent)
            content.worldInteractions.AddRange(new List<WorldInteractionData>
            {
                new WorldInteractionData { key = "choir_beacon_channel", label = "Channel ember into the beacon",
                    conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityEmber } } },
                new WorldInteractionData { key = "ember_cache_open", label = "Open the ember cache",
                    conditions = new List<DecisionConditionData> {
                        new DecisionConditionData { type = ConditionType.FlagIs, key = FlagBeaconSilenced, value = "1" },
                        new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityEmber } } }, // hidden: needs the route ability too
                new WorldInteractionData { key = "keepsake_search", label = "Search the crate",
                    conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "tide" } } },
                new WorldInteractionData { key = "keepsake_return", label = "Return the keepsake",
                    conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ItemHeld, key = ItemKeepsake } } },
                new WorldInteractionData { key = "barricade_brace", label = "Brace the barricade",
                    conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = DriveFlag, value = "stone" } } },
                new WorldInteractionData { key = "barricade_wedge", label = "Wedge the line with stillness",
                    conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityStone } } },
                new WorldInteractionData { key = "rubble_clear", label = "Clear the rubble",
                    conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.WorldStateIs, key = AreaHall, value = "barricade_fell" } } }
            });

            // ---------------------------------------------------------------- decision 5 (tide report - completes an objective through dialogue)
            content.decisions.Add(new DecisionNodeData
            {
                id = DecisionTideReport,
                promptText = "Mara waits by the east columns. What do you tell her about the twins?",
                codexEntryId = "c1_tide_report",
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData
                    {
                        id = "tell_all",
                        text = "Everything. The rush, the light, the small hand in yours.",
                        afterText = "Mara listens to all of it, and something in her shoulders lets go.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = "tide_reported", value = "1" },
                            new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 5 },
                            new DecisionEffectData { type = EffectType.AddAffinity, key = "tide", amount = 5 }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "keep_light",
                        text = "That it went fine. Some things should stay theirs.",
                        afterText = "Mara nods once, letting it be - but she catches your sleeve before you go.",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = "tide_reported", value = "1" },
                            new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 2 }
                        }
                    }
                }
            });

            // ---------------------------------------------------------------- graph 7 (Mara report)
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphMaraReport,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "start", speaker = "", text = "", branchPrefix = "report_line" },
                    new DialogueNodeData { id = "report_line_done", speaker = "Mara",
                        text = "You told me. I keep replaying it - the good part. Thank you for that.",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionTideReport, value = "" } } },
                    new DialogueNodeData { id = "report_line_keepsake", speaker = "Mara",
                        text = "You found it? The locket? Ari, they've been asking everyone for a week.",
                        nextId = "report_offer",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.ObjectiveCompleted, key = ObjectiveTideKeepsake } } },
                    new DialogueNodeData { id = "report_line_default", speaker = "Mara",
                        text = "The twins, the light, all of it - I want to hear how you're carrying it.",
                        nextId = "report_offer" },
                    new DialogueNodeData { id = "report_offer", speaker = "", text = "", decisionId = DecisionTideReport, branchPrefix = "report_after" },
                    new DialogueNodeData { id = "report_after_tell", speaker = "Mara",
                        text = "'That's who you are now,' she says. 'Don't lose them to the light.'",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionTideReport, value = "tell_all" } } },
                    new DialogueNodeData { id = "report_after_keep", speaker = "Mara",
                        text = "'Then it's theirs,' she agrees. 'But you're allowed to be proud of it.'",
                        nextId = "end",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionTideReport, value = "keep_light" } } },
                    new DialogueNodeData { id = "end", speaker = "", text = "", end = true }
                }
            });

            // ================================================================ COMBAT SYSTEM CONTENT
            // Damage types/attack shapes/health/defense/statuses/ability attacks/enemy
            // archetypes - all data (Gameplay/Combat runtime reads these rows only).

            // ---- status effects ----
            content.statusEffects.AddRange(new List<StatusEffectDefinitionData>
            {
                new StatusEffectDefinitionData
                {
                    id = StatusEchoBurn, name = "Echo Burn",
                    description = "The ember keeps burning after the pulse - heat gnawing at the Fracture's shell.",
                    durationSeconds = 4f, tickIntervalSeconds = 1f, healthPerTick = -4,
                    moveSpeedMultiplier = 1f, attackRateMultiplier = 1f
                },
                new StatusEffectDefinitionData
                {
                    id = StatusSuppression, name = "Suppression",
                    description = "The Choir's discipline drags at your limbs - everything feels heavier.",
                    durationSeconds = 2.5f, tickIntervalSeconds = 0f, healthPerTick = 0,
                    moveSpeedMultiplier = 0.65f, attackRateMultiplier = 1f
                },
                new StatusEffectDefinitionData
                {
                    id = StatusDodgeGuard, name = "Flowing Aside",
                    description = "You are already somewhere else. Incoming strikes miss.",
                    durationSeconds = 0.45f, tickIntervalSeconds = 0f, healthPerTick = 0,
                    moveSpeedMultiplier = 1f, attackRateMultiplier = 1f, grantsImmunity = true
                },
                new StatusEffectDefinitionData
                {
                    id = StatusTideSoothe, name = "Soothing Tide",
                    description = "The cool wash keeps mending what the fight bruises.",
                    durationSeconds = 3f, tickIntervalSeconds = 1f, healthPerTick = 6,
                    moveSpeedMultiplier = 1f, attackRateMultiplier = 1f
                }
            });

            // ---- ability combat payloads (existing abilities gain combat meaning;
            //      damage/heal scale with the CURRENT level row's power) ----
            content.abilityCombat.AddRange(new List<AbilityCombatData>
            {
                new AbilityCombatData
                {
                    abilityId = AbilityEmber, damageType = DamageType.Ember, damagePerPower = 10f,
                    applyStatusToTargets = new List<string> { StatusEchoBurn }
                },
                new AbilityCombatData
                {
                    abilityId = AbilityTide, damageType = DamageType.Tide, damagePerPower = 3f, healPlayerPerPower = 12f,
                    applyStatusToPlayer = new List<string> { StatusTideSoothe }
                },
                new AbilityCombatData
                {
                    abilityId = AbilityStone, damageType = DamageType.Stone, damagePerPower = 8f,
                    applyStatusToTargets = new List<string> { StatusSuppression }
                }
            });

            // ---- enemy archetypes (ONE prototype: the Choir Warden tracker) ----
            content.enemies.Add(new EnemyDefinitionData
            {
                id = EnemyChoirWarden, displayName = "Choir Warden",
                description = "A tracker-construct of the Choir: tall, patient, humming with hollow light. It marks who the Fracture touched - and collects them.",
                sheetRef = "REF-06",
                maxHealth = 60f, defense = 3f,
                resistances = new List<DamageResistEntry>
                {
                    new DamageResistEntry { type = DamageType.Kinetic, multiplier = 1f },
                    new DamageResistEntry { type = DamageType.Ember, multiplier = 1.25f },  // vulnerable: heat warps its shell
                    new DamageResistEntry { type = DamageType.Tide, multiplier = 0.8f },
                    new DamageResistEntry { type = DamageType.Stone, multiplier = 1f },
                    new DamageResistEntry { type = DamageType.Hollow, multiplier = 0.5f }   // shrugs off its own channel
                },
                moveSpeed = 1.55f, turnSpeed = 5f,
                detectionRadius = 9f, leashRadius = 15f, attackRange = 2.3f, staggerSeconds = 0.35f,
                attack = new AttackDefinitionData
                {
                    id = "warden_smite", name = "Hollow Smite", damageType = DamageType.Hollow,
                    delivery = AttackDelivery.MeleeArc, baseDamage = 12f,
                    range = 2.3f, arcDegrees = 70f, windupSeconds = 0.7f, cooldownSeconds = 2.2f,
                    applyStatusIds = new List<string> { StatusSuppression }
                },
                activationConditions = new List<DecisionConditionData>
                {
                    // the hunt starts because of the player's first decision (story-gated combat)
                    new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "" }
                },
                onDefeatEffects = new List<DecisionEffectData>
                {
                    new DecisionEffectData { type = EffectType.AddVar, key = VarWardenDrivenOff, amount = 1 },
                    new DecisionEffectData { type = EffectType.SpawnEntity, key = "choir_warden", value = "0" },
                    new DecisionEffectData { type = EffectType.SpawnEntity, key = "warden_wreckage", value = "1" },
                    new DecisionEffectData { type = EffectType.GrantEchoes, amount = 15 },
                    new DecisionEffectData { type = EffectType.AddCodex, key = "c1_warden_felled" }
                }
            });

            // ---- player combat settings (health/defense/basic strike/dodge/defeat policy) ----
            content.combat = new CombatSettingsData
            {
                playerMaxHealth = 100f, playerDefense = 2f,
                playerResistances = new List<DamageResistEntry>
                {
                    new DamageResistEntry { type = DamageType.Hollow, multiplier = 0.9f } // Ari carries the echo: it shields a little
                },
                basicAttack = new AttackDefinitionData
                {
                    id = "player_strike", name = "Strike", damageType = DamageType.Kinetic,
                    delivery = AttackDelivery.MeleeArc, baseDamage = 10f,
                    range = 2.8f, arcDegrees = 110f, windupSeconds = 0f, cooldownSeconds = 0.9f
                },
                dodgeDistance = 3.6f, dodgeDurationSeconds = 0.28f, dodgeCooldownSeconds = 1.6f,
                dodgeStatusId = StatusDodgeGuard,
                healthVarKey = VarPlayerHp,
                onPlayerDefeat = new List<DecisionEffectData>
                {
                    // a defeat NEVER destroys the save: it costs a count, worries Mara, and the hall sets you back on your feet
                    new DecisionEffectData { type = EffectType.AddVar, key = VarTimesFelled, amount = 1 },
                    new DecisionEffectData { type = EffectType.AddBond, key = "mara", amount = 1 },
                    new DecisionEffectData { type = EffectType.AddCodex, key = "c1_felled_once" }
                }
            };

            // ---- combat objective: encounter -> fight -> world/NPC state change ----
            content.objectives.Add(new ObjectiveDefinitionData
            {
                id = ObjectiveWardenHunt, title = "Drive Off the Choir Warden", type = ObjectiveType.Crisis,
                areaId = AreaHall, description = "The Choir sent a Warden to collect whoever the Fracture touched. That is you. It waits in the west transept - put it down before it reports.",
                offerConditions = new List<DecisionConditionData>
                {
                    new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "" }
                },
                completeConditions = new List<DecisionConditionData>
                {
                    new DecisionConditionData { type = ConditionType.VarAtLeast, key = VarWardenDrivenOff, amount = 1 }
                },
                consequences = new List<DecisionEffectData>
                {
                    new DecisionEffectData { type = EffectType.AddReputation, key = "choir", amount = -5 },
                    new DecisionEffectData { type = EffectType.AddReputation, key = "folk", amount = 4 },
                    new DecisionEffectData { type = EffectType.AddBond, key = "sera", amount = 5 },
                    new DecisionEffectData { type = EffectType.GrantEchoes, amount = 10 },
                    new DecisionEffectData { type = EffectType.AddCodex, key = "c1_warden_driven_off" }
                },
                completionNotice = "Objective complete - the Warden will not report you. Sera saw all of it."
            });

            // ================================================================ CAMPAIGN FRAMEWORK CONTENT
            // Branching story: chapters -> beats -> branches, all data (Gameplay/Campaign
            // only re-evaluates conditions; a designer adds chapters through content).

            // ---- second encounter: Sera reacts to the FIRST decision (branch dialogue) ----
            content.decisions.Add(new DecisionNodeData
            {
                id = DecisionSeraEcho,
                promptText = "Sera saw which way the light took you. What do you tell her?",
                options = new List<DecisionOptionData>
                {
                    new DecisionOptionData
                    {
                        id = "tell_her", text = "Tell her the truth of it.",
                        afterText = "Sera nods once, slow. 'Then we hold the line together.'",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = "sera_echo_seen", value = "1" },
                            new DecisionEffectData { type = EffectType.AddBond, key = "sera", amount = 2 }
                        }
                    },
                    new DecisionOptionData
                    {
                        id = "deflect", text = "Keep it behind your teeth.",
                        afterText = "'Fair,' Sera says. 'Keep it that way until it's ready.'",
                        effects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = "sera_echo_seen", value = "1" }
                        }
                    }
                }
            });

            content.graphs.Add(new DialogueGraphData
            {
                id = GraphSeraEcho,
                nodes = new List<DialogueNodeData>
                {
                    new DialogueNodeData { id = "n_se1", speaker = "Sera",
                        text = "You came back with the whole hall behind your eyes. Come on - out with it.",
                        nextId = "n_se_pick" },
                    new DialogueNodeData { id = "n_se_pick", branchPrefix = "se_line" },
                    new DialogueNodeData { id = "se_line", speaker = "Sera",
                        text = "Something changed in you out there. I can hear it in your steps." },
                    new DialogueNodeData { id = "se_line_ember", speaker = "Sera",
                        text = "Ember still hums under your nails. The beacon forgot your name - but the Choir sent a Warden to check on you. That is not finished.",
                        nextId = "n_se_dec",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "ember_reach" } } },
                    new DialogueNodeData { id = "se_line_tide", speaker = "Sera",
                        text = "You went into the water for the twins and came out carrying their peace. The tide leaves marks like that.",
                        nextId = "n_se_dec",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "tide_clear" } } },
                    new DialogueNodeData { id = "se_line_stone", speaker = "Sera",
                        text = "You held the line with your shoulders. Stone remembers hands that stay.",
                        nextId = "n_se_dec",
                        conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "stone_still" } } },
                    new DialogueNodeData { id = "n_se_dec", decisionId = DecisionSeraEcho }
                }
            });

            content.encounters.Add(new EncounterDefinitionData
            {
                id = EncounterSeraEcho, npcName = "Sera", graphId = GraphSeraEcho, startNodeId = "n_se1"
            });

            // sera's post-decision DEFAULT interaction (first in her list; condition-gated)
            NpcDefinitionData seraNpc = content.FindNpc("sera");
            if (seraNpc != null && seraNpc.interactions != null)
            {
                seraNpc.interactions.Insert(0, new NpcInteractionData
                {
                    id = "talk_echo", label = "Talk about what happened", encounterId = EncounterSeraEcho,
                    conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "" } }
                });
            }

            // ---- Chapter One: The First Light (the branching vertical slice) ----
            content.chapters.Add(new CampaignChapterData
            {
                id = ChapterFirstLight, title = "Chapter One", subtitle = "The First Light",
                description = "The Fracture Hall takes Ari in, asks its first question, and the answer routes everything after it.",
                beats = new List<StoryBeatData>
                {
                    new StoryBeatData
                    {
                        id = BeatArrival, title = "Answer the hall's first question",
                        journalText = "The Fracture Hall took you in - and asked its first question.",
                        resolveTrigger = BeatTrigger.DecisionMade, resolveKey = DecisionFirstLight, priority = 0
                    },
                    new StoryBeatData
                    {
                        id = "beat_sera_echo", title = "Tell Sera what the light left",
                        journalText = "You told Sera what the light left in you. She did not flinch.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "" } },
                        resolveTrigger = BeatTrigger.DecisionMade, resolveKey = DecisionSeraEcho,
                        requiredBeatIds = new List<string> { BeatArrival }, priority = 4
                    },
                    new StoryBeatData
                    {
                        id = "beat_warden", title = "Drive off the Choir Warden",
                        journalText = "The Warden will not report you. Sera saw all of it.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "" } },
                        resolveTrigger = BeatTrigger.ObjectiveCompleted, resolveKey = ObjectiveWardenHunt,
                        requiredBeatIds = new List<string> { BeatArrival }, priority = 5
                    },
                    new StoryBeatData
                    {
                        id = "beat_sera_confide", title = "Sera's waystation key",
                        journalText = "Sera trusts you enough to show the waystation key.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.BondAtLeast, key = "sera", amount = 7 } },
                        resolveTrigger = BeatTrigger.Conditions,
                        requiredBeatIds = new List<string> { BeatArrival },
                        onResolveEffects = new List<DecisionEffectData>
                        {
                            new DecisionEffectData { type = EffectType.SetFlag, key = "waystation_key", value = "1" },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 10 }
                        },
                        priority = 6
                    },
                    new StoryBeatData
                    {
                        id = "beat_ember_mastery", title = "The ember answers faster",
                        journalText = "The ember answers faster now. It wants a second door.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityEmber } },
                        resolveTrigger = BeatTrigger.Conditions,
                        requiredBeatIds = new List<string> { BeatArrival },
                        onResolveEffects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "ember_mastery", value = "1" } },
                        priority = 7
                    },
                    new StoryBeatData
                    {
                        id = "beat_ember_path", title = "Silence the Choir Beacon",
                        journalText = "The beacon is quiet. The annex belongs to the hall again.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = "path_ember", value = "1" } },
                        resolveTrigger = BeatTrigger.ObjectiveCompleted, resolveKey = ObjectiveEmberBeacon,
                        requiredBeatIds = new List<string> { BeatArrival }, priority = 10
                    },
                    new StoryBeatData
                    {
                        id = "beat_tide_path", title = "Carry the twins' peace back",
                        journalText = "The twins have their locket back, and Mara knows the truth of the rush.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = "path_tide", value = "1" } },
                        resolveTrigger = BeatTrigger.ObjectiveCompleted, resolveKey = "obj_tide_report",
                        requiredBeatIds = new List<string> { BeatArrival }, priority = 10
                    },
                    new StoryBeatData
                    {
                        id = "beat_stone_path", title = "Brace the north line",
                        journalText = "The barricade held. The north line is yours.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = "path_stone", value = "1" } },
                        resolveTrigger = BeatTrigger.ObjectiveCompleted, resolveKey = ObjectiveStoneBarricade,
                        requiredBeatIds = new List<string> { BeatArrival }, priority = 10
                    },
                    new StoryBeatData
                    {
                        id = "beat_stone_fell", title = "The line fell",
                        journalText = "The barricade fell. The hall breathed dust - and kept breathing.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = "path_stone", value = "1" } },
                        resolveTrigger = BeatTrigger.ObjectiveFailed, resolveKey = ObjectiveStoneBarricade,
                        requiredBeatIds = new List<string> { BeatArrival }, priority = 10
                    },
                    new StoryBeatData
                    {
                        id = "beat_recovery", title = "Haul the line back up",
                        journalText = "You hauled the line back up with your own hands.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = "path_fell", value = "1" } },
                        resolveTrigger = BeatTrigger.ObjectiveCompleted, resolveKey = ObjectiveStoneRebuild,
                        requiredBeatIds = new List<string> { "beat_stone_fell" }, priority = 11
                    },
                    new StoryBeatData
                    {
                        id = "beat_council", title = "The hall exhales",
                        journalText = "The hall held its breath - and let it out. Chapter One closes.",
                        offerConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagPathResolved, value = "1" } },
                        resolveTrigger = BeatTrigger.Conditions,
                        requiredBeatIds = new List<string> { BeatArrival }, priority = 20
                    }
                },
                branches = new List<CampaignBranchData>
                {
                    new CampaignBranchData { id = "br_trode_ember", fromBeatId = BeatArrival, toBeatId = "beat_ember_path",
                        label = "Path of Ember",
                        requiredConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "ember_reach" } },
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "path_ember", value = "1" } } },
                    new CampaignBranchData { id = "br_trode_tide", fromBeatId = BeatArrival, toBeatId = "beat_tide_path",
                        label = "Path of Tide",
                        requiredConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "tide_clear" } },
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "path_tide", value = "1" } } },
                    new CampaignBranchData { id = "br_trode_stone", fromBeatId = BeatArrival, toBeatId = "beat_stone_path",
                        label = "Path of Stone",
                        requiredConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "stone_still" } },
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "path_stone", value = "1" } } },
                    new CampaignBranchData { id = "br_ember_settled", fromBeatId = "beat_ember_path", toBeatId = "beat_council",
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = FlagPathResolved, value = "1" } } },
                    new CampaignBranchData { id = "br_tide_settled", fromBeatId = "beat_tide_path", toBeatId = "beat_council",
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = FlagPathResolved, value = "1" } } },
                    new CampaignBranchData { id = "br_stone_settled", fromBeatId = "beat_stone_path", toBeatId = "beat_council",
                        label = "The Line Held",
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = FlagPathResolved, value = "1" } } },
                    new CampaignBranchData { id = "br_line_fell", fromBeatId = "beat_stone_fell", toBeatId = "beat_recovery",
                        label = "The Line Fell",
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "path_fell", value = "1" } } },
                    new CampaignBranchData { id = "br_line_reheld", fromBeatId = "beat_recovery", toBeatId = "beat_council",
                        label = "The Line Held Again",
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = FlagPathResolved, value = "1" } } },
                    new CampaignBranchData { id = "br_told_sera", fromBeatId = "beat_sera_echo",
                        label = "Sera Holds the Line With You",
                        requiredConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionSeraEcho, value = "tell_her" } } },
                    new CampaignBranchData { id = "br_deflected", fromBeatId = "beat_sera_echo",
                        label = "Some Doors Stay Shut",
                        requiredConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionSeraEcho, value = "deflect" } } },
                    new CampaignBranchData { id = "br_second_door", fromBeatId = "beat_ember_mastery",
                        label = "The Ember Widens",
                        effects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "ember_second_door", value = "1" } } }
                },
                completionConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagPathResolved, value = "1" } },
                completionEffects = new List<DecisionEffectData>
                {
                    new DecisionEffectData { type = EffectType.SetFlag, key = FlagChapterOneComplete, value = "1" },
                    new DecisionEffectData { type = EffectType.AddCodex, key = "c1_ch1_complete" }
                },
                completionJournal = "Chapter One: The First Light - complete."
            });

            // ---- Chapter Two teaser: chapters chain through content data alone ----
            content.chapters.Add(new CampaignChapterData
            {
                id = ChapterWhispers, title = "Chapter Two", subtitle = "Whispers Under the Hall",
                description = "Teaser beat: the framework's proof that a designer adds the next chapter as pure data.",
                entryConditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.FlagIs, key = FlagChapterOneComplete, value = "1" } },
                beats = new List<StoryBeatData>
                {
                    new StoryBeatData
                    {
                        id = "beat_whispers", title = "Something knows your name",
                        journalText = "Somewhere under the hall, something whispers your new name.",
                        resolveTrigger = BeatTrigger.Conditions,
                        onResolveEffects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.SetFlag, key = "ch2_teaser", value = "1" } }
                    }
                },
                completionEffects = new List<DecisionEffectData> { new DecisionEffectData { type = EffectType.AddCodex, key = "c1_ch2_teaser" } },
                completionJournal = "To be continued."
            });

            // ---------------------------------------------------------------- world expansion (locations)
            // Three prototype locations, all data: the Fracture Hall hub (story/exploration),
            // the North Annex (combat challenge - gated by any route ability, like the scene's
            // energy seal) and the Tidewell Shrine (NPC focus - gated by the tide decision,
            // Sera relocates there). LocationManager only evaluates this data.
            content.locations.AddRange(new List<LocationDefinitionData>
            {
                new LocationDefinitionData
                {
                    id = LocationHall, name = "Fracture Hall", kind = (int)LocationKind.Hub,
                    sceneKey = "FirstLocation", checkpointId = CheckpointHall,
                    description = "The great central hall where the Trode asks its question. Story trunk, camp, the way to everywhere else.",
                    unlockRules = new List<GateRuleData>(), lockedHint = "",
                    entryConditions = new List<DecisionConditionData>(),
                    connections = new List<string> { LocationAnnex, LocationTidewell },
                    npcs = new List<string> { NpcMara, NpcSera },
                    encounters = new List<string> { EncounterFirstLight, EncounterSera, EncounterMaraConfide, EncounterSeraEcho },
                    objectives = new List<string>(),
                    worldStateChanges = new List<DecisionEffectData>(),
                    environment = new LocationEnvironmentData { profile = "hall_dawn", ambient = "3a4450", fog = "2b333d", fogDensity = 0.015f, sun = "cfe6f2", sunIntensity = 1.05f }
                },
                new LocationDefinitionData
                {
                    id = LocationAnnex, name = "North Annex", kind = (int)LocationKind.Combat,
                    sceneKey = "FirstLocation", checkpointId = CheckpointAnnex,
                    description = "Beyond the energy seal: the choir beacon, the Warden the Choir sent for it, and a cache that only answers ember.",
                    unlockRules = new List<GateRuleData>
                    {
                        new GateRuleData { opens = true, text = "The seal drinks the echo and parts. The North Annex lies open.",
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityEmber } } },
                        new GateRuleData { opens = true, text = "The seal softens like water around your hand. The North Annex lies open.",
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityTide } } },
                        new GateRuleData { opens = true, text = "The seal holds, then yields - unhurried, the way you asked it. The North Annex lies open.",
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.AbilityOwned, key = AbilityStone } } }
                    },
                    lockedHint = "A seal of the hall's own light. It only parts for an echoed voice.",
                    entryConditions = new List<DecisionConditionData>(),
                    connections = new List<string> { LocationHall },
                    npcs = new List<string>(),
                    encounters = new List<string> { EncounterShard, EncounterShrine },
                    objectives = new List<string> { ObjectiveEmberBeacon, ObjectiveEmberCache, ObjectiveWardenHunt },
                    worldStateChanges = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.SetWorldState, key = AreaAnnex, value = "reached" }
                    },
                    environment = new LocationEnvironmentData { profile = "ember_low", ambient = "46372e", fog = "31241d", fogDensity = 0.03f, sun = "ffb27a", sunIntensity = 0.85f }
                },
                new LocationDefinitionData
                {
                    id = LocationTidewell, name = "Tidewell Shrine", kind = (int)LocationKind.Npc,
                    sceneKey = "FirstLocation", checkpointId = CheckpointTidewell,
                    description = "A drowned shrine east of the hall. Sera keeps its lamp now, and the water remembers what you carried out of it.",
                    unlockRules = new List<GateRuleData>
                    {
                        new GateRuleData { opens = true, text = "The trapped water in the east passage recedes around your feet. The Tidewell Shrine lies open.",
                            conditions = new List<DecisionConditionData> { new DecisionConditionData { type = ConditionType.DecisionWas, key = DecisionFirstLight, value = "tide_clear" } } }
                    },
                    lockedHint = "The east passage hisses with trapped water. It answers only one who has already answered the tide.",
                    entryConditions = new List<DecisionConditionData>(),
                    connections = new List<string> { LocationHall },
                    npcs = new List<string> { NpcSera },
                    encounters = new List<string> { EncounterMaraReport, EncounterSeraEcho },
                    objectives = new List<string> { ObjectiveTideKeepsake, ObjectiveTideReport },
                    worldStateChanges = new List<DecisionEffectData>
                    {
                        new DecisionEffectData { type = EffectType.SetWorldState, key = AreaTidewell, value = "lit" }
                    },
                    environment = new LocationEnvironmentData { profile = "tide_glass", ambient = "2e4a52", fog = "22383f", fogDensity = 0.045f, sun = "bfeaf2", sunIntensity = 0.9f }
                }
            });

            // ---------------------------------------------------------------- campaign content pass (generated mirror)
            AppendCampaignContent(content);

            return content;
        }
    }
}
