#!/usr/bin/env python3
"""Renders PROJECT_MAP.png - a stylized blueprint of Assets/Scenes/Prototype/FirstLocation.unity
drawn from the REAL scene data (parses GameObjects/Transforms, footprints from the kit
collider table). Active objects solid, data-driven inactive entities ghosted."""
import re, math
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Circle, FancyBboxPatch
from matplotlib.lines import Line2D
from matplotlib.collections import PatchCollection

SCENE = "Assets/Scenes/Prototype/FirstLocation.unity"
txt = open(SCENE).read()

# ---------------------------------------------------------------- parse scene
gos, trs = {}, {}
for block in txt.split("--- !u!")[1:]:
    head = block.split("\n", 1)[0]              # e.g. "1 &1002"
    m = re.match(r"(\d+) &(\d+)", head.strip())
    if not m:
        continue
    kind, oid = int(m.group(1)), int(m.group(2))
    if kind == 1:
        nm = re.search(r"m_Name: (.*)", block)
        act = re.search(r"m_IsActive: (\d)", block)
        gos[oid] = {"name": nm.group(1).strip() if nm else "?", "active": int(act.group(1)) if act else 1}
    elif kind == 4:
        pos = re.search(r"m_LocalPosition: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", block)
        fth = re.search(r"m_Father: \{fileID: (\d+)\}", block)
        eul = re.search(r"m_LocalEulerAnglesHint: \{x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)\}", block)
        trs[oid] = {
            "pos": (float(pos.group(1)), float(pos.group(2)), float(pos.group(3))) if pos else (0, 0, 0),
            "father": int(fth.group(1)) if fth else 0,
            "yaw": float(eul.group(2)) if eul else 0.0,
            "go": None,
        }

tid_of_go = {}
for t in trs.values():
    pass
# map GameObject -> its transform: the GO block lists components; instead map transform->go via m_GameObject
for block in txt.split("--- !u!")[1:]:
    head = block.split("\n", 1)[0]
    m = re.match(r"4 &(\d+)", head.strip())
    if not m:
        continue
    tid = int(m.group(1))
    g = re.search(r"m_GameObject: \{fileID: (\d+)\}", block)
    if g:
        trs[tid]["go"] = int(g.group(1))

memo = {}
def world(tid):
    if tid not in memo:
        t = trs[tid]
        f = t["father"] if t["father"] in trs else 0
        memo[tid] = t["pos"] if f == 0 else tuple(a + b for a, b in zip(world(f), t["pos"]))
    return memo[tid]

# collect (name, active, world x/z, yaw) for every transform that has a GameObject
objects = []
for tid, t in trs.items():
    if t["go"] is not None and t["go"] in gos:
        g = gos[t["go"]]
        w = world(tid)
        objects.append({"name": g["name"], "active": g["active"], "x": w[0], "z": w[2], "y": w[1], "yaw": t["yaw"]})

# ---------------------------------------------------------------- palette
BG = "#101216"
FLOOR = "#1d2026"
FLOOR_EDGE = "#2a2e36"
COL_FILL, COL_EDGE = "#767d8a", "#3a3f49"
WALL_FILL, WALL_EDGE = "#3f444e", "#22252b"
GOLD, EMBER, TIDE, STONE = "#e8c56a", "#ff7a3d", "#4fc3d9", "#b9a48a"
GHOST = "#8b93a3"
PLAYER = "#ffffff"

# kit footprints (name, w(x), d(z)) - rotated by yaw when ~90/270
FOOT = {
    "SM_FloorTile": (10, 10), "SM_Column": (1.15, 1.15), "SM_WallPanel": (10, 0.55),
    "SM_BalconyBlock": (10, 3.0), "SM_Railing": (10, 0.12), "SM_DoorFrame": (4.9, 1.0),
    "SM_Door": (3.6, 0.26), "SM_GlazingPanel": (10, 0.3), "SM_LightBeam": (2.4, 2.4),
    "SM_OrbCore": (1.0, 1.0), "SM_OrbRing": (3.0, 3.0), "SM_HoloPanel": (2.2, 0.3),
}

fig, ax = plt.subplots(figsize=(15, 16), dpi=140)
fig.patch.set_facecolor(BG)
ax.set_facecolor(BG)
ax.set_aspect("equal")

# faint 5 m grid
for g in range(-25, 35, 5):
    ax.axhline(g, color="#191c22", lw=0.6, zorder=0)
    ax.axvline(g, color="#191c22", lw=0.6, zorder=0)

def rotated_rect(x, z, w, d, yaw):
    if abs((yaw % 360) - 90) < 5 or abs((yaw % 360) - 270) < 5:
        w, d = d, w
    return Rectangle((x - w / 2, z - d / 2), w, d)

