using System.Collections.Generic;

namespace Crossroads.Narrative
{
    /// <summary>
    /// Authoring helpers + the FIRST story encounter ("The First Light", Fracture Hall).
    /// Content here is the runtime fallback AND the headless-test source; the shipping
    /// content lives in ScriptableObject assets under Assets/_Project/Data/ (generated
    /// from scripts/story_content.json and validated to match this builder).
    ///
    /// GAME_DESIGN hooks:
    ///  - First decision-of-the-game style D1 choice (untimed, §4.1), in the Fracture Hall
    ///    awakening beat (§11.2 P->C1L1), with Mara - the canonical first-decision NPC
    ///    ("first friendship choices (Mara)", §2.2; bond -100..100, §9.1).
    ///  - Choice seeds the run's first affinity (Ember/Tide/Stone §3.2) and sets the hall's
    ///    world state (§5.2 "the city remembers") + persistable entity toggles.
    ///  - Aftermath lines branch per choice (condition-gated nodes) and re-talks differ
    ///    (DecisionNotMade / DecisionWas) - consequences, not cosmetics.
    /// </summary>
    public static class StoryContentBuilder
    {
        public const string EncounterFirstLight = "c1_hall_first_light";
        public const string DecisionFirstLight = "dec_c1_hall_first_light";
        public const string GraphFirstLight = "g_c1_hall_first_light";
        public const string DriveFlag = "c1_hall_drive";
        public const string AreaHall = "hall";

        public static StoryContentData CreateFirstLightContent()
        {
            var content = new StoryContentData();

            // ---------------------------------------------------------------- decision
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
                            new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "ember" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "ember_marker", value = "1" },
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
                            new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "tide" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "tide_marker", value = "1" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "tide_bystanders", value = "1" },
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
                            new DecisionEffectData { type = EffectType.SetWorldState, key = AreaHall, value = "stone" },
                            new DecisionEffectData { type = EffectType.SpawnEntity, key = "stone_marker", value = "1" },
                            new DecisionEffectData { type = EffectType.AddCodex, key = "c1_echo_stone" },
                            new DecisionEffectData { type = EffectType.GrantEchoes, amount = 15 }
                        }
                    }
                }
            });

            // ---------------------------------------------------------------- dialogue graph
            content.graphs.Add(new DialogueGraphData
            {
                id = GraphFirstLight,
                nodes = new List<DialogueNodeData>
                {
                    // entry: first run vs. re-visit (same NPC, different opener - condition-gated)
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

                    // the embedded decision node (choices surface as UI cards)
                    new DialogueNodeData { id = "decide", speaker = "", text = "", decisionId = DecisionFirstLight, branchPrefix = "after" },

                    // condition-gated aftermath variants - the choice's visible consequences
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

            // ---------------------------------------------------------------- encounter
            content.encounters.Add(new EncounterDefinitionData
            {
                id = EncounterFirstLight,
                npcName = "Mara",
                graphId = GraphFirstLight,
                startNodeId = "start"
            });

            return content;
        }
    }
}
