"""Billiard Rogue enemy + boss models: ONE command rebuilds every FBX, icon, manifest and review sheet.

    Tools/.venv/bin/python Tools/Blender/enemies/build_enemies.py \
        [--only Enemy_Slime,Boss_KingSlime] [--jobs 6] [--preview-dir DIR] [--no-preview] [--review]

Outputs (staging mirror of Starter/Assets, copied into the Unity project by the integration step):
    Tools/Staging/Assets/Models/BilliardRogue/Enemies/Enemy_<Type>.fbx      (9 enemies)
    Tools/Staging/Assets/Models/BilliardRogue/Bosses/Boss_<Type>.fbx        (3 bosses)
    Tools/Staging/Assets/Sprites/BilliardRogue/Icons/Enemy_<Type>.png       (48x48, bosses use Enemy_<BossType>)
    Tools/Blender/enemies/enemy_models_manifest.json   (parts, paths, pivots, tris, bounds, emissive rule,
                                                         golem shield_face_yaw)
    <preview-dir>/contact_sheet.png (+ golem ShieldCrystal on each sim Face), arena_act*.png, arena_acts.png,
    context_act*_x3.png + context_acts.png (occlusion rows, bone wall beside crate/bone pile, golem beside the Act 3
    crystals, luma value check), icons.png                                  (review only, not game assets)
Checks (build fails): TDD 14.1 part names, tri budget, baked rotation/scale, nothing below the pivot, every part
inside its footprint + 0.02 m (per-part Model.overhang whitelist; ShieldCrystal checked for all 4 quarter turns),
FBX round-trip (names, hierarchy, identity transforms), icon never touching the border (except a deliberate crop).
Deterministic: geometry/PNGs are byte-stable; FBX files are only replaced when their content changes
(bl_common.export_fbx_if_changed), so re-running causes no Unity reimport churn.
Per model: Blender -b --factory-startup --python-exit-code 1 --python bl_build_model.py -- --model <Name> ...
"""
import argparse
import json
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
STAGING = os.path.join(ROOT, "Tools", "Staging", "Assets")
WORK = os.path.join(ROOT, "Tools", "Staging", "_work", "enemies")
PALETTE_DIR = os.path.join(ROOT, "Starter", "Assets", "Textures", "BilliardRogue", "Palette")
MANIFEST = os.path.join(HERE, "enemy_models_manifest.json")
DEFAULT_PREVIEW = os.path.join(WORK, "previews")

MODELS = ["Enemy_Slime", "Enemy_Bat", "Enemy_Skeleton", "Enemy_ShieldKnight", "Enemy_Mage", "Enemy_Healer",
          "Enemy_Bomber", "Enemy_Totem", "Enemy_BoneWall", "Boss_KingSlime", "Boss_BoneLich", "Boss_CrystalGolem"]
OUTLINE_RGB = (12, 12, 16)  # palette gray 0


def fbx_path(model):
    folder = "Bosses" if model.startswith("Boss_") else "Enemies"
    return os.path.join(STAGING, "Models", "BilliardRogue", folder, model + ".fbx")


def icon_path(model):
    return os.path.join(STAGING, "Sprites", "BilliardRogue", "Icons", "Enemy_" + model.split("_", 1)[1] + ".png")


def finalize_icon(raw, out, crop_bottom=False):
    """Hard alpha + 1 px dark outline (4-neighbour) on a transparent background. crop_bottom: the model is cut at
    the bottom border on purpose (tall models framed by visual mass); the cut row becomes outline."""
    a = np.array(Image.open(raw).convert("RGBA"))
    solid = a[..., 3] >= 128
    if crop_bottom:
        cut = solid[-1, :].copy()
        solid[-1, :] = False
    grow = solid.copy()
    grow[1:, :] |= solid[:-1, :]
    grow[:-1, :] |= solid[1:, :]
    grow[:, 1:] |= solid[:, :-1]
    grow[:, :-1] |= solid[:, 1:]
    out_px = np.zeros_like(a)
    out_px[solid, :3] = a[solid, :3]
    out_px[solid, 3] = 255
    ring = grow & ~solid
    if crop_bottom:
        ring[-1, :] |= cut
    out_px[ring, :3] = OUTLINE_RGB
    out_px[ring, 3] = 255
    if solid[0, :].any() or solid[-1, :].any() or solid[:, 0].any() or solid[:, -1].any():
        raise RuntimeError(f"{raw}: model touches the icon border (no room for the outline)")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    Image.fromarray(out_px, "RGBA").save(out, optimize=True)


