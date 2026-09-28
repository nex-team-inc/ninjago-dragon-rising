"""Billiard Rogue audio build: shared-repo picks + synthesized gaps -> staged WAV/OGG, manifest, review sheets.

Regenerate everything (deterministic, re-runnable; ~25 s warm, a few minutes when the LFS objects are not local yet):

    /Users/simonbut/project/VibeProject3/Tools/.venv/bin/python /Users/simonbut/project/VibeProject3/Tools/Audio/build_audio.py

Options: --staging DIR (default Tools/Staging)  --cache DIR (default Tools/Staging/.cache/audio_src)
         --preview DIR (review PNG/JSON/MD; default: none)  --jobs N (parallel LFS fetches)
Outputs (paths mirror Starter/Assets; integration copies Tools/Staging/Assets into the Unity project):
  Assets/Audio/Sfx/BilliardRogue/<SoundEffect>_<n>.wav   44.1 kHz mono 16-bit PCM, trimmed, faded, loudness-matched
  Assets/Audio/Bgm/BilliardRogue/<BgmType>.ogg           44.1 kHz stereo Vorbis q5, -16 LUFS (static gain), loops intact
  Assets/Audio/Bgm/BilliardRogue/Stinger_<StingerType>.ogg  stereo stingers for BgmManager.PlayStinger (same sources
                                                         as the StageClear/BossAppear/Victory/GameOver SoundEffects)
  Tools/Audio/audio_manifest.json                        enum name -> Unity asset paths (+ source of every file)
Sources are materialized per file from ~/Documents/music-cell-shared-assets (see shared_repo.py); picks: picks.py.
"""
import argparse
import json
import os
import sys

import numpy as np

import audiolib as al
import picks
import shared_repo
import sheet
from synth_presets import synth

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SFX_REL = "Assets/Audio/Sfx/BilliardRogue"
BGM_REL = "Assets/Audio/Bgm/BilliardRogue"
PEAK_CEILING_DB = -1.0


# ----------------------------------------------------------------------------------------------------------
# SFX
# ----------------------------------------------------------------------------------------------------------

def render_layer(layer, paths):
    if isinstance(layer, picks.S):
        x = synth(layer.preset, layer.p, layer.seed)
    else:
        x = al.decode(paths[layer.rel], 1)
        x = al.cut(x, layer.start, layer.end)
        x = al.trim(x, start_db=-45.0, end_db=-60.0)  # onset-align every layer before offsets are applied
        if layer.from_peak_ms >= 0:
            hop = int(0.001 * al.SR)  # al.envelope(x, 1.0) uses this hop (~1 ms bins)
            onset = max(0, int(np.argmax(al.envelope(x, 1.0))) - int(layer.from_peak_ms))
            x = al.fade(x[onset * hop:], in_ms=3.0, out_ms=0.0)
        if layer.hp:
            x = al.highpass(x, layer.hp)
        if layer.lp:
            x = al.lowpass(x, layer.lp)
        if layer.semis:
            x = al.repitch(x, layer.semis)
        if layer.max_s:
            x = al.fit_length(x, layer.max_s, fade_ms=min(120.0, layer.max_s * 300))
    x = x * (10 ** (PEAK_CEILING_DB / 20) / (np.max(np.abs(x)) or 1.0))
    return x, layer.gain, layer.at


def render_sfx(spec: picks.Sfx, variant: picks.V, paths) -> np.ndarray:
    x = al.mix(*[render_layer(layer, paths) for layer in variant.layers])
    if variant.semis:
        x = al.repitch(x, variant.semis)
    if variant.drive_db:
        drive = 10 ** (variant.drive_db / 20)
        x = np.tanh(x / np.max(np.abs(x)) * drive) / np.tanh(drive)
    x = al.highpass(x, 25.0)  # DC / sub-rumble that TV speakers cannot play anyway
    x = al.trim(x, start_db=-50.0, end_db=-60.0, preroll_ms=0.5)
    if variant.max_s:
        x = al.fit_length(x, variant.max_s, variant.fade_ms)
    return al.fade(x, in_ms=0.5, out_ms=variant.fade_ms)


def loudness(spec: picks.Sfx, x: np.ndarray) -> float:
    return al.integrated_lufs(x) if spec.category == "stinger" else al.max_short_lufs(x)


def normalize_variants(spec: picks.Sfx, mixes: list) -> list:
    """All variants of one SoundEffect land on the same loudness: the category target, or lower when the -1 dBFS
    ceiling stops the peakiest variant from reaching it (random round-robin picks must not jump in level)."""
    target = spec.target if spec.target is not None else picks.TARGETS[spec.category]
    reachable = [min(target, loudness(spec, x) + PEAK_CEILING_DB - al.db(np.max(np.abs(x)))) for x in mixes]
    common = min(reachable)
    return [x * 10 ** ((common - loudness(spec, x)) / 20) for x in mixes]


