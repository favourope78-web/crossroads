#!/usr/bin/env python3
"""ART PRODUCTION PASS compositors: the new player-facing screens (loading, menu with
keyart + wordmark, story intro, chapter card, pause, settings, world map) rendered from
the REAL art assets (Assets/Game/UI/Art) with the layout constants of the C# code.

Run: python3 scripts/compose_art_pass.py
"""
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance
import os

W, H = 1920, 1080
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(HERE, "..", "Assets/Game/UI/Art")
RAW = os.path.join(HERE, "..", "reference/visual_slice/raw")
OUT = os.path.join(HERE, "..", "reference/visual_slice")
F = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FB = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

def font(sz, bold=False):
    return ImageFont.truetype(FB if bold else F, int(sz))

def rgba(c, a=1.0):
    return (int(c[0]*255), int(c[1]*255), int(c[2]*255), int(a*255))

ACCENT   = (0.30, 0.85, 0.95)
TEXTMAIN = (0.93, 0.95, 0.97)
TEXTDIM  = (0.60, 0.66, 0.72)
STONE    = (0.85, 0.68, 0.32)
GOOD     = (0.32, 0.78, 0.55)

def art(name):
    return Image.open(os.path.join(ART, name)).convert("RGBA")

def cover(im, w, h):
    s = max(w / im.width, h / im.height)
    im = im.resize((int(im.width*s+0.5), int(im.height*s+0.5)), Image.LANCZOS)
    x = (im.width - w) // 2
    y = (im.height - h) // 2
    return im.crop((x, y, x+w, y+h))

def vignette(im, strength=0.32):
    w, h = im.size
    v = Image.new("L", (w//4, h//4), 0)
    vd = ImageDraw.Draw(v)
    vd.ellipse((-w//10, -h//10, w//4+w//10, h//4+h//10), fill=255)
    v = v.resize((w, h)).filter(ImageFilter.GaussianBlur(80))
    black = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    black.putalpha(v.point(lambda p: int((255-p)*strength)))
    im.alpha_composite(black)

def rrect(dr, box, radius, fill, outline=None, ow=2):
    dr.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=ow if outline else 0)

def text(dr, xy, s, sz, color, bold=False, anchor="la", a=1.0):
    dr.text(xy, s, font=font(sz, bold), fill=rgba(color, a), anchor=anchor)

# ================================================================ 1. LOADING SCREEN
def compose_loading():
    im = cover(art("art_keyart_menu.png"), W, H)
    im = ImageEnhance.Brightness(im).enhance(0.52)
    vignette(im, 0.4)
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    logo = art("logo_crossroads.png")
    lw, lh = 860, 258
    im.alpha_composite(logo.resize((lw, lh), Image.LANCZOS), ((W-lw)//2, int(H*0.62) - lh//2))
    # progress rail (620x10 at 0.30 height) with accent fill at 68%
    cx = W // 2
    ry = int(H * 0.30)
    rrect(dr, (cx-310, ry-5, cx+310, ry+5), 5, (13, 19, 28, 230))
    rrect(dr, (cx-307, ry-3, cx-307+int(614*0.68), ry+3), 3, rgba(ACCENT, 1.0))
    text(dr, (cx, ry+56), "Choices are remembered - by the city, and by the people in it.", 26, TEXTDIM, anchor="mm")
    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "loading.png"))
    print("loading.png")

# ================================================================ 2. MAIN MENU (keyart + wordmark)
def compose_menu():
    im = cover(art("art_keyart_menu.png"), W, H)
    im = ImageEnhance.Brightness(im).enhance(0.9)
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    # left glass shade (0..0.62 width) per MainMenuUI
    shade = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shade)
    sd.rectangle((0, 0, int(W*0.62), H), fill=(3, 5, 9, 220))
    ov.alpha_composite(shade)
    # accent edge at 0.62
    dr.rectangle((int(W*0.62)-2, 0, int(W*0.62)+2, H), fill=rgba(ACCENT, 0.5))
    # wordmark top-left (title block: offset 110,-236..-96)
    logo = art("logo_crossroads.png")
    im.alpha_composite(logo.resize((760, 228), Image.LANCZOS), (96, 64))
    text(dr, (116, 316), "every choice rewires the city", 34, TEXTDIM)
    text(dr, (116, 378), "autosave \u00B7 checkpoint: Fracture Hall", 26, GOOD)
    # buttons: 520x96 from y=436, step 112
    entries = [("CONTINUE", (0.10, 0.42, 0.48), True),
               ("NEW GAME", (0.13, 0.19, 0.26), False),
               ("SETTINGS", (0.13, 0.19, 0.26), False),
               ("CREDITS", (0.13, 0.19, 0.26), False),
               ("QUIT", (0.24, 0.10, 0.09), False)]
    for i, (label, col, hero) in enumerate(entries):
        y = 436 + i * 112
        rrect(dr, (110, y, 110+520, y+96), 20, rgba(col, 0.95),
              outline=rgba(ACCENT, 0.85 if hero else 0.22), ow=3 if hero else 2)
        text(dr, (132, y+48), label, 38, TEXTMAIN, True, "lm")
        if hero:
            text(dr, (588, y+48), "\u25B8", 34, ACCENT, True, "mm")
    text(dr, (W-44, 30), "prototype \u00B7 visual pass", 22, TEXTDIM, anchor="rm")
    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "menu_artpass.png"))
    print("menu_artpass.png")

