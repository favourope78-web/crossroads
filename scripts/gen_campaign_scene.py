"""Campaign content pass - scene extension for gen_firstlocation_scene.py.

Imported (not run) by the scene generator right before SceneRoots are written. Uses the
generator's own emit_* helpers, registry and primitive palette, and adds - for the thirteen
campaign locations of GAME_DESIGN.md §11.2 - inside the SAME FirstLocation scene:

  * a 20 x 20 kit room per location west of the hall (campaign_layout.json), with the
    LocationAnchor_<id> / AreaTrigger_<id> contracts the LocationTransitionFader + AreaTrigger use
  * NPC roots (<Name>_NPC: NpcInteractable + NpcAgent) using CHARACTER_REFERENCE palette
    materials so recurring characters read the same everywhere (Mara = REF-02 in every chapter,
    Dax = REF-03, Archivist = REF-05, mentors derived per §4)
  * enemy roots (EnemyAgent(enemyId)) for every archetype skin / boss / fate insert
  * cutscene + tutorial triggers (StoryEventInteractable) and important objects
    (WorldActionInteractable) that drive the objectives' flags/vars
  * world-state variants (StoryWorldState.areaVariants) for market / docks / spire / vessa
  * entity bindings for every SpawnEntity key the campaign content writes
  * NpcRelocator targets for the MoveNpc effects (mentors -> interlude camp, archivist -> pier)
  * an Env_<Kit>.prefab per location (validate_assets.py §5)

Everything here is data-driven from scripts/story_content.json where it matters (ids, prompts,
flags) - the scene only supplies geometry and wiring.
"""
import json, os

