"""One command to regenerate the Billiard Rogue environment kit + dressing layouts + review previews.

    Tools/.venv/bin/python Tools/Blender/environment/build_all.py [--preview-dir DIR] [--jobs 4]
        [--skip-models] [--skip-previews] [--acts 1,2,3] [--only Env_Bush,...]

Steps (all deterministic):
  1. build_env.py in parallel Blender processes -> Tools/Staging/Assets/Models/BilliardRogue/Environment/Env_*.fbx
     (rewritten only when the content changed)
  2. verify_env.py re-imports every FBX and checks the naming / emissive / UV / transform / budget contract
  3. make_layouts.py -> Tools/Blender/environment/layouts.json (kit, per-act dressing, lights, LightShaft volumes,
     AmbientParticles emitters, ActLightingPreset values)
  4. previews (review only, never in the repo): stand-in surfaces (2D-art Surfaces missing from staging, incl. the
     requested per-act arena floors), per-piece sheet, per act a diorama from the ArenaConfig camera, the same with
     actors (boss, every enemy type, props, cat from the other model areas' staging output) + an actor-ID mask, the
     diorama from the requested camera pose and a wide overview, post-processed like the HD-2D stack, contact sheets.
  5. measure_previews.py: arena vs scenery luma, arena texture std, actor-vs-floor dE (readability_*.json) and
     boss-backdrop crops.
     Preview dir: --preview-dir, else $ENV_PREVIEW_DIR, else $TMPDIR/billiard_env_preview (keep previews out of the
     repo).
"""
import argparse
import glob
import json
import os
import subprocess
import sys
import time
from concurrent.futures import ThreadPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))
BLENDER = "/Applications/Blender.app/Contents/MacOS/Blender"
VENV_PY = os.path.join(REPO, "Tools", ".venv", "bin", "python")
MODELS = os.path.join(REPO, "Tools", "Staging", "Assets", "Models", "BilliardRogue", "Environment")


def blender(script, *args):
    cmd = [BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "--python", os.path.join(HERE, script), "--",
           *args]
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0:
        tail = "\n".join((proc.stdout + proc.stderr).splitlines()[-40:])
        raise SystemExit(f"{script} {' '.join(args)} failed:\n{tail}")
    return proc.stdout


def py(script, *args, venv=True):
    cmd = [VENV_PY if venv else sys.executable, os.path.join(HERE, script), *args]
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0:
        raise SystemExit(f"{script} failed:\n{proc.stdout}\n{proc.stderr}")
    return proc.stdout