def render_stinger_stereo(spec: picks.Sfx, variant: picks.V, paths) -> np.ndarray:
    layer = variant.layers[0]
    x = al.trim(al.decode(paths[layer.rel], 2), start_db=-50.0, end_db=-60.0, preroll_ms=0.5)
    if variant.max_s:
        x = al.fit_length(x, variant.max_s, variant.fade_ms)
    x = al.fade(x, in_ms=0.5, out_ms=variant.fade_ms)
    y, _, _ = al.normalize_integrated(x, picks.BGM_TARGET_LUFS, picks.BGM_PEAK_CEILING_DB)
    return y


def source_label(variant: picks.V) -> str:
    parts = []
    for layer in variant.layers:
        parts.append(f"synth:{layer.preset}" if isinstance(layer, picks.S) else shared_repo.repo_path(layer.rel))
    return " + ".join(parts)


# ----------------------------------------------------------------------------------------------------------
# BGM
# ----------------------------------------------------------------------------------------------------------

def process_bgm(src: str):
    x = al.decode(src, 2)
    info = {"source_samples": len(x)}
    if len(x) > 1 and np.array_equal(x[-1], x[0]) and np.any(x[0] != 0):
        x = x[:-1]  # the file repeats the loop's first frame at the end -> drop it (1-sample stutter)
        info["dropped_duplicate_wrap_frame"] = True
    before = al.integrated_lufs(x)
    y, gain, capped = al.normalize_integrated(x, picks.BGM_TARGET_LUFS, picks.BGM_PEAK_CEILING_DB)
    shortfall = (picks.BGM_TARGET_LUFS - before) - gain
    if capped and shortfall > 1.0:
        # More than 1 LU short: take the full gain and round the few peaks off with a memoryless (loop-safe) knee.
        y = al.soft_limit(x * 10 ** ((picks.BGM_TARGET_LUFS - before) / 20), picks.BGM_PEAK_CEILING_DB, 3.0)
        info["soft_limited"] = True
    info.update({"source_lufs": round(before, 2), "gain_db": round(gain, 2), "peak_capped": bool(capped)})
    return y, info


def verify_ogg(path: str, reference: np.ndarray) -> dict:
    z = al.decode(path, 2)
    return {
        "samples": len(z),
        "length_matches_source": len(z) == len(reference),
        "int_lufs": round(al.integrated_lufs(z), 2),
        "peak_db": round(al.db(float(np.max(np.abs(z)))), 2),
        "loop": al.loop_report(z),
        "wrap_step_vs_p999": round(float(np.max(np.abs(z[0] - z[-1])) /
                                         np.percentile(np.abs(np.diff(z, axis=0)).max(axis=1), 99.9)), 3),
    }


# ----------------------------------------------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------------------------------------------

