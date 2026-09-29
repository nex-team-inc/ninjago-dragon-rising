"""Cat-arm pointer sprites for the hand-controlled reward pick (GDD v2 §4) + the reward ball rings.

Run:  Tools/.venv/bin/python Tools/Textures/make_arms.py [--preview-dir DIR]
Out:  Starter/Assets/Sprites/BilliardRogue/UI/ (and the Tools/Staging mirror):
        Arm_P1, Arm_P2              24x16  fur sleeve segment, tiles vertically (UI Image Tiled, shown at 3x)
        Paw_P1_Open, Paw_P1_Grab,   36x40  white paw (toe beans / curled fist) over a fur cuff that joins the sleeve;
        Paw_P2_Open, Paw_P2_Grab           palm centre at (18, 20) = pivot (0.5, 0.5)
        Ring_Hold                   64x64  white ring with ink edges (radial hold fill + its dim track)
        Glow_Disc                   64x64  dithered soft disc (hover glow, pick flash), tinted at runtime
        Shadow_Ball                 40x12  soft ellipse (ball drop shadow), tinted at runtime
        Bar_Fill_Hype               64x8   grey bar fill (HUD Hype meter, tinted per tier; same shading as Bar_Fill_Hp)
      + arms.png contact sheet in DIR.
P1 = orange tabby, P2 = charcoal (colours sampled from Portrait_CatP1/P2); the paws are white like the cats' socks.
Import: ImportSettingsBuilder (UI sprites: PPU 100/3, point, no 9-slice). Deterministic.
"""
import argparse
import os
import shutil
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pixelkit as pk  # noqa: E402

OUT_DIR = ("Sprites", "BilliardRogue", "UI")
STARTER_UI = os.path.join(pk.REPO, "Starter", "Assets", *OUT_DIR)

INK = "#1a0e06"
PAW = {"hi": "#f4f6fb", "lt": "#e3e8f2", "md": "#d6dae4", "dk": "#babdc9"}
BEAN = {"md": "#e88aa6", "dk": "#bb698e"}
FUR = {
    "P1": {"hi": "#ee8a5e", "lt": "#e47b52", "md": "#d67244", "base": "#c96936", "dk": "#bb612a", "xd": "#8f4c0d"},
    "P2": {"hi": "#6a7082", "lt": "#5d6272", "md": "#595e6c", "base": "#535865", "dk": "#474b58", "xd": "#292b34"},
}

SLEEVE_W, SLEEVE_H = 24, 16
PAW_W, PAW_H = 36, 40
CUFF_TOP = 30  # rows >= this are the fur cuff (22 wide, same as the sleeve body)


def sleeve(fur):
    """Vertical fur segment: ink sides, lit left / shaded right, a tabby chevron; tiles seamlessly top to bottom."""
    img = pk.canvas(SLEEVE_W, SLEEVE_H, pk.rgba(fur["base"]))
    x = np.arange(SLEEVE_W)[None, :].repeat(SLEEVE_H, 0)
    y = np.arange(SLEEVE_H)[:, None].repeat(SLEEVE_W, 1)
    pk.put(img, (x >= 1) & (x <= 4), fur["lt"])
    pk.put(img, (x >= 2) & (x <= 3) & (y % 8 < 5), fur["hi"])
    pk.put(img, (x >= 17) & (x <= 19), fur["dk"])
    pk.put(img, (x >= 20) & (x <= 22), fur["xd"])
    # Tabby chevron: a V band two pixels thick, point down, once per tile.
    v = np.abs(x - 11.5)
    band = (y >= 4 + (v * 0.45).astype(int)) & (y <= 5 + (v * 0.45).astype(int)) & (x >= 3) & (x <= 20)
    pk.put(img, band, fur["xd"])
    # Fur tufts: a few lighter/darker ticks (fixed coordinates keep it deterministic and seamless).
    for tx, ty in ((7, 1), (14, 10), (9, 12), (16, 3)):
        pk.put(img, pk.pixels(SLEEVE_W, SLEEVE_H, [(tx, ty), (tx, ty + 1)]), fur["md"])
    pk.put(img, (x == 0) | (x == SLEEVE_W - 1), INK)
    return img


