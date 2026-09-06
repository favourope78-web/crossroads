#!/usr/bin/env python3
"""Concept turnaround sheets -> per-character albedo atlases + measured proportions.

Release pass, PRIORITY 1 (real character assets). The turnaround sheets in reference/concept/
were generated from the reference-video identity frames (CHARACTER_REFERENCE.md REF-02..07)
with the approved Ari sheet as the style anchor. This script is the same method that produced
Ari_Albedo.png (CHARACTER_PROTOTYPE_REPORT.md): detect the figure silhouettes on the flat grey
sheet, cut FRONT and BACK views, dilate the figure colours into the background (so off-axis
faces sample plausible colours), and pack them into one atlas (front = left half, back = right
half).  The silhouette is also measured row by row (head / shoulder / wrist span / hem / knee /
ankle) so blender_build_characters.py builds every body to its own sheet.

Outputs (per character <Name>):
  Assets/_Project/Art/Characters/<Name>/<Name>_Albedo.png (+ .meta, Android ASTC 6x6, max 1024)
  reference/concept/build/<name>_atlas.json   (uv rects + proportions; consumed by Blender)

Idempotent; deterministic. Run: python3 scripts/build_character_atlases.py
"""
import json, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
CONCEPT = os.path.join(ROOT, "reference", "concept")
BUILD = os.path.join(CONCEPT, "build")
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters")
ATLAS = 1024           # square atlas, two 512 x 1024 cells
CELL_W, CELL_H = ATLAS // 2, ATLAS

# character -> (sheet, figure index for FRONT, sheet, figure index for BACK, guid suffix)
# figure indices follow reading order (row by row, left to right) of the detected silhouettes.
# back = None  -> synthesised from the front (mirrored, face region filled with hair colour)
CHARACTERS = {
    # Ari keeps the approved 2048 x 1024 atlas (Ari_Albedo.png, guid ...0001) - this entry only
    # measures the approved sheet crops so blender_build_characters.py builds the hero on the
    # same humanoid skeleton as the cast. atlas=None -> no texture written.
    "Ari":        {"front": ("ari_front.png", 0),             "back": ("ari_back.png", 0), "atlas": None},
    "Mara":       {"front": ("mara_turnaround.png", 0),       "back": ("mara_turnaround.png", 1)},
    "Mara_Dress": {"front": ("mara_dress_turnaround.png", 0), "back": ("mara_dress_turnaround.png", 1)},
    "Dax":        {"front": ("dax_turnaround.png", 0),        "back": ("dax_turnaround.png", 1)},
    "Archivist":  {"front": ("archivist_turnaround.png", 0),  "back": ("archivist_turnaround.png", 1)},
    "Kael":       {"front": ("mentors_turnaround.png", 0),    "back": ("mentors_back_turnaround.png", 0)},
    "Odalys":     {"front": ("mentors_turnaround.png", 1),    "back": ("mentors_back_turnaround.png", 1)},
    "Bran":       {"front": ("mentors_turnaround.png", 3),    "back": ("mentors_back_turnaround.png", 2)},
    "Sera":       {"front": ("civilians_turnaround.png", 0),  "back": None},
    "Civilian":   {"front": ("civilians_turnaround.png", 1),  "back": ("civilians_turnaround.png", 3)},
    "Soldier_A":  {"front": ("soldiers_turnaround.png", 0),   "back": ("soldiers_back_turnaround.png", 0)},
    "Soldier_B":  {"front": ("soldiers_turnaround.png", 1),   "back": ("soldiers_back_turnaround.png", 1)},
    "Soldier_C":  {"front": ("soldiers_turnaround.png", 2),   "back": ("soldiers_back_turnaround.png", 2)},
}
# texture guids: c0a1fed2...0400 + index (registry range 0x400.. reserved for character assets)
GUID_BASE = 0x400

_sheet_cache = {}