def blender(script, *args):
    cmd = [BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "--python", os.path.join(HERE, script),
           "--", *args]
    return subprocess.run(cmd, capture_output=True, text=True)


def build_one(model, review_dir, lenient=False):
    raw = os.path.join(WORK, "icons_raw", model + ".png")
    args = ["--model", model, "--fbx", fbx_path(model), "--icon-raw", raw, "--icon-size", "48",
            "--palette-json", os.path.join(PALETTE_DIR, "palette.json"),
            "--palette-png", os.path.join(PALETTE_DIR, "Palette_Main.png")]
    if review_dir:
        args += ["--review-dir", review_dir]
    if lenient:
        args.append("--lenient")
    t = time.time()
    proc = blender("bl_build_model.py", *args)
    line = next((ln for ln in proc.stdout.splitlines() if ln.startswith("ENEMY_STATS ")), None)
    if proc.returncode != 0 or line is None:
        tail = "\n".join((proc.stdout + proc.stderr).splitlines()[-25:])
        return model, None, f"FAILED ({proc.returncode})\n{tail}"
    stats = json.loads(line[len("ENEMY_STATS "):])
    crop = stats.get("icon_crop", 0) > 0
    finalize_icon(raw, icon_path(model), crop)
    if review_dir:
        finalize_icon(raw, os.path.join(review_dir, model + "_icon.png"), crop)
    return model, stats, f"{time.time() - t:.1f}s"


EMISSIVE_RULE = ("Emission is palette-driven, NOT name-driven: every part is UV-mapped into Palette_Main and must use "
                 "M_Palette (ToonLit, _EMISSION always on, _EmissionMap = Palette_Emission). The palette's right half "
                 "(emissive=True swatches) decides what glows, face by face, so parts whose names do not contain "
                 "Emissive/Gem/Orb/Flame/Crystal/Fuse/Eyes still glow where they must (eyes in the Head of Skeleton, "
                 "ShieldKnight, Mage and CrystalGolem, BoneLich Skull, beetle eyes in Bomber Body, BoneWall pupils in "
                 "Wall, totem head eyes in Base, KingSlime Crown gems, Healer Spores, BoneLich Robe gem and HandL "
                 "wisp), and the dark pupils inside 'Eyes' parts do NOT glow. Name-based Emissive flags (TDD 14.1) "
                 "apply only to non-palette materials. Per-part 'emissive_faces' is the source of truth.")
# ShieldCrystal turn per sim Face: Unity local Euler Y of the ShieldCrystal transform (pivot = model centre), valid
# with EnemyView's 180 deg root turn (model +Z faces the player = Face.Bottom). Sim rotation order
# Bottom -> Left -> Top -> Right (EnemyPhaseResolver.RotateShield) is +90 deg per step.
SHIELD_FACE_YAW = {"Bottom": 0, "Left": 90, "Top": 180, "Right": 270}


