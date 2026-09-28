"""Generate the shared model palette used by every Billiard Rogue character/prop model.

Layout (32 x 16 px, point filtered, uncompressed in Unity):
  row    = colour family (16 families, see FAMILIES)
  column = shade 0 (darkest) .. 15 (lightest) in the LEFT half,
           the same colours again in the RIGHT half (columns 16..31) for EMISSIVE faces.
Outputs (staging mirror Tools/Staging/Assets/Textures/BilliardRogue/Palette/, copied into Starter/Assets by
Tools/sync_staging.sh; Palette_Main / Palette_Emission / palette.json are byte-identical to the committed ones):
  Palette_Main.png      albedo (both halves identical colours)
  Palette_Emission.png  black on the left half, the colour on the right half -> emission mask/colour
  Palette_CatP2.png     Palette_Main with two rows recoloured for the player-2 cat (M_Palette_CatP2, cat only):
                          'orange' -> charcoal ramp (GDD 8 "P2 grey tuxedo": dark fur, white markings stay white)
                          'blue'   -> crimson ramp (P2 cape; the cat uses blue only on its cape)
  palette.json          lookup for Blender scripts: size, emissiveOffset, families{name:{row,hex[]}}
Models point every face's UVs at one texel centre (Tools/Blender/bl_common.paint_faces).

Usage: Tools/.venv/bin/python Tools/Textures/make_palette.py [--out DIR]
"""
import argparse
import colorsys
import json
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Tools", "Staging", "Assets", "Textures", "BilliardRogue", "Palette")

# (name, hue degrees, saturation) - one row each, 16 rows. Order is a contract: append-only.
FAMILIES = [
    ("red", 355, 0.75), ("orange", 25, 0.80), ("yellow", 50, 0.80), ("lime", 80, 0.65),
    ("green", 125, 0.60), ("teal", 165, 0.60), ("cyan", 190, 0.65), ("sky", 205, 0.55),
    ("blue", 225, 0.65), ("indigo", 250, 0.55), ("purple", 275, 0.55), ("magenta", 305, 0.60),
    ("pink", 335, 0.45), ("brown", 25, 0.50), ("skin", 20, 0.35), ("gray", 230, 0.08),
]
SHADES = 16

# P2 cat fur: hand-tuned charcoal ramp. The cat model uses orange 7-8 (ear backs), 9-10 (stripes), 11 (limbs),
# 12 (body), 13 (head); white markings are gray 14/15 and stay white -> a real tuxedo read.
P2_CHARCOAL = ["#0c0c10", "#111217", "#16171d", "#1b1c23", "#1f2129", "#23252d", "#262831", "#292b34",
               "#2c2e37", "#31333d", "#2f313a", "#474b58", "#535865", "#5d6272", "#6d7383", "#7e8595"]
P2_CAPE = (350, 0.78)  # crimson (hue, saturation) replaces the 'blue' row for the P2 cape


def ramp(hue_deg, sat):
    out = []
    for i in range(SHADES):
        t = i / (SHADES - 1)
        # hue-shifted ramps: cool shadows, warm highlights (hand-made pixel-art style)
        h = (hue_deg + (1 - t) * 18 - t * 12) % 360 / 360.0
        s = min(max(sat * (0.55 + 0.6 * (1 - abs(t - 0.45) * 1.4)), 0.0), 1.0)
        v = 0.08 + 0.92 * (t ** 0.85)
        r, g, b = colorsys.hsv_to_rgb(h, s, v)
        out.append((round(r * 255), round(g * 255), round(b * 255)))
    return out


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def build(families, overrides=None):
    """overrides: {family name: [16 (r, g, b)]} replaces a generated ramp (same row, same layout)."""
    albedo = np.zeros((len(families), SHADES * 2, 4), np.uint8)
    emission = np.zeros_like(albedo)
    lookup = {"size": [SHADES * 2, len(families)], "shades": SHADES, "emissiveOffset": SHADES, "families": {}}
    for row, (name, hue, sat) in enumerate(families):
        colors = ramp(hue, sat)
        if name == "gray":
            colors[0], colors[-1] = (12, 12, 16), (255, 255, 255)
        if overrides and name in overrides:
            colors = overrides[name]
        albedo[row, :SHADES, :3] = colors
        albedo[row, SHADES:, :3] = colors
        emission[row, SHADES:, :3] = colors
        lookup["families"][name] = {"row": row, "hex": ["#%02x%02x%02x" % c for c in colors]}
    albedo[..., 3] = 255
    emission[..., 3] = 255
    return albedo, emission, lookup


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=OUT)
    out = ap.parse_args().out
    os.makedirs(out, exist_ok=True)
    albedo, emission, lookup = build(FAMILIES)
    Image.fromarray(albedo, "RGBA").save(os.path.join(out, "Palette_Main.png"))
    Image.fromarray(emission, "RGBA").save(os.path.join(out, "Palette_Emission.png"))
    p2_albedo, _, _ = build(FAMILIES, {"orange": [hex_rgb(h) for h in P2_CHARCOAL], "blue": ramp(*P2_CAPE)})
    Image.fromarray(p2_albedo, "RGBA").save(os.path.join(out, "Palette_CatP2.png"))
    with open(os.path.join(out, "palette.json"), "w") as f:
        json.dump(lookup, f, indent=1)
    print("palette written to", out)


if __name__ == "__main__":
    main()
