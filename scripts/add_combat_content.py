#!/usr/bin/env python3
"""One-shot authoring script: merges the COMBAT phase content into
scripts/story_content.json (the source of truth consumed by gen_story_content.py
and mirrored by StoryContentBuilder.cs). Mirrors the exact strings authored in
the C# builder so validate_assets.py's parity checks pass.

Adds: statusEffects / abilityCombat / enemies / combat (settings), the
obj_warden_hunt crisis objective, and Sera's Shieldmate state (inserted first
so it wins over her other reaction states)."""
import json

P = "scripts/story_content.json"
c = json.load(open(P))

if "statusEffects" in c:
    raise SystemExit(
        "story_content.json already carries the combat phase content.\n"
        "This was a one-shot merge script (kept for provenance); edit the JSON"
        " and StoryContentBuilder.cs directly, then re-run gen_story_content.py."
)

# ---------------------------------------------------------------- enum reminders
c["_comment"] = (
    "Prototype story content for CROSSROADS (decision + progression + world/objective + combat phase). "
    "Generated into ScriptableObject assets by gen_story_content.py and mirrored by StoryContentBuilder.cs "
    "(validated by validate_assets.py). Enums are the ints from ContentData.cs "
    "(ConditionType: FlagIs=0 FlagIsNot=1 FlagMissing=2 VarAtLeast=3 AffinityAtLeast=4 BondAtLeast=5 "
    "DecisionWas=6 DecisionNotMade=7 CodexOwned=8 ReputationAtLeast=9 ItemHeld=10 AbilityOwned=11 "
    "AreaUnlocked=12 SkillAtLeast=13 EchoesAtLeast=14 AbilityLevelBelow=15 ObjectiveActive=16 "
    "ObjectiveCompleted=17 ObjectiveFailed=18 WorldStateIs=19 | EffectType: SetFlag=0 ClearFlag=1 "
    "AddAffinity=2 SetAffinity=3 AddBond=4 SetVar=5 AddVar=6 SetWorldState=7 SpawnEntity=8 AddCodex=9 "
    "GrantEchoes=10 AddReputation=11 SetReputation=12 UnlockAbility=13 AddSkillLevel=14 AddItem=15 "
    "RemoveItem=16 UnlockArea=17 UpgradeAbility=18 BlockAbility=19 MoveNpc=20 CloseArea=21 ReopenArea=22 "
    "UnlockInteraction=23 | ObjectiveType: Main=0 Side=1 Crisis=2 Recovery=3 | DamageType: Kinetic=0 "
    "Ember=1 Tide=2 Stone=3 Hollow=4 | AttackDelivery: MeleeArc=0 RadiusPulse=1)."
)

def cond(t, key, value="", amount=0):
    return {"type": t, "key": key, "value": value, "amount": amount}

def eff(t, key, value="", amount=0):
    return {"type": t, "key": key, "value": value, "amount": amount}

def resist(t, m):
    return {"type": t, "multiplier": m}

# ---------------------------------------------------------------- status effects
c["statusEffects"] = [
    {
        "id": "echo_burn", "name": "Echo Burn",
        "description": "The ember keeps burning after the pulse - heat gnawing at the Fracture's shell.",
        "durationSeconds": 4.0, "tickIntervalSeconds": 1.0, "healthPerTick": -4,
        "moveSpeedMultiplier": 1.0, "attackRateMultiplier": 1.0, "grantsImmunity": False,
    },
    {
        "id": "suppression", "name": "Suppression",
        "description": "The Choir's discipline drags at your limbs - everything feels heavier.",
        "durationSeconds": 2.5, "tickIntervalSeconds": 0.0, "healthPerTick": 0,
        "moveSpeedMultiplier": 0.65, "attackRateMultiplier": 1.0, "grantsImmunity": False,
    },
    {
        "id": "dodge_guard", "name": "Flowing Aside",
        "description": "You are already somewhere else. Incoming strikes miss.",
        "durationSeconds": 0.45, "tickIntervalSeconds": 0.0, "healthPerTick": 0,
        "moveSpeedMultiplier": 1.0, "attackRateMultiplier": 1.0, "grantsImmunity": True,
    },
    {
        "id": "tide_soothe", "name": "Soothing Tide",
        "description": "The cool wash keeps mending what the fight bruises.",
        "durationSeconds": 3.0, "tickIntervalSeconds": 1.0, "healthPerTick": 6,
        "moveSpeedMultiplier": 1.0, "attackRateMultiplier": 1.0, "grantsImmunity": False,
    },
]