def rect(x, z, w, d, yaw, **props):
    r = rotated_rect(x, z, w, d, yaw)
    r.set(**props)
    ax.add_patch(r)
    return r

# ---------------------------------------------------------------- draw kit pieces
glows = []
for o in objects:
    n = o["name"]
    base = n.split("_annex_")[0] if "_annex_" in n else n
    if "_" in base and base.split("_")[0] == "SM":
        base = base[: base.rfind("_")] if base[-1].isdigit() else base
    # normalize e.g. SM_FloorTile_003 -> SM_FloorTile ; SM_WallPanel_flank_w -> SM_WallPanel
    m = re.match(r"(SM_[A-Za-z]+)", n)
    key = m.group(1) if m else None
    if key not in FOOT:
        continue
    w, d = FOOT[key]
    if key == "SM_FloorTile":
        rect(o["x"], o["z"], w, d, o["yaw"], facecolor=FLOOR, edgecolor=FLOOR_EDGE, linewidth=0.8)
    elif key == "SM_Column":
        ax.add_patch(Circle((o["x"], o["z"]), 0.62, facecolor=COL_FILL, edgecolor=COL_EDGE, lw=1.0, zorder=6))
    elif key == "SM_LightBeam":
        glows.append((o["x"], o["z"], 1.15, "#f5e2a8", 0.10))
        glows.append((o["x"], o["z"], 0.45, "#ffe9b0", 0.28))
        ax.add_patch(Circle((o["x"], o["z"]), 0.5, facecolor="#ffe9b0", edgecolor="none", alpha=0.85, zorder=7))
    elif key == "SM_OrbCore":
        ax.add_patch(Circle((o["x"], o["z"]), 0.5, facecolor=GOLD, edgecolor="none", zorder=7))
        glows.append((o["x"], o["z"], 1.0, GOLD, 0.12))
    elif key == "SM_OrbRing":
        ax.add_patch(Circle((o["x"], o["z"]), 1.5, facecolor="none", edgecolor=GOLD, lw=1.4, alpha=0.8, zorder=7))
    elif key == "SM_GlazingPanel":
        rect(o["x"], o["z"], w, d, o["yaw"], facecolor=TIDE, alpha=0.35, edgecolor=TIDE, linewidth=0.8)
    elif key == "SM_Door":
        rect(o["x"], o["z"], w, d, o["yaw"], facecolor="#7a6a55", edgecolor=WALL_EDGE, linewidth=0.8)
    elif key == "SM_HoloPanel":
        rect(o["x"], o["z"], w, d, o["yaw"], facecolor=TIDE, alpha=0.5, edgecolor="none")
    else:  # walls, balconies, railings, doorframes
        rect(o["x"], o["z"], w, d, o["yaw"], facecolor=WALL_FILL, edgecolor=WALL_EDGE, linewidth=0.9)

for gx, gz, r, c, a in glows:
    ax.add_patch(Circle((gx, gz), r, facecolor=c, alpha=a, edgecolor="none", zorder=3))

# ---------------------------------------------------------------- story actors & entities
def find(name):
    for o in objects:
        if o["name"] == name:
            return o
    return None

def dot(pos, color, label=None, lx=1.4, lz=1.1, ghost=False, star=False):
    if pos is None:
        return
    x, z = pos["x"], pos["z"]
    alpha = 0.35 if ghost else 1.0
    if star:
        ang = math.pi / 2
        pts = []
        for i in range(10):
            r = 0.95 if i % 2 == 0 else 0.4
            a = ang + i * math.pi / 5
            pts.append((x + r * math.cos(a), z + r * math.sin(a)))
        ax.add_patch(plt.Polygon(pts, facecolor=color, edgecolor="none", alpha=alpha, zorder=10))
    else:
        ax.add_patch(Circle((x, z), 0.55, facecolor=color, edgecolor="#0c0d10", lw=1.0, alpha=alpha, zorder=10))
    if label:
        ax.annotate(label, (x, z), xytext=(x + lx, z + lz), fontsize=8.5, color="#dfe3ea" if not ghost else GHOST,
                    alpha=1.0 if not ghost else 0.75, zorder=12,
                    path_effects=[], fontweight="bold" if not ghost else "normal")

import matplotlib.patheffects as pe
for t in ax.texts:
    t.set_path_effects([pe.withStroke(linewidth=2.6, foreground=BG)])

# player spawn (FirstLocationBootstrap transform carries the spawn position)
spawn = find("FirstLocationBootstrap")
if spawn:
    x, z = spawn["x"], spawn["z"]
    ax.annotate("", xy=(x, z + 1.6), xytext=(x, z - 1.2),
                arrowprops=dict(arrowstyle="-|>", color=PLAYER, lw=2.4), zorder=11)
    ax.add_patch(Circle((x, z), 0.75, facecolor="none", edgecolor=PLAYER, lw=1.6, zorder=11))
    ax.text(x, z - 2.6, "ARI — spawn", color=PLAYER, fontsize=9, fontweight="bold", ha="center", zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])