def sheet(name):
    if name not in _sheet_cache:
        im = np.asarray(Image.open(os.path.join(CONCEPT, name)).convert("RGB")).astype(np.int16)
        border = np.concatenate([im[0], im[-1], im[:, 0], im[:, -1]])
        bg = np.median(border, axis=0)
        d = np.abs(im - bg).sum(axis=2)
        # The sheets have an almost perfectly flat background (per-channel std ~1) while grey
        # garments (hoodies, parkas, plates) sit only ~10-25 levels away from it. A plain
        # threshold eats those, so: background = pixels within a TIGHT tolerance of the border
        # colour that are CONNECTED to the border (flood fill); everything else is figure.
        near = d <= 9
        lab_bg, nb = ndimage.label(near)
        edge_labels = set(np.unique(np.concatenate([lab_bg[0], lab_bg[-1], lab_bg[:, 0], lab_bg[:, -1]])))
        edge_labels.discard(0)
        background = np.isin(lab_bg, list(edge_labels))
        mask = ~background
        mask = ndimage.binary_opening(mask, iterations=2)
        # label BEFORE any closing (closing would bridge neighbouring figures' fingertips),
        # then close/fill each figure on its own
        lab, n = ndimage.label(mask)
        h, w = mask.shape
        figs = []
        for i in range(1, n + 1):
            ys, xs = np.where(lab == i)
            if len(ys) < 0.002 * h * w:
                continue
            figs.append((int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()), i))
        clean = np.zeros_like(mask)
        for (x0, y0, x1, y1, li) in figs:
            sub = lab[y0:y1 + 1, x0:x1 + 1] == li
            sub = ndimage.binary_closing(sub, iterations=4)
            sub = ndimage.binary_fill_holes(sub)
            clean[y0:y1 + 1, x0:x1 + 1] |= sub
        lab, n = ndimage.label(clean)
        figs = []
        for i in range(1, n + 1):
            ys, xs = np.where(lab == i)
            if len(ys) < 0.002 * h * w:
                continue
            figs.append((int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()), i))
        # reading order: rows (by top y band), then x
        figs.sort(key=lambda b: (round(b[1] / (h * 0.4)), b[0]))
        _sheet_cache[name] = (im, bg, lab, figs)
    return _sheet_cache[name]


def cut(name, index):
    """Returns (rgb uint8 HxWx3, mask bool HxW) of one figure, tight bbox."""
    im, bg, lab, figs = sheet(name)
    x0, y0, x1, y1, li = figs[index]
    rgb = im[y0:y1 + 1, x0:x1 + 1].astype(np.uint8)
    mask = lab[y0:y1 + 1, x0:x1 + 1] == li
    return rgb, mask


def dilate_fill(rgb, mask, iterations=64):
    """Pushes figure colours outward into the background (nearest-figure-pixel colour)."""
    dist, (iy, ix) = ndimage.distance_transform_edt(~mask, return_indices=True)
    filled = rgb[iy, ix]
    return filled


def measure(mask):
    """Row spans in bbox-relative units (same bands as reference/concept/ari_proportions.json)."""
    h, w = mask.shape
    def span_at(frac):
        y = min(h - 1, max(0, int(round(frac * (h - 1)))))
        xs = np.where(mask[y])[0]
        if len(xs) == 0:
            return [0.5, 0.5]
        return [xs.min() / w, (xs.max() + 1) / w]
    def widest_between(f0, f1):
        best, besty = 0, int(f0 * h)
        for y in range(int(f0 * h), int(f1 * h)):
            xs = np.where(mask[y])[0]
            if len(xs) and xs.max() - xs.min() > best:
                best, besty = xs.max() - xs.min(), y
        xs = np.where(mask[besty])[0]
        return [xs.min() / w, (xs.max() + 1) / w], besty / h
    wrist, wrist_y = widest_between(0.38, 0.56)
    # torso width just under the armpits where arms have separated from the trunk: take the
    # central connected run at 0.33
    def central_run(frac):
        y = int(frac * (h - 1))
        row = mask[y]
        c = w // 2
        if not row[c]:
            return span_at(frac)
        l = c
        while l > 0 and row[l - 1]:
            l -= 1
        r = c
        while r < w - 1 and row[r + 1]:
            r += 1
        return [l / w, (r + 1) / w]
    return {
        "size": [w, h],
        "spans": {
            "head": span_at(0.065),
            "shoulder": span_at(0.20),
            "torso": central_run(0.33),
            "wrist": wrist,
            "hem": central_run(0.59),
            "knee": central_run(0.725),
            "ankle": central_run(0.925),
        },
        "wrist_y": wrist_y,
    }


def hair_colour(rgb, mask):
    """Median colour of the top 6 % of the figure (hair cap)."""
    h = mask.shape[0]
    band = mask[: max(2, int(0.06 * h))]
    px = rgb[: band.shape[0]][band]
    return np.median(px, axis=0) if len(px) else np.array([40, 40, 40])


def synth_back(rgb, mask):
    """Back view from the front: mirror, then paint hair colour over the face oval."""
    back = rgb[:, ::-1].copy()
    m = mask[:, ::-1]
    h, w = m.shape
    hc = hair_colour(rgb, mask)
    # face oval ~ rows 5..15 % of height, centred on the head span
    ys, xs = np.where(m[: int(0.16 * h)])
    if len(xs):
        cx = (xs.min() + xs.max()) / 2
        for y in range(int(0.045 * h), int(0.155 * h)):
            row = np.where(m[y])[0]
            if not len(row):
                continue
            l, r = row.min(), row.max()
            back[y, l:r + 1] = (hc * 0.92).astype(np.uint8)
    return back, m


