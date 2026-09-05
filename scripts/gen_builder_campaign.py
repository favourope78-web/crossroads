#!/usr/bin/env python3
"""Generates the C# mirror of the campaign content pass from the canonical JSON.

  scripts/story_content.json + scripts/campaign_pass_manifest.json
      -> Assets/_Project/Scripts/Narrative/Content/StoryContentBuilder.Campaign.cs

StoryContentBuilder is a partial class: CreateFirstLightContent() (hand-written, chapter one)
calls AppendCampaignContent(content), which this file generates. The runtime fallback
(RuntimeContentSource) and the headless tests therefore see exactly the JSON's content, and
scripts/validate_assets.py §3 keeps checking every JSON string against the builder sources.
Re-run after any change to the JSON records listed in the manifest.
"""
import json, os

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
C = json.load(open(os.path.join(HERE, "story_content.json")))
M = json.load(open(os.path.join(HERE, "campaign_pass_manifest.json")))
OUT = os.path.join(ROOT, "Assets/_Project/Scripts/Narrative/Content/StoryContentBuilder.Campaign.cs")

COND = ["FlagIs", "FlagIsNot", "FlagMissing", "VarAtLeast", "AffinityAtLeast", "BondAtLeast", "DecisionWas", "DecisionNotMade",
        "CodexOwned", "ReputationAtLeast", "ItemHeld", "AbilityOwned", "AreaUnlocked", "SkillAtLeast", "EchoesAtLeast",
        "AbilityLevelBelow", "ObjectiveActive", "ObjectiveCompleted", "ObjectiveFailed", "WorldStateIs"]
EFF = ["SetFlag", "ClearFlag", "AddAffinity", "SetAffinity", "AddBond", "SetVar", "AddVar", "SetWorldState", "SpawnEntity", "AddCodex",
       "GrantEchoes", "AddReputation", "SetReputation", "UnlockAbility", "AddSkillLevel", "AddItem", "RemoveItem", "UnlockArea",
       "UpgradeAbility", "BlockAbility", "MoveNpc", "CloseArea", "ReopenArea", "UnlockInteraction"]
PERSONALITY = ["Reserved", "Friendly", "Wary", "Curious"]
DAMAGE = ["Kinetic", "Ember", "Tide", "Stone", "Hollow"]
DELIVERY = ["MeleeArc", "RadiusPulse"]
TRIGGER = ["Conditions", "DecisionMade", "ObjectiveCompleted", "ObjectiveFailed"]
OBJTYPE = ["Main", "Side", "Crisis", "Recovery"]
CATEGORY = ["Active", "Passive", "Utility"]

def S(s):
    """C# string literal whose source text contains the JSON string verbatim (ASCII) or
    with \\uXXXX escapes (non-ASCII) - exactly what validate_assets.py §3 looks for."""
    out = []
    for ch in s:
        o = ord(ch)
        if ch == '"': out.append('\\"')
        elif ch == "\\": out.append("\\\\")
        elif ch == "\n": out.append("\\n")
        elif o > 126: out.append("\\u%04x" % o)
        else: out.append(ch)
    return '"' + "".join(out) + '"'

def F(v):
    v = float(v)
    if v == int(v): return "%df" % int(v)
    return ("%.4f" % v).rstrip("0").rstrip(".") + "f"

def B(v): return "true" if v else "false"
def I(v): return str(int(v))

def conds(lst):
    if not lst: return "N()"
    return "L(" + ", ".join("Cd(ConditionType.%s, %s, %s, %s)" % (COND[c["type"]], S(c["key"]), S(c["value"]), I(c["amount"])) for c in lst) + ")"

def effs(lst):
    if not lst: return "E()"
    return "L(" + ", ".join("Ef(EffectType.%s, %s, %s, %s)" % (EFF[e["type"]], S(e["key"]), S(e["value"]), I(e["amount"])) for e in lst) + ")"

def strs(lst):
    if not lst: return "new List<string>()"
    return "new List<string> { " + ", ".join(S(x) for x in lst) + " }"

def decision(d):
    opts = ",\n".join("                    new DecisionOptionData { id = %s, text = %s, afterText = %s, conditions = %s, effects = %s }"
                      % (S(o["id"]), S(o["text"]), S(o["afterText"]), conds(o["conditions"]), effs(o["effects"])) for o in d["options"])
    return ("            content.decisions.Add(new DecisionNodeData\n            {\n                id = %s, promptText = %s, timeLimitSeconds = %s, timeoutOptionIndex = %s, codexEntryId = %s,\n"
            "                options = new List<DecisionOptionData>\n                {\n%s\n                }\n            });"
            % (S(d["id"]), S(d["promptText"]), F(d["timeLimitSeconds"]), I(d["timeoutOptionIndex"]), S(d["codexEntryId"]), opts))

