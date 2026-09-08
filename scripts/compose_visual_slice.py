#!/usr/bin/env python3
"""VISUAL SLICE COMPOSITOR (visual transformation pass, task 16).

Composites the mobile HUD over the offline renders of the real scene, using the SAME
layout constants, colours and sizes the C# HUD code uses (PlayerHUD, MiniMapHUD,
ObjectiveTrackerHUD, AbilityHotbarHUD, MobileControlsUI, VirtualJoystick, InteractionHUD,
DialogueUI, CombatHUD - values transcribed from those files; reference resolution
1920x1080 per RuntimeMenuFactory.RefWidth/RefHeight).

Output: reference/visual_slice/{gameplay,dialogue,combat}.png

This is a preview composite of the shipped layout, not a device capture - see
VISUAL_PASS_REPORT.md for what still needs a Unity editor pass.
"""
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import math, os

W, H = 1920, 1080
HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "..", "reference", "visual_slice", "raw")
OUT = os.path.join(HERE, "..", "reference", "visual_slice")
F = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FB = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

def font(sz, bold=False):
    return ImageFont.truetype(FB if bold else F, int(sz))

def rgba(c, a=1.0):
    r, g, b = int(c[0]*255), int(c[1]*255), int(c[2]*255)
    return (r, g, b, int(a*255))

# ---- HudTheme / RuntimeMenuFactory colours (transcribed) ----
ACCENT   = (0.30, 0.85, 0.95)
TEXTMAIN = (0.93, 0.95, 0.97)
TEXTDIM  = (0.60, 0.66, 0.72)
EMBER    = (0.95, 0.38, 0.22)
TIDE     = (0.25, 0.80, 0.85)
STONE    = (0.85, 0.68, 0.32)
GOOD     = (0.32, 0.78, 0.55)
GLASS    = (0.047, 0.067, 0.098)
GLASSSOFT= (0.055, 0.078, 0.114)
GLASSDEEP= (0.028, 0.042, 0.063)
STROKE   = (0.30, 0.85, 0.95)
CORNER = 40
SMALL_BUTTON = 104
ABILITY_BUTTON = 132

def rounded(dr, box, radius, fill):
    dr.rounded_rectangle(box, radius=radius, fill=fill)

def pill(dr, x, y, w, h, fill):
    rounded(dr, (x, y, x+w, y+h), h/2, fill)

def circle(dr, cx, cy, r, fill):
    dr.ellipse((cx-r, cy-r, cx+r, cy+r), fill=fill)

def ring(dr, cx, cy, r, color, width=5, a=1.0):
    dr.ellipse((cx-r, cy-r, cx+r, cy+r), outline=rgba(color, a), width=width)

def text(dr, xy, s, sz, color, bold=False, anchor="la", a=1.0):
    dr.text(xy, s, font=font(sz, bold), fill=rgba(color, a), anchor=anchor)

def rrect_card(dr, box, radius, fill, outline=None, ow=2):
    dr.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=ow if outline else 0)

# ================================================================ shared HUD blocks
def draw_player_hud(dr):
    # panel root: top-left (CORNER, CORNER), 560x210
    px, py = CORNER, CORNER
    # portrait
    circle(dr, px+62, py+62, 52, rgba((0.05, 0.075, 0.11), 0.95))
    ring(dr, px+62, py+62, 56, STROKE, 5, 0.30)
    text(dr, (px+62, py+62), "A", 46, ACCENT, True, "mm")
    text(dr, (px+130, py+74), "ARI", 30, TEXTMAIN, True, "lm")
    # hp bar
    pill(dr, px+130, py+116, 360, 36, rgba((0.03, 0.05, 0.075), 0.92))
    pill(dr, px+136, py+122, 348, 24, rgba((0.9, 0.45, 0.3), 0.55))     # ghost
    pill(dr, px+136, py+122, int(348*0.82), 24, rgba(GOOD, 1.0))        # fill (82%)
    text(dr, (px+130+180, py+116+18), "82/100", 24, TEXTMAIN, True, "mm")
    # echoes chip (floats below panel)
    pill(dr, px+130, py+210+34, 210, 54, rgba(GLASSSOFT, 0.62))
    text(dr, (px+130+105, py+210+34+27), "\u25C8 12", 26, STONE, True, "mm")

