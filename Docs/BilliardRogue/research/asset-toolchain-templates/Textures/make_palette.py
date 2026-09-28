"""Generate the shared 16x16 palette atlas (PNG) + palette.json lookup.

Row = colour family, column = shade (0 darkest .. 15 lightest).
Ramps are hue-shifted (cool shadows, warm highlights) like hand-made pixel-art ramps.

Usage: python make_palette.py <out_png> <out_json>
"""
import colorsys
import json
import os
import sys

import numpy as np
from PIL import Image

# (name, hue degrees, saturation) - one row each, 16 rows.
FAMILIES = [
    ("red", 355, 0.75), ("orange", 25, 0.80), ("yellow", 50, 0.80), ("lime", 80, 0.65),
    ("green", 125, 0.60), ("teal", 165, 0.60), ("cyan", 190, 0.65), ("sky", 205, 0.55),
    ("blue", 225, 0.65), ("indigo", 250, 0.55), ("purple", 275, 0.55), ("magenta", 305, 0.60),
    ("pink", 335, 0.45), ("brown", 25, 0.50), ("skin", 20, 0.35), ("gray", 230, 0.08),
]
SIZE = 16


def ramp(hue_deg: float, sat: float) -> list[tuple[int, int, int]]:
    out = []
    for i in range(SIZE):
        t = i / (SIZE - 1)
        # shadows drift toward blue/purple, highlights toward yellow
        h = (hue_deg + (1 - t) * 18 - t * 12) % 360 / 360.0
        s = sat * (0.55 + 0.6 * (1 - abs(t - 0.45) * 1.4))
        s = min(max(s, 0.0), 1.0)
        v = 0.08 + 0.92 * (t ** 0.85)
        r, g, b = colorsys.hsv_to_rgb(h, s, v)
        out.append((round(r * 255), round(g * 255), round(b * 255)))
    return out


def main(out_png: str, out_json: str) -> None:
    img = np.zeros((SIZE, SIZE, 4), np.uint8)
    lookup = {"size": SIZE, "families": {}}
    for row, (name, hue, sat) in enumerate(FAMILIES):
        colors = ramp(hue, sat)
        if name == "gray":  # pure black / white at the ends
            colors[0], colors[-1] = (12, 12, 16), (255, 255, 255)
        img[row, :, :3] = colors
        img[row, :, 3] = 255
        lookup["families"][name] = {"row": row, "hex": ["#%02x%02x%02x" % c for c in colors]}
    for path in (out_png, out_json):
        os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    Image.fromarray(img, "RGBA").save(out_png)
    with open(out_json, "w") as f:
        json.dump(lookup, f, indent=1)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
