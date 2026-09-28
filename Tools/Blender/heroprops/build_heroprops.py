"""Billiard Rogue hero, cue, ball, arena props and pickups — ONE COMMAND regenerates everything:

    /Users/simonbut/project/VibeProject3/Tools/.venv/bin/python \
        /Users/simonbut/project/VibeProject3/Tools/Blender/heroprops/build_heroprops.py [--preview-dir DIR]

1. Blender 5.2 headless runs heroprops_models.py: builds the 10 models (TDD 14.1) from deterministic bmesh code,
   palette-UVs them (Palette_Main.png, emissive faces in the right half), exports FBX only when content changed:
     Tools/Staging/Assets/Models/BilliardRogue/Player/{Cat_Hero,Cue_Stick}.fbx
     Tools/Staging/Assets/Models/BilliardRogue/Balls/Ball.fbx
     Tools/Staging/Assets/Models/BilliardRogue/Props/{Prop_Pillar,Prop_Crate,Prop_Portal,Prop_Mud,
                                                     Pickup_ExtraBall,Pickup_Heal,Pickup_Power}.fbx
   and renders review passes (ToonLit-like 4-band cel shading + emission-only pass) + raw 128 px portraits.
2. This script (venv: numpy/Pillow/scipy) post-processes:
     Tools/Staging/Assets/Sprites/BilliardRogue/UI/Portrait_CatP1.png / Portrait_CatP2.png (128x128, 1 px outline)
   and writes review images to --preview-dir: game_act{1,2,3}.png (640x360 gameplay-camera mock with bloom +
   tilt-shift, shown 2x), crops at 4x, per-model tiles, contact_sheet.png.
Options: --only Cat_Hero,Ball (subset, no contact sheet), --no-render (FBX only), --post-only (skip Blender).
"""
import argparse
import json
import os
import subprocess
import sys
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
PALETTE_DIR = os.path.join(os.path.dirname(ROOT), "Starter", "Assets", "Textures", "BilliardRogue", "Palette")
STAGING = os.path.join(ROOT, "Staging", "Assets")
ACT_BG = {"act1": (38, 30, 24), "act2": (10, 12, 22), "act3": (18, 10, 26)}
OUTLINE_RGB = (28, 18, 30)
MODEL_ORDER = ["Cat_Hero", "Cat_HeroP2", "Cue_Stick", "Ball", "Prop_Pillar", "Prop_Crate", "Prop_Portal",
               "Prop_Mud", "Pickup_ExtraBall", "Pickup_Heal", "Pickup_Power"]


def run_blender(a):
    cmd = [BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "--python",
           os.path.join(HERE, "heroprops_models.py"), "--",
           "--palette-json", os.path.join(PALETTE_DIR, "palette.json"),
           "--palette-png", os.path.join(PALETTE_DIR, "Palette_Main.png"),
           "--staging", a.staging, "--preview-dir", a.raw, "--context-staging", STAGING]
    if a.only:
        cmd += ["--only", a.only]
    if a.no_render:
        cmd += ["--no-render"]
    t = time.time()
    proc = subprocess.run(cmd, capture_output=True, text=True)
    for line in proc.stdout.splitlines():
        if line.startswith("ASSET_STATS") or "Error" in line or "Traceback" in line:
            print(line)
    if proc.returncode != 0:
        print(proc.stdout[-3000:], proc.stderr[-3000:])
        sys.exit("blender failed (%d)" % proc.returncode)
    print("blender done in %.1fs" % (time.time() - t))


# ------------------------------------------------------------------------------------------ image helpers
def load(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.float32) / 255.0


def bloom(beauty, emis, strength=1.0):
    """URP-ish bloom: emission pass blurred at two radii and added (screen space, pre-upscale)."""
    e = emis[..., :3] * emis[..., 3:4]
    e = np.clip(e - 0.25, 0, None) / 0.75  # soft threshold: dim emissives glow less (URP threshold ~1 in HDR)
    glow = ndimage.gaussian_filter(e, (1.5, 1.5, 0)) * 0.55 + ndimage.gaussian_filter(e, (6.0, 6.0, 0)) * 0.6
    out = beauty.copy()
    out[..., :3] = np.clip(out[..., :3] + glow * strength, 0, 1)
    out[..., 3] = np.clip(out[..., 3] + glow.max(axis=2), 0, 1)
    return out