# NPCs
dot(find("Mara_NPC"), TIDE, "MARA", 1.2, 1.0)
dot(find("Sera_NPC"), EMBER, "SERA", 1.2, 1.0)

by = find("Seq_Tide_Bystanders")
if by:
    for dx in (0.0, 1.1):
        ax.add_patch(Circle((by["x"] + dx, by["z"]), 0.42, facecolor=GHOST, edgecolor="none", zorder=10))
    ax.text(by["x"] + 2.0, by["z"] + 0.1, "the twins", color=GHOST, fontsize=8, zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])
calm = find("Seq_Tide_Calm")
if calm:
    ax.add_patch(Circle((calm["x"], calm["z"]), 0.42, facecolor=GHOST, alpha=0.3, edgecolor="none", zorder=10))
    ax.text(calm["x"] + 1.6, calm["z"] + 0.1, "twins, calmed (spawn)", color=GHOST, alpha=0.8, fontsize=7.5, zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])

# ---- COMBAT: the Choir Warden + rings
w = find("ChoirWarden")
if w:
    wx, wz = w["x"], w["z"]
    ax.add_patch(Circle((wx, wz), 15, facecolor="none", edgecolor=GOLD, lw=0.9, ls=(0, (2, 4)), alpha=0.35, zorder=4))
    ax.add_patch(Circle((wx, wz), 9, facecolor=GOLD, alpha=0.05, edgecolor=GOLD, lw=1.2, ls=(0, (5, 4)), zorder=4))
    ax.add_patch(Circle((wx, wz), 2.3, facecolor="none", edgecolor="#ff5c5c", lw=1.0, ls=(0, (3, 3)), alpha=0.8, zorder=4))
    ang = math.pi / 2
    pts = []
    for i in range(10):
        r = 1.5 if i % 2 == 0 else 0.62
        a = ang + i * math.pi / 5
        pts.append((wx + r * math.cos(a), wz + r * math.sin(a)))
    ax.add_patch(plt.Polygon(pts, facecolor=GOLD, edgecolor="#0c0d10", lw=1.2, zorder=10))
    ax.add_patch(Circle((wx, wz), 0.28, facecolor="#fff3d0", edgecolor="none", zorder=11))
    ax.text(wx, wz + 2.9, "CHOIR WARDEN", color=GOLD, fontsize=10, fontweight="bold", ha="center", zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])
    ax.text(wx + 9.3, wz + 0.2, "detect 9 m", color=GOLD, alpha=0.85, fontsize=7.5, zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])
    ax.text(wx - 15.4, wz + 0.2, "leash 15 m", color=GOLD, alpha=0.5, fontsize=7.5, zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])
    ax.text(wx + 2.6, wz - 2.6, "smite 2.3 m", color="#ff8f8f", alpha=0.9, fontsize=7, zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])

wr = find("WardenWreckage")
if wr:
    ax.add_patch(Circle((wr["x"], wr["z"]), 0.9, facecolor="none", edgecolor=GHOST, lw=1.1, ls=(0, (2, 2)), alpha=0.5, zorder=9))
    ax.text(wr["x"] - 1.2, wr["z"] - 2.1, "wreckage (on defeat)", color=GHOST, alpha=0.75, fontsize=7.5, ha="center", zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])

# interactables / world objects
def diamond(name, color, label, ghost=False, dx=1.5, dz=1.2, ha="left"):
    o = find(name)
    if not o:
        return
    x, z = o["x"], o["z"]
    s = 0.62
    pts = [(x, z + s), (x + s, z), (x, z - s), (x - s, z)]
    ax.add_patch(plt.Polygon(pts, facecolor=color, edgecolor="#0c0d10", lw=0.8, alpha=0.35 if ghost else 1.0, zorder=9))
    if ghost:
        ax.add_patch(plt.Polygon(pts, facecolor="none", edgecolor=color, lw=1.0, ls=(0, (2, 2)), alpha=0.6, zorder=9))
    xt = x + dx if ha == "left" else x - dx
    ax.text(xt, z + dz, label, color=color if not ghost else GHOST, fontsize=8,
            fontweight="bold" if not ghost else "normal", alpha=1 if not ghost else 0.75, ha=ha, zorder=12,
            path_effects=[pe.withStroke(linewidth=3, foreground=BG)])