def graph(g):
    nodes = ",\n".join("                    new DialogueNodeData { id = %s, speaker = %s, text = %s, nextId = %s, branchPrefix = %s, decisionId = %s, conditions = %s, end = %s }"
                       % (S(n["id"]), S(n["speaker"]), S(n["text"]), S(n["nextId"]), S(n["branchPrefix"]), S(n["decisionId"]), conds(n["conditions"]), B(n["end"]))
                       for n in g["nodes"])
    return ("            content.graphs.Add(new DialogueGraphData\n            {\n                id = %s,\n                nodes = new List<DialogueNodeData>\n                {\n%s\n                }\n            });"
            % (S(g["id"]), nodes))

def encounter(e):
    return "            content.encounters.Add(new EncounterDefinitionData { id = %s, npcName = %s, graphId = %s, startNodeId = %s });" % (
        S(e["id"]), S(e["npcName"]), S(e["graphId"]), S(e["startNodeId"]))

def npc(n):
    b = n["behaviour"]
    states = ",\n".join("                    new NpcStateData { conditions = %s, title = %s, moodLine = %s, approachDistance = %s, avoidDistance = %s, moveSpeed = %s, reactRadius = %s }"
                        % (conds(s["conditions"]), S(s["title"]), S(s["moodLine"]), F(s["approachDistance"]), F(s["avoidDistance"]), F(s["moveSpeed"]), F(s["reactRadius"]))
                        for s in n["states"])
    inter = ",\n".join("                    new NpcInteractionData { id = %s, label = %s, encounterId = %s, conditions = %s }"
                       % (S(i["id"]), S(i["label"]), S(i["encounterId"]), conds(i["conditions"])) for i in n["interactions"])
    routine = ", ".join("new NpcStopData { position = new Point3(%s, %s, %s), dwellSeconds = %s }"
                        % (F(r["position"]["x"]), F(r["position"]["y"]), F(r["position"]["z"]), F(r["dwellSeconds"])) for r in n["routine"])
    return ("            content.npcs.Add(new NpcDefinitionData\n            {\n                id = %s, displayName = %s, sheetRef = %s,\n                description = %s,\n"
            "                behaviour = new NpcBehaviourData { personality = NpcPersonality.%s, facesPlayer = %s, reactRadius = %s, approachDistance = %s, avoidDistance = %s, talkDistance = %s, moveSpeed = %s, turnSpeed = %s, usesRoutine = %s },\n"
            "                states = new List<NpcStateData>\n                {\n%s\n                },\n"
            "                interactions = new List<NpcInteractionData>\n                {\n%s\n                },\n"
            "                routine = new List<NpcStopData> { %s }\n            });"
            % (S(n["id"]), S(n["displayName"]), S(n["sheetRef"]), S(n["description"]), PERSONALITY[int(b["personality"])], B(b["facesPlayer"]), F(b["reactRadius"]),
               F(b["approachDistance"]), F(b["avoidDistance"]), F(b["talkDistance"]), F(b["moveSpeed"]), F(b["turnSpeed"]), B(b["usesRoutine"]), states, inter, routine))

def objective(o):
    steps = ", ".join("new ObjectiveStepData { text = %s, conditions = %s }" % (S(s["text"]), conds(s["conditions"])) for s in o["steps"])
    return ("            content.objectives.Add(new ObjectiveDefinitionData\n            {\n                id = %s, title = %s,\n                description = %s,\n"
            "                type = ObjectiveType.%s, areaId = %s, giverNpcId = %s, offerConditions = %s, autoActivate = %s,\n"
            "                completeConditions = %s, failConditions = %s,\n                counterVar = %s, counterTarget = %s, counterText = %s,\n"
            "                steps = new List<ObjectiveStepData> { %s },\n                consequences = %s,\n                failureConsequences = %s,\n"
            "                followUps = %s, completionNotice = %s, failureNotice = %s\n            });"
            % (S(o["id"]), S(o["title"]), S(o["description"]), OBJTYPE[int(o["type"])], S(o["areaId"]), S(o["giverNpcId"]), conds(o["offerConditions"]), B(o["autoActivate"]),
               conds(o["completeConditions"]), conds(o["failConditions"]), S(o["counterVar"]), I(o["counterTarget"]), S(o["counterText"]), steps,
               effs(o["consequences"]), effs(o["failureConsequences"]), strs(o["followUps"]), S(o["completionNotice"]), S(o["failureNotice"])))

