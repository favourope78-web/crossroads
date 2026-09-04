#!/usr/bin/env python3
"""One-shot authoring script: merges the WORLD/OBJECTIVE phase content into
scripts/story_content.json (the source of truth consumed by gen_story_content.py
and mirrored by StoryContentBuilder.cs). Mirrors the exact strings authored in
the C# builder so validate_assets.py's three-way parity check passes."""
import json

P = "scripts/story_content.json"
c = json.load(open(P))

if "objectives" in c:
    raise SystemExit(
        "story_content.json already carries the world/objective phase content.\n"
        "This was a one-shot merge script (kept for provenance); edit the JSON\n"
        "and StoryContentBuilder.cs directly, then re-run gen_story_content.py."
)

# ---------------------------------------------------------------- enum reminders
c["_comment"] = (
    "Prototype story content for CROSSROADS (decision + progression + world/objective phase). "
    "Generated into ScriptableObject assets by gen_story_content.py and mirrored by StoryContentBuilder.cs "
    "(validated by validate_assets.py). Enums are the ints from ContentData.cs "
    "(ConditionType: FlagIs=0 FlagIsNot=1 FlagMissing=2 VarAtLeast=3 AffinityAtLeast=4 BondAtLeast=5 "
    "DecisionWas=6 DecisionNotMade=7 CodexOwned=8 ReputationAtLeast=9 ItemHeld=10 AbilityOwned=11 "
    "AreaUnlocked=12 SkillAtLeast=13 EchoesAtLeast=14 AbilityLevelBelow=15 ObjectiveActive=16 "
    "ObjectiveCompleted=17 ObjectiveFailed=18 WorldStateIs=19 | EffectType: SetFlag=0 ClearFlag=1 "
    "AddAffinity=2 SetAffinity=3 AddBond=4 SetVar=5 AddVar=6 SetWorldState=7 SpawnEntity=8 AddCodex=9 "
    "GrantEchoes=10 AddReputation=11 SetReputation=12 UnlockAbility=13 AddSkillLevel=14 AddItem=15 "
    "RemoveItem=16 UnlockArea=17 UpgradeAbility=18 BlockAbility=19 MoveNpc=20 CloseArea=21 ReopenArea=22 "
    "UnlockInteraction=23 | ObjectiveType: Main=0 Side=1 Crisis=2 Recovery=3)."
)

def cond(t, key, value="", amount=0):
    return {"type": t, "key": key, "value": value, "amount": amount}

def eff(t, key, value="", amount=0):
    return {"type": t, "key": key, "value": value, "amount": amount}

# ---------------------------------------------------------------- progression: new items
c["progression"]["items"] += [
    {
        "id": "twins_keepsake",
        "name": "Twins' Keepsake",
        "description": "A stamped tin locket, warm from being held too tight. It belongs to the twins by the east columns.",
    },
    {
        "id": "ember_core",
        "name": "Ember Core",
        "description": "What the beacon guarded before it forgot your name. It hums with banked heat.",
    },
]

# ---------------------------------------------------------------- decision 5 (tide report)
c["decisions"].append({
    "id": "dec_tide_report",
    "promptText": "Mara waits by the east columns. What do you tell her about the twins?",
    "timeLimitSeconds": 0.0,
    "timeoutOptionIndex": 0,
    "codexEntryId": "c1_tide_report",
    "options": [
        {
            "id": "tell_all",
            "text": "Everything. The rush, the light, the small hand in yours.",
            "afterText": "Mara listens to all of it, and something in her shoulders lets go.",
            "conditions": [],
            "effects": [
                eff(0, "tide_reported", "1"),
                eff(4, "mara", "", 5),
                eff(2, "tide", "", 5),
            ],
        },
        {
            "id": "keep_light",
            "text": "That it went fine. Some things should stay theirs.",
            "afterText": "Mara nods once, letting it be - but she catches your sleeve before you go.",
            "conditions": [],
            "effects": [
                eff(0, "tide_reported", "1"),
                eff(4, "mara", "", 2),
            ],
        },
    ],
})

