"""One command for the whole Billiard Rogue 2D-art area: pixel fonts, surfaces, particles, icons, UI kit + logo.

Run:  Tools/.venv/bin/python Tools/Textures/build_art2d.py [--preview-dir DIR] [--check]
Out:  Tools/Staging/Assets/{Fonts/BilliardRogue, Textures/BilliardRogue/Surfaces,
      Sprites/BilliardRogue/{Particles,UI,Icons}} (Icons: Ball_/Status_/Telegraph_/Reward_ only; Enemy_* belongs to
      the models pipeline, UI/Portrait_* to the hero pipeline) + previews in DIR (default $BR_PREVIEW_DIR/art2d).
      Prints an inventory (path, bytes, pixel size) of every file this area owns.
Ownership = what the run writes (file mtimes >= the build start, in the folders below), recorded in
Tools/Staging/_work/art2d_manifest.json (outside Assets, never synced). A file the PREVIOUS run wrote that this run no
longer writes (a renamed or removed sprite) is STALE: it is deleted from staging and listed so the integrator deletes
it from Starter/Assets too. Files other pipelines put in these shared folders (Enemy_* icons, UI/Portrait_*,
make_ui_extra's icons) are never in the manifest and are never touched.
--check: builds twice and fails if any owned output differs between the runs (determinism gate).
Order matters: the fonts are built first because the UI mock-up renders text with them.
Unity side (after copying the staging mirror into Starter/Assets): ImportSettingsBuilder.Run() then
FontAssetsBuilder.Run() (Tools/Staging/EditorScripts/).
"""
import argparse
import hashlib
import json
import os
import subprocess
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
STAGING = os.path.join(TOOLS, "Staging", "Assets")
MANIFEST = os.path.join(TOOLS, "Staging", "_work", "art2d_manifest.json")
STEPS = [
    os.path.join(TOOLS, "Fonts", "build_pixel_font.py"),
    os.path.join(HERE, "make_surfaces.py"),
    os.path.join(HERE, "make_particles.py"),
    os.path.join(HERE, "make_icons.py"),
    os.path.join(HERE, "make_ui.py"),
]
FOLDERS = ["Fonts/BilliardRogue", "Textures/BilliardRogue/Surfaces", "Sprites/BilliardRogue/Particles",
           "Sprites/BilliardRogue/UI", "Sprites/BilliardRogue/Icons"]  # shared with other pipelines (UI, Icons)


def folder_files():
    out = []
    for rel in FOLDERS:
        folder = os.path.join(STAGING, rel)
        if os.path.isdir(folder):
            out += [os.path.join(folder, n) for n in sorted(os.listdir(folder)) if not n.startswith(".")]
    return out


def read_manifest():
    try:
        with open(MANIFEST) as f:
            return {os.path.join(STAGING, p) for p in json.load(f)["files"]}
    except (OSError, ValueError, KeyError):
        return set()


def owned_files():
    return sorted(p for p in read_manifest() if os.path.exists(p))


def digest():
    return {p: hashlib.sha256(open(p, "rb").read()).hexdigest() for p in owned_files()}


def build(preview_dir):
    """Runs every generator, records what it wrote as the owned set and prunes last run's outputs it no longer
    writes. Returns the pruned (stale) paths."""
    previous = read_manifest()
    t0 = time.time_ns()
    for step in STEPS:
        res = subprocess.run([sys.executable, step, "--preview-dir", preview_dir], capture_output=True, text=True)
        if res.returncode != 0:
            sys.stderr.write(res.stdout + res.stderr)
            sys.exit(f"FAILED: {os.path.relpath(step, TOOLS)}")
        print(res.stdout.strip().splitlines()[-1] if res.stdout.strip() else os.path.basename(step))
    written = {p for p in folder_files() if os.stat(p).st_mtime_ns >= t0}
    stale = sorted(p for p in previous - written if os.path.exists(p))
    for p in stale:
        os.remove(p)
    os.makedirs(os.path.dirname(MANIFEST), exist_ok=True)
    with open(MANIFEST, "w") as f:
        json.dump({"note": "files the last build_art2d.py run wrote (paths relative to Tools/Staging/Assets)",
                   "files": sorted(os.path.relpath(p, STAGING) for p in written)}, f, indent=1)
    return stale


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
    stale = build(preview)
    if stale:
        print("STALE (a previous run wrote them, this one did not; removed from staging, delete from Starter/Assets):",
              ", ".join(os.path.relpath(p, STAGING) for p in stale))
    if a.check:
        first = digest()
        if build(preview):
            sys.exit("NOT DETERMINISTIC: the second build did not write every file the first one did")
        second = digest()
        diff = sorted(p for p in set(first) | set(second) if first.get(p) != second.get(p))
        if diff:
            sys.exit("NOT DETERMINISTIC: " + ", ".join(os.path.relpath(p, STAGING) for p in diff))
        print("DETERMINISTIC", {"files": len(first)})
    inventory()


if __name__ == "__main__":
    main()