def draw_corner_cluster(dr):
    # pause + map, top-right (right-handed)
    x = W - CORNER - SMALL_BUTTON
    circle(dr, x+SMALL_BUTTON/2, CORNER+SMALL_BUTTON/2, SMALL_BUTTON/2, rgba((0.05, 0.075, 0.11), 0.85))
    ring(dr, x+SMALL_BUTTON/2, CORNER+SMALL_BUTTON/2, SMALL_BUTTON/2-2, STROKE, 4, 0.30)
    text(dr, (x+SMALL_BUTTON/2, CORNER+SMALL_BUTTON/2), "\u275A\u275A", 30, TEXTMAIN, True, "mm")
    mx = W - (CORNER + SMALL_BUTTON + 14) - SMALL_BUTTON
    circle(dr, mx+SMALL_BUTTON/2, CORNER+SMALL_BUTTON/2, SMALL_BUTTON/2, rgba((0.05, 0.075, 0.11), 0.85))
    ring(dr, mx+SMALL_BUTTON/2, CORNER+SMALL_BUTTON/2, SMALL_BUTTON/2-2, STROKE, 4, 0.30)
    text(dr, (mx+SMALL_BUTTON/2, CORNER+SMALL_BUTTON/2), "\u25A4", 34, TEXTMAIN, True, "mm")

def draw_minimap(dr, im, markers):
    """markers: list of (x, y, color, size) in minimap-local px, plus heading degrees."""
    size = 260
    top = CORNER + SMALL_BUTTON + 24
    left = W - CORNER - size
    cx, cy = left + size/2, top + size/2
    # backdrop disc + rings
    circle(dr, cx, cy, size/2, rgba((0.03, 0.045, 0.07), 0.80))
    ring(dr, cx, cy, size/2-3, STROKE, 5, 0.30)
    ring(dr, cx, cy, size*0.36, STROKE, 3, 0.14)
    # map content: clip a soft schematic (blurred hall footprint + annex) inside the disc
    mask = Image.new("L", (size, size), 0)
    md = ImageDraw.Draw(mask)
    md.ellipse((0, 0, size, size), fill=170)
    mmap = Image.new("RGB", (size, size), (16, 22, 30))
    mmd = ImageDraw.Draw(mmap)
    # schematic rooms (top = north): hall centre, annex north, tidewell east, west rooms
    mmd.rectangle((70, 92, 190, 212), fill=(34, 46, 58), outline=(70, 92, 104), width=2)      # hall
    mmd.rectangle((105, 52, 155, 92), fill=(30, 40, 50), outline=(64, 84, 96), width=2)       # annex
    mmd.rectangle((190, 122, 236, 182), fill=(26, 46, 50), outline=(52, 96, 100), width=2)    # tidewell
    for i, (rx, ry) in enumerate([(8, 40), (8, 108), (8, 176), (40, 12)]):                     # west rooms
        mmd.rectangle((rx, ry, rx+44, ry+52), fill=(24, 32, 42), outline=(54, 70, 82), width=2)
    for (mx, my, mc, ms) in markers:
        circle(mmd, mx, my, ms/2, rgba(mc, 1.0))
    mmap = mmap.filter(ImageFilter.GaussianBlur(0.6))
    im.paste(mmap, (int(left), int(top)), mask)
    # north chip
    ncx, ncy = cx, top + 18
    circle(dr, ncx, ncy, 17, rgba((0.10, 0.16, 0.22), 0.92))
    text(dr, (ncx, ncy), "N", 20, TEXTMAIN, True, "mm")
    # player arrow (centre, pointing up)
    pa = [(cx, cy-16), (cx-11, cy+12), (cx, cy+5), (cx+11, cy+12)]
    dr.polygon(pa, fill=rgba(ACCENT, 1.0))

