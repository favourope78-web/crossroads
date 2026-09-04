#!/usr/bin/env python3
"""One-shot authoring script: merges the CAMPAIGN (branching story framework) content
into scripts/story_content.json. Mirrors the exact strings authored in StoryContentBuilder.cs
(validate_assets.py checks parity).

Adds: the Sera "echo" second encounter (branch-conditioned dialogue + decision), a
condition-gated sera interaction, and the chapters/beats/branches vertical slice
(Chapter 1 - The First Light + a Chapter 2 teaser that proves chaining)."""
import json

P = "scripts/story_content.json"
c = json.load(open(P))

if "chapters" in c:
    raise SystemExit(
        "story_content.json already carries the campaign phase content.\n"
        "This was a one-shot merge script (kept for provenance); edit the JSON"
        " and StoryContentBuilder.cs directly, then re-run gen_story_content.py."
    )

def cond(t, key, value="", amount=0):
    return {"type": t, "key": key, "value": value, "amount": amount}

def eff(t, key, value="", amount=0):
    return {"type": t, "key": key, "value": value, "amount": amount}

def node(id, speaker="", text="", nextId="", branchPrefix="", decisionId="", conditions=None, end=False):
    return {"id": id, "speaker": speaker, "text": text, "nextId": nextId,
            "branchPrefix": branchPrefix, "decisionId": decisionId,
            "conditions": conditions or [], "end": end}

FIRST_LIGHT = "dec_c1_hall_first_light"

# ---------------------------------------------------------------- decision: sera echo
c["decisions"].append({
    "id": "camp_sera_echo_dec",
    "promptText": "Sera saw which way the light took you. What do you tell her?",
    "timeLimitSeconds": 0.0,
    "timeoutOptionIndex": 0,
    "codexEntryId": "c1_sera_echo",
    "options": [
        {
            "id": "tell_her",
            "text": "Tell her the truth of it.",
            "afterText": "Sera nods once, slow. 'Then we hold the line together.'",
            "conditions": [],
            "effects": [
                eff(0, "sera_echo_seen", "1"),
                eff(4, "sera", "", 2),
            ],
        },
        {
            "id": "deflect",
            "text": "Keep it behind your teeth.",
            "afterText": "'Fair,' Sera says. 'Keep it that way until it's ready.'",
            "conditions": [],
            "effects": [
                eff(0, "sera_echo_seen", "1"),
            ],
        },
    ],
})

# ---------------------------------------------------------------- graph: sera echo (branch-conditioned)
c["graphs"].append({
    "id": "g_sera_echo",
    "nodes": [
        node("n_se1", "Sera",
             "You came back with the whole hall behind your eyes. Come on - out with it.",
             nextId="n_se_pick"),
        node("n_se_pick", branchPrefix="se_line"),
        node("se_line", "Sera",
             "Something changed in you out there. I can hear it in your steps."),
        node("se_line_ember", "Sera",
             "Ember still hums under your nails. The beacon forgot your name - but the Choir sent a Warden to check on you. That is not finished.",
             nextId="n_se_dec", conditions=[cond(6, FIRST_LIGHT, "ember_reach")]),
        node("se_line_tide", "Sera",
             "You went into the water for the twins and came out carrying their peace. The tide leaves marks like that.",
             nextId="n_se_dec", conditions=[cond(6, FIRST_LIGHT, "tide_clear")]),
        node("se_line_stone", "Sera",
             "You held the line with your shoulders. Stone remembers hands that stay.",
             nextId="n_se_dec", conditions=[cond(6, FIRST_LIGHT, "stone_still")]),
        node("n_se_dec", decisionId="camp_sera_echo_dec"),
    ],
})

# ---------------------------------------------------------------- encounter: sera echo
c["encounters"].append({
    "id": "camp_sera_echo",
    "npcName": "Sera",
    "graphId": "g_sera_echo",
    "startNodeId": "n_se1",
})

# ---------------------------------------------------------------- sera interaction (FIRST: the post-decision default)
sera = [n for n in c["npcs"] if n["id"] == "sera"][0]
sera["interactions"].insert(0, {
    "id": "talk_echo",
    "label": "Talk about what happened",
    "encounterId": "camp_sera_echo",
    "conditions": [cond(6, FIRST_LIGHT, "")],
})