def over_bg(img, rgb):
    bg = np.ones_like(img)
    bg[..., :3] = np.array(rgb, np.float32) / 255.0
    a = img[..., 3:4]
    bg[..., :3] = img[..., :3] * a + bg[..., :3] * (1 - a)
    return bg


def tilt_shift(img, centre=0.5, half=0.3, falloff=0.18, max_sigma=2.2):
    h = img.shape[0]
    blurred = ndimage.gaussian_filter(img, (max_sigma, max_sigma, 0))
    y = 1.0 - (np.arange(h) + 0.5) / h  # 0 bottom .. 1 top
    w = np.clip((np.abs(y - centre) - half) / falloff, 0, 1)[:, None, None]
    return img * (1 - w) + blurred * w


def to_img(arr):
    return Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")


def up(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def font(size):
    try:
        return ImageFont.load_default(size=size)
    except TypeError:
        return ImageFont.load_default()


def pixel_outline(img, rgb=OUTLINE_RGB):
    """Binary alpha + 1 px outer outline (8-neighbour dilation) -> crisp pixel-art sprite."""
    a = np.asarray(img.convert("RGBA")).copy()
    solid = a[..., 3] >= 128
    a[..., 3] = np.where(solid, 255, 0)
    a[~solid, :3] = 0
    ring = ndimage.binary_dilation(solid, structure=np.ones((3, 3), bool)) & ~solid
    a[ring, :3] = rgb
    a[ring, 3] = 255
    return Image.fromarray(a, "RGBA")


# ------------------------------------------------------------------------------------------ steps
MOUTH = ["X.X.X", ".X.X."]  # pixel 'w' mouth under the nose (5 x 2)


def draw_mouth(img, centre, rgb=(52, 30, 40)):
    a = np.asarray(img).copy()
    x0, y0 = int(round(centre[0])) - 2, int(round(centre[1]))
    for dy, row in enumerate(MOUTH):
        for dx, ch in enumerate(row):
            if ch == "X" and a[y0 + dy, x0 + dx, 3] > 0:
                a[y0 + dy, x0 + dx, :3] = rgb
    return Image.fromarray(a, "RGBA")


def portraits(a):
    out_dir = os.path.join(a.staging, "Sprites", "BilliardRogue", "UI")
    os.makedirs(out_dir, exist_ok=True)
    meta_path = os.path.join(a.raw, "portrait_meta.json")
    meta = json.load(open(meta_path)) if os.path.exists(meta_path) else None
    made = []
    for p in (1, 2):
        raw = os.path.join(a.raw, "portrait_p%d_raw.png" % p)
        if not os.path.exists(raw):
            continue
        img = pixel_outline(Image.open(raw))
        if meta:
            img = draw_mouth(img, meta["mouth_px"])
        path = os.path.join(out_dir, "Portrait_CatP%d.png" % p)
        img.save(path, optimize=True)
        up(img, 4).save(os.path.join(a.preview, "portrait_p%d_x4.png" % p))
        made.append(path)
    return made


def game_views(a):
    views = {}
    for act in ("act1", "act2", "act3"):
        base = os.path.join(a.raw, "game_" + act)
        if not os.path.exists(base + ".png"):
            continue
        img = bloom(load(base + ".png"), load(base + "_emis.png"), 1.0)
        img = tilt_shift(over_bg(img, ACT_BG[act]), centre=0.46, half=0.34)
        im = to_img(img)
        im.save(os.path.join(a.preview, "game_%s_640.png" % act))
        up(im, 2).save(os.path.join(a.preview, "game_%s_x2.png" % act))
        views[act] = im
    if "act1" in views:  # 4x crops: launch zone with the cats, and the prop-dense middle
        v = views["act1"]
        up(v.crop((200, 250, 440, 360)), 4).save(os.path.join(a.preview, "crop_launch_x4.png"))
        up(v.crop((170, 90, 470, 250)), 3).save(os.path.join(a.preview, "crop_field_x3.png"))
    return views


def model_tile(a, name, view):
    base = os.path.join(a.raw, "model_%s_%s" % (name, view))
    if not os.path.exists(base + ".png"):
        return None
    img = bloom(load(base + ".png"), load(base + "_emis.png"), 0.8)
    return to_img(over_bg(img, (46, 44, 56)))


def contact_sheet(a, stats, views):
    f_big, f_small = font(22), font(14)
    tile = 256
    cols = 6
    names = [n for n in MODEL_ORDER if os.path.exists(os.path.join(a.raw, "model_%s_front.png" % n))]
    tiles = []
    for n in names:
        for view in ("front", "game", "idle"):
            t = model_tile(a, n, view)
            if t is not None:
                tiles.append((n, view, t))
    rows = (len(tiles) + cols - 1) // cols
    gw, gh = 1280, 720
    W = max(cols * tile, gw + 40 + 520)
    H = 60 + gh + 30 + 360 + 40 + rows * (tile + 44) + 40
    sheet = Image.new("RGBA", (W, H), (24, 22, 30, 255))
    d = ImageDraw.Draw(sheet)
    d.text((16, 16), "Billiard Rogue - hero / ball / props / pickups  (Tools/Blender/heroprops)", fill=(240, 230, 210),
           font=f_big)
    y = 60
    if "act1" in views:
        sheet.paste(up(views["act1"], 2), (16, y))
        d.text((24, y + 6), "Gameplay camera mock 640x360 (FOV 28, pitch 58, ~28 px/cell), shown 2x - Act 1",
               fill=(255, 255, 255), font=f_small)
    x2 = 16 + gw + 24
    for k, p in enumerate((1, 2)):
        pp = os.path.join(a.staging, "Sprites", "BilliardRogue", "UI", "Portrait_CatP%d.png" % p)
        if os.path.exists(pp):
            im = Image.open(pp)
            sheet.alpha_composite(up(im, 2), (x2 + k * 264, y + 20))
            sheet.alpha_composite(im, (x2 + k * 264, y + 300))
            d.text((x2 + k * 264, y), "Portrait_CatP%d (2x, 1x)" % p, fill=(240, 230, 210), font=f_small)
    y += gh + 30
    x = 16
    for act in ("act2", "act3"):
        if act in views:
            sheet.paste(views[act], (x, y))
            d.text((x + 6, y + 6), act.replace("act", "Act ") + " lighting (1x)", fill=(255, 255, 255), font=f_small)
            x += 640 + 16
    crop = os.path.join(a.preview, "crop_launch_x4.png")
    if os.path.exists(crop):
        c = Image.open(crop)
        c = c.resize((c.width // 2, c.height // 2), Image.NEAREST)
        if x + c.width < W:
            sheet.paste(c, (x, y))
            d.text((x + 6, y + 6), "launch zone crop (2x)", fill=(255, 255, 255), font=f_small)
    y += 360 + 40
    for k, (n, view, t) in enumerate(tiles):
        cx, cy = 16 + (k % cols) * (tile + 8), y + (k // cols) * (tile + 44)
        sheet.paste(t, (cx, cy))
        key = "Cat_Hero" if n == "Cat_HeroP2" else n
        st = stats.get(key, {})
        label = "%s  %s" % (n, view)
        info = "%d tris  %.2fx%.2fx%.2f m" % (st.get("tris", 0), *st.get("size", (0, 0, 0))) if st else ""
        d.text((cx, cy + tile + 2), label, fill=(240, 230, 210), font=f_small)
        d.text((cx, cy + tile + 20), info, fill=(170, 170, 190), font=f_small)
    path = os.path.join(a.preview, "contact_sheet.png")
    sheet.convert("RGB").save(path, optimize=True)
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--staging", default=STAGING)
    ap.add_argument("--preview-dir", default=os.path.join(ROOT, "Staging", ".cache", "heroprops_preview"))
    ap.add_argument("--only", default="")
    ap.add_argument("--no-render", action="store_true")
    ap.add_argument("--post-only", action="store_true")
    a = ap.parse_args()
    a.preview = os.path.abspath(a.preview_dir)
    a.raw = os.path.join(a.preview, "raw")
    os.makedirs(a.raw, exist_ok=True)
    if not a.post_only:
        run_blender(a)
    if a.no_render:
        return
    made = portraits(a)
    views = game_views(a)
    stats_path = os.path.join(a.raw, "stats.json")
    stats = json.load(open(stats_path)) if os.path.exists(stats_path) else {}
    if not a.only:
        print("contact sheet:", contact_sheet(a, stats, views))
    for p in made:
        print("portrait:", p, os.path.getsize(p), "bytes")


if __name__ == "__main__":
    main()
