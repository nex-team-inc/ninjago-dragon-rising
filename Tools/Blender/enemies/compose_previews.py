"""Compose the review sheets for the enemy/boss models (review only - nothing here is a game asset).

Tools/.venv/bin/python Tools/Blender/enemies/compose_previews.py <work_dir> <out_dir> <manifest.json>
  work_dir/preview_raw/arena_<act>_{beauty,emit}.png  (bl_preview.py)   -> arena_<act>.png (bloom, x2), arena_acts.png
  work_dir/review/<model>_<view>.png                   (bl_build_model.py --review-dir)
                                                        -> contact_sheet.png, review_all.png
  Staging icons                                         -> icons.png
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ICONS = os.path.join(ROOT, "Tools", "Staging", "Assets", "Sprites", "BilliardRogue", "Icons")
BG = (38, 40, 50)
PANEL = (62, 66, 80)
TEXT = (236, 238, 245)
DIM = (160, 166, 184)


def font(size):
    return ImageFont.load_default(size=size)


def bloom(beauty, emit):
    """Heavy HD-2D style bloom from the emission-only pass (two Gaussian radii at 640x360)."""
    b = np.asarray(beauty.convert("RGB"), np.float32) / 255.0
    e = np.asarray(emit.convert("RGB"), np.float32) / 255.0
    glow = np.zeros_like(e)
    for sigma, gain in ((1.5, 0.9), (5.0, 0.8), (12.0, 0.5)):
        glow += gain * np.stack([ndimage.gaussian_filter(e[..., c], sigma) for c in range(3)], axis=-1)
    out = 1.0 - (1.0 - b) * (1.0 - np.clip(glow, 0, 1))  # screen blend
    return Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


def outlined(img, px=2, rgb=(12, 12, 16)):
    a = np.array(img.convert("RGBA"))
    solid = a[..., 3] > 16
    ring = ndimage.binary_dilation(solid, iterations=px) & ~solid
    a[ring] = rgb + (255,)
    return Image.fromarray(a, "RGBA")


def on_panel(img, size, color=PANEL):
    bg = Image.new("RGBA", size, color + (255,))
    img = img.convert("RGBA")
    bg.alpha_composite(img, ((size[0] - img.width) // 2, (size[1] - img.height) // 2))
    return bg


def label(draw, xy, text, size=16, fill=TEXT):
    draw.text(xy, text, fill=fill, font=font(size))


def arenas(work, out):
    raw = os.path.join(work, "preview_raw")
    info = json.load(open(os.path.join(raw, "arena_info.json")))
    frames = []
    for act in ("act1", "act2", "act3"):
        img = bloom(Image.open(os.path.join(raw, f"arena_{act}_beauty.png")),
                    Image.open(os.path.join(raw, f"arena_{act}_emit.png")))
        big = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
        d = ImageDraw.Draw(big)
        label(d, (12, 10), f"{info[act]}  -  640x360 game camera (FOV {info['fov']:.0f}, pitch {info['pitch']:.0f}, "
                           f"~{info['px_per_cell']:.0f} px/cell), x2 nearest, bloom from emissive palette half", 18)
        big.save(os.path.join(out, f"arena_{act}.png"), optimize=True)
        frames.append(img)
    strip = Image.new("RGB", (640 * 3 + 16, 360 + 30), BG)
    d = ImageDraw.Draw(strip)
    for i, (act, img) in enumerate(zip(("act1", "act2", "act3"), frames)):
        strip.paste(img, (i * 648, 30))
        label(d, (i * 648 + 6, 6), info[act], 16)
    strip.save(os.path.join(out, "arena_acts.png"), optimize=True)
    # x4 crop of the regular-enemy rows (true pixel density check)
    crop = frames[0].crop((200, 90, 440, 230))
    crop.resize((crop.width * 4, crop.height * 4), Image.NEAREST).save(os.path.join(out, "arena_act1_crop_x4.png"))


def contact_sheet(work, out, manifest):
    review = os.path.join(work, "review")
    models = [m for m in manifest["order"] if m in manifest["models"]]
    cols, tile_w, tile_h = 4, 470, 430
    rows = (len(models) + cols - 1) // cols
    sheet = Image.new("RGB", (cols * tile_w + 20, rows * tile_h + 70), BG)
    d = ImageDraw.Draw(sheet)
    label(d, (14, 12), "Billiard Rogue - enemies & bosses. Big: game camera angle (58 deg pitch, cel-shaded, 4 bands). "
                       "Top right: 3/4 view. Bottom right: 48x48 icon (x3).", 18)
    for i, model in enumerate(models):
        info = manifest["models"][model]
        x0, y0 = 10 + (i % cols) * tile_w, 50 + (i // cols) * tile_h
        d.rectangle((x0, y0, x0 + tile_w - 12, y0 + tile_h - 12), fill=(50, 53, 65))
        game = outlined(Image.open(os.path.join(review, f"{model}_game.png")))
        sheet.paste(on_panel(game, (300, 300)), (x0 + 6, y0 + 34))
        tq = outlined(Image.open(os.path.join(review, f"{model}_threeq.png")).resize((150, 150), Image.LANCZOS), 1)
        sheet.paste(on_panel(tq, (150, 150)), (x0 + 312, y0 + 34))
        icon = Image.open(os.path.join(ICONS, os.path.basename(info["icon"])))
        sheet.paste(on_panel(icon.resize((144, 144), Image.NEAREST), (150, 150), (86, 92, 108)), (x0 + 312, y0 + 190))
        parts = ", ".join(p["name"] for p in info["parts"])
        label(d, (x0 + 8, y0 + 6), f"{model}", 18)
        label(d, (x0 + 250, y0 + 9), f"{info['tris']} / {info['budget']} tris   h {info['height']:.2f} m", 14, DIM)
        label(d, (x0 + 8, y0 + 342), f"parts: {parts}", 13, DIM)
        piv = [f"{p['path']} ({p['pivot_unity'][0]:.2f}, {p['pivot_unity'][1]:.2f}, {p['pivot_unity'][2]:.2f})"
               for p in info["parts"]]
        for r in range(0, len(piv), 2):
            label(d, (x0 + 8, y0 + 360 + 9 * r), "   ".join(piv[r:r + 2]), 12, DIM)
    sheet.save(os.path.join(out, "contact_sheet.png"), optimize=True)


def icon_sheet(out, manifest):
    """Every icon x4 on a dark and a light UI background, plus 1:1."""
    icons = [(m, Image.open(os.path.join(ICONS, os.path.basename(manifest["models"][m]["icon"]))))
             for m in manifest["order"] if m in manifest["models"]]
    per_row, cw, ch = 6, 420, 250
    rows = (len(icons) + per_row - 1) // per_row
    sheet = Image.new("RGB", (per_row * cw + 20, rows * ch + 50), BG)
    d = ImageDraw.Draw(sheet)
    label(d, (12, 10), "Enemy icons 48x48: x4 nearest on dark / light UI backgrounds, then 1:1", 18)
    for i, (m, ic) in enumerate(icons):
        x, y = 10 + (i % per_row) * cw, 44 + (i // per_row) * ch
        sheet.paste(on_panel(ic.resize((192, 192), Image.NEAREST), (200, 200), (34, 30, 46)), (x, y))
        sheet.paste(on_panel(ic.resize((192, 192), Image.NEAREST), (200, 200), (214, 206, 190)), (x + 204, y))
        sheet.paste(on_panel(ic, (56, 56), (34, 30, 46)), (x, y + 204))
        sheet.paste(on_panel(ic, (56, 56), (214, 206, 190)), (x + 60, y + 204))
        label(d, (x + 124, y + 222), os.path.basename(manifest["models"][m]["icon"]), 14, DIM)
    sheet.save(os.path.join(out, "icons.png"), optimize=True)


def main():
    work, out, manifest_path = sys.argv[1:4]
    os.makedirs(out, exist_ok=True)
    manifest = json.load(open(manifest_path))
    arenas(work, out)
    contact_sheet(work, out, manifest)
    icon_sheet(out, manifest)
    print("previews written to", out)


if __name__ == "__main__":
    main()