def clean_stale(folder: str, keep: set, exts=(".wav", ".ogg")):
    for name in sorted(os.listdir(folder)):
        if name.endswith(exts) and name not in keep:
            os.remove(os.path.join(folder, name))
            print(f"removed stale {name}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--staging", default=os.path.join(ROOT, "Tools", "Staging"))
    ap.add_argument("--cache", default=os.path.join(ROOT, "Tools", "Staging", ".cache", "audio_src"))
    ap.add_argument("--preview", default="")
    ap.add_argument("--jobs", type=int, default=12)
    a = ap.parse_args()

    sfx_dir = os.path.join(a.staging, SFX_REL)
    bgm_dir = os.path.join(a.staging, BGM_REL)
    os.makedirs(sfx_dir, exist_ok=True)
    os.makedirs(bgm_dir, exist_ok=True)

    rels = {b.rel for b in picks.BGM.values()}
    for spec in picks.SFX.values():
        for variant in spec.variants:
            rels.update(layer.rel for layer in variant.layers if isinstance(layer, picks.R))
    paths = shared_repo.fetch_all(rels, a.cache, a.jobs)
    pruned = shared_repo.prune_cache(a.cache, rels)
    print(f"sources: {len(paths)} files materialized/verified ({pruned} unused cache files pruned)")

    manifest = {"sfx": {}, "bgm": {}, "stingers": {}, "sources": {}}
    report = {"sfx": {}, "bgm": {}, "stingers": {}}
    review_rows = []
    kept_sfx, kept_bgm = set(), set()

    for name in picks.SFX_ORDER:
        spec = picks.SFX[name]
        files = []
        mixes = normalize_variants(spec, [render_sfx(spec, variant, paths) for variant in spec.variants])
        for i, (variant, x) in enumerate(zip(spec.variants, mixes), start=1):
            fname = f"{name}_{i}.wav"
            al.write_wav_mono16(os.path.join(sfx_dir, fname), x)
            written = al.decode(os.path.join(sfx_dir, fname), 1)
            info = al.analyze(written)
            info.update({"category": spec.category, "bytes": os.path.getsize(os.path.join(sfx_dir, fname)),
                         "probe": shared_repo.probe(os.path.join(sfx_dir, fname))})
            report["sfx"][fname] = info
            files.append(f"{SFX_REL}/{fname}")
            manifest["sources"][fname] = source_label(variant)
            kept_sfx.add(fname)
            review_rows.append((name, fname, spec, written, info))
        manifest["sfx"][name] = files

    for name, bgm in picks.BGM.items():
        fname = f"{name}.ogg"
        y, info = process_bgm(paths[bgm.rel])
        out = os.path.join(bgm_dir, fname)
        al.encode_ogg(out, y, 5.0)
        info.update(verify_ogg(out, y))
        info["bytes"] = os.path.getsize(out)
        info["source"] = shared_repo.repo_path(bgm.rel)
        report["bgm"][fname] = info
        manifest["bgm"][name] = f"{BGM_REL}/{fname}"
        manifest["sources"][fname] = shared_repo.repo_path(bgm.rel)
        kept_bgm.add(fname)
    for alias, target in picks.BGM_ALIASES.items():
        manifest["bgm"][alias] = manifest["bgm"][target]
    manifest["bgm"] = {k: manifest["bgm"][k] for k in ["Main", "Title", "Act1", "Act2", "Act3", "Boss", "Reward"]}

    for name, sfx_name in picks.STINGERS.items():
        spec = picks.SFX[sfx_name]
        fname = f"Stinger_{name}.ogg"
        y = render_stinger_stereo(spec, spec.variants[0], paths)
        out = os.path.join(bgm_dir, fname)
        al.encode_ogg(out, y, 5.0)
        info = verify_ogg(out, y)
        info["bytes"] = os.path.getsize(out)
        info["source"] = source_label(spec.variants[0])
        report["stingers"][fname] = info
        manifest["stingers"][name] = f"{BGM_REL}/{fname}"
        manifest["sources"][fname] = source_label(spec.variants[0])
        kept_bgm.add(fname)

    clean_stale(sfx_dir, kept_sfx)
    clean_stale(bgm_dir, kept_bgm)
    with open(os.path.join(HERE, "audio_manifest.json"), "w") as fh:
        json.dump(manifest, fh, indent=2)
        fh.write("\n")

    problems = check(report)
    print_table(report)
    if a.preview:
        write_preview(a.preview, review_rows, report, bgm_dir)
    if problems:
        print("PROBLEMS:\n  " + "\n  ".join(problems))
        return 1
    print(f"OK: {len(report['sfx'])} SFX, {len(report['bgm'])} BGM, {len(report['stingers'])} stingers -> {a.staging}")
    return 0


def check(report) -> list:
    problems = []
    for fname, r in report["sfx"].items():
        p = r["probe"]
        if (p.get("codec_name"), p.get("sample_rate"), p.get("channels"), p.get("bits_per_sample")) != ("pcm_s16le", "44100", "1", "16"):
            problems.append(f"{fname}: unexpected format {p}")
        if r["peak_db"] > PEAK_CEILING_DB + 0.05:
            problems.append(f"{fname}: peak {r['peak_db']} dBFS over ceiling")
    for fname, r in {**report["bgm"], **report["stingers"]}.items():
        if not r["length_matches_source"]:
            problems.append(f"{fname}: decoded length {r['samples']} != source")
        if r["peak_db"] > -0.3:
            problems.append(f"{fname}: decoded peak {r['peak_db']} dBFS")
    for fname, r in report["bgm"].items():
        if r["wrap_step_vs_p999"] > 1.0:
            problems.append(f"{fname}: loop wrap step {r['wrap_step_vs_p999']} x p99.9 (possible click)")
        if abs(r["int_lufs"] - picks.BGM_TARGET_LUFS) > 1.0:
            problems.append(f"{fname}: {r['int_lufs']} LUFS (target {picks.BGM_TARGET_LUFS})")
    return problems


def print_table(report):
    print(f"{'file':28s} {'dur':>6s} {'peak':>6s} {'sLUFS':>6s} {'cen':>6s} {'att':>6s}")
    for fname, r in report["sfx"].items():
        print(f"{fname:28s} {r['dur_s']:6.2f} {r['peak_db']:6.1f} {r['short_lufs']:6.1f} {r['centroid_hz']:6d} {r['attack_ms']:6.1f}")
    for fname, r in {**report["bgm"], **report["stingers"]}.items():
        print(f"{fname:28s} {r['samples'] / al.SR:6.1f}s I {r['int_lufs']:6.1f} pk {r['peak_db']:5.1f} "
              f"wrap {r['wrap_step_vs_p999']:.2f} {r['bytes'] // 1024} KB")