# ---------------------------------------------------------------- graph 7 (Mara report)
c["graphs"].append({
    "id": "g_c1_hall_mara_report",
    "nodes": [
        {"id": "start", "speaker": "", "text": "", "nextId": "", "branchPrefix": "report_line", "decisionId": "", "conditions": [], "end": False},
        {"id": "report_line_done", "speaker": "Mara",
         "text": "You told me. I keep replaying it - the good part. Thank you for that.",
         "nextId": "end", "branchPrefix": "", "decisionId": "",
         "conditions": [cond(6, "dec_tide_report")], "end": False},
        {"id": "report_line_keepsake", "speaker": "Mara",
         "text": "You found it? The locket? Ari, they've been asking everyone for a week.",
         "nextId": "report_offer", "branchPrefix": "", "decisionId": "",
         "conditions": [cond(17, "obj_tide_keepsake")], "end": False},
        {"id": "report_line_default", "speaker": "Mara",
         "text": "The twins, the light, all of it - I want to hear how you're carrying it.",
         "nextId": "report_offer", "branchPrefix": "", "decisionId": "", "conditions": [], "end": False},
        {"id": "report_offer", "speaker": "", "text": "", "nextId": "", "branchPrefix": "report_after", "decisionId": "dec_tide_report", "conditions": [], "end": False},
        {"id": "report_after_tell", "speaker": "Mara",
         "text": "'That's who you are now,' she says. 'Don't lose them to the light.'",
         "nextId": "end", "branchPrefix": "", "decisionId": "",
         "conditions": [cond(6, "dec_tide_report", "tell_all")], "end": False},
        {"id": "report_after_keep", "speaker": "Mara",
         "text": "'Then it's theirs,' she agrees. 'But you're allowed to be proud of it.'",
         "nextId": "end", "branchPrefix": "", "decisionId": "",
         "conditions": [cond(6, "dec_tide_report", "keep_light")], "end": False},
        {"id": "end", "speaker": "", "text": "", "nextId": "", "branchPrefix": "", "decisionId": "", "conditions": [], "end": True},
    ],
})

c["encounters"].append({
    "id": "c1_hall_mara_report", "npcName": "Mara", "graphId": "g_c1_hall_mara_report", "startNodeId": "start",
})