def _cuff(img, fur):
    """Fur cuff under the paw: 22 wide like the sleeve body, a zig-zag fluffy top edge."""
    h, w = img.shape[:2]
    x = np.arange(w)[None, :].repeat(h, 0)
    y = np.arange(h)[:, None].repeat(w, 1)
    x0, x1 = (w - SLEEVE_W) // 2, (w - SLEEVE_W) // 2 + SLEEVE_W - 1
    zig = CUFF_TOP - ((x // 2) % 2)
    body = (x > x0) & (x < x1) & (y >= zig)
    edge = ((x == x0) | (x == x1)) & (y >= CUFF_TOP)
    top = body & ~pk.shift(body, 0, 1)
    pk.put(img, body, fur["base"])
    pk.put(img, body & (x <= x0 + 4), fur["lt"])
    pk.put(img, body & (x >= x1 - 5), fur["dk"])
    pk.put(img, body & (x >= x1 - 2), fur["xd"])
    pk.put(img, top | edge, INK)


def _stamp(img, mask, fill, shade, extra=None):
    """Mask with a 1px ink outline, bottom-right shade and extra colour masks painted on top."""
    pk.put(img, pk.dilate(mask, False) & ~mask, INK)
    pk.put(img, mask, fill)
    pk.put(img, mask & ~pk.shift(mask, -1, -1), shade)
    for colour, m in (extra or {}).items():
        pk.put(img, m & mask, colour)


def paw_open(fur):
    img = pk.canvas(PAW_W, PAW_H)
    w, h = PAW_W, PAW_H
    toes = [(7.5, 13.0), (14.0, 8.0), (22.0, 8.0), (28.5, 13.0)]
    toe_masks = [pk.ellipse(w, h, cx, cy, 3.6, 4.6) for cx, cy in toes]
    palm = pk.ellipse(w, h, 18.0, 22.0, 11.5, 8.5) | pk.rect(w, h, 8, 22, 28, CUFF_TOP + 1)
    beans = pk.ellipse(w, h, 18.0, 23.5, 5.6, 4.0) | pk.ellipse(w, h, 14.5, 20.5, 2.6, 2.4) | pk.ellipse(w, h, 21.5, 20.5, 2.6, 2.4)
    _stamp(img, palm, PAW["md"], PAW["dk"], {PAW["lt"]: pk.ellipse(w, h, 13.0, 18.0, 4.0, 2.5), BEAN["md"]: beans,
                                             BEAN["dk"]: beans & ~pk.shift(beans, -1, -1)})
    for (cx, cy), toe in zip(toes, toe_masks):
        bean = pk.ellipse(w, h, cx, cy + 0.8, 2.0, 2.4)
        _stamp(img, toe, PAW["md"], PAW["dk"], {PAW["hi"]: pk.pixels(w, h, [(int(cx) - 2, int(cy) - 3)]), BEAN["md"]: bean,
                                                BEAN["dk"]: bean & ~pk.shift(bean, -1, -1)})
    _cuff(img, fur)
    return img


def paw_grab(fur):
    """Curled fist: knuckle bumps with ink creases, no beans, slightly smaller than the open paw."""
    img = pk.canvas(PAW_W, PAW_H)
    w, h = PAW_W, PAW_H
    knuckles = [(9.5, 15.0), (15.2, 13.2), (20.8, 13.2), (26.5, 15.0)]
    fist = pk.ellipse(w, h, 18.0, 21.0, 11.0, 8.0) | pk.rect(w, h, 8, 21, 28, CUFF_TOP + 1)
    for cx, cy in knuckles:
        fist |= pk.ellipse(w, h, cx, cy, 3.3, 3.4)
    creases = pk.pixels(w, h, [(12, 13), (12, 14), (12, 15), (18, 12), (18, 13), (18, 14), (24, 13), (24, 14), (24, 15)])
    shine = pk.pixels(w, h, [(8, 14), (14, 12), (20, 12), (26, 14)])
    _stamp(img, fist, PAW["md"], PAW["dk"], {PAW["lt"]: pk.ellipse(w, h, 14.0, 18.5, 5.0, 2.5), PAW["hi"]: shine,
                                             INK: creases})
    _cuff(img, fur)
    return img


def ring_hold():
    s = 64
    outer = pk.disc(s, s, 31.5, 31.5, 30.6)
    inner = pk.disc(s, s, 31.5, 31.5, 23.5)
    band = outer & ~inner
    img = pk.canvas(s, s)
    pk.put(img, band, "#ffffff")
    pk.put(img, band & ~pk.erode(band, False), "#20242e")
    return img


def glow_disc():
    """Soft disc: alpha steps 255..0 with a 4x4 Bayer dither between them (reads as pixel art, tints cleanly)."""
    s = 64
    yy, xx = np.mgrid[0:s, 0:s]
    d = np.sqrt((xx - 31.5) ** 2 + (yy - 31.5) ** 2) / 32.0
    level = np.clip(1.0 - d, 0.0, 1.0) ** 1.4
    steps = np.array([0, 70, 130, 190, 255], np.float32)
    idx = level * (len(steps) - 1)
    lo = np.floor(idx).astype(int)
    frac = idx - lo
    up = frac > pk.bayer(s, s, 4) + 0.5
    a = steps[np.clip(lo + up, 0, len(steps) - 1)]
    img = pk.canvas(s, s)
    img[..., :3] = 255
    img[..., 3] = a.astype(np.uint8)
    return img


def shadow_ball():
    w, h = 40, 12
    img = pk.canvas(w, h)
    pk.put(img, pk.ellipse(w, h, 19.5, 5.5, 19.5, 5.5), (255, 255, 255, 110))
    pk.put(img, pk.ellipse(w, h, 19.5, 5.5, 13.0, 3.6), (255, 255, 255, 170))
    return img


def bar_fill_hype():
    rows = [236, 255, 226, 204, 204, 170, 140, 104]
    img = pk.canvas(64, 8)
    for y, v in enumerate(rows):
        img[y, :, :3] = v
        img[y, :, 3] = 255
    return img


def build():
    sprites = {}
    for player, fur in FUR.items():
        sprites[f"Arm_{player}"] = sleeve(fur)
        sprites[f"Paw_{player}_Open"] = paw_open(fur)
        sprites[f"Paw_{player}_Grab"] = paw_grab(fur)
    sprites["Ring_Hold"] = ring_hold()
    sprites["Glow_Disc"] = glow_disc()
    sprites["Shadow_Ball"] = shadow_ball()
    sprites["Bar_Fill_Hype"] = bar_fill_hype()
    return sprites


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    a = ap.parse_args()
    preview_dir = a.preview_dir or pk.default_preview_dir("arms")
    sprites = build()
    os.makedirs(STARTER_UI, exist_ok=True)
    for name, img in sprites.items():
        h, w = img.shape[:2]
        assert w % 4 == 0 and h % 4 == 0, (name, w, h)
        staged = pk.staging(*OUT_DIR, name + ".png")
        pk.save_rgba(staged, img)
        shutil.copyfile(staged, os.path.join(STARTER_UI, name + ".png"))
    # Preview: a tiled 4-segment arm under each paw frame.
    items = list(sprites.items())
    for player in FUR:
        arm = np.concatenate([sprites[f"Arm_{player}"]] * 4, axis=0)
        items.append((f"{player} arm x4", arm))
    pk.save_rgb(os.path.join(preview_dir, "arms.png"), pk.contact_sheet(items, scale=6, cols=6))
    print("arms", {"sprites": list(sprites), "out": STARTER_UI, "preview": preview_dir})


if __name__ == "__main__":
    main()