def draw_tracker(dr, lines, highlight=0):
    top = CORNER + SMALL_BUTTON + 24 + 260 + 18
    left = W - CORNER - 560
    rrect_card(dr, (left, top, left+560, top+96), 18, rgba((0.035, 0.055, 0.085), 0.88))
    for i, (txt, done) in enumerate(lines):
        col = GOOD if done else TEXTMAIN
        mark = "\u2713" if done else "\u25CB"
        text(dr, (left+26, top+20+i*36), mark, 26, col, True, "lm")
        text(dr, (left+64, top+20+i*36), txt, 27, TEXTDIM if done else TEXTMAIN, i == highlight, "lm")

def draw_joystick(dr, tilt=(48, -36)):
    zx, zy = 0, H - 520
    cx, cy = zx + 230, zy + 218
    circle(dr, cx, cy, 170, rgba(GLASS, 0.86))
    ring(dr, cx, cy, 166, STROKE, 5, 0.30)
    ring(dr, cx, cy, 122, STROKE, 3, 0.14)
    kx, ky = cx + tilt[0], cy + tilt[1]
    circle(dr, kx, ky, 75, rgba((0.10, 0.16, 0.22), 0.95))
    ring(dr, kx, ky, 72, STROKE, 4, 0.30)

def draw_ability_arc(dr, cooling=1):
    """arc of 4 ability circles over the ATK/DODGE cluster; cooling = index partially cooled."""
    specs = [("E", EMBER, 0.0), ("T", TIDE, 0.55), ("S", STONE, 0.0), ("+", GOOD, 0.0)]
    for i, (glyph, col, cd) in enumerate(specs):
        x = W - 56 - i*158 - ABILITY_BUTTON
        y = H - (470 + (44 if i % 2 else 0)) - ABILITY_BUTTON
        cx, cy = x + ABILITY_BUTTON/2, y + ABILITY_BUTTON/2
        circle(dr, cx, cy, ABILITY_BUTTON/2, rgba((0.05, 0.075, 0.11), 0.88))
        if cd > 0:  # dark cooldown sweep pie
            pie = Image.new("RGBA", (ABILITY_BUTTON, ABILITY_BUTTON), (0, 0, 0, 0))
            pd = ImageDraw.Draw(pie)
            pd.pieslice((0, 0, ABILITY_BUTTON, ABILITY_BUTTON), -90, -90 + 360*cd, fill=(1, 2, 3, 200))
            dr._image.alpha_composite(pie, (int(x), int(y)))
        ring(dr, cx, cy, ABILITY_BUTTON/2 - 3, col if cd == 0 else STROKE, 4, 1.0 if cd == 0 else 0.3)
        text(dr, (cx, cy), glyph, 52, col, True, "mm")

def draw_action_buttons(dr):
    # ATK 210 bottom-right corner (margin 70, lift 190); DODGE 150 at margin 300, lift 64
    ax = W - 70 - 210
    ay = H - 190 - 210
    circle(dr, ax+105, ay+105, 105, rgba((0.42, 0.12, 0.08), 0.92))
    ring(dr, ax+105, ay+105, 101, EMBER, 6, 0.9)
    text(dr, (ax+105, ay+105), "ATK", 44, TEXTMAIN, True, "mm")
    dx = W - 300 - 150
    dy = H - 64 - 150
    circle(dr, dx+75, dy+75, 75, rgba((0.10, 0.24, 0.32), 0.92))
    ring(dr, dx+75, dy+75, 71, TIDE, 4, 0.8)
    text(dr, (dx+75, dy+75), "DODGE", 26, TEXTMAIN, True, "mm")

def draw_interact_prompt(dr, label="TALK TO MARA"):
    # ring at (-64, 640) size 168; label pill at (-252, 694) size 520x60
    rx = W - 64 - 168
    ry = H - 640 - 168
    cx, cy = rx + 84, ry + 84
    circle(dr, cx, cy, 84, rgba(GLASS, 0.86))
    ring(dr, cx, cy, 80, STROKE, 5, 0.5)
    text(dr, (cx, cy), "\u25B2", 46, ACCENT, True, "mm")
    lx = W - 252 - 520
    ly = H - 694 - 60
    pill(dr, lx, ly, 520, 60, rgba((0.035, 0.055, 0.085), 0.88))
    text(dr, (lx+260, ly+30), label, 30, TEXTMAIN, True, "mm")