# ----------------------------------------------------------------------------------------------------------
# Review output (not part of the game assets)
# ----------------------------------------------------------------------------------------------------------

SHEETS = {
    "audio_sfx_ui_flow.png": ("UI, flow, pickups, player", {"ui", "ui_accent", "pickup"}, 1.2),
    "audio_sfx_combat.png": ("Cue, ball, hits, enemies, status", {"ball", "bounce", "hit", "big", "magic", "enemy"}, 1.0),
    "audio_sfx_stingers.png": ("Stingers (SoundEffects)", {"stinger"}, 8.0),
}
SHEET_CATEGORY = {"ui": "ui", "ui_accent": "flow", "pickup": "pickup", "ball": "ball", "bounce": "ball", "hit": "impact",
                  "big": "enemy", "magic": "magic", "enemy": "enemy", "stinger": "stinger"}


def write_preview(out_dir, rows, report, bgm_dir):
    os.makedirs(out_dir, exist_ok=True)
    for png, (title, cats, max_s) in SHEETS.items():
        items = []
        for name, fname, spec, x, info in rows:
            if spec.category not in cats:
                continue
            target = spec.target if spec.target is not None else picks.TARGETS[spec.category]
            loud = f"I {info.get('int_lufs', 0):.1f}" if spec.category == "stinger" else f"sL {info['short_lufs']:.1f}/{target:.0f}"
            items.append({"label": f"{fname}  [{spec.category}]",
                          "sub": f"{info['dur_s']:.2f}s pk {info['peak_db']:.1f} {loud} cen {info['centroid_hz']} att {info['attack_ms']}ms",
                          "samples": x, "category": SHEET_CATEGORY[spec.category]})
        sheet.sfx_sheet(items, os.path.join(out_dir, png), f"Billiard Rogue SFX - {title}", cols=4, max_s=max_s)
    bars = []
    for name, fname, spec, x, info in rows:
        if spec.category == "stinger":
            continue
        target = spec.target if spec.target is not None else picks.TARGETS[spec.category]
        bars.append({"label": f"{fname} [{spec.category}]", "value_db": info["short_lufs"], "category": SHEET_CATEGORY[spec.category],
                     "target": target})
    sheet.loudness_bars(bars, os.path.join(out_dir, "audio_sfx_loudness.png"),
                        "SFX short-window (100 ms) loudness, LUFS - white tick = category target (-1 dBFS peak cap)")
    bgm_rows = []
    for fname, r in {**report["bgm"], **report["stingers"]}.items():
        z = al.decode(os.path.join(bgm_dir, fname), 2)
        feats = al.music_features(z) if fname in report["bgm"] else {}
        extra = f" bpm~{feats['bpm']} LRA {feats['lra_lu']}" if feats else ""
        bgm_rows.append({"label": f"{fname}  <- {os.path.basename(r.get('source', '') or fname)}",
                         "sub": f"{r['samples'] / al.SR:.2f}s I {r['int_lufs']} LUFS pk {r['peak_db']} dBFS "
                                f"wrap {r['wrap_step_vs_p999']}x p99.9 {r['bytes'] // 1024} KB{extra}",
                         "samples": z, "loop": fname in report["bgm"]})
    sheet.bgm_sheet(bgm_rows, os.path.join(out_dir, "audio_bgm.png"), "Billiard Rogue BGM + stereo stingers (decoded OGG)")
    with open(os.path.join(out_dir, "audio_report.json"), "w") as fh:
        json.dump(report, fh, indent=1)
    with open(os.path.join(HERE, "audio_manifest.json")) as fh:
        manifest = json.load(fh)
    lines = ["| Key | File(s) | Source(s) |", "|---|---|---|"]
    for key, files in manifest["sfx"].items():
        names = [os.path.basename(f) for f in files]
        srcs = sorted({manifest["sources"][n] for n in names})
        lines.append(f"| SoundEffect.{key} | {', '.join(names)} | {'<br>'.join(s.replace('music-cell-shared-assets/Assets/', '') for s in srcs)} |")
    for key, f in manifest["bgm"].items():
        n = os.path.basename(f)
        lines.append(f"| BgmType.{key} | {n} | {manifest['sources'][n].replace('music-cell-shared-assets/Assets/', '')} |")
    with open(os.path.join(out_dir, "assignments.md"), "w") as fh:
        fh.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    sys.exit(main())
