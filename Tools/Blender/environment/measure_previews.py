"""Readability metrics of the act diorama previews (review tooling; build_all.py runs it after the renders).

    Tools/.venv/bin/python Tools/Blender/environment/measure_previews.py --preview-dir DIR [--acts 1,2,3]
        [--camera game] [--layouts layouts.json] [--out DIR/readability.json] [--crops DIR/boss_backdrops.png]

Inputs (from build_all): DIR/_raw/act<N>_<camera>.npy (no actors), act<N>_<camera>_actors.npy and
act<N>_<camera>_mask.npy/.json (flat actor IDs, render_diorama.py --mask). Every image is post-processed exactly like
the preview PNGs (post_diorama.process at 640x360) before measuring. Pixels are classified by casting the camera ray
onto the arena floor (y 0) / ground (y -0.2) plane with the pose stored in layouts.json.

Metrics per act (luma = Rec.709 on sRGB values, colour difference = CIE76 dE on Lab D65):
  * arena: mean / std luma of the grid floor (z 1.6..11.6) and of grid + launch zone (z 0..11.6)
  * band: mean luma of the ground 0-3 m outside the wall outer faces (left, right, top and all together);
    ratio = arena mean / band mean (target >= 1.2)
  * actors: per actor, dE / dL of its mean colour against a 4 px ring of floor around it in the no-actor render
    (target dE >= 35 for every enemy and boss)
Targets that fail are listed under "fails"; the script never exits non-zero (review numbers, not a contract).
"""
import argparse
import json
import math
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import post_diorama  # noqa: E402

ARENA_X, WALL_OUT, TOP_Z, TOP_OUT = 3.5, 3.9, 11.6, 12.0
BAND = 3.0
TARGET_RATIO, TARGET_DE, TARGET_STD = 1.2, 35.0, 0.09


def ground_hits(pose, w, h, y_plane):
    """World (x, z) where each pixel's camera ray meets the plane y = y_plane (nan when it misses)."""
    p = math.radians(pose["pitchDeg"])
    fwd = np.array([0.0, -math.sin(p), math.cos(p)])
    up = np.array([0.0, math.cos(p), math.sin(p)])
    right = np.array([1.0, 0.0, 0.0])
    tx, ty, tz = pose["target"]
    cam = np.array([tx, ty, tz]) - fwd * pose["distance"]
    t = math.tan(math.radians(pose["fovDeg"]) / 2)
    ys, xs = np.mgrid[0:h, 0:w]
    sx = (2 * (xs + 0.5) / w - 1) * t * w / h
    sy = (1 - 2 * (ys + 0.5) / h) * t
    d = fwd[None, None] + right[None, None] * sx[..., None] + up[None, None] * sy[..., None]
    k = (y_plane - cam[1]) / d[..., 1]
    k = np.where(k > 0, k, np.nan)
    return cam[0] + d[..., 0] * k, cam[2] + d[..., 2] * k


def srgb_to_lab(c):
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def luma(c):
    return c[..., 0] * 0.2126 + c[..., 1] * 0.7152 + c[..., 2] * 0.0722


def load(npy, grade):
    return np.asarray(post_diorama.process(npy, grade, 1), np.float32) / 255.0


def hue_sat(rgb):
    r, g, b = rgb
    mx, mn = max(rgb), min(rgb)
    if mx - mn < 1e-6:
        return 0.0, 0.0
    if mx == r:
        hue = (g - b) / (mx - mn) % 6
    elif mx == g:
        hue = (b - r) / (mx - mn) + 2
    else:
        hue = (r - g) / (mx - mn) + 4
    return round(hue * 60, 1), round((mx - mn) / mx, 3)