def vignette(im, strength=0.35):
    w, h = im.size
    v = Image.new("L", (w//4, h//4), 0)
    vd = ImageDraw.Draw(v)
    vd.ellipse((-w//10, -h//10, w//4 + w//10, h//4 + h//10), fill=255)
    v = v.resize((w, h)).filter(ImageFilter.GaussianBlur(80))
    black = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    black.putalpha(v.point(lambda p: int((255 - p) * strength)))
    im.alpha_composite(black)

# ================================================================ 1. GAMEPLAY
def compose_gameplay():
    im = Image.open(os.path.join(RAW, "gameplay_raw.png")).convert("RGBA")
    vignette(im, 0.30)
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)

    draw_player_hud(dr)
    draw_corner_cluster(dr)
    # minimap: Mara (tide), Sera (stone), objective marker (ember) in map-local coords
    draw_minimap(dr, ov, [(130, 128, TIDE, 16), (168, 148, STONE, 12), (118, 66, EMBER, 14)])
    draw_tracker(dr, [("Reach the Fracture Monument", False), ("Ask Mara about the Trode", False)], highlight=1)
    draw_joystick(dr, tilt=(46, -30))
    draw_ability_arc(dr, cooling=1)
    draw_interact_prompt(dr, "TALK TO MARA")

    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "gameplay.png"))
    print("gameplay.png")

# ================================================================ 2. DIALOGUE
def compose_dialogue():
    im = Image.open(os.path.join(RAW, "dialogue_raw.png")).convert("RGBA")
    vignette(im, 0.24)
    # cinematic letterbox (DialogueUI cinematic blend)
    bar = int(H * 0.055)
    black = Image.new("RGBA", (W, bar), (0, 0, 0, 200))
    im.alpha_composite(black, (0, 0))
    im.alpha_composite(black, (0, H - bar))

    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    draw_player_hud(dr)
    draw_corner_cluster(dr)

    # dialogue sheet: offsetMin x36 y28, height 380 + n*(128+14)
    n = 2
    sheet_h = 380 + n * (128 + 14)
    sx0, sx1 = 36, W - 36
    sy1 = H - 28
    sy0 = sy1 - sheet_h
    rrect_card(dr, (sx0, sy0, sx1, sy1), 26, rgba((0.035, 0.055, 0.085), 0.95))
    # speaker strip (10px accent, Mara = tide)
    rounded(dr, (sx0, sy0, sx0+10, sy1), 5, rgba(TIDE, 1.0))
    text(dr, (sx0+44, sy0+18), "MARA", 34, ACCENT, True, "lm")
    text(dr, (sx0+44, sy0+56), "Mara \u00B7 Warm", 26, TEXTDIM, False, "lm")
    body = "You heard it too, then. The hall asked its question before you were even through the door."
    text(dr, (sx0+44, sy0+96), body, 40, TEXTMAIN, False, "lm")
    text(dr, (sx1-44, sy1-52), "tap to continue  \u25BC", 30, TEXTDIM, False, "rm")
    # two choice cards (y = 100 + i*(128+14) from sheet bottom)
    choices = ["\u201CI heard it. I want to know what it is.\u201D", "\u201CLater. The seal first - I need to be ready.\u201D"]
    for i, ch in enumerate(choices):
        y = 100 + i * (128 + 14)
        cy0, cy1 = sy1 - y - 128, sy1 - y
        rrect_card(dr, (sx0+44, cy0, sx1-44, cy1), 20, rgba((0.075, 0.105, 0.15), 0.97))
        rounded(dr, (sx0+60, cy0+11, sx0+67, cy1-11), 3, rgba(STONE, 0.9))
        text(dr, (sx0+96, (cy0+cy1)/2), ch, 34, TEXTMAIN, True, "lm")
        text(dr, (sx1-60, (cy0+cy1)/2), "\u25B8", 34, STONE, True, "mm")

    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "dialogue.png"))
    print("dialogue.png")