# ================================================================ 3. INTRO still (beat 3)
def compose_intro():
    im = cover(art("art_still_city.png"), W, H)
    vignette(im, 0.45)
    # letterbox
    bar = int(H * 0.07)
    black = Image.new("RGBA", (W, bar), (0, 0, 0, 210))
    im.alpha_composite(black, (0, 0))
    im.alpha_composite(black, (0, H - bar))
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    text(dr, (W//2, int(H*0.155)), "NOW", 30, ACCENT, True, "mm")
    caption = "Ten years on, the echoes of that night still answer to a voice like yours.\nThe Trode is waiting to ask its question."
    text(dr, (W//2, int(H*0.845)), caption, 38, TEXTMAIN, anchor="mm")
    text(dr, (W//2, int(H*0.945)), "tap to continue", 26, TEXTDIM, anchor="mm")
    # skip pill top-right
    rrect(dr, (W-40-200, 40, W-40, 40+74), 20, (15, 23, 33, 220), outline=rgba(ACCENT, 0.3), ow=2)
    text(dr, (W-40-100, 40+37), "SKIP  \u25B8", 28, TEXTMAIN, True, "mm")
    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "intro_beat.png"))
    print("intro_beat.png")

# ================================================================ 4. CHAPTER CARD over gameplay
def compose_chapter():
    base = Image.open(os.path.join(RAW, "gameplay_raw.png")).convert("RGBA")
    base = base.resize((W, H), Image.LANCZOS)
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    band = Image.new("RGBA", (W, H), (2, 4, 6, 140))
    ov.alpha_composite(band)
    text(dr, (W//2, H//2 + 78), "CHAPTER 2", 30, ACCENT, True, "mm")
    text(dr, (W//2, H//2), "The Last Summer", 74, TEXTMAIN, True, "mm")
    dr.rectangle((W//2-215, H//2+58, W//2+215, H//2+60), fill=rgba(ACCENT, 0.3))
    dr.rectangle((W//2-215, H//2-58, W//2+215, H//2-56), fill=rgba(ACCENT, 0.3))
    base.alpha_composite(ov)
    base.convert("RGB").save(os.path.join(OUT, "chapter_card.png"))
    print("chapter_card.png")

# ================================================================ 5. PAUSE (over gameplay)
def compose_pause():
    base = Image.open(os.path.join(RAW, "gameplay_raw.png")).convert("RGBA")
    base = base.resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(6))
    base = ImageEnhance.Brightness(base).enhance(0.5)
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    # centered 760x640 glass panel
    px0, py0 = (W-760)//2, (H-640)//2
    rrect(dr, (px0, py0, px0+760, py0+640), 26, (12, 17, 24, 235))
    dr.rectangle((px0, py0+40, px0+5, py0+600), fill=rgba(ACCENT, 1.0))
    text(dr, (W//2, py0+64), "PAUSED", 56, TEXTMAIN, True, "mm")
    text(dr, (W//2, py0+118), "autosaved 12s ago \u00B7 Fracture Hall", 24, TEXTDIM, anchor="mm")
    entries = [("RESUME", (0.10, 0.42, 0.48), True),
               ("SETTINGS", (0.13, 0.19, 0.26), False),
               ("SAVE & CLOSE", (0.16, 0.30, 0.20), False)]
    for i, (label, col, hero) in enumerate(entries):
        y = py0 + 170 + i * 108
        rrect(dr, (W//2-260, y, W//2+260, y+92), 20, rgba(col, 0.95),
              outline=rgba(ACCENT, 0.85 if hero else 0.22), ow=3 if hero else 2)
        text(dr, (W//2, y+46), label, 36, TEXTMAIN, True, "mm")
    text(dr, (W//2, py0+640-34), "the city holds its breath", 22, TEXTDIM, anchor="mm")
    base.alpha_composite(ov)
    base.convert("RGB").save(os.path.join(OUT, "pause.png"))
    print("pause.png")

# ================================================================ 6. SETTINGS (980x940 panel)
def compose_settings():
    base = Image.open(os.path.join(RAW, "gameplay_raw.png")).convert("RGBA")
    base = base.resize((W, H), Image.LANCZOS).filter(ImageFilter.GaussianBlur(8))
    base = ImageEnhance.Brightness(base).enhance(0.45)
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    px0, py0 = (W-980)//2, (H-940)//2
    rrect(dr, (px0, py0, px0+980, py0+940), 26, (12, 17, 24, 240))
    text(dr, (W//2, py0+60), "SETTINGS", 48, TEXTMAIN, True, "mm")
    tabs = ["GAMEPLAY", "CONTROLS", "GRAPHICS", "AUDIO"]
    for i, t in enumerate(tabs):
        x = px0 + 60 + i * 222
        sel = i == 1
        rrect(dr, (x, py0+120, x+204, py0+172), 18, rgba(ACCENT, 0.18 if sel else 0.05),
              outline=rgba(ACCENT, 0.8 if sel else 0.15), ow=2)
        text(dr, (x+102, py0+146), t, 24, TEXTMAIN if sel else TEXTDIM, True, "mm")
    rows = [
        ("Control layout", "Right-handed"),
        ("Touch scale", "100%"),
        ("Look sensitivity", "60%"),
        ("Invert look Y", "OFF"),
        ("Auto-sprint", "ON"),
    ]
    for i, (k, v) in enumerate(rows):
        y = py0 + 226 + i * 96
        rrect(dr, (px0+60, y, px0+920, y+80), 18, (18, 26, 37, 220))
        text(dr, (px0+92, y+40), k, 28, TEXTMAIN, False, "lm")
        # value pill
        rrect(dr, (px0+640, y+14, px0+892, y+66), 16, rgba(ACCENT, 0.14), outline=rgba(ACCENT, 0.35), ow=2)
        text(dr, (px0+766, y+40), v, 24, ACCENT, True, "mm")
    # close button
    rrect(dr, (W//2-160, py0+940-96, W//2+160, py0+940-20), 20, rgba((0.10, 0.42, 0.48), 0.95))
    text(dr, (W//2, py0+940-58), "CLOSE", 34, TEXTMAIN, True, "mm")
    base.alpha_composite(ov)
    base.convert("RGB").save(os.path.join(OUT, "settings.png"))
    print("settings.png")

# ================================================================ 7. WORLD MAP
def compose_map():
    im = cover(art("art_still_city.png"), W, H)
    im = ImageEnhance.Brightness(im).enhance(0.55)
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)
    text(dr, (90, 70), "THE CITY", 52, TEXTMAIN, True, "lm")
    text(dr, (94, 130), "6 of 16 places found \u00B7 the rest answer later", 26, TEXTDIM, False, "lm")
    # stylised map graph (locations as cards/nodes on the district art)
    nodes = [
        ("Fracture Hall", 880, 560, True, "you are here"),
        ("North Annex", 1010, 380, True, "seal broken"),
        ("Tidewell Shrine", 1130, 610, True, "lamp lit"),
        ("The Old Market", 560, 470, True, "intact"),
        ("Contested Docks", 380, 620, False, "locked - reach the market"),
        ("The Long Wall", 640, 260, False, "locked - the wall holds"),
    ]
    links = [((880, 560), (1010, 380)), ((880, 560), (1130, 610)), ((880, 560), (560, 470)), ((560, 470), (380, 620)), ((560, 470), (640, 260))]
    for (a, b) in links:
        dr.line([a, b], fill=rgba(ACCENT, 0.35), width=3)
    for (name, x, y, unlocked, note) in nodes:
        col = ACCENT if unlocked else TEXTDIM
        dr.ellipse((x-14, y-14, x+14, y+14), outline=rgba(col, 1.0), width=4)
        if unlocked:
            dr.ellipse((x-6, y-6, x+6, y+6), fill=rgba(col, 1.0))
        rrect(dr, (x-150, y+24, x+150, y+92), 16, (10, 15, 22, 225),
              outline=rgba(col, 0.4 if unlocked else 0.15), ow=2)
        text(dr, (x, y+44), name, 26, TEXTMAIN if unlocked else TEXTDIM, True, "mm")
        text(dr, (x, y+74), note, 20, col if unlocked else TEXTDIM, anchor="mm")
    text(dr, (W-60, H-40), "tap a place to travel \u00B7 \u25A4 map", 24, TEXTDIM, anchor="rm")
    im.alpha_composite(ov)
    im.convert("RGB").save(os.path.join(OUT, "worldmap.png"))
    print("worldmap.png")

if __name__ == "__main__":
    compose_loading()
    compose_menu()
    compose_intro()
    compose_chapter()
    compose_pause()
    compose_settings()
    compose_map()
    # dressed room stills get light HUD frames
    for room, label in [("room_market_raw", "THE OLD MARKET"), ("room_sanctuary_raw", "THE SANCTUARY")]:
        base = Image.open(os.path.join(RAW, room + ".png")).convert("RGBA")
        base = base.resize((W, H), Image.LANCZOS)
        vignette(base, 0.28)
        ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        dr = ImageDraw.Draw(ov)
        text(dr, (90, 64), label, 40, TEXTMAIN, True, "lm")
        text(dr, (94, 116), "campaign location \u00B7 dressed (art production pass)", 24, TEXTDIM, False, "lm")
        base.alpha_composite(ov)
        base.convert("RGB").save(os.path.join(OUT, room.replace("_raw", "") + ".png"))
        print(room.replace("_raw", "") + ".png")
    print("DONE")