def build(g):
    """g = the generator's globals() dict."""
    emit_gameobject, emit_transform, emit_meshfilter, emit_renderer = g["emit_gameobject"], g["emit_transform"], g["emit_meshfilter"], g["emit_renderer"]
    emit_boxcollider, emit_capsulecollider, emit_monobehaviour = g["emit_boxcollider"], g["emit_capsulecollider"], g["emit_monobehaviour"]
    emit_char_root, child_renderer_id, add_block = g["emit_char_root"], g["child_renderer_id"], g["add_block"]
    cond_yaml, eff_yaml, world_action_fields = g["cond_yaml"], g["eff_yaml"], g["world_action_fields"]
    REG, root_gids, COLLIDERS = g["REG"], g["root_gids"], g["COLLIDERS"]
    CUBE, CAPSULE, SPHERE = g["CUBE"], g["CAPSULE"], g["SPHERE"]
    ROOT, HERE = g["ROOT"], g["HERE"]
    env_prefab = g["env_prefab"]
    entity_bindings = g["CAMPAIGN_ENTITIES"]      # list of (key, gameobject id, defaultActive)
    area_variants = g["CAMPAIGN_VARIANTS"]         # list of (area, variant, gameobject id)
    relocations = g["CAMPAIGN_RELOCATIONS"]        # list of (npcId, locationKey, transform id, notice)

    content = json.load(open(os.path.join(HERE, "story_content.json")))
    layout = json.load(open(os.path.join(HERE, "campaign_layout.json")))["rooms"]
    npc_by_id = {n["id"]: n for n in content["npcs"]}
    enemy_by_id = {e["id"]: e for e in content["enemies"]}
    loc_by_id = {l["id"]: l for l in content["locations"]}

    # ---- CHARACTER_REFERENCE palette (canonical, reused by every appearance) ----
    # material key -> (baseColor rgb, emission rgb). Files are cloned from M_Seq_Tide.mat (URP Lit).
    PALETTE = {
        "M_Char_Mara":       ((0.114, 0.102, 0.110), (0.02, 0.02, 0.02)),   # REF-02 hair #1D1A1C over dress
        "M_Char_Mara_Dress": ((0.957, 0.949, 0.937), (0.05, 0.05, 0.05)),   # REF-02 white dress #F4F2EF
        "M_Char_Mara_Hoodie":((0.561, 0.576, 0.612), (0.02, 0.02, 0.03)),   # REF-02 grey hoodie #8F939C (C2/C3)
        "M_Char_Dax_Blazer": ((0.165, 0.192, 0.251), (0.02, 0.02, 0.04)),   # REF-03 navy #2A3140
        "M_Char_Dax_Hair":   ((0.290, 0.208, 0.153), (0.01, 0.01, 0.01)),   # REF-03 chestnut #4A3527
        "M_Char_Dax_Tee":    ((0.94, 0.94, 0.93), (0.03, 0.03, 0.03)),      # REF-03 white tee
        "M_Char_Kael":       ((0.32, 0.26, 0.22), (0.45, 0.14, 0.05)),      # mentor: Ember-trim coat
        "M_Char_Odalys":     ((0.16, 0.42, 0.44), (0.08, 0.30, 0.34)),      # mentor: Tide-teal robe
        "M_Char_Bran":       ((0.42, 0.44, 0.46), (0.06, 0.06, 0.07)),      # mentor: Stone-grey ward coat
        "M_Char_Archivist":  ((0.812, 0.902, 0.949), (0.498, 0.847, 0.910)),# REF-05 #CFE6F2 body, holo #7FD8E8
        "M_Char_Hair_Silver":((0.910, 0.945, 0.965), (0.30, 0.45, 0.50)),   # REF-05 silver-white #E8F1F6
        "M_Choir_Grunt":     ((0.541, 0.435, 0.322), (0.10, 0.30, 0.34)),   # REF-06A tan #8A6F52, light #66D9E8
        "M_Choir_Bruiser":   ((0.298, 0.333, 0.282), (0.10, 0.30, 0.34)),   # REF-06B olive #4C5548
        "M_Choir_Elite":     ((0.780, 0.800, 0.824), (0.20, 0.55, 0.62)),   # REF-06C white #C7CCD2
        "M_Choir_Hit":       ((1.0, 0.96, 0.85), (1.0, 0.85, 0.55)),        # damage flash (all Choir)
        "M_Boss_Echo":       ((0.55, 0.62, 0.95), (0.45, 0.55, 1.0)),       # First Echo: Fracture light
        "M_Boss_Choirmaster":((0.16, 0.14, 0.20), (0.50, 0.20, 0.70)),      # Choirmaster: hollow violet
        "M_Hollow":          ((0.08, 0.07, 0.10), (0.35, 0.10, 0.45)),      # Hollow dressing / turned characters
        "M_Env_Summer":      ((0.86, 0.72, 0.42), (0.20, 0.12, 0.02)),      # last summer: warm wood
        "M_Env_Water":       ((0.05, 0.28, 0.36), (0.10, 0.45, 0.60)),      # docks / sanctuary water
        "M_Env_Ruin":        ((0.30, 0.26, 0.22), (0.0, 0.0, 0.0)),         # ruined dressing
        "M_Env_Rebuilt":     ((0.78, 0.66, 0.48), (0.30, 0.20, 0.08)),      # rebuilt lantern wood
        "M_Env_Contested":   ((0.36, 0.24, 0.30), (0.25, 0.05, 0.20)),      # Choir graffiti tint
    }
    MAT_DIR = os.path.join(ROOT, "Assets/Game/Environment/Materials")
    seq = open(os.path.join(MAT_DIR, "M_Seq_Tide.mat")).read()
    NATIVE = g["NATIVE"]
    write_meta_if_missing = g["write_meta_if_missing"]
    for name, (base, emit) in PALETTE.items():
        p = os.path.join(MAT_DIR, name + ".mat")
        if not os.path.exists(p):
            txt = seq.replace("m_Name: M_Seq_Tide", "m_Name: " + name)
            txt = txt.replace("_BaseColor: {r: 0.03, g: 0.16, b: 0.18, a: 1}", "_BaseColor: {r: %s, g: %s, b: %s, a: 1}" % base)
            txt = txt.replace("_EmissionColor: {r: 0.1, g: 0.85, b: 0.95, a: 1}", "_EmissionColor: {r: %s, g: %s, b: %s, a: 1}" % emit)
            open(p, "w").write(txt)
        write_meta_if_missing(p, NATIVE, REG[name])

    # ---- helpers ----
    def room(loc_id, floor_mat="M_Hall_Concrete", wall_mat="M_Hall_Concrete", open_sides=()):
        """20 x 20 room: 4 floor tiles, walls on the closed sides, 4 corner columns, 2 trusses."""
        cx, cz = layout[loc_id]
        pieces = []
        for dx in (-5, 5):
            for dz in (-5, 5):
                pieces.append(("SM_FloorTile", (cx + dx, 0.05, cz + dz), 0, floor_mat))
        if "n" not in open_sides:
            pieces += [("SM_WallPanel", (cx - 5, 3, cz + 10), 0, wall_mat), ("SM_WallPanel", (cx + 5, 3, cz + 10), 0, wall_mat)]
        if "s" not in open_sides:
            pieces += [("SM_WallPanel", (cx - 5, 3, cz - 10), 0, wall_mat), ("SM_WallPanel", (cx + 5, 3, cz - 10), 0, wall_mat)]
        if "e" not in open_sides:
            pieces += [("SM_WallPanel", (cx + 10, 3, cz - 5), 90, wall_mat), ("SM_WallPanel", (cx + 10, 3, cz + 5), 90, wall_mat)]
        if "w" not in open_sides:
            pieces += [("SM_WallPanel", (cx - 10, 3, cz - 5), 90, wall_mat), ("SM_WallPanel", (cx - 10, 3, cz + 5), 90, wall_mat)]
        for dx in (-8.5, 8.5):
            for dz in (-8.5, 8.5):
                pieces.append(("SM_Column", (cx + dx, 0, cz + dz), 0, "M_Hall_Metal"))
        pieces += [("SM_Truss", (cx, 9.2, cz - 4), 0, "M_Hall_Metal"), ("SM_Truss", (cx, 9.2, cz + 4), 0, "M_Hall_Metal")]
        for (piece, pos, yaw, matk) in pieces:
            comps = ["transform", "meshfilter", "renderer"]
            if piece in COLLIDERS: comps.append("collider")
            gid, ids = emit_gameobject("%s_%s_%s_%s" % (piece, loc_id, pos[0], pos[2]), comps)
            emit_transform(ids["transform"], gid, pos, (0, yaw, 0), (1, 1, 1))
            emit_meshfilter(ids["meshfilter"], gid, REG[piece])
            emit_renderer(ids["renderer"], gid, REG[matk])
            if "collider" in ids:
                c = COLLIDERS[piece]
                emit_boxcollider(ids["collider"], gid, c[1], c[2])
            root_gids.append(gid)
        # travel anchor (fader teleport target) + area trigger (persisted currentArea)
        a_gid, a_ids = emit_gameobject("LocationAnchor_" + loc_id, ["transform"])
        emit_transform(a_ids["transform"], a_gid, (cx, 0, cz - 7), (0, 0, 0), (1, 1, 1))
        root_gids.append(a_gid)
        t_gid, t_ids = emit_gameobject("AreaTrigger_" + loc_id, ["transform", "col", "area"])
        emit_transform(t_ids["transform"], t_gid, (cx, 1.5, cz), (0, 0, 0), (1, 1, 1))
        add_block("""--- !u!65 &%d
BoxCollider:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: %d}
  m_Material: {fileID: 0}
  m_IncludeGestures: 0
  m_IsTrigger: 1
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {x: 19, y: 3, z: 19}
  m_Center: {x: 0, y: 0, z: 0}""" % (t_ids["col"], t_gid))
        emit_monobehaviour(t_ids["area"], t_gid, REG["AreaTrigger.cs"], "  areaId: " + loc_id)
        root_gids.append(t_gid)
        return cx, cz

    def sign(loc_id, name, pos, mat, label, encounter_id, active=1, priority=26, radius=3.2, entity_key=None):
        """A glowing story marker that runs a cutscene/dialogue encounter (StoryEventInteractable)."""
        gid, ids, _ = emit_char_root(name, ["event"], pos, (0, 0, 0), active, [
            ("Pylon", "M_Hall_Metal", CUBE, (0, 0.9, 0), (0.28, 1.8, 0.28)),
            ("Glow", mat, SPHERE, (0, 2.05, 0), (0.5, 0.5, 0.5)),
        ])
        emit_monobehaviour(ids["event"], gid, REG["StoryEventInteractable.cs"],
            "  encounterId: %s\n  promptLabel: %s\n  interactRadius: %s\n  priority: %d" % (encounter_id, label, radius, priority))
        if entity_key: entity_bindings.append((entity_key, gid, active))
        return gid

    def action(name, pos, prims, prompt, conds, effects, use_var, max_uses, consume, locked, used, label, active=1, radius=3.0, priority=23, entity_key=None):
        gid, ids, _ = emit_char_root(name, ["action"], pos, (0, 0, 0), active, prims)
        emit_monobehaviour(ids["action"], gid, REG["WorldActionInteractable.cs"],
            world_action_fields(prompt, conds, effects, use_var, max_uses, consume, locked, used, label, radius, priority))
        if entity_key: entity_bindings.append((entity_key, gid, active))
        return gid

    def human(name, npc_id, pos, yaw, body_mat, hair_mat, prompt, accent=None, height=1.0, active=1, entity_key=None, extra_prims=None):
        """<Name>_NPC root: NpcInteractable + NpcAgent, primitives in the character's canonical palette."""
        prims = [("Body", body_mat, CAPSULE, (0, 0.78 * height, 0), (0.55, 0.72 * height, 0.55)),
                 ("Head", "M_Npc_Civilian", SPHERE, (0, 1.62 * height, 0), (0.34, 0.34, 0.34)),
                 ("Hair", hair_mat, SPHERE, (0, 1.72 * height, -0.04), (0.36, 0.26, 0.36))]
        if accent: prims.append(accent)
        if extra_prims: prims += extra_prims
        gid, ids, children = emit_char_root(name, ["collider", "npc", "fate"], pos, (0, yaw, 0), active, prims)
        emit_capsulecollider(ids["collider"], gid, 0.35, 1.7 * height, (0, 0.85 * height, 0))
        emit_monobehaviour(ids["npc"], gid, REG["NpcInteractable.cs"],
            "  npc: {fileID: %d}\n  promptLabel: %s\n  interactRadius: 3.2\n  priority: 20" % (ids["fate"], prompt))
        emit_monobehaviour(ids["fate"], gid, REG["NpcAgent.cs"],
            "  npcId: %s\n  baseTitle: \"\"\n  playerRef: {fileID: 0}\n  bodyRenderer: {fileID: %d}\n  baseMaterial: {fileID: 2100000, guid: %s, type: 2}\n  avatarPrefab: {fileID: 0}\n  visualVariants: []"
            % (npc_id, child_renderer_id(children, "Body"), REG[body_mat]))
        if entity_key: entity_bindings.append((entity_key, gid, active))
        return gid, ids

    def foe(name, enemy_id, pos, yaw, mat, scale=1.0, active=1, entity_key=None, core_mat="M_Hall_OrbGold"):
        gid, ids, children = emit_char_root(name, ["collider", "enemy"], pos, (0, yaw, 0), active, [
            ("Body", mat, CAPSULE, (0, 1.05 * scale, 0), (0.62 * scale, 0.88 * scale, 0.62 * scale)),
            ("Head", mat, SPHERE, (0, 2.18 * scale, 0), (0.38 * scale, 0.38 * scale, 0.38 * scale)),
            ("Core", core_mat, SPHERE, (0, 1.45 * scale, 0.22 * scale), (0.16, 0.16, 0.16)),
            ("Pauldron_L", mat, CUBE, (-0.52 * scale, 1.72 * scale, 0), (0.34, 0.18, 0.30)),
            ("Pauldron_R", mat, CUBE, (0.52 * scale, 1.72 * scale, 0), (0.34, 0.18, 0.30)),
        ])
        emit_capsulecollider(ids["collider"], gid, 0.45 * scale, 2.4 * scale, (0, 1.2 * scale, 0))
        emit_monobehaviour(ids["enemy"], gid, REG["EnemyAgent.cs"],
            "  enemyId: %s\n  bodyRenderer: {fileID: %d}\n  baseMaterial: {fileID: 2100000, guid: %s, type: 2}\n  hitMaterial: {fileID: 2100000, guid: %s, type: 2}\n  sinkSeconds: 1.2"
            % (enemy_id, child_renderer_id(children, "Body"), REG[mat], REG["M_Choir_Hit"]))
        if entity_key is None: entity_key = enemy_id
        entity_bindings.append((entity_key, gid, active))
        return gid

    def dressing(name, pos, prims, active=0):
        gid, _, _ = emit_char_root(name, [], pos, (0, 0, 0), active, prims)
        return gid

    def spot(name, pos, yaw=0):
        sgid, sids = emit_gameobject(name, ["transform"])
        emit_transform(sids["transform"], sgid, pos, (0, yaw, 0), (1, 1, 1))
        root_gids.append(sgid)
        return sids["transform"]

    def q(s): return '"' + s.replace('"', '\\"') + '"'

    # ================================================================ P1 The Last Summer
    cx, cz = room("last_summer", "M_Env_Summer", "M_Hall_Concrete")
    sign("last_summer", "Tutorial_Controls", (cx, 0, cz - 4), "M_Hall_Holo", "How to play", "p1_tutorial", priority=30)
    action("PaperKite", (cx + 6, 0, cz + 6), [("Kite", "M_Seq_Ember", CUBE, (0, 0.6, 0), (0.9, 0.02, 0.9)), ("Tail", "M_Npc_Civilian", CUBE, (0, 0.3, -0.6), (0.06, 0.6, 0.06))],
           "Pick up the paper kite", [], [eff_yaml(0, "tut_moved", '"1"')], "kite_uses", 1, "paper_kite_prop",
           q("The kite is right there. Walk to it (left stick), then tap INTERACT."), q("Mara's kite. The string is broken."), "Paper Kite", entity_key="paper_kite_prop")
    action("PierBell", (cx - 7, 0, cz + 7), [("Post", "M_Hall_Metal", CUBE, (0, 1.0, 0), (0.2, 2.0, 0.2)), ("Bell", "M_Hall_OrbGold", SPHERE, (0, 2.1, 0), (0.4, 0.4, 0.4))],
           "Ring the pier bell", [cond_yaml(0, "tut_moved", '"1"')], [eff_yaml(10, "", amount=2)], "bell_uses", 3, "",
           q("The bell is for later. Find the kite first."), q("The bell rings out over the water. Mara looks up."), "Pier Bell")
    human("MaraYoung_NPC", "mara_young", (cx - 2, 0, cz + 2), 160, "M_Char_Mara_Dress", "M_Char_Mara", "Talk to Mara", height=0.7,
          accent=("KiteString", "M_Seq_Ember", CUBE, (0.25, 0.7, 0), (0.04, 0.5, 0.04)))
    # the water beyond the pier edge
    dressing("Pier_Water", (cx, 0, cz + 12), [("Water", "M_Env_Water", CUBE, (0, -0.1, 0), (24, 0.2, 6))], active=1)

    # ================================================================ C1L1 Night of the Fracture
    cx, cz = room("fracture_night", "M_Hall_Concrete", "M_Env_Contested", open_sides=("n",))
    sign("fracture_night", "Cutscene_FractureOpens", (cx, 0, cz - 5), "M_Boss_Echo", "The light comes down", "c1_fracture_open", priority=30)
    for m, mat, hair, px in (("Kael", "M_Char_Kael", "M_Npc_Civilian", -4), ("Odalys", "M_Char_Odalys", "M_Char_Hair_Silver", 0), ("Bran", "M_Char_Bran", "M_Char_Bran", 4)):
        human(m + "_NPC", m.lower(), (cx + px, 0, cz - 2), 180, mat, hair, "Talk to " + m, active=1, entity_key=m.lower(),
              accent=("Token", "M_Seq_Ember" if m == "Kael" else ("M_Seq_Tide" if m == "Odalys" else "M_Seq_Stone"), SPHERE, (0.3, 1.2, 0.2), (0.14, 0.14, 0.14)))
    human("Dax_NPC", "dax", (cx + 6, 0, cz + 3), 200, "M_Char_Dax_Blazer", "M_Char_Dax_Hair", "Talk to Dax",
          accent=("Glasses", "M_Hall_Glazing", CUBE, (0, 1.6, 0.17), (0.3, 0.06, 0.06)),
          extra_prims=[("Tee", "M_Char_Dax_Tee", CUBE, (0, 1.0, 0.2), (0.28, 0.5, 0.08))])
    # three arenas: grunts + chargers, dormant until the street is visited (activationConditions)
    foe("ChoirGrunt_A1", "choir_grunt", (cx - 6, 0, cz + 6), 180, "M_Choir_Grunt")
    foe("ChoirGrunt_A2", "choir_grunt", (cx + 6, 0, cz + 6), 180, "M_Choir_Grunt", entity_key="choir_grunt_2")
    foe("ChoirCharger_A2", "choir_charger", (cx, 0, cz + 8), 180, "M_Choir_Grunt", scale=1.1)
    foe("ChoirCharger_A3", "choir_charger", (cx - 4, 0, cz + 9), 180, "M_Choir_Grunt", scale=1.1, entity_key="choir_charger_2")
    action("StreetBarricade", (cx - 8, 0, cz + 2), [("Beam", "M_Hall_Metal", CUBE, (0, 0.5, 0), (2.2, 0.3, 0.3)), ("Post", "M_Hall_Metal", CUBE, (0, 0.5, 0.6), (0.3, 1.0, 0.3))],
           "Drag the barricade across the street", [], [eff_yaml(6, "fn_barricades", amount=1), eff_yaml(10, "", amount=3)], "fn_barricades", 2, "",
           q("Nothing to brace against yet."), q("The barricade scrapes across. One less lane for the Choir."), "Street Barricade")
    action("PharmacyDoor", (cx + 8, 0, cz + 1), [("Door", "M_Hall_Metal", CUBE, (0, 1.6, 0), (1.6, 3.2, 0.2)), ("Light", "M_Seq_Tide", SPHERE, (0, 3.4, 0), (0.3, 0.3, 0.3))],
           "Get the family through the door", [cond_yaml(16, "obj_fn_civilians")], [eff_yaml(0, "fn_family_saved", '"1"')], "family_uses", 1, "pharmacy_family",
           q("The shutter is down. Clear the street first."), q("Three of them, through the gap and gone up the stairs. Safe."), "Pharmacy Shutter", entity_key="pharmacy_family")
    dressing("Fracture_Sky", (cx, 12, cz), [("Tear", "M_Boss_Echo", CUBE, (0, 0, 0), (0.6, 0.6, 22))], active=1)

    # ================================================================ C1L2 Under the Spire
    cx, cz = room("under_spire", "M_Tidewell_Stone", "M_Hall_Concrete", open_sides=("s",))
    foe("ChoirCaster_S1", "choir_caster", (cx - 6, 0, cz + 5), 160, "M_Choir_Elite")
    foe("ChoirCaster_S2", "choir_caster", (cx + 6, 0, cz + 5), 200, "M_Choir_Elite", entity_key="choir_caster_2")
    foe("ChoirBruiser_S", "choir_bruiser", (cx, 0, cz + 2), 180, "M_Choir_Bruiser", scale=1.2)
    sign("under_spire", "Cutscene_FirstEchoIntro", (cx, 0, cz + 6), "M_Boss_Echo", "Something is forming", "c1_first_echo_intro", priority=27)
    foe("FirstEcho_Boss", "first_echo", (cx, 0, cz + 8), 180, "M_Boss_Echo", scale=1.45, core_mat="M_Seq_Tide")
    dressing("FirstEcho_Husk", (cx, 0, cz + 8), [("Husk", "M_Boss_Echo", CUBE, (0, 0.2, 0), (1.6, 0.4, 1.0)), ("Ribbon", "M_Seq_Tide", CUBE, (0.6, 0.6, 0.2), (0.2, 1.2, 0.2))])
    entity_bindings.append(("first_echo_husk", g["last_gid"](), 0))
    sign("under_spire", "Cutscene_FirstEchoFallen", (cx + 3, 0, cz + 8), "M_Seq_Tide", "The light settles", "c1_first_echo_fallen", active=0, priority=28, entity_key="first_echo_sign")
    action("SpireLift", (cx - 8, 0, cz - 6), [("Cage", "M_Hall_Metal", CUBE, (0, 1.5, 0), (1.4, 3.0, 1.4))],
           "Wrench the Spire lift open", [], [eff_yaml(10, "", amount=5), eff_yaml(9, "c1_lift_notes")], "lift_uses", 1, "",
           q("The lift is jammed."), q("The cage shrieks open. Someone left notes in Choir script inside."), "Spire Lift")

    # ================================================================ I2 Interlude: Becoming (shrine camp)
    cx, cz = room("interlude_becoming", "M_Hall_Concrete", "M_Hall_Concrete")
    human("Archivist_NPC", "archivist", (cx, 0, cz + 5), 180, "M_Char_Archivist", "M_Char_Hair_Silver", "Consult the Archivist",
          accent=("Halo", "M_Char_Archivist", CUBE, (0, 2.1, 0), (0.7, 0.04, 0.7)))
    archivist_tid = None
    camp_tid = spot("Loc_Mentor_InterludeCamp", (cx + 4, 0, cz - 3), 200)
    for m in ("kael", "odalys", "bran"):
        relocations.append((m, "interlude_camp", camp_tid, "Your mentor has made camp above the Reaches."))
    human("MaraC2_NPC", "mara_c2", (cx - 4, 0, cz + 4), 150, "M_Char_Mara_Hoodie", "M_Char_Mara", "Talk to Mara", entity_key="mara_c2",
          accent=("KiteString", "M_Seq_Ember", CUBE, (0.28, 0.9, 0), (0.05, 0.05, 0.12)))
    dressing("Camp_Fire", (cx, 0, cz - 6), [("Logs", "M_Hall_Metal", CUBE, (0, 0.15, 0), (1.0, 0.3, 1.0)), ("Flame", "M_Seq_Ember", SPHERE, (0, 0.6, 0), (0.5, 0.7, 0.5))], active=1)

    # ================================================================ C2A Contested Docks
    cx, cz = room("docks", "M_Hall_Concrete", "M_Env_Contested", open_sides=("w",))
    sign("docks", "Cutscene_DocksOpen", (cx, 0, cz - 5), "M_Env_Contested", "The Docks", "c2_docks_open", priority=30)
    for i, (dx, dz) in enumerate(((-6, 2), (6, 2), (-6, 7), (6, 7))):
        foe("ChoirSentinel_D%d" % i, "choir_sentinel", (cx + dx, 0, cz + dz), 180, "M_Choir_Grunt", entity_key="choir_sentinel_d%d" % i)
    for i, (dx, dz) in enumerate(((-2, 5), (2, 5), (0, 8), (-3, 8))):
        foe("ChoirLancer_D%d" % i, "choir_lancer", (cx + dx, 0, cz + dz), 180, "M_Choir_Grunt", scale=1.1, entity_key="choir_lancer_d%d" % i)
    foe("ChoirElite_Docks", "choir_elite", (cx, 0, cz + 9), 180, "M_Choir_Elite", scale=1.3, entity_key="choir_elite_docks")
    sign("docks", "FuelShed", (cx + 8, 0, cz - 7), "M_Seq_Ember", "The fuel shed", "c2_docks_shed", priority=26)
    dressing("Docks_Fire", (cx + 8, 0, cz - 7), [("Blaze", "M_Seq_Ember", SPHERE, (0, 1.5, 0), (3.0, 3.0, 3.0))])
    entity_bindings.append(("docks_fire", g["last_gid"](), 0))
    # the crane moment (shared by all three paths; spawned by the Elite's fall)
    crane_gid = sign("docks", "Cutscene_MaraCrane", (cx - 3, 0, cz - 2), "M_Seq_Tide", "Mara - the crane!", "c2_mara_pressure", active=0, priority=31, entity_key="mara_crane")
    # docks variants (StoryWorldState.areaVariants)
    area_variants.append(("docks", "working", dressing("Docks_Working", (cx, 0, cz - 8), [("Crane", "M_Hall_Metal", CUBE, (0, 4, 0), (0.5, 8, 0.5)), ("Arm", "M_Hall_Metal", CUBE, (2, 8, 0), (5, 0.4, 0.4))])))
    area_variants.append(("docks", "flooded", dressing("Docks_Flooded", (cx, 0, cz), [("Flood", "M_Env_Water", CUBE, (0, 0.12, 0), (18, 0.2, 18))])))
    area_variants.append(("docks", "fortified", dressing("Docks_Fortified", (cx, 0, cz - 8), [("Wall", "M_Env_Ruin", CUBE, (0, 1.5, 0), (14, 3, 0.8))])))
    area_variants.append(("docks", "contested", dressing("Docks_Contested", (cx - 8, 0, cz - 8), [("Graffiti", "M_Env_Contested", CUBE, (0, 2, 0), (0.1, 3, 6))])))

    # ================================================================ C2B The Sanctuary
    cx, cz = room("sanctuary", "M_Tidewell_Stone", "M_Hall_Concrete", open_sides=("w",))
    sign("sanctuary", "Cutscene_SanctuaryOpen", (cx, 0, cz - 5), "M_Env_Water", "The Sanctuary", "c2_sanctuary_open", priority=30)
    dressing("Sanctuary_Pool", (cx, 0, cz + 4), [("Water", "M_Env_Water", CUBE, (0, 0.1, 0), (12, 0.2, 10))], active=1)
    dressing("Sanctuary_Altar", (cx, 0, cz + 8), [("Altar", "M_Tidewell_Stone", CUBE, (0, 0.6, 0), (2, 1.2, 1)), ("Light", "M_Seq_Tide", SPHERE, (0, 1.6, 0), (0.5, 0.5, 0.5))], active=1)
    for i, (dx, dz) in enumerate(((-6, 0), (6, 0), (-6, 6), (6, 6))):
        foe("ChoirSentinel_S%d" % i, "choir_sentinel", (cx + dx, 0, cz + dz), 180, "M_Choir_Grunt", entity_key="choir_sentinel_s%d" % i)
    for i, (dx, dz) in enumerate(((-2, 3), (2, 3), (0, 7), (3, 7))):
        foe("ChoirLancer_S%d" % i, "choir_lancer", (cx + dx, 0, cz + dz), 180, "M_Choir_Grunt", scale=1.1, entity_key="choir_lancer_s%d" % i)
    foe("ChoirElite_Sanctuary", "choir_elite", (cx, 0, cz - 2), 0, "M_Choir_Elite", scale=1.3, entity_key="choir_elite_sanctuary")
    action("SanctuarySluice", (cx - 8, 0, cz + 2), [("Wheel", "M_Hall_Metal", CUBE, (0, 1.0, 0), (0.9, 0.9, 0.2))],
           "Close the sluice", [cond_yaml(0, "c2_path", "sanctuary")], [eff_yaml(10, "", amount=4), eff_yaml(6, "sluices_closed", amount=1)], "sluices_closed", 3, "",
           q("The sluice is not yours to close."), q("The wheel turns. The water drops a hand's width from the altar steps."), "Sluice Wheel")
    action("SanctuaryBreach", (cx + 8, 0, cz + 2), [("Crack", "M_Env_Ruin", CUBE, (0, 1.0, 0), (0.6, 2.0, 0.6)), ("Rush", "M_Env_Water", SPHERE, (0, 0.4, 0), (0.8, 0.5, 0.8))],
           "Hold the breach", [cond_yaml(0, "c2_path", "sanctuary")], [eff_yaml(6, "sanctuary_breaches", amount=1)], "sanctuary_breaches", 3, "",
           q("Nothing breaks here yet."), q("The water takes another step. If it reaches the altar three times, the Sanctuary is lost."), "The Breach (danger)", priority=10)
    sign("sanctuary", "Cutscene_MaraCrane_S", (cx - 3, 0, cz - 2), "M_Seq_Tide", "Mara - the crane!", "c2_mara_pressure", active=0, priority=31, entity_key="mara_crane_s")

    # ================================================================ C2C The Long Wall
    cx, cz = room("long_wall", "M_Hall_Concrete", "M_Env_Ruin", open_sides=("n",))
    sign("long_wall", "Cutscene_WallOpen", (cx, 0, cz - 5), "M_Seq_Stone", "The Long Wall", "c2_wall_open", priority=30)
    dressing("Wall_Gate", (cx, 0, cz + 9), [("Gate", "M_Hall_Metal", CUBE, (0, 2, 0), (5, 4, 0.5)), ("Tower_L", "M_Env_Ruin", CUBE, (-4, 3, 0), (2, 6, 2)), ("Tower_R", "M_Env_Ruin", CUBE, (4, 3, 0), (2, 6, 2))], active=1)
    for i, (dx, dz) in enumerate(((-6, 4), (6, 4), (-3, 7), (3, 7))):
        foe("ChoirSentinel_W%d" % i, "choir_sentinel", (cx + dx, 0, cz + dz), 180, "M_Choir_Grunt", entity_key="choir_sentinel_w%d" % i)
    for i, (dx, dz) in enumerate(((-6, 0), (6, 0), (0, 5), (-2, 8))):
        foe("ChoirLancer_W%d" % i, "choir_lancer", (cx + dx, 0, cz + dz), 180, "M_Choir_Grunt", scale=1.1, entity_key="choir_lancer_w%d" % i)
    foe("ChoirElite_Wall", "choir_elite", (cx, 0, cz + 6), 180, "M_Choir_Elite", scale=1.3, entity_key="choir_elite_wall")
    action("WallGateBrace", (cx - 3, 0, cz + 7), [("Brace", "M_Hall_Metal", CUBE, (0, 0.6, 0), (0.35, 1.2, 0.25))],
           "Brace the gate", [cond_yaml(0, "c2_path", "long_wall")], [eff_yaml(10, "", amount=4), eff_yaml(6, "gate_braces", amount=1)], "gate_braces", 3, "",
           q("The Wardens have not asked for your hands here."), q("The brace bites. The gate will hold one more push."), "Gate Brace")
    action("WallBreach", (cx + 8, 0, cz + 4), [("Gap", "M_Env_Ruin", CUBE, (0, 1.0, 0), (0.6, 2.0, 1.2))],
           "The gate gives", [cond_yaml(0, "c2_path", "long_wall")], [eff_yaml(6, "wall_breaches", amount=1)], "wall_breaches", 3, "",
           q("The wall holds here."), q("The gate splinters another hand's width. Three breaches and the Outskirts are theirs."), "The Gate (danger)", priority=10)
    sign("long_wall", "Cutscene_MaraCrane_W", (cx - 3, 0, cz - 2), "M_Seq_Tide", "Mara - the crane!", "c2_mara_pressure", active=0, priority=31, entity_key="mara_crane_w")

    # ================================================================ C2X Dax Confrontation
    cx, cz = room("dax_arena", "M_Hall_Concrete", "M_Env_Ruin")
    sign("dax_arena", "Cutscene_DaxConfront", (cx, 0, cz - 4), "M_Char_Dax_Blazer", "Dax is waiting", "c2_dax_confront", priority=30)
    foe("Dax_Rival_Boss", "dax_rival", (cx, 0, cz + 4), 180, "M_Char_Dax_Blazer", scale=1.05, core_mat="M_Char_Dax_Tee")
    foe("ChoirHunter_Boss", "choir_hunter", (cx + 6, 0, cz + 7), 200, "M_Choir_Elite", scale=1.35)
    sign("dax_arena", "Cutscene_DaxDown", (cx, 0, cz + 4), "M_Char_Dax_Blazer", "Dax is down", "c2_dax_duel_end", active=0, priority=31, entity_key="dax_down")
    sign("dax_arena", "Cutscene_HunterFallen", (cx + 6, 0, cz + 7), "M_Seq_Tide", "The Hunter falls", "c2_dax_hunter_fallen", active=0, priority=31, entity_key="hunter_fallen")
    dressing("Dax_Yielded", (cx + 2, 0, cz + 2), [("Glasses", "M_Hall_Glazing", CUBE, (0, 0.05, 0), (0.3, 0.04, 0.06))])
    entity_bindings.append(("dax_yielded", g["last_gid"](), 0))
    dressing("Arena_Ridge", (cx, 0, cz + 9), [("Ridge", "M_Env_Ruin", CUBE, (0, 1.2, 0), (18, 2.4, 1.2))], active=1)

    # ================================================================ I3 Interlude: Reckoning
    cx, cz = room("interlude_reckoning", "M_Hall_Concrete", "M_Hall_Concrete")
    reck_tid = spot("Loc_Archivist_Reckoning", (cx, 0, cz + 5), 180)
    relocations.append(("archivist", "reckoning_camp", reck_tid, "The Archivist waits at the reckoning shrine below the Spire."))
    sign("interlude_reckoning", "ReckoningShrine", (cx + 5, 0, cz + 2), "M_Hall_OrbGold", "Echo Shrine", "i3_shrine", priority=24)
    sign("interlude_reckoning", "DarkPlinth", (cx - 5, 0, cz + 2), "M_Hollow", "A dark plinth", "i3_hollow_shrine", priority=24, entity_key="hollow_shrine")
    human("MaraC3_NPC", "mara_c3", (cx - 4, 0, cz - 4), 30, "M_Char_Mara_Hoodie", "M_Char_Mara", "Talk to Mara", entity_key="mara_c3",
          accent=("KiteString", "M_Seq_Ember", CUBE, (0.28, 0.9, 0), (0.05, 0.05, 0.12)))

    # ================================================================ C3L1 The Old Market (variant by world state)
    cx, cz = room("market", "M_Hall_Concrete", "M_Hall_Concrete", open_sides=("e",))
    sign("market", "Cutscene_MarketOpen", (cx, 0, cz - 5), "M_Env_Rebuilt", "The Old Market", "c3_market_open", priority=30)
    area_variants.append(("market", "intact", dressing("Market_Intact", (cx, 0, cz + 2), [("Stall_A", "M_Env_Rebuilt", CUBE, (-5, 1, 0), (2.4, 2, 1.2)), ("Stall_B", "M_Env_Rebuilt", CUBE, (5, 1, 0), (2.4, 2, 1.2))])))
    area_variants.append(("market", "contested", dressing("Market_Contested", (cx, 0, cz + 2), [("Stall_A", "M_Env_Contested", CUBE, (-5, 1, 0), (2.4, 2, 1.2)), ("Patrol_Mark", "M_Env_Contested", CUBE, (5, 0.05, 0), (3, 0.1, 3))])))
    area_variants.append(("market", "ruined", dressing("Market_Ruined", (cx, 0, cz + 2), [("Rubble_A", "M_Env_Ruin", CUBE, (-5, 0.4, 0), (2.6, 0.8, 1.6)), ("Rubble_B", "M_Env_Ruin", CUBE, (5, 0.3, 0), (2.0, 0.6, 1.4))])))
    area_variants.append(("market", "rebuilt", dressing("Market_Rebuilt", (cx, 0, cz + 2), [("Stall_A", "M_Env_Rebuilt", CUBE, (-5, 1, 0), (2.4, 2, 1.2)), ("Stall_B", "M_Env_Rebuilt", CUBE, (5, 1, 0), (2.4, 2, 1.2)), ("Lantern", "M_Hall_OrbGold", SPHERE, (0, 3, 0), (0.5, 0.5, 0.5))])))
    dressing("Market_Fountain", (cx, 0, cz + 7), [("Basin", "M_Tidewell_Stone", CUBE, (0, 0.4, 0), (3, 0.8, 3))], active=1)
    for i, (dx, dz) in enumerate(((-6, 3), (6, 3), (0, 5))):
        foe("HollowHusk_M%d" % i, "hollow_husk", (cx + dx, 0, cz + dz), 180, "M_Hollow", entity_key="hollow_husk_%d" % i)
    foe("ChoirCantor_Boss", "choir_cantor", (cx, 0, cz + 7), 180, "M_Choir_Elite", scale=1.4, core_mat="M_Hollow")

    # ================================================================ C3L2 Ascent of the Spire
    cx, cz = room("spire_ascent", "M_Hall_Concrete", "M_Hall_Concrete")
    sign("spire_ascent", "Cutscene_AscentOpen", (cx, 0, cz - 5), "M_Hall_Holo", "The breached Spire", "c3_ascent_open", priority=30)
    dressing("Spire_TiltedFloor", (cx, 0, cz + 3), [("Slab_A", "M_Hall_Concrete", CUBE, (-4, 1.0, 0), (6, 0.3, 6)), ("Slab_B", "M_Hall_Concrete", CUBE, (4, 2.0, 2), (6, 0.3, 6))], active=1)
    for i, (dx, dz) in enumerate(((-6, 6), (6, 6))):
        action("GravityAnomaly_%d" % i, (cx + dx, 0, cz + dz), [("Core", "M_Hall_Holo", SPHERE, (0, 1.6, 0), (0.9, 0.9, 0.9)), ("Dust", "M_Npc_Civilian", CUBE, (0, 3.0, 0), (0.1, 1.5, 0.1))],
               "Anchor the gravity anomaly", [], [eff_yaml(6, "anomaly_count", amount=1), eff_yaml(10, "", amount=5)], "anomaly_%d" % i, 1, "",
               q("The anomaly spins out of reach."), q("The anomaly locks. Dust falls down again, for a moment."), "Gravity Anomaly")
    foe("SpireWarden_A0", "spire_warden", (cx - 3, 0, cz + 4), 180, "M_Choir_Bruiser", scale=1.25, entity_key="spire_warden_0")
    foe("SpireWarden_A1", "spire_warden", (cx + 3, 0, cz + 8), 180, "M_Choir_Bruiser", scale=1.25, entity_key="spire_warden_1")
    area_variants.append(("spire", "sealed", dressing("Spire_Sealed", (cx, 0, cz + 9), [("Seal", "M_Hall_Holo", CUBE, (0, 2, 0), (8, 4, 0.3))])))
    area_variants.append(("spire", "breached", dressing("Spire_Breached", (cx, 0, cz + 9), [("Gap_L", "M_Hall_Holo", CUBE, (-3.5, 2, 0), (1.5, 4, 0.3)), ("Gap_R", "M_Hall_Holo", CUBE, (3.5, 2, 0), (1.5, 4, 0.3))])))
    area_variants.append(("spire", "collapsed", dressing("Spire_Collapsed", (cx, 0, cz + 9), [("Rubble", "M_Hollow", CUBE, (0, 1, 0), (9, 2, 2))])))

    # ================================================================ C3B The Choirmaster
    cx, cz = room("choirmaster", "M_Tidewell_Stone", "M_Hall_Concrete")
    sign("choirmaster", "Cutscene_ChoirmasterOpen", (cx, 0, cz - 5), "M_Boss_Choirmaster", "The Choirmaster", "c3_cm_open", priority=30)
    foe("Choirmaster_P1", "choirmaster_p1", (cx, 0, cz + 6), 180, "M_Boss_Choirmaster", scale=1.5, core_mat="M_Hollow")
    foe("Choirmaster_P2", "choirmaster_p2", (cx, 0, cz + 6), 180, "M_Boss_Choirmaster", scale=1.6, active=0, core_mat="M_Hollow")
    foe("Choirmaster_P3", "choirmaster_p3", (cx, 0, cz + 6), 180, "M_Hollow", scale=1.7, active=0, core_mat="M_Boss_Choirmaster")
    sign("choirmaster", "Cutscene_DoorInTheSong", (cx - 6, 0, cz), "M_Hall_Holo", "A door in the song", "c3_cm_transition", active=0, priority=32, entity_key="cm_door")
    sign("choirmaster", "Cutscene_PhaseTwo", (cx + 6, 0, cz), "M_Boss_Choirmaster", "The chorus", "c3_cm_phase2", active=0, priority=29, entity_key="cm_finale_sign")
    sign("choirmaster", "Cutscene_PhaseThree", (cx + 6, 0, cz - 3), "M_Hollow", "The finale", "c3_cm_phase3", active=0, priority=29, entity_key="cm_finale_sign_2")
    # fate inserts (phase two): enemies AND allies
    foe("Dax_FinalEnemy", "dax_final", (cx - 4, 0, cz + 3), 160, "M_Char_Dax_Blazer", scale=1.05, active=0, core_mat="M_Hollow")
    foe("Mara_Turned", "mara_turned", (cx + 4, 0, cz + 3), 200, "M_Char_Mara_Hoodie", scale=1.0, active=0, core_mat="M_Hollow")
    human("MaraAlly_CM_NPC", "mara_c3", (cx - 4, 0, cz + 1), 20, "M_Char_Mara_Hoodie", "M_Char_Mara", "Mara", active=0, entity_key="mara_ally_cm")
    human("DaxAlly_CM_NPC", "dax", (cx + 4, 0, cz + 1), -20, "M_Char_Dax_Blazer", "M_Char_Dax_Hair", "Dax", active=0, entity_key="dax_ally_cm")
    human("Kael_CM_NPC", "kael", (cx, 0, cz - 2), 0, "M_Char_Kael", "M_Npc_Civilian", "Kael", active=0, entity_key="kael_cm")
    human("Odalys_CM_NPC", "odalys", (cx, 0, cz - 2), 0, "M_Char_Odalys", "M_Char_Hair_Silver", "Odalys", active=0, entity_key="odalys_cm")
    human("Bran_CM_NPC", "bran", (cx, 0, cz - 2), 0, "M_Char_Bran", "M_Char_Bran", "Bran", active=0, entity_key="bran_cm")
    dressing("Fracture_Heart", (cx, 0, cz + 6), [("Heart", "M_Boss_Echo", SPHERE, (0, 1.5, 0), (2.2, 2.2, 2.2))])
    heart_gid = g["last_gid"]()
    entity_bindings.append(("fracture_heart", heart_gid, 0))
    sign("choirmaster", "FinalDecision", (cx, 0, cz + 3), "M_Hall_OrbGold", "The Fracture's heart", "c3_final_decision", active=0, priority=33, entity_key="fracture_heart_sign")
    # the final-decision sign follows the heart: same key spawns both
    entity_bindings.append(("fracture_heart", g["last_gid"](), 0))

    # ================================================================ EP Epilogue
    cx, cz = room("epilogue", "M_Env_Summer", "M_Hall_Concrete", open_sides=("n",))
    pier_tid = spot("Loc_Archivist_Pier", (cx, 0, cz + 4), 180)
    relocations.append(("archivist", "epilogue_pier", pier_tid, "The Archivist is at the pier, keeping the last of the count."))
    sign("epilogue", "Cutscene_Epilogue", (cx, 0, cz + 2), "M_Char_Archivist", "Hear the count", "ep_epilogue", priority=30)
    action("MemorialStone", (cx - 6, 0, cz + 6), [("Stone", "M_Tidewell_Stone", CUBE, (0, 0.8, 0), (1.2, 1.6, 0.4))],
           "Touch the memorial stone", [], [eff_yaml(9, "ep_memorial"), eff_yaml(10, "", amount=1)], "memorial_uses", 1, "",
           q("The stone is warm."), q("Names, cut small. You know most of them."), "Memorial Stone")
    dressing("Pier_Water_After", (cx, 0, cz + 12), [("Water", "M_Env_Water", CUBE, (0, -0.1, 0), (24, 0.2, 6))], active=1)
    for variant, mat in (("ashen", "M_Seq_Ember"), ("healed", "M_Seq_Tide"), ("sealed", "M_Seq_Stone"), ("hollow", "M_Hollow"), ("balanced", "M_Hall_OrbGold"), ("dawn", "M_Char_Archivist"), ("quiet", "M_Env_Summer")):
        area_variants.append(("vessa", variant, dressing("Vessa_" + variant.capitalize(), (cx + 7, 0, cz - 6), [("Sky", mat, CUBE, (0, 4, 0), (0.4, 8, 0.4)), ("Horizon", mat, CUBE, (0, 8, 0), (8, 0.3, 0.3))])))

    # ================================================================ per-location environment kits (validate §5)
    KITS = {"last_summer": ("LastSummer", "Env_LastSummer_Gold"), "fracture_night": ("FractureNight", "Env_FractureNight_Violet"),
            "under_spire": ("UnderSpire", "Env_UnderSpire_Root"), "interlude_becoming": ("InterludeBecoming", "Env_InterludeBecoming_Dusk"),
            "docks": ("ContestedDocks", "Env_Docks_Rust"), "sanctuary": ("Sanctuary", "Env_Sanctuary_Teal"), "long_wall": ("LongWall", "Env_LongWall_Grey"),
            "dax_arena": ("DaxArena", "Env_DaxArena_Dusk"), "interlude_reckoning": ("InterludeReckoning", "Env_InterludeReckoning_Blue"),
            "market": ("OldMarket", "Env_Market_Ember"), "spire_ascent": ("SpireAscent", "Env_SpireAscent_White"),
            "choirmaster": ("Choirmaster", "Env_Choirmaster_Gold"), "epilogue": ("Epilogue", "Env_Epilogue_Dawn")}
    def hex_rgb(h):
        return tuple("%.3f" % (int(h[i:i + 2], 16) / 255.0) for i in (0, 2, 4))
    for loc_id, (kit_dir, go_name) in KITS.items():
        env = loc_by_id[loc_id]["environment"]
        env_prefab(kit_dir, "Env_%s.prefab" % kit_dir, go_name, hex_rgb(env["sun"]), str(env["sunIntensity"]))
    return KITS
