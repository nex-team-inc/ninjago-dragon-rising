"""Post-process preview renders like the game's HD-2D stack: HDR bloom -> soft shoulder -> sRGB -> per-act grade
-> tilt-shift band blur -> vignette -> 8-bit -> nearest-neighbour upscale (640x360 x3 = 1920x1080).

    Tools/.venv/bin/python Tools/Blender/environment/post_diorama.py --npy act1.npy --act 1 --out act1.png
        [--layouts layouts.json] [--scale 3] [--no-tilt]
    Tools/.venv/bin/python Tools/Blender/environment/post_diorama.py --sheet out.png a.png b.png c.png
"""
import argparse
import json
import os

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))


def lum(c):
    return c[..., 0] * 0.2126 + c[..., 1] * 0.7152 + c[..., 2] * 0.0722


def bloom(lin, threshold=1.0, knee=0.5, intensity=0.85):
    l = lum(lin)[..., None]
    soft = np.clip(l - threshold + knee, 0, 2 * knee) ** 2 / (4 * knee + 1e-6)
    w = np.maximum(soft, l - threshold) / np.maximum(l, 1e-4)
    bright = lin * w
    acc = np.zeros_like(lin)
    for sigma, weight in ((1.5, 0.45), (4.0, 0.35), (10.0, 0.3), (22.0, 0.2)):
        acc += weight * ndimage.gaussian_filter(bright, (sigma, sigma, 0), mode="nearest")
    return lin + acc * intensity


def shoulder(x, start=0.8):
    over = np.maximum(x - start, 0)
    return np.where(x > start, start + over / (1 + over / (1.0 - start + 0.25)), x)


def to_srgb(x):
    x = np.clip(x, 0, 1)
    return np.where(x <= 0.0031308, 12.92 * x, 1.055 * np.power(x, 1 / 2.4) - 0.055)


def grade(c, g):
    l = lum(c)[..., None]
    sh = np.array(g["shadows"], np.float32)
    hi = np.array(g["highlights"], np.float32)
    sh, hi = sh / sh.mean(), hi / hi.mean()
    w = np.clip(l, 0, 1) ** 1.2
    tint = (1 - w) * (1 + (sh - 1) * 0.35) + w * (1 + (hi - 1) * 0.25)
    c = c * tint
    l = lum(c)[..., None]
    c = l + (c - l) * g.get("saturation", 1.0)
    c = 0.5 + (c - 0.5) * g.get("contrast", 1.0)
    return np.clip(c, 0, 1)


def tilt_shift(c, band=(0.12, 0.86), max_sigma=2.2):
    """Horizontal focus band (screen fractions from the top). GDD §4: the whole grid (top wall at ~0.13) stays sharp."""
    h = c.shape[0]
    ys = (np.arange(h) + 0.5) / h
    t = np.where(ys < band[0], (band[0] - ys) / band[0], np.where(ys > band[1], (ys - band[1]) / (1 - band[1]), 0.0))
    sig = np.clip(t, 0, 1) ** 1.3 * max_sigma
    levels = [0.0, 0.6, 1.2, 1.8, max_sigma]
    blurred = [c] + [ndimage.gaussian_filter(c, (s, s, 0), mode="nearest") for s in levels[1:]]
    out = np.empty_like(c)
    for y in range(h):
        s = sig[y]
        i = min(len(levels) - 2, int(np.searchsorted(levels, s, side="right") - 1))
        f = (s - levels[i]) / (levels[i + 1] - levels[i])
        out[y] = blurred[i][y] * (1 - f) + blurred[i + 1][y] * f
    return out


def vignette(c, amount):
    h, w = c.shape[:2]
    y, x = np.mgrid[0:h, 0:w]
    r2 = ((x / w - 0.5) * 2) ** 2 * 0.6 + ((y / h - 0.5) * 2) ** 2 * 0.8
    return c * (1 - amount * np.clip(r2, 0, 1.4) ** 1.5 * 0.6)[..., None]


def process(npy, act_grade, scale, tilt=True):
    lin = np.load(npy).astype(np.float32)
    c = to_srgb(shoulder(bloom(lin)))
    c = grade(c, act_grade)
    if tilt:
        c = tilt_shift(c)
    c = vignette(c, act_grade.get("vignette", 0.25))
    img = Image.fromarray((np.clip(c, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
    return img.resize((img.width * scale, img.height * scale), Image.NEAREST) if scale > 1 else img


def sheet(out, paths, labels=None, width=1280):
    ims = [Image.open(p).convert("RGB") for p in paths]
    ims = [im.resize((width, int(im.height * width / im.width)), Image.LANCZOS) for im in ims]
    pad = 6
    W, H = width + 2 * pad, sum(im.height for im in ims) + pad * (len(ims) + 1)
    canvas = Image.new("RGB", (W, H), (18, 16, 24))
    y = pad
    d = ImageDraw.Draw(canvas)
    for i, im in enumerate(ims):
        canvas.paste(im, (pad, y))
        if labels:
            d.rectangle((pad, y, pad + 8 * len(labels[i]) + 10, y + 18), fill=(0, 0, 0))
            d.text((pad + 5, y + 3), labels[i], fill=(255, 240, 200))
        y += im.height + pad
    canvas.save(out, optimize=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--npy")
    ap.add_argument("--act")
    ap.add_argument("--out", required=False)
    ap.add_argument("--layouts", default=os.path.join(HERE, "layouts.json"))
    ap.add_argument("--scale", type=int, default=3)
    ap.add_argument("--no-tilt", action="store_true")
    ap.add_argument("--sheet", help="write a vertical contact sheet of the given PNGs")
    ap.add_argument("--labels", default="")
    ap.add_argument("images", nargs="*")
    a = ap.parse_args()
    if a.sheet:
        sheet(a.sheet, a.images, a.labels.split("|") if a.labels else None)
        print("sheet ->", a.sheet)
        return
    with open(a.layouts) as f:
        g = next(x for x in json.load(f)["acts"] if str(x["id"]) == a.act)["lighting"]["grade"]
    process(a.npy, g, a.scale, not a.no_tilt).save(a.out, optimize=True)
    print("post ->", a.out)


if __name__ == "__main__":
    main()