# ------------------------------------------------- ability combat payloads
# (existing abilities gain combat meaning; damage/heal scale with the level row's power)
c["abilityCombat"] = [
    {
        "abilityId": "ember_pulse", "damageType": 1, "damagePerPower": 10.0, "healPlayerPerPower": 0.0,
        "applyStatusToTargets": ["echo_burn"], "applyStatusToPlayer": [],
    },
    {
        "abilityId": "tide_mend", "damageType": 2, "damagePerPower": 3.0, "healPlayerPerPower": 12.0,
        "applyStatusToTargets": [], "applyStatusToPlayer": ["tide_soothe"],
    },
    {
        "abilityId": "stone_ward", "damageType": 3, "damagePerPower": 8.0, "healPlayerPerPower": 0.0,
        "applyStatusToTargets": ["suppression"], "applyStatusToPlayer": [],
    },
]

# ---------------------------------------------------------------- enemy archetypes
c["enemies"] = [
    {
        "id": "choir_warden", "displayName": "Choir Warden",
        "description": "A tracker-construct of the Choir: tall, patient, humming with hollow light. It marks who the Fracture touched - and collects them.",
        "sheetRef": "REF-06",
        "maxHealth": 60.0, "defense": 3.0,
        "resistances": [
            resist(0, 1.0), resist(1, 1.25), resist(2, 0.8), resist(3, 1.0), resist(4, 0.5),
        ],
        "moveSpeed": 1.55, "turnSpeed": 5.0,
        "detectionRadius": 9.0, "leashRadius": 15.0, "attackRange": 2.3, "staggerSeconds": 0.35,
        "attack": {
            "id": "warden_smite", "name": "Hollow Smite", "damageType": 4, "delivery": 0,
            "baseDamage": 12.0, "range": 2.3, "arcDegrees": 70.0, "radius": 0.0,
            "windupSeconds": 0.7, "cooldownSeconds": 2.2,
            "applyStatusIds": ["suppression"],
        },
        "activationConditions": [cond(6, "dec_c1_hall_first_light", "")],
        "onDefeatEffects": [
            eff(6, "warden_driven_off", "", 1),
            eff(8, "choir_warden", "0"),
            eff(8, "warden_wreckage", "1"),
            eff(10, "", "", 15),
            eff(9, "c1_warden_felled", ""),
        ],
    },
]

# ---------------------------------------------------------------- player combat settings
c["combat"] = {
    "playerMaxHealth": 100.0, "playerDefense": 2.0,
    "playerResistances": [resist(4, 0.9)],
    "basicAttack": {
        "id": "player_strike", "name": "Strike", "damageType": 0, "delivery": 0,
        "baseDamage": 10.0, "range": 2.8, "arcDegrees": 110.0, "radius": 0.0,
        "windupSeconds": 0.0, "cooldownSeconds": 0.9,
        "applyStatusIds": [],
    },
    "dodgeDistance": 3.6, "dodgeDurationSeconds": 0.28, "dodgeCooldownSeconds": 1.6,
    "dodgeStatusId": "dodge_guard", "healthVarKey": "player_hp",
    "onPlayerDefeat": [
        eff(6, "times_felled", "", 1),
        eff(4, "mara", "", 1),
        eff(9, "c1_felled_once", ""),
    ],
}

# ---------------------------------------------------------------- combat objective
c["objectives"].append({
    "id": "obj_warden_hunt",
    "title": "Drive Off the Choir Warden",
    "description": "The Choir sent a Warden to collect whoever the Fracture touched. That is you. It waits in the west transept - put it down before it reports.",
    "type": 2,
    "areaId": "hall",
    "giverNpcId": "",
    "offerConditions": [cond(6, "dec_c1_hall_first_light", "")],
    "autoActivate": True,
    "completeConditions": [cond(3, "warden_driven_off", "", 1)],
    "failConditions": [],
    "counterVar": "",
    "counterTarget": 0,
    "counterText": "",
    "steps": [],
    "consequences": [
        eff(11, "choir", "", -5),
        eff(11, "folk", "", 4),
        eff(4, "sera", "", 5),
        eff(10, "", "", 10),
        eff(9, "c1_warden_driven_off", ""),
    ],
    "failureConsequences": [],
    "followUps": [],
    "completionNotice": "Objective complete - the Warden will not report you. Sera saw all of it.",
    "failureNotice": "",
})

# ---------------------------------------------------------------- sera: Shieldmate state first
sera = [n for n in c["npcs"] if n["id"] == "sera"][0]
sera["states"].insert(0, {
    "conditions": [cond(17, "obj_warden_hunt", "")],
    "title": "Sera · Shieldmate",
    "moodLine": "'You put the Warden down.' Sera looks at you like the hall just changed its mind about you.",
    "approachDistance": 1.3, "avoidDistance": 0.0, "moveSpeed": 1.0, "reactRadius": -1.0,
})

json.dump(c, open(P, "w"), indent=1)
print("combat content merged into", P)