# ---------------------------------------------------------------- objectives
c["objectives"] = [
    {
        "id": "obj_ember_beacon",
        "title": "Silence the Choir Beacon",
        "description": "The Choir's beacon in the north annex marks everyone the light touched - starting with you. Ember answers heat: make the beacon forget your name.",
        "type": 0,
        "areaId": "annex",
        "giverNpcId": "",
        "offerConditions": [cond(6, "dec_c1_hall_first_light", "ember_reach")],
        "autoActivate": True,
        "completeConditions": [cond(0, "beacon_silenced", "1")],
        "failConditions": [],
        "counterVar": "",
        "counterTarget": 0,
        "counterText": "",
        "steps": [],
        "consequences": [
            eff(7, "annex", "quiet"),
            eff(8, "ember_cache", "1"),
            eff(11, "choir", "", -10),
            eff(11, "folk", "", 5),
            eff(4, "sera", "", 4),
            eff(20, "sera", "annex_gate"),
            eff(10, "", "", 10),
            eff(9, "c1_beacon_silenced"),
        ],
        "failureConsequences": [],
        "followUps": ["obj_ember_cache"],
        "completionNotice": "Objective complete - the beacon no longer knows your name.",
        "failureNotice": "",
    },
    {
        "id": "obj_ember_cache",
        "title": "Claim the Ember Cache",
        "description": "Where the beacon stood, the hall left a gift for the hand that quieted it. Open the cache.",
        "type": 1,
        "areaId": "annex",
        "giverNpcId": "",
        "offerConditions": [cond(17, "obj_ember_beacon")],
        "autoActivate": True,
        "completeConditions": [cond(0, "ember_cache_opened", "1")],
        "failConditions": [],
        "counterVar": "",
        "counterTarget": 0,
        "counterText": "",
        "steps": [],
        "consequences": [
            eff(15, "ember_core"),
            eff(10, "", "", 15),
            eff(9, "c1_ember_cache"),
        ],
        "failureConsequences": [],
        "followUps": [],
        "completionNotice": "Objective complete - the hall's gift is yours.",
        "failureNotice": "",
    },
    {
        "id": "obj_tide_keepsake",
        "title": "The Twins' Keepsake",
        "description": "The twins you pulled clear lost their mother's keepsake in the rush. The tide left it glinting by the east columns - find it and bring it back.",
        "type": 0,
        "areaId": "hall",
        "giverNpcId": "sera",
        "offerConditions": [cond(6, "dec_c1_hall_first_light", "tide_clear")],
        "autoActivate": True,
        "completeConditions": [cond(0, "keepsake_returned", "1")],
        "failConditions": [],
        "counterVar": "",
        "counterTarget": 0,
        "counterText": "",
        "steps": [
            {"text": "Find the keepsake by the east columns", "conditions": [cond(0, "keepsake_found", "1")]},
            {"text": "Bring it back to the twins", "conditions": [cond(0, "keepsake_returned", "1")]},
        ],
        "consequences": [
            eff(4, "sera", "", 8),
            eff(11, "folk", "", 6),
            eff(8, "tide_bystanders", "0"),
            eff(8, "tide_calm", "1"),
            eff(7, "hall", "twins_blessed"),
            eff(10, "", "", 10),
            eff(9, "c1_twins_keepsake"),
        ],
        "failureConsequences": [],
        "followUps": ["obj_tide_report"],
        "completionNotice": "Objective complete - the twins hold their mother's keepsake again.",
        "failureNotice": "",
    },
    {
        "id": "obj_tide_report",
        "title": "Tell Mara What the Light Did",
        "description": "Mara has been watching since the light chose you. She should hear what the twins' gratitude looked like.",
        "type": 1,
        "areaId": "hall",
        "giverNpcId": "mara",
        "offerConditions": [cond(17, "obj_tide_keepsake")],
        "autoActivate": True,
        "completeConditions": [cond(6, "dec_tide_report")],
        "failConditions": [],
        "counterVar": "",
        "counterTarget": 0,
        "counterText": "",
        "steps": [],
        "consequences": [
            eff(4, "mara", "", 5),
            eff(2, "tide", "", 5),
            eff(9, "c1_tide_report"),
        ],
        "failureConsequences": [],
        "followUps": [],
        "completionNotice": "Objective complete - Mara knows what the light did.",
        "failureNotice": "",
    },
    {
        "id": "obj_stone_barricade",
        "title": "Steady the North Barricade",
        "description": "The Choir's next sweep will come through the north passage. The barricade holds twice as long with hands on it - or once, forever, with stillness. Careful: give your echo back to the shrine and the wood remembers gravity.",
        "type": 2,
        "areaId": "hall",
        "giverNpcId": "",
        "offerConditions": [cond(6, "dec_c1_hall_first_light", "stone_still")],
        "autoActivate": True,
        "completeConditions": [cond(3, "brace_count", "", 2)],
        "failConditions": [cond(0, "c1_echo_sealed", "1")],
        "counterVar": "brace_count",
        "counterTarget": 2,
        "counterText": "Braces set",
        "steps": [],
        "consequences": [
            eff(11, "wards", "", 8),
            eff(11, "folk", "", 3),
            eff(4, "mara", "", 2),
            eff(7, "hall", "barricade_held"),
            eff(10, "", "", 10),
            eff(9, "c1_barricade_held"),
        ],
        "failureConsequences": [
            eff(8, "barricade", "0"),
            eff(8, "barricade_rubble", "1"),
            eff(7, "hall", "barricade_fell"),
            eff(11, "wards", "", -6),
            eff(21, "annex"),
            eff(9, "c1_barricade_fell"),
        ],
        "followUps": ["obj_stone_rebuild"],
        "completionNotice": "Objective complete - the north passage will hold.",
        "failureNotice": "Objective failed - you gave your stillness back, and the barricade fell.",
    },
    {
        "id": "obj_stone_rebuild",
        "title": "Clear the Fallen Barricade",
        "description": "Stillness was given back, and the wood remembered gravity. Clear the rubble - the passage is needed either way.",
        "type": 3,
        "areaId": "hall",
        "giverNpcId": "",
        "offerConditions": [cond(18, "obj_stone_barricade")],
        "autoActivate": True,
        "completeConditions": [cond(3, "rubble_count", "", 2)],
        "failConditions": [],
        "counterVar": "rubble_count",
        "counterTarget": 2,
        "counterText": "Rubble cleared",
        "steps": [],
        "consequences": [
            eff(11, "folk", "", 3),
            eff(7, "hall", "passage_cleared"),
            eff(22, "annex"),
            eff(10, "", "", 5),
        ],
        "failureConsequences": [],
        "followUps": [],
        "completionNotice": "Objective complete - the passage is clear again.",
        "failureNotice": "",
    },
]