def write_manifest(all_stats):
    doc = {"_comment": "Generated by Tools/Blender/enemies/build_enemies.py - do not edit. Units metres; "
                       "pivot_unity = local-space pivot in the imported model (x right, y up, z forward); "
                       "bounds_unity = model-space AABB after import. Look parts up recursively by name (they are "
                       "nested: see 'path'). EMISSION: " + EMISSIVE_RULE,
           "emissive_rule": EMISSIVE_RULE, "order": MODELS, "models": {}}
    for model in MODELS:
        s = all_stats.get(model)
        if s is None:
            continue
        doc["models"][model] = {
            "fbx": os.path.relpath(fbx_path(model), os.path.join(ROOT, "Tools", "Staging")),
            "icon": os.path.relpath(icon_path(model), os.path.join(ROOT, "Tools", "Staging")),
            "tris": s["tris"], "budget": s["budget"], "footprint": s["footprint"],
            "height": s["bounds_max_blender"][2],
            "bounds_unity": {"min": unity_bounds(s)[0], "max": unity_bounds(s)[1]},
            "emissive_faces": sum(p["emissive_faces"] for p in s["parts"]),
            "parts": [{k: p[k] for k in ("name", "path", "parent", "tris", "pivot_unity", "emissive_faces")}
                      for p in s["parts"]],
        }
        if model == "Boss_CrystalGolem":
            doc["models"][model]["shield_face_yaw"] = {
                "_comment": "sim Face -> Unity localEulerAngles.y of the ShieldCrystal part (turns about the model "
                            "centre), assuming EnemyView's 180 deg root turn; Bottom->Left->Top->Right = +90 per turn",
                **SHIELD_FACE_YAW}
    with open(MANIFEST, "w") as f:
        json.dump(doc, f, indent=1, sort_keys=True)
        f.write("\n")


def unity_bounds(stats):
    """Blender (x, y, z) -> Unity (-x, z, -y) model-space AABB, rounded to cm."""
    lo, hi = stats["bounds_min_blender"], stats["bounds_max_blender"]
    return ([round(-hi[0], 2), round(lo[2], 2), round(-hi[1], 2)],
            [round(-lo[0], 2), round(hi[2], 2), round(-lo[1], 2)])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--jobs", type=int, default=6)
    ap.add_argument("--preview-dir", default=DEFAULT_PREVIEW)
    ap.add_argument("--no-preview", action="store_true")
    ap.add_argument("--review", action="store_true", help="per-model hi-res review strips (slower)")
    ap.add_argument("--lenient", action="store_true", help="dev only: do not fail on tri budgets")
    a = ap.parse_args()
    only = [m for m in a.only.split(",") if m]
    models = [m for m in MODELS if not only or m in only]
    # hi-res review views feed the contact sheet, so they are rendered whenever previews are built
    review_dir = os.path.join(WORK, "review") if (a.review or not a.no_preview) else None
    t = time.time()
    all_stats = {}
    failed = 0
    with ThreadPoolExecutor(a.jobs) as pool:
        for model, stats, info in pool.map(lambda m: build_one(m, review_dir, a.lenient), models):
            if stats is None:
                failed += 1
                print(f"ERR {model:<20} {info}")
                continue
            all_stats[model] = stats
            parts = " ".join(f"{p['name']}={p['tris']}" for p in stats["parts"])
            print(f"OK  {model:<20} {stats['tris']:>5} tris  fbx_changed={stats.get('fbx_changed')}  {info}  [{parts}]")
    if failed:
        sys.exit(1)
    if not only:
        write_manifest(all_stats)
    if review_dir:
        os.makedirs(a.preview_dir, exist_ok=True)
        subprocess.run([sys.executable, os.path.join(HERE, "review_sheet.py"), review_dir,
                        os.path.join(a.preview_dir, "review_" + ("all" if not only else "_".join(only)) + ".png"),
                        *models], check=True)
    if not a.no_preview and not only:
        proc = blender("bl_preview.py", "--staging", STAGING, "--work", os.path.join(WORK, "preview_raw"),
                       "--palette-dir", PALETTE_DIR, "--models", ",".join(MODELS))
        if proc.returncode != 0:
            print("\n".join((proc.stdout + proc.stderr).splitlines()[-30:]))
            sys.exit(1)
        subprocess.run([sys.executable, os.path.join(HERE, "compose_previews.py"), WORK, a.preview_dir, MANIFEST],
                       check=True)
    print(f"built {len(models)} models in {time.time() - t:.1f}s")


if __name__ == "__main__":
    main()
