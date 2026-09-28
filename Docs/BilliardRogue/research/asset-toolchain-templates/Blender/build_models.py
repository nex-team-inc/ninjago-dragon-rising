"""Run every Blender model script headless (in parallel) -> FBX + icon into the Unity project.

Tools/.venv/bin/python Tools/Blender/build_models.py [--only slime,rock] [--jobs 4] [--unity-assets Starter/Assets]
Each model_<name>.py must accept the bl_common CLI (--palette-json --palette-png --fbx --icon).
"""
import argparse
import glob
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
HERE = os.path.dirname(os.path.abspath(__file__))


def pascal(name):
    return "".join(part.capitalize() for part in name.split("_"))


def run(script, a):
    name = os.path.basename(script)[len("model_"):-3]
    asset = pascal(name)
    cmd = [BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "--python", script, "--",
           "--palette-json", a.palette_json, "--palette-png", a.palette_png,
           "--fbx", os.path.join(a.unity_assets, "Models/BilliardRogue", asset + ".fbx"),
           "--icon", os.path.join(a.unity_assets, "Sprites/BilliardRogue/Icons", "Icon_" + asset + ".png")]
    t = time.time()
    proc = subprocess.run(cmd, capture_output=True, text=True)
    stats = next((l for l in proc.stdout.splitlines() if l.startswith("ASSET_STATS")), "")
    return asset, proc.returncode, round(time.time() - t, 2), stats or proc.stdout[-800:] + proc.stderr[-800:]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--unity-assets", required=True)
    ap.add_argument("--palette-json", required=True)
    ap.add_argument("--palette-png", required=True)
    a = ap.parse_args()
    only = set(filter(None, a.only.split(",")))
    scripts = sorted(s for s in glob.glob(os.path.join(HERE, "model_*.py"))
                     if not only or os.path.basename(s)[6:-3] in only)
    t = time.time()
    failed = 0
    with ThreadPoolExecutor(a.jobs) as pool:
        for asset, code, secs, info in pool.map(lambda s: run(s, a), scripts):
            failed += code != 0
            print(f"{'OK ' if code == 0 else 'ERR'} {asset:<20} {secs:>5}s {info}")
    print(f"built {len(scripts)} models in {time.time() - t:.2f}s, failed={failed}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
