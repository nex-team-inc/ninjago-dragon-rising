"""One command for the whole Billiard Rogue 2D-art area: pixel fonts, surfaces, particles, icons, UI kit + logo.

Run:  Tools/.venv/bin/python Tools/Textures/build_art2d.py [--preview-dir DIR] [--check]
Out:  Tools/Staging/Assets/{Fonts/BilliardRogue, Textures/BilliardRogue/Surfaces,
      Sprites/BilliardRogue/{Particles,UI,Icons}} (Icons: Ball_/Status_/Telegraph_/Reward_ only; Enemy_* belongs to
      the models pipeline, UI/Portrait_* to the hero pipeline) + previews in DIR (default $BR_PREVIEW_DIR/art2d).
      Prints an inventory (path, bytes, pixel size) of every file this area owns.
--check: builds twice and fails if any owned output differs between the runs (determinism gate).
Order matters: the fonts are built first because the UI mock-up renders text with them.
Unity side (after copying the staging mirror into Starter/Assets): ImportSettingsBuilder.Run() then
FontAssetsBuilder.Run() (Tools/Staging/EditorScripts/).
"""
import argparse
import hashlib
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
STAGING = os.path.join(TOOLS, "Staging", "Assets")
STEPS = [
    os.path.join(TOOLS, "Fonts", "build_pixel_font.py"),
    os.path.join(HERE, "make_surfaces.py"),
    os.path.join(HERE, "make_particles.py"),
    os.path.join(HERE, "make_icons.py"),
    os.path.join(HERE, "make_ui.py"),
]
OWNED = [
    ("Fonts/BilliardRogue", lambda n: True),
    ("Textures/BilliardRogue/Surfaces", lambda n: True),
    ("Sprites/BilliardRogue/Particles", lambda n: True),
    ("Sprites/BilliardRogue/UI", lambda n: not n.startswith("Portrait_")),
    ("Sprites/BilliardRogue/Icons", lambda n: n.startswith(("Ball_", "Status_", "Telegraph_", "Reward_", "icons."))),
]


def owned_files():
    out = []
    for rel, keep in OWNED:
        folder = os.path.join(STAGING, rel)
        if not os.path.isdir(folder):
            continue
        for name in sorted(os.listdir(folder)):
            if keep(name) and not name.startswith("."):
                out.append(os.path.join(folder, name))
    return out


def digest():
    return {p: hashlib.sha256(open(p, "rb").read()).hexdigest() for p in owned_files()}


def build(preview_dir):
    for step in STEPS:
        res = subprocess.run([sys.executable, step, "--preview-dir", preview_dir], capture_output=True, text=True)
        if res.returncode != 0:
            sys.stderr.write(res.stdout + res.stderr)
            sys.exit(f"FAILED: {os.path.relpath(step, TOOLS)}")
        print(res.stdout.strip().splitlines()[-1] if res.stdout.strip() else os.path.basename(step))


def inventory():
    from PIL import Image
    total = 0
    for p in owned_files():
        size = os.path.getsize(p)
        total += size
        dims = ""
        if p.endswith(".png"):
            with Image.open(p) as im:
                dims = "%dx%d %s" % (im.size[0], im.size[1], im.mode)
        print("  %-70s %7d B  %s" % (os.path.relpath(p, os.path.dirname(STAGING)), size, dims))
    print("INVENTORY", {"files": len(owned_files()), "bytes": total})


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=None)
    ap.add_argument("--check", action="store_true", help="build twice and compare hashes")
    a = ap.parse_args()
    preview = a.preview_dir or os.path.join(os.environ.get("BR_PREVIEW_DIR", "/tmp/billiard_rogue_previews"), "art2d")
    os.makedirs(preview, exist_ok=True)
    build(preview)
    if a.check:
        first = digest()
        build(preview)
        second = digest()
        diff = sorted(p for p in set(first) | set(second) if first.get(p) != second.get(p))
        if diff:
            sys.exit("NOT DETERMINISTIC: " + ", ".join(os.path.relpath(p, STAGING) for p in diff))
        print("DETERMINISTIC", {"files": len(first)})
    inventory()


if __name__ == "__main__":
    main()