diamond("ChoirBeacon", EMBER, "Choir Beacon", dx=1.6, dz=1.4)
diamond("EmberCache", EMBER, "ember cache\n(spawn)", ghost=True, dx=-1.6, dz=1.2, ha="right")
diamond("KeepsakeCrate", TIDE, "keepsake crate", dx=1.6, dz=1.2)
diamond("Barricade", STONE, "barricade", dx=1.7, dz=1.3)
diamond("WardStone", STONE, "ward stone", dx=-1.6, dz=1.3, ha="right")
diamond("Rubble", STONE, "rubble (spawn)", ghost=True, dx=1.7, dz=-2.4)
diamond("EchoShard", "#ffffff", "echo shard", dx=1.5, dz=1.1)
diamond("EnergySeal", "#c9d1ff", "energy seal", dx=1.5, dz=1.1)
for nm, c in (("Seq_Ember_Marker", EMBER), ("Seq_Tide_Marker", TIDE), ("Seq_Stone_Marker", STONE)):
    o = find(nm)
    if o:
        ax.add_patch(Circle((o["x"], o["z"]), 0.34, facecolor="none", edgecolor=c, lw=1.2, alpha=0.85, zorder=9))

# area labels
ax.text(0, -13.5, "FRACTURE HALL", color="#3d434e", fontsize=15, fontweight="bold", ha="center", alpha=0.9, zorder=2)
ax.text(-13.5, -8.6, "WEST TRANSEPT\ncombat test area", color="#6a5c3a", fontsize=9.5, fontweight="bold",
        ha="center", alpha=0.95, zorder=2)
ax.text(8.5, 23.5, "NORTH ANNEX", color="#6a5c3a", fontsize=10, fontweight="bold", ha="center", alpha=0.95, zorder=2)
ax.text(13.5, 3.0, "east columns", color="#39404b", fontsize=8.5, ha="center", alpha=0.9, zorder=2)

# compass + scale
ax.annotate("", xy=(20.5, 30.5), xytext=(20.5, 27.5), arrowprops=dict(arrowstyle="-|>", color="#8b93a3", lw=1.6))
ax.text(20.5, 31.2, "N", color="#8b93a3", fontsize=10, ha="center", fontweight="bold")
ax.add_patch(Rectangle((-23, -22.6), 5, 0.5, facecolor="#8b93a3", edgecolor="none"))
ax.text(-20.5, -23.6, "5 m", color="#8b93a3", fontsize=8, ha="center")

# ---------------------------------------------------------------- legend
handles = [
    Line2D([], [], marker="o", ls="none", ms=9, mfc=PLAYER, mec="#0c0d10", label="player spawn (Ari)"),
    Line2D([], [], marker="o", ls="none", ms=9, mfc=TIDE, mec="#0c0d10", label="NPC (Mara · Sera)"),
    Line2D([], [], marker="*", ls="none", ms=12, mfc=GOLD, mec="#0c0d10", label="enemy — Choir Warden"),
    Line2D([], [], marker="D", ls="none", ms=8, mfc=EMBER, mec="#0c0d10", label="story interactable"),
    Line2D([], [], marker="D", ls="none", ms=8, mfc="none", mec=GHOST, label="entity, inactive (spawns on event)"),
    Line2D([], [], color=GOLD, ls=(0, (5, 4)), lw=1.2, label="detection 9 m / leash 15 m"),
    Line2D([], [], color="#ff5c5c", ls=(0, (3, 3)), lw=1.0, label="attack range 2.3 m"),
    Line2D([], [], marker="o", ls="none", ms=8, mfc=COL_FILL, mec=COL_EDGE, label="column"),
    Line2D([], [], marker="s", ls="none", ms=8, mfc=WALL_FILL, mec=WALL_EDGE, label="wall / panel"),
    Line2D([], [], marker="o", ls="none", ms=9, mfc="#ffe9b0", mec="none", label="light beam / orb"),
]
leg = ax.legend(handles=handles, loc="upper left", bbox_to_anchor=(0.012, 0.995), frameon=True, fontsize=8.6,
                labelcolor="#c9cfda", facecolor="#16181d", edgecolor="#2a2e36", labelspacing=0.9,
                handletextpad=0.6, borderpad=1.0)
leg.set_zorder(20)

ax.set_xlim(-24, 24)
ax.set_ylim(-25, 33)
ax.set_xticks([]); ax.set_yticks([])
for s in ax.spines.values():
    s.set_color("#2a2e36")

fig.suptitle("CROSSROADS  ·  FirstLocation — the Fracture Hall", x=0.5, y=0.975, fontsize=17,
             fontweight="bold", color="#e8ecf3")
ax.set_title("west transept: the Warden encounter   ·   north annex: ember path   ·   drawn from FirstLocation.unity scene data",
             fontsize=9.5, color="#7d8593", pad=12)

plt.tight_layout(rect=[0, 0.008, 1, 0.965])
plt.savefig("PROJECT_MAP.png", facecolor=BG, bbox_inches="tight")
print("objects parsed:", len(objects), "| wrote PROJECT_MAP.png")