def place(cell, rgb, mask, cell_w, cell_h, margin=10):
    """Fit the figure into the cell (keep aspect), return uv rect (u0, v0, u1, v1) in cell units
    (v measured from the BOTTOM, Unity convention)."""
    h, w = mask.shape
    scale = min((cell_h - 2 * margin) / h, (cell_w - 2 * margin) / w)
    nw, nh = max(1, int(w * scale)), max(1, int(h * scale))
    filled = dilate_fill(rgb, mask)
    im = Image.fromarray(filled).resize((nw, nh), Image.LANCZOS)
    mk = Image.fromarray((mask * 255).astype(np.uint8)).resize((nw, nh), Image.LANCZOS)
    x0 = (cell_w - nw) // 2
    y0 = (cell_h - nh) // 2
    arr = np.asarray(im)
    cell[y0:y0 + nh, x0:x0 + nw] = arr
    # dilate the whole cell background from the placed figure so bleed is smooth everywhere
    cmask = np.zeros(cell.shape[:2], bool)
    cmask[y0:y0 + nh, x0:x0 + nw] = np.asarray(mk) > 127
    cell[:] = dilate_fill(cell, cmask)
    u0, u1 = x0 / cell_w, (x0 + nw) / cell_w
    v1, v0 = 1 - y0 / cell_h, 1 - (y0 + nh) / cell_h
    return [u0, v0, u1, v1]


TEXTURE_META = """fileFormatVersion: 2
guid: %s
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 1
  streamingMipmapsPriority: 0
  vTOnly: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 1024
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 1
  lightmap: 0
  compressionQuality: 50
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Android
    maxTextureSize: 1024
    resizeAlgorithm: 0
    textureFormat: 50
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData: 
    physicsShape: []
    bones: []
    spriteID: 
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def guid(i):
    return ("c0a1fed2" + ("%024x" % (GUID_BASE + i)))[:32]


def main():
    os.makedirs(BUILD, exist_ok=True)
    summary = {}
    for idx, (name, spec) in enumerate(CHARACTERS.items()):
        f_rgb, f_mask = cut(*spec["front"])
        if spec["back"] is None:
            b_rgb, b_mask = synth_back(f_rgb, f_mask)
        else:
            b_rgb, b_mask = cut(*spec["back"])
        out_dir = os.path.join(ART, name)
        os.makedirs(out_dir, exist_ok=True)
        png = os.path.join(out_dir, "%s_Albedo.png" % name)
        if spec.get("atlas", True) is None:
            # approved atlas already exists (Ari): front = left half, back = right half, full height
            front_rect, back_rect = [0.0, 0.0, 1.0, 1.0], [0.0, 0.0, 1.0, 1.0]
            g = "c0a1fed0000000000000000000000001"
        else:
            atlas = np.zeros((ATLAS, ATLAS, 3), np.uint8)
            front_rect = place(atlas[:, :CELL_W], f_rgb, f_mask, CELL_W, CELL_H)
            back_rect = place(atlas[:, CELL_W:], b_rgb, b_mask, CELL_W, CELL_H)
            Image.fromarray(atlas).save(png, optimize=True)
            meta = png + ".meta"
            g = guid(idx)
            if not os.path.exists(meta) or ("guid: " + g) not in open(meta).read():
                open(meta, "w").write(TEXTURE_META % g)
        props = measure(f_mask)
        props_back = measure(b_mask)
        hc = hair_colour(f_rgb, f_mask)
        info = {
            "name": name, "texture_guid": g, "atlas": [ATLAS, ATLAS],
            # uv rects of the figure inside the atlas (u across the WHOLE atlas)
            "front_uv": [front_rect[0] * 0.5, front_rect[1], front_rect[2] * 0.5, front_rect[3]],
            "back_uv": [0.5 + back_rect[0] * 0.5, back_rect[1], 0.5 + back_rect[2] * 0.5, back_rect[3]],
            "front": props, "back": props_back,
            "hair_rgb": [int(c) for c in hc],
        }
        json.dump(info, open(os.path.join(BUILD, "%s_atlas.json" % name.lower()), "w"), indent=1)
        summary[name] = {"png": os.path.relpath(png, ROOT), "size_kb": os.path.getsize(png) // 1024,
                         "figure_px": props["size"], "wrist_span": props["spans"]["wrist"]}
        print("%-11s atlas %4d KB  figure %sx%s  head %.3f  shoulder %.3f  wrist %.3f  hem %.3f" % (
            name, summary[name]["size_kb"], props["size"][0], props["size"][1],
            props["spans"]["head"][1] - props["spans"]["head"][0],
            props["spans"]["shoulder"][1] - props["spans"]["shoulder"][0],
            props["spans"]["wrist"][1] - props["spans"]["wrist"][0],
            props["spans"]["hem"][1] - props["spans"]["hem"][0]))
    json.dump(summary, open(os.path.join(BUILD, "summary.json"), "w"), indent=1)
    print("atlases:", len(summary))


if __name__ == "__main__":
    main()