def world_interaction(w):
    return "            content.worldInteractions.Add(new WorldInteractionData { key = %s, label = %s, conditions = %s });" % (S(w["key"]), S(w["label"]), conds(w["conditions"]))

def status(s):
    return ("            content.statusEffects.Add(new StatusEffectDefinitionData { id = %s, name = %s, description = %s, durationSeconds = %s, tickIntervalSeconds = %s, healthPerTick = %s, moveSpeedMultiplier = %s, attackRateMultiplier = %s, grantsImmunity = %s });"
            % (S(s["id"]), S(s["name"]), S(s["description"]), F(s["durationSeconds"]), F(s["tickIntervalSeconds"]), I(s["healthPerTick"]), F(s["moveSpeedMultiplier"]), F(s["attackRateMultiplier"]), B(s["grantsImmunity"])))

def ability(a):
    rows = ",\n".join("                    new AbilityLevelData { level = %s, cooldown = %s, power = %s, radius = %s, duration = %s, energyCost = %s, description = %s }"
                      % (I(r["level"]), F(r["cooldown"]), F(r["power"]), F(r["radius"]), F(r["duration"]), I(r["energyCost"]), S(r["description"])) for r in a["levels"])
    return ("            content.progression.abilities.Add(new AbilityDefinitionData\n            {\n                id = %s, name = %s, line = %s, category = AbilityCategory.%s,\n"
            "                description = %s,\n                unlockHint = %s,\n                unlockConditions = %s,\n"
            "                vfxRef = %s, sfxRef = %s, echoCostPerLevel = %s,\n                levels = new List<AbilityLevelData>\n                {\n%s\n                }\n            });"
            % (S(a["id"]), S(a["name"]), S(a["line"]), CATEGORY[int(a["category"])], S(a["description"]), S(a["unlockHint"]), conds(a["unlockConditions"]),
               S(a["vfxRef"]), S(a["sfxRef"]), I(a["echoCostPerLevel"]), rows))

def ability_combat(a):
    return ("            content.abilityCombat.Add(new AbilityCombatData { abilityId = %s, damageType = DamageType.%s, damagePerPower = %s, healPlayerPerPower = %s, applyStatusToTargets = %s, applyStatusToPlayer = %s });"
            % (S(a["abilityId"]), DAMAGE[int(a["damageType"])], F(a["damagePerPower"]), F(a["healPlayerPerPower"]), strs(a["applyStatusToTargets"]), strs(a["applyStatusToPlayer"])))

def attack(a):
    return ("new AttackDefinitionData { id = %s, name = %s, damageType = DamageType.%s, delivery = AttackDelivery.%s, baseDamage = %s, range = %s, arcDegrees = %s, radius = %s, windupSeconds = %s, cooldownSeconds = %s, applyStatusIds = %s }"
            % (S(a["id"]), S(a["name"]), DAMAGE[int(a["damageType"])], DELIVERY[int(a["delivery"])], F(a["baseDamage"]), F(a["range"]), F(a["arcDegrees"]), F(a["radius"]), F(a["windupSeconds"]), F(a["cooldownSeconds"]), strs(a["applyStatusIds"])))

def enemy(e):
    res = ", ".join("new DamageResistEntry { type = DamageType.%s, multiplier = %s }" % (DAMAGE[int(r["type"])], F(r["multiplier"])) for r in e["resistances"])
    return ("            content.enemies.Add(new EnemyDefinitionData\n            {\n                id = %s, displayName = %s, sheetRef = %s,\n                description = %s,\n"
            "                maxHealth = %s, defense = %s, resistances = new List<DamageResistEntry> { %s },\n"
            "                moveSpeed = %s, turnSpeed = %s, detectionRadius = %s, leashRadius = %s, attackRange = %s, staggerSeconds = %s,\n"
            "                attack = %s,\n                activationConditions = %s,\n                onDefeatEffects = %s\n            });"
            % (S(e["id"]), S(e["displayName"]), S(e["sheetRef"]), S(e["description"]), F(e["maxHealth"]), F(e["defense"]), res, F(e["moveSpeed"]), F(e["turnSpeed"]),
               F(e["detectionRadius"]), F(e["leashRadius"]), F(e["attackRange"]), F(e["staggerSeconds"]), attack(e["attack"]), conds(e["activationConditions"]), effs(e["onDefeatEffects"])))

