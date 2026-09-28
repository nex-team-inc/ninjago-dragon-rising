"""Generate the shared model palette used by every Billiard Rogue character/prop model.

Layout (32 x 16 px, point filtered, uncompressed in Unity):
  row    = colour family (16 families, see FAMILIES)
  column = shade 0 (darkest) .. 15 (lightest) in the LEFT half,
           the same colours again in the RIGHT half (columns 16..31) for EMISSIVE faces.
Outputs (all in Starter/Assets/Textures/BilliardRogue/Palette/):
  Palette_Main.png      albedo (both halves identical colours)
  Palette_Emission.png  black on the left half, the colour on the right half -> emission mask/colour
  Palette_CatP2.png     Palette_Main with the 'orange' family recoloured to grey-blue (player 2 cat)
  palette.json          lookup for Blender scripts: size, emissiveOffset, families{name:{row,hex[]}}
Models point every face's UVs at one texel centre (Tools/Blender/bl_common.paint_faces).

Usage: Tools/.venv/bin/python Tools/Textures/make_palette.py
"""
import colorsys
import json
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Starter", "Assets", "Textures", "BilliardRogue", "Palette")

# (name, hue degrees, saturation) - one row each, 16 rows. Order is a contract: append-only.
FAMILIES = [
    ("red", 355, 0.75), ("orange", 25, 0.80), ("yellow", 50, 0.80), ("lime", 80, 0.65),
    ("green", 125, 0.60), ("teal", 165, 0.60), ("cyan", 190, 0.65), ("sky", 205, 0.55),
    ("blue", 225, 0.65), ("indigo", 250, 0.55), ("purple", 275, 0.55), ("magenta", 305, 0.60),
    ("pink", 335, 0.45), ("brown", 25, 0.50), ("skin", 20, 0.35), ("gray", 230, 0.08),
]
SHADES = 16


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


def build(families):
    albedo = np.zeros((len(families), SHADES * 2, 4), np.uint8)
    emission = np.zeros_like(albedo)
    lookup = {"size": [SHADES * 2, len(families)], "shades": SHADES, "emissiveOffset": SHADES, "families": {}}
    for row, (name, hue, sat) in enumerate(families):
        colors = ramp(hue, sat)
        if name == "gray":
            colors[0], colors[-1] = (12, 12, 16), (255, 255, 255)
        albedo[row, :SHADES, :3] = colors
        albedo[row, SHADES:, :3] = colors
        emission[row, SHADES:, :3] = colors
        lookup["families"][name] = {"row": row, "hex": ["#%02x%02x%02x" % c for c in colors]}
    albedo[..., 3] = 255
    emission[..., 3] = 255
    return albedo, emission, lookup


def main():
    os.makedirs(OUT, exist_ok=True)
    albedo, emission, lookup = build(FAMILIES)
    Image.fromarray(albedo, "RGBA").save(os.path.join(OUT, "Palette_Main.png"))
    Image.fromarray(emission, "RGBA").save(os.path.join(OUT, "Palette_Emission.png"))
    p2 = [(n, 215, 0.18) if n == "orange" else (n, h, s) for n, h, s in FAMILIES]
    p2_albedo, _, _ = build(p2)
    Image.fromarray(p2_albedo, "RGBA").save(os.path.join(OUT, "Palette_CatP2.png"))
    with open(os.path.join(OUT, "palette.json"), "w") as f:
        json.dump(lookup, f, indent=1)
    print("palette written to", OUT)


if __name__ == "__main__":
    main()
