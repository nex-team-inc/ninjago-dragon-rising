"""Compose per-model review strips (hi-res views + true-scale pixel view + icon) for fast iteration.
Tools/.venv/bin/python Tools/Blender/enemies/review_sheet.py <review_dir> <out.png> [model ...]"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage


def outlined(img, px=2, rgb=(12, 12, 16)):
    """Silhouette outline from alpha (review only)."""
    a = np.array(img)
    solid = a[..., 3] > 16
    ring = ndimage.binary_dilation(solid, iterations=px) & ~solid
    a[ring] = rgb + (255,)
    return Image.fromarray(a, "RGBA")

VIEWS = ("game", "front", "threeq", "back")


def main():
    src, out = sys.argv[1], sys.argv[2]
    models = sys.argv[3:] or sorted({f.rsplit("_", 1)[0] for f in os.listdir(src) if f.endswith("_game.png")})
    font = ImageFont.load_default(size=14)
    rows = []
    for m in models:
        tiles = [outlined(Image.open(os.path.join(src, f"{m}_{v}.png")).convert("RGBA")) for v in VIEWS]
        pix = Image.open(os.path.join(src, f"{m}_pixel.png")).convert("RGBA")
        pix = pix.resize((pix.width * 320 // pix.height, 320), Image.NEAREST)
        extra = [pix]
        icon_p = os.path.join(src, f"{m}_icon.png")
        if os.path.exists(icon_p):
            ic = Image.open(icon_p).convert("RGBA")
            extra.append(ic.resize((ic.width * 5, ic.height * 5), Image.NEAREST))
        w = sum(t.width for t in tiles + extra) + 10 * (len(tiles) + len(extra))
        row = Image.new("RGBA", (w, 344), (58, 62, 74, 255))
        x = 0
        for t in tiles + extra:
            bg = Image.new("RGBA", t.size, (86, 92, 104, 255))
            bg.alpha_composite(t)
            row.paste(bg, (x, 22))
            x += t.width + 10
        ImageDraw.Draw(row).text((6, 3), m, fill=(255, 255, 255, 255), font=font)
        rows.append(row)
    sheet = Image.new("RGBA", (max(r.width for r in rows), sum(r.height for r in rows)), (40, 40, 48, 255))
    y = 0
    for r in rows:
        sheet.paste(r, (0, y))
        y += r.height
    sheet.convert("RGB").save(out)


if __name__ == "__main__":
    main()