def beat(b):
    return ("                    new StoryBeatData { id = %s, title = %s, journalText = %s, offerConditions = %s, resolveTrigger = BeatTrigger.%s, resolveKey = %s, resolveConditions = %s, requiredBeatIds = %s, onResolveEffects = %s, priority = %s }"
            % (S(b["id"]), S(b["title"]), S(b["journalText"]), conds(b["offerConditions"]), TRIGGER[int(b["resolveTrigger"])], S(b["resolveKey"]), conds(b["resolveConditions"]), strs(b["requiredBeatIds"]), effs(b["onResolveEffects"]), I(b["priority"])))

def branch(br):
    return ("                    new CampaignBranchData { id = %s, fromBeatId = %s, toBeatId = %s, label = %s, requiredConditions = %s, effects = %s }"
            % (S(br["id"]), S(br["fromBeatId"]), S(br["toBeatId"]), S(br["label"]), conds(br["requiredConditions"]), effs(br["effects"])))

def chapter(ch):
    return ("            content.chapters.Add(new CampaignChapterData\n            {\n                id = %s, title = %s, subtitle = %s,\n                description = %s,\n                entryConditions = %s,\n"
            "                beats = new List<StoryBeatData>\n                {\n%s\n                },\n                branches = new List<CampaignBranchData>\n                {\n%s\n                },\n"
            "                completionConditions = %s, completionEffects = %s,\n                completionJournal = %s\n            });"
            % (S(ch["id"]), S(ch["title"]), S(ch["subtitle"]), S(ch["description"]), conds(ch["entryConditions"]), ",\n".join(beat(b) for b in ch["beats"]),
               ",\n".join(branch(b) for b in ch["branches"]), conds(ch["completionConditions"]), effs(ch["completionEffects"]), S(ch["completionJournal"])))

def location(l):
    rules = ", ".join("new GateRuleData { conditions = %s, opens = %s, text = %s }" % (conds(r["conditions"]), B(r["opens"]), S(r["text"])) for r in l["unlockRules"])
    env = l["environment"]
    return ("            content.locations.Add(new LocationDefinitionData\n            {\n                id = %s, name = %s, kind = %s, sceneKey = %s, checkpointId = %s,\n                description = %s,\n"
            "                unlockRules = new List<GateRuleData> { %s },\n                lockedHint = %s, entryConditions = %s,\n"
            "                connections = %s, npcs = %s,\n                encounters = %s,\n                objectives = %s,\n                worldStateChanges = %s,\n"
            "                environment = new LocationEnvironmentData { profile = %s, ambient = %s, fog = %s, fogDensity = %s, sun = %s, sunIntensity = %s }\n            });"
            % (S(l["id"]), S(l["name"]), I(l["kind"]), S(l["sceneKey"]), S(l["checkpointId"]), S(l["description"]), rules, S(l["lockedHint"]), conds(l["entryConditions"]),
               strs(l["connections"]), strs(l["npcs"]), strs(l["encounters"]), strs(l["objectives"]), effs(l["worldStateChanges"]),
               S(env["profile"]), S(env["ambient"]), S(env["fog"]), F(env["fogDensity"]), S(env["sun"]), F(env["sunIntensity"])))

def pick(key, ids, sub=None):
    src = C["progression"][key] if sub else C[key]
    idk = "abilityId" if key == "abilityCombat" else ("key" if key == "worldInteractions" else "id")
    return [x for x in src if x[idk] in ids]

sections = []
def section(name, body_lines):
    sections.append((name, body_lines))

section("Progression", ["            content.progression.areas.Add(new AreaDefinitionData { id = %s, name = %s });" % (S(a["id"]), S(a["name"])) for a in pick("areas", M["areas"], True)]
        + ["            content.progression.items.Add(new ItemDefinitionData { id = %s, name = %s, description = %s });" % (S(i["id"]), S(i["name"]), S(i["description"])) for i in pick("items", M["items"], True)]
        + [status(s) for s in pick("statusEffects", M["statusEffects"])]
        + [ability(a) for a in pick("abilities", M["abilities"], True)]
        + [ability_combat(a) for a in pick("abilityCombat", M["abilityCombat"])])
section("Enemies", [enemy(e) for e in pick("enemies", M["enemies"])])
section("Npcs", [npc(n) for n in pick("npcs", M["npcs"])])
dec_list = pick("decisions", M["decisions"])
half = (len(dec_list) + 1) // 2
section("DecisionsA", [decision(d) for d in dec_list[:half]])
section("DecisionsB", [decision(d) for d in dec_list[half:]])
graphs = pick("graphs", M["graphs"])
third = (len(graphs) + 2) // 3
section("GraphsA", [graph(g) for g in graphs[:third]])
section("GraphsB", [graph(g) for g in graphs[third:2 * third]])
section("GraphsC", [graph(g) for g in graphs[2 * third:]])
section("Encounters", [encounter(e) for e in pick("encounters", M["encounters"])] + [world_interaction(w) for w in pick("worldInteractions", M["worldInteractions"])])
section("Objectives", [objective(o) for o in pick("objectives", M["objectives"])])
section("Chapters", [chapter(ch) for ch in pick("chapters", M["chapters"])])
section("Locations", [location(l) for l in pick("locations", M["locations"])])