def piece_names():
    # env_pieces needs bpy, so read the registry keys from the source instead of importing it
    src = open(os.path.join(HERE, "env_pieces.py")).read()
    reg = src[src.index("PIECES = {"):src.index("TRI_BUDGET")]
    return [line.split('"')[1] for line in reg.splitlines() if line.strip().startswith('"Env_')]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--preview-dir", default=os.environ.get(
        "ENV_PREVIEW_DIR", os.path.join(os.environ.get("TMPDIR", "/tmp"), "billiard_env_preview")))
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--skip-models", action="store_true")
    ap.add_argument("--skip-previews", action="store_true")
    ap.add_argument("--acts", default="1,2,3")
    ap.add_argument("--only", default="")
    a = ap.parse_args()
    t0 = time.time()
    names = [n for n in piece_names() if not a.only or n in a.only.split(",")]
    stats = {}
    if not a.skip_models:
        chunks = [names[i::a.jobs] for i in range(a.jobs)]
        work = os.path.join(a.preview_dir, "_stats")
        os.makedirs(work, exist_ok=True)

        def run(i):
            if not chunks[i]:
                return
            blender("build_env.py", "--only", ",".join(chunks[i]), "--stats", os.path.join(work, f"stats_{i}.json"))
        with ThreadPoolExecutor(a.jobs) as pool:
            list(pool.map(run, range(a.jobs)))
        for f in glob.glob(os.path.join(work, "stats_*.json")):
            stats.update(json.load(open(f)))
            os.remove(f)
        with open(os.path.join(a.preview_dir, "env_stats.json"), "w") as f:
            json.dump(stats, f, indent=1, sort_keys=True)
        changed = sum(1 for s in stats.values() if s["fbx_changed"])
        print(f"models: {len(stats)} FBX ({changed} changed), max tris {max(s['tris'] for s in stats.values())}")
        out = blender("verify_env.py", "--report", os.path.join(a.preview_dir, "env_verify.json"))
        print(next(line for line in out.splitlines() if line.startswith("VERIFY_SUMMARY")))
    print(py("make_layouts.py", venv=False).strip())
    if a.skip_previews:
        print(f"done in {time.time() - t0:.1f}s")
        return
    pv = a.preview_dir
    stand_in = os.path.join(pv, "preview_surfaces")
    py("make_preview_surfaces.py", "--out-dir", stand_in)
    acts = [x for x in a.acts.split(",") if x]
    layouts = json.load(open(os.path.join(HERE, "layouts.json")))
    cams = ("game", "requested") if "requested" in layouts["camera"] else ("game",)   # requested = pending pose
    jobs = [("render_pieces.py", "--out-dir", os.path.join(pv, "_pieces"), "--preview-surfaces", stand_in)]
    for act in acts:
        for cam, extra, tag in (("game", (), "game"), ("game", ("--actors",), "game_actors"),
                                ("game", ("--mask",), "game_mask"), ("requested", (), "requested"),
                                ("requested", ("--actors",), "requested_actors"),
                                ("requested", ("--mask",), "requested_mask"), ("overview", (), "overview")):
            if cam not in cams and cam != "overview":
                continue
            jobs.append(("render_diorama.py", "--act", act, "--camera", cam, "--preview-surfaces", stand_in,
                         "--out", os.path.join(pv, "_raw", f"act{act}_{tag}"), *extra))
    os.makedirs(os.path.join(pv, "_raw"), exist_ok=True)
    with ThreadPoolExecutor(a.jobs) as pool:
        list(pool.map(lambda j: blender(*j), jobs))
    outs, labels = [], []
    names = {str(x["id"]): x["name"] for x in layouts["acts"]}
    req_outs, req_labels = [], []
    for act in acts:
        for tag, name, extra in (("game", "diorama", ()), ("game_actors", "actors", ()),
                                 ("requested", "requested", ()), ("requested_actors", "requested_actors", ()),
                                 ("overview", "overview", ("--no-tilt",))):
            if tag.startswith("requested") and "requested" not in cams:
                continue
            out = os.path.join(pv, f"act{act}_{name}.png")
            py("post_diorama.py", "--npy", os.path.join(pv, "_raw", f"act{act}_{tag}.npy"), "--act", act,
               "--out", out, *extra)
            if name in ("diorama", "actors"):
                outs.append(out)
                labels.append(f"Act {act} - {names[act]}" + (" (with actors)" if name == "actors" else "") +
                              " - ArenaConfig camera")
            elif name.startswith("requested"):
                req_outs.append(out)
                req_labels.append(f"Act {act} - {names[act]}" + (" (with actors)" if "actors" in name else "") +
                                  " - requested camera (aim z 5.8)")
    py("post_diorama.py", "--sheet", os.path.join(pv, "acts_contact_sheet.png"), "--labels", "|".join(labels), *outs)
    if req_outs:
        py("post_diorama.py", "--sheet", os.path.join(pv, "acts_requested_camera_sheet.png"), "--labels",
           "|".join(req_labels), *req_outs)
    for cam in cams:
        print(py("measure_previews.py", "--preview-dir", pv, "--acts", ",".join(acts), "--camera", cam,
                 "--crops", os.path.join(pv, f"boss_backdrops_{cam}.png")).strip())
    py("sheet_pieces.py", os.path.join(pv, "_pieces"), os.path.join(pv, "pieces_sheet.png"))
    print(f"previews -> {pv} ({time.time() - t0:.1f}s)")


if __name__ == "__main__":
    main()