# ================================================================ 3. COMBAT
def compose_combat():
    im = Image.open(os.path.join(RAW, "combat_raw.png")).convert("RGBA")
    vignette(im, 0.34)
    # hit feedback: warm edge flash on the left (recent player hit)
    flash = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    fd = ImageDraw.Draw(flash)
    fd.rectangle((0, 0, 26, H), fill=(200, 60, 30, 70))
    flash = flash.filter(ImageFilter.GaussianBlur(18))
    im.alpha_composite(flash)

    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    draw_player_hud(dr)
    draw_corner_cluster(dr)
    draw_minimap(dr, ov, [(128, 60, EMBER, 16), (128, 128, ACCENT, 12)])
    draw_tracker(dr, [("Defeat the Choir Warden", False), ("Break the beacon's shield (0/1)", False)], highlight=0)

    # enemy plate (CombatHUD): top-centre-left, pivot (0,1) - name + bar 676x12 + flash strip
    ex, ey = 560, 64
    text(dr, (ex, ey), "CHOIR WARDEN", 36, (0.95, 0.55, 0.75), True, "lm")
    pill(dr, ex, ey+52, 676, 16, rgba((0.03, 0.05, 0.075), 0.92))
    pill(dr, ex+2, ey+54, int(672*0.64), 12, rgba((0.72, 0.22, 0.42), 1.0))
    # floating damage numbers (DamageNumberUI: bold, crit larger + warm)
    text(dr, (1180, 470), "-38", 44, (0.98, 0.86, 0.4), True, "mm")
    text(dr, (1105, 520), "-12", 32, (0.95, 0.95, 0.97), True, "mm")
    text(dr, (1246, 402), "86!", 58, (0.99, 0.62, 0.25), True, "mm")
    # combat toast
    pill(dr, 660, 200, 600, 54, rgba((0.035, 0.055, 0.085), 0.88))
    text(dr, (960, 227), "CINDER BURST READY", 28, EMBER, True, "mm")

    draw_joystick(dr, tilt=(52, 18))
    draw_ability_arc(dr, cooling=3)
    draw_action_buttons(dr)

    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "combat.png"))
    print("combat.png")



# ================================================================ 4. MAIN MENU (layout from MainMenuUI.cs)
def compose_menu():
    # backdrop: the hall render, heavily blurred + darkened (menu background mode)
    im = Image.open(os.path.join(RAW, "gameplay_raw.png")).convert("RGBA")
    im = im.filter(ImageFilter.GaussianBlur(14)).point(lambda p: int(p * 0.55))
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    # accent edge strip (4x900 from the left)
    dr.rectangle((70, 90, 74, 990), fill=rgba(ACCENT, 1.0))
    # title block: offsetMin (110,-236) offsetMax (1100,-96) from top-left
    text(dr, (110, 96), "CROSSROADS", 104, TEXTMAIN, True, "lm")
    text(dr, (116, 244), "every choice rewires the city", 34, TEXTDIM, False, "lm")
    text(dr, (116, 306), "autosave \u00B7 checkpoint: Fracture Hall", 26, GOOD, False, "lm")
    # buttons: 520 wide, 96 tall, stacked from top=430? (MenuButton top param) -> top = 436 + i*112
    entries = [("CONTINUE", (0.10, 0.42, 0.48), True),
               ("NEW GAME", (0.13, 0.19, 0.26), False),
               ("SETTINGS", (0.13, 0.19, 0.26), False),
               ("CREDITS", (0.13, 0.19, 0.26), False),
               ("QUIT", (0.24, 0.10, 0.09), False)]
    top0 = 436
    for i, (label, col, hero) in enumerate(entries):
        y = top0 + i * 112
        rrect_card(dr, (110, y, 110+520, y+96), 20, rgba(col, 0.95),
                   outline=rgba(ACCENT, 0.85 if hero else 0.22), ow=3 if hero else 2)
        text(dr, (132, y+48), label, 38, TEXTMAIN, True, "lm")
        if hero:
            text(dr, (588, y+48), "\u25B8", 34, ACCENT, True, "mm")
    # version (bottom-right): offsetMin (-360,22) offsetMax (-44,56)
    text(dr, (W-44, 30), "prototype \u00B7 visual pass", 22, TEXTDIM, False, "rm")
    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "menu.png"))
    print("menu.png")


if __name__ == "__main__":
    compose_gameplay()
    compose_dialogue()
    compose_combat()
    compose_menu()
    print("DONE")