# the hall hub's extra connections (edited in place by the merge script)
hall = [l for l in C["locations"] if l["id"] == "hall"][0]
extra_edges = [x for x in hall["connections"] if x not in ("annex", "tidewell")]

src = []
src.append("// <auto-generated> by scripts/gen_builder_campaign.py from scripts/story_content.json - DO NOT EDIT BY HAND.")
src.append("// The complete playable campaign content pass (GAME_DESIGN.md §2/§5/§6/§7/§9/§11): prologue -> fracture ->")
src.append("// becoming (3 paths + Dax) -> reckoning (market/ascent/choirmaster) -> epilogue (7 endings). Mirror of the JSON")
src.append("// so RuntimeContentSource, the headless tests and validate_assets.py §3 all see one content truth.")
src.append("using System.Collections.Generic;")
src.append("using Crossroads.Core;")
src.append("")
src.append("namespace Crossroads.Narrative")
src.append("{")
src.append("    public static partial class StoryContentBuilder")
src.append("    {")
src.append("        public const string ChapterPrologue = \"ch_prologue\";")
src.append("        public const string ChapterFracture = \"ch_fracture\";")
src.append("        public const string ChapterBecoming = \"ch_becoming\";")
src.append("        public const string ChapterReckoning = \"ch_reckoning\";")
src.append("        public const string ChapterEpilogue = \"ch_epilogue\";")
src.append("        public const string DecisionMentor = \"dec_mentor\";")
src.append("        public const string DecisionSaveMara = \"dec_save_mara\";")
src.append("        public const string DecisionEnding = \"dec_ending\";")
src.append("        public const string FlagCampaignEnded = \"campaign_ended\";")
src.append("        public const string FlagEnding = \"ending\";")
src.append("        public static readonly string[] CampaignLocationIds = { %s };" % ", ".join(S(x) for x in M["locations"]))
src.append("        public const int CampaignAbilityCount = %d;" % len(M["abilities"]))
src.append("        public const int CampaignObjectiveCount = %d;" % len(M["objectives"]))
src.append("        public const int CampaignWorldInteractionCount = %d;" % len(M["worldInteractions"]))
src.append("        public const int CampaignStatusCount = %d;" % len(M["statusEffects"]))
src.append("        public const int CampaignEnemyCount = %d;" % len(M["enemies"]))
src.append("        public const int CampaignNpcCount = %d;" % len(M["npcs"]))
src.append("        public const int CampaignDecisionCount = %d;" % len(M["decisions"]))
src.append("        public static readonly string[] EndingIds = { \"ashen_crown\", \"tides_embrace\", \"the_unmoved\", \"hollow_throne\", \"balance\", \"long_way_home\", \"martyrs_dawn\" };")
src.append("")
src.append("        private static List<DecisionConditionData> N() { return new List<DecisionConditionData>(); }")
src.append("        private static List<DecisionEffectData> E() { return new List<DecisionEffectData>(); }")
src.append("        private static List<T> L<T>(params T[] items) { return new List<T>(items); }")
src.append("        private static DecisionConditionData Cd(ConditionType t, string k, string v, int a) { return new DecisionConditionData { type = t, key = k, value = v, amount = a }; }")
src.append("        private static DecisionEffectData Ef(EffectType t, string k, string v, int a) { return new DecisionEffectData { type = t, key = k, value = v, amount = a }; }")
src.append("")
src.append("        /// <summary>Appends the campaign pass to the chapter-one content (called by CreateFirstLightContent).</summary>")
src.append("        public static void AppendCampaignContent(StoryContentData content)")
src.append("        {")
for name, _ in sections:
    src.append("            Append%s(content);" % name)
src.append("            LocationDefinitionData hall = content.FindLocation(\"hall\");")
src.append("            if (hall != null) hall.connections.AddRange(new[] { %s });" % ", ".join(S(x) for x in extra_edges))
src.append("        }")
for name, body in sections:
    src.append("")
    src.append("        private static void Append%s(StoryContentData content)" % name)
    src.append("        {")
    src.extend(body)
    src.append("        }")
src.append("    }")
src.append("}")
open(OUT, "w").write("\n".join(src) + "\n")
print("wrote", os.path.relpath(OUT, ROOT), "-", sum(len(b) for _, b in sections), "records,", len(src), "lines")