# ---------------------------------------------------------------- chapters / beats / branches
c["chapters"] = [
    {
        "id": "ch_first_light",
        "title": "Chapter One",
        "subtitle": "The First Light",
        "description": "The Fracture Hall takes Ari in, asks its first question, and the answer routes everything after it.",
        "entryConditions": [],
        "beats": [
            {
                "id": "beat_arrival", "title": "Answer the hall's first question",
                "journalText": "The Fracture Hall took you in - and asked its first question.",
                "offerConditions": [], "resolveTrigger": 1, "resolveKey": FIRST_LIGHT,
                "resolveConditions": [], "requiredBeatIds": [], "onResolveEffects": [], "priority": 0,
            },
            {
                "id": "beat_sera_echo", "title": "Tell Sera what the light left",
                "journalText": "You told Sera what the light left in you. She did not flinch.",
                "offerConditions": [cond(6, FIRST_LIGHT, "")], "resolveTrigger": 1, "resolveKey": "camp_sera_echo_dec",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"], "onResolveEffects": [], "priority": 4,
            },
            {
                "id": "beat_warden", "title": "Drive off the Choir Warden",
                "journalText": "The Warden will not report you. Sera saw all of it.",
                "offerConditions": [cond(6, FIRST_LIGHT, "")], "resolveTrigger": 2, "resolveKey": "obj_warden_hunt",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"], "onResolveEffects": [], "priority": 5,
            },
            {
                "id": "beat_sera_confide", "title": "Sera's waystation key",
                "journalText": "Sera trusts you enough to show the waystation key.",
                "offerConditions": [cond(5, "sera", "", 7)], "resolveTrigger": 0, "resolveKey": "",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"],
                "onResolveEffects": [eff(0, "waystation_key", "1"), eff(10, "", "", 10)], "priority": 6,
            },
            {
                "id": "beat_ember_mastery", "title": "The ember answers faster",
                "journalText": "The ember answers faster now. It wants a second door.",
                "offerConditions": [cond(11, "ember_pulse")], "resolveTrigger": 0, "resolveKey": "",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"],
                "onResolveEffects": [eff(0, "ember_mastery", "1")], "priority": 7,
            },
            {
                "id": "beat_ember_path", "title": "Silence the Choir Beacon",
                "journalText": "The beacon is quiet. The annex belongs to the hall again.",
                "offerConditions": [cond(0, "path_ember", "1")], "resolveTrigger": 2, "resolveKey": "obj_ember_beacon",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"], "onResolveEffects": [], "priority": 10,
            },
            {
                "id": "beat_tide_path", "title": "Carry the twins' peace back",
                "journalText": "The twins have their locket back, and Mara knows the truth of the rush.",
                "offerConditions": [cond(0, "path_tide", "1")], "resolveTrigger": 2, "resolveKey": "obj_tide_report",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"], "onResolveEffects": [], "priority": 10,
            },
            {
                "id": "beat_stone_path", "title": "Brace the north line",
                "journalText": "The barricade held. The north line is yours.",
                "offerConditions": [cond(0, "path_stone", "1")], "resolveTrigger": 2, "resolveKey": "obj_stone_barricade",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"], "onResolveEffects": [], "priority": 10,
            },
            {
                "id": "beat_stone_fell", "title": "The line fell",
                "journalText": "The barricade fell. The hall breathed dust - and kept breathing.",
                "offerConditions": [cond(0, "path_stone", "1")], "resolveTrigger": 3, "resolveKey": "obj_stone_barricade",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"], "onResolveEffects": [], "priority": 10,
            },
            {
                "id": "beat_recovery", "title": "Haul the line back up",
                "journalText": "You hauled the line back up with your own hands.",
                "offerConditions": [cond(0, "path_resolved", "1")], "resolveTrigger": 2, "resolveKey": "obj_stone_rebuild",
                "resolveConditions": [], "requiredBeatIds": ["beat_stone_fell"], "onResolveEffects": [], "priority": 11,
            },
            {
                "id": "beat_council", "title": "The hall exhales",
                "journalText": "The hall held its breath - and let it out. Chapter One closes.",
                "offerConditions": [cond(0, "path_resolved", "1")], "resolveTrigger": 0, "resolveKey": "",
                "resolveConditions": [], "requiredBeatIds": ["beat_arrival"], "onResolveEffects": [], "priority": 20,
            },
        ],
        "branches": [
            # the three-branch trunk: the first decision routes the whole run
            {"id": "br_trode_ember", "fromBeatId": "beat_arrival", "toBeatId": "beat_ember_path",
             "label": "Path of Ember",
             "requiredConditions": [cond(6, FIRST_LIGHT, "ember_reach")],
             "effects": [eff(0, "path_ember", "1")]},
            {"id": "br_trode_tide", "fromBeatId": "beat_arrival", "toBeatId": "beat_tide_path",
             "label": "Path of Tide",
             "requiredConditions": [cond(6, FIRST_LIGHT, "tide_clear")],
             "effects": [eff(0, "path_tide", "1")]},
            {"id": "br_trode_stone", "fromBeatId": "beat_arrival", "toBeatId": "beat_stone_path",
             "label": "Path of Stone",
             "requiredConditions": [cond(6, FIRST_LIGHT, "stone_still")],
             "effects": [eff(0, "path_stone", "1")]},
            # path settle routes (objective outcome -> finale unlock)
            {"id": "br_ember_settled", "fromBeatId": "beat_ember_path", "toBeatId": "beat_council",
             "label": "",
             "requiredConditions": [], "effects": [eff(0, "path_resolved", "1")]},
            {"id": "br_tide_settled", "fromBeatId": "beat_tide_path", "toBeatId": "beat_council",
             "label": "",
             "requiredConditions": [], "effects": [eff(0, "path_resolved", "1")]},
            {"id": "br_stone_settled", "fromBeatId": "beat_stone_path", "toBeatId": "beat_council",
             "label": "The Line Held",
             "requiredConditions": [], "effects": [eff(0, "path_resolved", "1")]},
            # FAILURE is a route, not an ending: the fallen barricade opens the recovery branch
            {"id": "br_line_fell", "fromBeatId": "beat_stone_fell", "toBeatId": "beat_recovery",
             "label": "The Line Fell",
             "requiredConditions": [], "effects": [eff(0, "path_resolved", "1")]},
            {"id": "br_line_reheld", "fromBeatId": "beat_recovery", "toBeatId": "beat_council",
             "label": "The Line Held Again",
             "requiredConditions": [], "effects": []},
            # the echo choice shades the relationship (branch-dependent NPC future)
            {"id": "br_told_sera", "fromBeatId": "beat_sera_echo", "toBeatId": "",
             "label": "Sera Holds the Line With You",
             "requiredConditions": [cond(6, "camp_sera_echo_dec", "tell_her")], "effects": []},
            {"id": "br_deflected", "fromBeatId": "beat_sera_echo", "toBeatId": "",
             "label": "Some Doors Stay Shut",
             "requiredConditions": [cond(6, "camp_sera_echo_dec", "deflect")], "effects": []},
            # ability-dependent branch: owning the ember widens the story
            {"id": "br_second_door", "fromBeatId": "beat_ember_mastery", "toBeatId": "",
             "label": "The Ember Widens",
             "requiredConditions": [], "effects": [eff(0, "ember_second_door", "1")]},
        ],
        "completionConditions": [cond(0, "path_resolved", "1")],
        "completionEffects": [eff(0, "ch1_complete", "1"), eff(9, "c1_ch1_complete")],
        "completionJournal": "Chapter One: The First Light - complete.",
    },
    {
        # teaser: proves chapters chain through content data alone (entry = previous chapter's flag)
        "id": "ch_whispers",
        "title": "Chapter Two",
        "subtitle": "Whispers Under the Hall",
        "description": "Teaser beat: the framework's proof that a designer adds the next chapter as pure data.",
        "entryConditions": [cond(0, "ch1_complete", "1")],
        "beats": [
            {
                "id": "beat_whispers", "title": "Something knows your name",
                "journalText": "Somewhere under the hall, something whispers your new name.",
                "offerConditions": [], "resolveTrigger": 0, "resolveKey": "",
                "resolveConditions": [], "requiredBeatIds": [], "onResolveEffects": [eff(0, "ch2_teaser", "1")], "priority": 0,
            },
        ],
        "branches": [],
        "completionConditions": [],
        "completionEffects": [eff(9, "c1_ch2_teaser")],
        "completionJournal": "To be continued.",
    },
]

json.dump(c, open(P, "w"), indent=1)
print("campaign content merged into", P)
