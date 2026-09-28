"""Compose the review sheets for the enemy/boss models (review only - nothing here is a game asset).

Tools/.venv/bin/python Tools/Blender/enemies/compose_previews.py <work_dir> <out_dir> <manifest.json>
  work_dir/preview_raw/<scene>_<act>_{albedo,emis,light,normal}.png (bl_preview.py) -> game_look.py (ToonLit +
                                                        act lighting + URP post) -> arena_<act>.png (x2), arena_acts.png
  work_dir/review/<model>_<view>.png                   (bl_build_model.py --review-dir)
                                                        -> contact_sheet.png, review_all.png
  Staging icons                                         -> icons.png
  measure_readability.py                                -> readability.json / .txt (silhouette px, floor contrast)
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

import game_look

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
ICONS = os.path.join(ROOT, "Tools", "Staging", "Assets", "Sprites", "BilliardRogue", "Icons")
BG = (38, 40, 50)
PANEL = (62, 66, 80)
TEXT = (236, 238, 245)
DIM = (160, 166, 184)


def font(size):
    return ImageFont.load_default(size=size)


def game_frame(raw, name, act):
    """In-game look of one rendered scene: the four raw passes re-shaded by game_look (see its docstring)."""
    n = int(act[-1])
    load = lambda k: np.asarray(Image.open(os.path.join(raw, f"{name}_{k}.png")).convert("RGB"), np.float32) / 255.0  # noqa: E731
    normal = load("normal") * 2.0 - 1.0
    normal /= np.maximum(np.linalg.norm(normal, axis=-1, keepdims=True), 1e-4)
    info = json.load(open(os.path.join(raw, "arena_info.json")))
    p = math.radians(info["pitch"])
    view = np.array([0.0, -math.cos(p), math.sin(p)])
    col = game_look.shade(load("albedo"), load("emis"), load("light")[..., 0], normal, game_look.act_lighting(n), view)
    out = game_look.post(col, game_look.volume(n))
    return Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGB")


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
        img = game_frame(raw, f"arena_{act}", act)
        big = img.resize((img.width * 2, img.height * 2), Image.NEAREST)
        d = ImageDraw.Draw(big)
        label(d, (12, 10), f"{info[act]}  -  640x360 game camera (FOV {info['fov']:.0f}, pitch {info['pitch']:.0f}, "
                           f"{info['px_per_cell']:.0f} px/cell mid-arena), x2 nearest, in-game look emulation "
                           f"(ToonLit + act light + bloom + grading)", 18)
        big.save(os.path.join(out, f"arena_{act}.png"), optimize=True)
        frames.append(img)
    strip = Image.new("RGB", (640 * 3 + 16, 360 + 30), BG)
    d = ImageDraw.Draw(strip)
    for i, (act, img) in enumerate(zip(("act1", "act2", "act3"), frames)):
        strip.paste(img, (i * 648, 30))
        label(d, (i * 648 + 6, 6), info[act], 16)
    strip.save(os.path.join(out, "arena_acts.png"), optimize=True)
    # x4 crop of the regular-enemy rows (true pixel density check)
    crop = frames[0].crop((205, 92, 435, 232))
    crop.resize((crop.width * 4, crop.height * 4), Image.NEAREST).save(os.path.join(out, "arena_act1_crop_x4.png"))
    context(raw, out, info)


def gray(img):
    """Value check: Rec.601 luma, so value contrast against the floor can be judged without hue."""
    return img.convert("L").convert("RGB")


def context(raw, out, info):
    """Context scene: occlusion rows, bone wall beside crate / bone pile, golem beside the Act 3 crystals."""
    if not os.path.exists(os.path.join(raw, "context_act1_albedo.png")):
        return
    frames = []
    for act in ("act1", "act2", "act3"):
        img = game_frame(raw, f"context_{act}", act)
        big = img.resize((img.width * 3, img.height * 3), Image.NEAREST)
        d = ImageDraw.Draw(big)
        label(d, (12, 10), f"{info[act]} - context x3: rows 2-3 occlusion (tall in front of short), row 5 bone wall "
                           f"beside crate / bone pile / pillar, row 8 golem beside Env_Crystal_A/B", 20)
        if info.get("context_skipped"):
            label(d, (12, 36), "not staged, skipped: " + ", ".join(info["context_skipped"]), 16, DIM)
        big.save(os.path.join(out, f"context_{act}_x3.png"), optimize=True)
        frames.append(img)
    strip = Image.new("RGB", (640 * 3 + 16, 2 * 360 + 40), BG)
    d = ImageDraw.Draw(strip)
    for i, (act, img) in enumerate(zip(("act1", "act2", "act3"), frames)):
        strip.paste(img, (i * 648, 30))
        strip.paste(gray(img), (i * 648, 40 + 360))
        label(d, (i * 648 + 6, 6), info[act] + " - top: colour, bottom: value (luma)", 16)
    strip.save(os.path.join(out, "context_acts.png"), optimize=True)


def golem_tiles(work, scale=3):
    """Four game-camera crops of the golem (Act 3 lighting, bloom) with the shield on each sim face."""
    raw = os.path.join(work, "preview_raw")
    info = json.load(open(os.path.join(raw, "arena_info.json")))
    x0, y0, x1, y1 = info["golem_px"]
    pad = 10
    box = (max(0, x0 - pad), max(0, y0 - pad), min(640, x1 + pad), min(360, y1 + pad))
    tiles = []
    for face in info["golem_faces"]:
        img = game_frame(raw, f"golem_{face}", "act3").crop(box)
        tiles.append((face, info["golem_yaw"][face], img.resize((img.width * scale, img.height * scale),
                                                                Image.NEAREST)))
    return tiles


def contact_sheet(work, out, manifest):
    review = os.path.join(work, "review")
    models = [m for m in manifest["order"] if m in manifest["models"]]
    cols, tile_w, tile_h = 4, 470, 430
    rows = (len(models) + cols - 1) // cols
    golem = golem_tiles(work)
    gh = golem[0][2].height + 60 if golem else 0
    sheet = Image.new("RGB", (cols * tile_w + 20, rows * tile_h + 70 + gh), BG)
    d = ImageDraw.Draw(sheet)
    label(d, (14, 12), "Billiard Rogue - enemies & bosses. Big: design view from the game angle (58 deg pitch, Blender "
                       "toon, review outline). Top right: 3/4 view. Bottom right: 48x48 icon (x3). In-game look: arena_*.png",
          18)
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
    if golem:
        gy = 50 + rows * tile_h
        label(d, (14, gy), "Boss_CrystalGolem ShieldCrystal per sim Face - 640x360 game camera at the boss spawn "
                           "(col 2.5, rows 0-1), Act 3 light + bloom, x3. Yaw = ShieldCrystal local Euler Y in Unity "
                           "(after EnemyView's 180 deg turn).", 16)
        tw = (cols * tile_w) // 4
        for i, (face, yaw, img) in enumerate(golem):
            x = 10 + i * tw + (tw - img.width) // 2
            sheet.paste(img, (x, gy + 44))
            label(d, (x + 6, gy + 50), f"Face.{face}  (yaw {yaw})", 18)
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
    import measure_readability as mr
    res = mr.measure(work)
    with open(os.path.join(out, "readability.json"), "w") as f:
        json.dump(res, f, indent=1, sort_keys=True)
    with open(os.path.join(out, "readability.txt"), "w") as f:
        f.write(mr.table(res) + "\n")
    print("previews written to", out)


if __name__ == "__main__":
    main()