# ---------------------------------------------------------------- world interactions
c["worldInteractions"] = [
    {"key": "choir_beacon_channel", "label": "Channel ember into the beacon",
     "conditions": [cond(11, "ember_pulse")]},
    {"key": "ember_cache_open", "label": "Open the ember cache",
     "conditions": [cond(0, "beacon_silenced", "1")]},
    {"key": "keepsake_search", "label": "Search the crate",
     "conditions": [cond(0, "c1_hall_drive", "tide")]},
    {"key": "keepsake_return", "label": "Return the keepsake",
     "conditions": [cond(10, "twins_keepsake")]},
    {"key": "barricade_brace", "label": "Brace the barricade",
     "conditions": [cond(0, "c1_hall_drive", "stone")]},
    {"key": "barricade_wedge", "label": "Wedge the line with stillness",
     "conditions": [cond(11, "stone_ward")]},
    {"key": "rubble_clear", "label": "Clear the rubble",
     "conditions": [cond(19, "hall", "barricade_fell")]},
]

# ---------------------------------------------------------------- NPC updates (objective-driven)
mara = next(n for n in c["npcs"] if n["id"] == "mara")
sera = next(n for n in c["npcs"] if n["id"] == "sera")

mara["states"].insert(0, {
    "conditions": [cond(17, "obj_tide_report")],
    "title": "Mara \u00b7 Heartened",
    "moodLine": "Mara stands easier since you told her how it felt.",
    "approachDistance": 1.2,
    "avoidDistance": -1.0,
    "moveSpeed": -1.0,
    "reactRadius": -1.0,
})
mara["interactions"].insert(0, {
    "id": "report",
    "label": "Tell her about the twins",
    "encounterId": "c1_hall_mara_report",
    "conditions": [cond(17, "obj_tide_keepsake"), cond(7, "dec_tide_report")],
})

sera["states"].insert(0, {
    "conditions": [cond(17, "obj_ember_beacon")],
    "title": "Sera \u00b7 Vanguard",
    "moodLine": "'The beacon's quiet. First time in days I can hear myself think.' Sera takes her watch by the annex gate.",
    "approachDistance": 1.4,
    "avoidDistance": 0.0,
    "moveSpeed": 1.0,
    "reactRadius": -1.0,
})
sera["states"].insert(1, {
    "conditions": [cond(17, "obj_stone_barricade")],
    "title": "Sera \u00b7 Steadied",
    "moodLine": "'It held. You held it.' Sera's shoulders drop an inch.",
    "approachDistance": 1.5,
    "avoidDistance": 0.0,
    "moveSpeed": 1.0,
    "reactRadius": -1.0,
})

# ---------------------------------------------------------------- write (repo style: 1-space indent)
with open(P, "w") as f:
    json.dump(c, f, indent=1, ensure_ascii=False)
    f.write("\n")
print("story_content.json updated:",
      len(c["objectives"]), "objectives,",
      len(c["worldInteractions"]), "world interactions,",
      len(c["decisions"]), "decisions,",
      len(c["graphs"]), "graphs,",
      len(c["encounters"]), "encounters")