def measure_act(pv, act, layouts, camera, crops):
    raw = os.path.join(pv, "_raw", f"act{act}_{camera}")
    if not os.path.exists(raw + ".npy"):
        return None
    doc_act = next(x for x in layouts["acts"] if str(x["id"]) == act)
    grade = doc_act["lighting"]["grade"]
    pose = layouts["camera"]["requested"] if camera == "requested" else layouts["camera"]
    img = load(raw + ".npy", grade)
    h, w = img.shape[:2]
    lum = luma(img)
    fx, fz = ground_hits(pose, w, h, 0.0)
    gx, gz = ground_hits(pose, w, h, -0.2)
    with np.errstate(invalid="ignore"):
        grid = (np.abs(fx) <= ARENA_X) & (fz >= 1.6) & (fz <= TOP_Z)
        arena = (np.abs(fx) <= ARENA_X) & (fz >= 0.0) & (fz <= TOP_Z)
        side = (np.abs(gx) >= WALL_OUT) & (np.abs(gx) <= WALL_OUT + BAND) & (gz <= TOP_OUT + BAND)
        left, right = side & (gx < 0), side & (gx > 0)
        top = (gz >= TOP_OUT) & (gz <= TOP_OUT + BAND) & (np.abs(gx) < WALL_OUT)
    band = left | right | top
    mean = lambda m: round(float(lum[m].mean()), 4) if m.any() else None  # noqa: E731
    floor_rgb = img[grid].mean(0)
    out = {"act": act, "camera": camera, "arenaGridMean": mean(grid), "arenaGridStd": round(float(lum[grid].std()), 4),
           "arenaMean": mean(arena), "bandMean": mean(band), "bandLeft": mean(left), "bandRight": mean(right),
           "bandTop": mean(top), "floorHueSat": hue_sat(floor_rgb.tolist()),
           "gridRowsVisible": [r for r in range(10) if ((fz >= 11.6 - r - 1) & (fz <= 11.6 - r) &
                                                         (np.abs(fx) <= ARENA_X)).any()],
           "launchLineVisible": bool(((np.abs(fz - 0.55) < 0.05) & (np.abs(fx) < 3.0)).any())}
    out["ratio"] = round(out["arenaGridMean"] / max(out["bandMean"], 1e-4), 3)
    out["ratioSides"] = round(out["arenaGridMean"] / max(max(out["bandLeft"] or 0, out["bandRight"] or 0), 1e-4), 3)
    fails = []
    if out["ratio"] < TARGET_RATIO:
        fails.append(f"arena/band luma ratio {out['ratio']} < {TARGET_RATIO}")
    if out["arenaGridStd"] > TARGET_STD:
        fails.append(f"arena luma std {out['arenaGridStd']} > {TARGET_STD}")
    # actors
    if os.path.exists(raw + "_mask.npy") and os.path.exists(raw + "_actors.npy"):
        with open(raw + "_mask.json") as f:
            meta = json.load(f)
        mk = np.load(raw + "_mask.npy")
        ids = np.where(mk[..., 1] > 0.5, np.round(mk[..., 0] / meta["maskStep"]).astype(int) - 1, -1)
        act_img = load(raw + "_actors.npy", grade)
        lab_a, lab_f = srgb_to_lab(act_img), srgb_to_lab(img)
        anyact = ndimage.binary_dilation(ids >= 0, iterations=1)
        rows = []
        for a in meta["actors"]:
            m = ids == a["id"]
            if m.sum() < 8:
                rows.append({"model": a["model"], "pixels": int(m.sum())})
                continue
            ring = ndimage.binary_dilation(m, iterations=4) & ~anyact & arena
            if ring.sum() < 8:
                ring = ndimage.binary_dilation(m, iterations=4) & ~anyact
            la, lf = lab_a[m].mean(0), lab_f[ring].mean(0)
            de = float(np.linalg.norm(la - lf))
            rows.append({"model": a["model"], "pixels": int(m.sum()), "dE": round(de, 1),
                         "dL": round(float(la[0] - lf[0]), 1), "lab": [round(float(v), 1) for v in la],
                         "floorLab": [round(float(v), 1) for v in lf]})
            if (a["model"].startswith("Enemy_") or a["model"].startswith("Boss_")) and de < TARGET_DE:
                fails.append(f"{a['model']} dE {de:.1f} < {TARGET_DE}")
        out["actors"] = rows
        vals = [r["dE"] for r in rows if "dE" in r and r["model"].startswith(("Enemy_", "Boss_"))]
        out["minEnemyDE"] = round(min(vals), 1) if vals else None
        if crops is not None:
            boss = next((a for a in meta["actors"] if a["model"].startswith("Boss_")), None)
            if boss is not None:
                ys, xs = np.nonzero(ids == boss["id"])
                cx, cy = int(xs.mean()), int(ys.mean())
                x0, y0 = max(0, cx - 115), max(0, cy - 70)
                crops.append((act_img[y0:y0 + 120, x0:x0 + 230], img[y0:y0 + 120, x0:x0 + 230]))
    out["fails"] = fails
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", required=True)
    ap.add_argument("--acts", default="1,2,3")
    ap.add_argument("--camera", default="game")
    ap.add_argument("--layouts", default=os.path.join(HERE, "layouts.json"))
    ap.add_argument("--out")
    ap.add_argument("--crops")
    a = ap.parse_args()
    with open(a.layouts) as f:
        layouts = json.load(f)
    crops = [] if a.crops else None
    res = [r for r in (measure_act(a.preview_dir, act, layouts, a.camera, crops) for act in a.acts.split(",")) if r]
    for r in res:
        print(f"act {r['act']} [{r['camera']}]: arena {r['arenaGridMean']} (std {r['arenaGridStd']}, floor hue/sat "
              f"{r['floorHueSat']}) band {r['bandMean']} (L {r['bandLeft']} R {r['bandRight']} T {r['bandTop']}) "
              f"ratio {r['ratio']} sides {r['ratioSides']} | min enemy dE {r.get('minEnemyDE')} | rows "
              f"{r['gridRowsVisible'][0]}-{r['gridRowsVisible'][-1]} launch line {r['launchLineVisible']}")
        for row in r.get("actors", []):
            if "dE" in row:
                print(f"    {row['model']:<22} dE {row['dE']:5.1f}  dL {row['dL']:+6.1f}  Lab {row['lab']}")
        for fl in r["fails"]:
            print("    FAIL", fl)
    out = a.out or os.path.join(a.preview_dir, f"readability_{a.camera}.json")
    with open(out, "w") as f:
        json.dump(res, f, indent=1)
    if crops:
        tiles = [np.concatenate([c[0], np.full((c[0].shape[0], 4, 3), 0.05), c[1]], 1) for c in crops]
        wmax = max(t.shape[1] for t in tiles)
        tiles = [np.pad(t, ((0, 4), (0, wmax - t.shape[1]), (0, 0)), constant_values=0.05) for t in tiles]
        sheet = (np.clip(np.concatenate(tiles, 0), 0, 1) * 255 + 0.5).astype(np.uint8)
        im = Image.fromarray(sheet, "RGB")
        im.resize((im.width * 3, im.height * 3), Image.NEAREST).save(a.crops, optimize=True)
        print("boss backdrop crops ->", a.crops)
    print("readability ->", out)


if __name__ == "__main__":
    main()
