"""Billiard Rogue audio build: shared-repo picks + synthesized gaps -> staged WAV/OGG, manifest, review sheets.

Regenerate everything (deterministic, re-runnable; ~25 s warm, a few minutes when the LFS objects are not local yet):

    /Users/simonbut/project/VibeProject3/Tools/.venv/bin/python /Users/simonbut/project/VibeProject3/Tools/Audio/build_audio.py

Options: --staging DIR (default Tools/Staging)  --cache DIR (default Tools/Staging/.cache/audio_src)
         --preview DIR (review PNG/JSON/MD; default: none)  --jobs N (parallel LFS fetches)
Outputs (paths mirror Starter/Assets; integration copies Tools/Staging/Assets into the Unity project):
  Assets/Audio/Sfx/BilliardRogue/<SoundEffect>_<n>.wav   44.1 kHz mono 16-bit PCM, trimmed, faded, loudness-matched,
                                                         peak <= -3 dBFS
  Assets/Audio/Bgm/BilliardRogue/<BgmType>.ogg           44.1 kHz stereo Vorbis q5, -18 LUFS (static gain), loops intact
  Assets/Audio/Bgm/BilliardRogue/Stinger_<StingerType>.ogg  stereo music stingers for BgmManager.PlayStinger (the
                                                         StageClear/BossAppear/Victory/GameOver SoundEffects are short
                                                         accents that layer on top of them, not copies)
  Tools/Audio/audio_manifest.json                        enum name -> Unity asset paths, source of every file, and each
                                                         SFX file's peak/length (AudioRegistryBuilder's import check)
Checks (exit 1 on failure): formats, peak ceilings, loop seams, BGM loudness, combat loudness ladder, onset timing of
instant-VFX SFX and UI clicks, C6 ping tuning of the hit families, and worst-case summing headroom (picks.py).
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
PEAK_CEILING_DB = picks.SFX_PEAK_CEILING_DB


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
            # The fade-in must end before the peak (a 2 ms lead gets a 1 ms ramp).
            x = al.fade(x[onset * hop:], in_ms=min(3.0, max(0.5, layer.from_peak_ms / 2)), out_ms=0.0)
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


def target_of(spec: picks.Sfx) -> float:
    return spec.target if spec.target is not None else picks.TARGETS[spec.category]


def normalize_variants(spec: picks.Sfx, mixes: list) -> list:
    """All variants of one SoundEffect land on the same short-window loudness: the target, or lower when the peak
    ceiling stops the peakiest variant from reaching it (random round-robin picks must not jump in level)."""
    reachable = [min(target_of(spec), al.max_short_lufs(x) + PEAK_CEILING_DB - al.db(np.max(np.abs(x)))) for x in mixes]
    common = min(reachable)
    return [x * 10 ** ((common - al.max_short_lufs(x)) / 20) for x in mixes]


def render_stinger_stereo(stinger: picks.Stinger, paths) -> np.ndarray:
    x = al.trim(al.decode(paths[stinger.rel], 2), start_db=-50.0, end_db=-60.0, preroll_ms=0.5)
    x = al.fade(x, in_ms=0.5, out_ms=stinger.fade_ms)
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

    rels = {b.rel for b in picks.BGM.values()} | {st.rel for st in picks.STINGERS.values()}
    for spec in picks.SFX.values():
        for variant in spec.variants:
            rels.update(layer.rel for layer in variant.layers if isinstance(layer, picks.R))
    paths = shared_repo.fetch_all(rels, a.cache, a.jobs)
    pruned = shared_repo.prune_cache(a.cache, rels)
    print(f"sources: {len(paths)} files materialized/verified ({pruned} unused cache files pruned)")

    manifest = {"sfx": {}, "bgm": {}, "stingers": {}, "sources": {}, "sfx_levels": {}}
    report = {"sfx": {}, "bgm": {}, "stingers": {}}
    review_rows = []
    kept_sfx, kept_bgm = set(), set()
    sfx_files = {}

    for name in picks.SFX_ORDER:
        if name in picks.SFX_ALIASES:
            continue
        spec = picks.SFX[name]
        files = []
        mixes = normalize_variants(spec, [render_sfx(spec, variant, paths) for variant in spec.variants])
        for i, (variant, x) in enumerate(zip(spec.variants, mixes), start=1):
            fname = f"{name}_{i}.wav"
            al.write_wav_mono16(os.path.join(sfx_dir, fname), x)
            written = al.decode(os.path.join(sfx_dir, fname), 1)
            info = al.analyze(written)
            info.update({"effect": name, "category": spec.category, "bytes": os.path.getsize(os.path.join(sfx_dir, fname)),
                         "probe": shared_repo.probe(os.path.join(sfx_dir, fname))})
            if name in picks.PING_FAMILIES:
                info["ping_partial_hz"] = round(al.strongest_partial_hz(written), 2)
            report["sfx"][fname] = info
            files.append(f"{SFX_REL}/{fname}")
            manifest["sources"][fname] = source_label(variant)
            # What the file holds, for AudioRegistryBuilder's check that Unity imported it without re-normalizing.
            manifest["sfx_levels"][f"{SFX_REL}/{fname}"] = {"peak_db": info["peak_db"], "samples": info["samples"]}
            kept_sfx.add(fname)
            review_rows.append((name, fname, spec, written, info))
        sfx_files[name] = files
    manifest["sfx"] = {name: sfx_files[picks.SFX_ALIASES.get(name, name)] for name in picks.SFX_ORDER}

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

    for name, stinger in picks.STINGERS.items():
        fname = f"Stinger_{name}.ogg"
        y = render_stinger_stereo(stinger, paths)
        out = os.path.join(bgm_dir, fname)
        al.encode_ogg(out, y, 5.0)
        info = verify_ogg(out, y)
        info["bytes"] = os.path.getsize(out)
        info["source"] = shared_repo.repo_path(stinger.rel)
        report["stingers"][fname] = info
        manifest["stingers"][name] = f"{BGM_REL}/{fname}"
        manifest["sources"][fname] = shared_repo.repo_path(stinger.rel)
        kept_bgm.add(fname)

    clean_stale(sfx_dir, kept_sfx)
    clean_stale(bgm_dir, kept_bgm)
    manifest["sfx_levels"] = dict(sorted(manifest["sfx_levels"].items()))
    with open(os.path.join(HERE, "audio_manifest.json"), "w") as fh:
        json.dump(manifest, fh, indent=2)
        fh.write("\n")

    report["headroom"] = headroom(sfx_dir, bgm_dir)
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
            problems.append(f"{fname}: peak {r['peak_db']} dBFS over the {PEAK_CEILING_DB} dBFS ceiling")
    for fname, r in {**report["bgm"], **report["stingers"]}.items():
        if not r["length_matches_source"]:
            problems.append(f"{fname}: decoded length {r['samples']} != source")
        if r["peak_db"] > picks.BGM_PEAK_CEILING_DB + 0.5:  # Vorbis may overshoot the pre-encode ceiling slightly
            problems.append(f"{fname}: decoded peak {r['peak_db']} dBFS")
    for fname, r in report["bgm"].items():
        if r["wrap_step_vs_p999"] > 1.0:
            problems.append(f"{fname}: loop wrap step {r['wrap_step_vs_p999']} x p99.9 (possible click)")
        if abs(r["int_lufs"] - picks.BGM_TARGET_LUFS) > 1.0:
            problems.append(f"{fname}: {r['int_lufs']} LUFS (target {picks.BGM_TARGET_LUFS})")
    return problems + mix_problems(report)


def files_of(report, effects) -> list:
    return [r for r in report["sfx"].values() if r["effect"] in effects]


def mix_problems(report) -> list:
    """Director-review mix rules: monotonic loudness ladder, instant onsets, tight UI clicks, in-tune pings, headroom."""
    problems = []
    below = None
    for tier, effects in picks.LADDER:
        key = "onset_lufs" if tier == "big" else "short_lufs"
        values = [r[key] for r in files_of(report, effects)]
        if below is not None and min(values) < below[1] + picks.LADDER_MIN_STEP_LU - 0.05:
            problems.append(f"ladder: {tier} min {key} {min(values):.2f} < {below[0]} max {below[1]:.2f} + {picks.LADDER_MIN_STEP_LU} LU")
        below = (tier, max(values))
    big_short = min(r["short_lufs"] for r in files_of(report, picks.LADDER[-1][1]))
    for r in files_of(report, picks.NOT_LOUDER_THAN_BIG):
        if r["short_lufs"] > big_short + 0.05:
            problems.append(f"ladder: {r['effect']} {r['short_lufs']:.2f} is louder than the quietest big-tier file ({big_short:.2f})")
    for fname, r in report["sfx"].items():
        if r["effect"] in picks.INSTANT:
            if r["to_3db_ms"] > picks.INSTANT_MAX_MS:
                problems.append(f"{fname}: -3 dB of peak at {r['to_3db_ms']} ms (instant VFX sound, max {picks.INSTANT_MAX_MS})")
            if r["onset_lufs"] < r["short_lufs"] - picks.INSTANT_ONSET_LU:
                problems.append(f"{fname}: onset {r['onset_lufs']:.2f} is > {picks.INSTANT_ONSET_LU} LU under its loudest "
                                f"window {r['short_lufs']:.2f} (loudest part trails the VFX)")
        if r["effect"] in picks.UI_CLICKS and r["to_3db_ms"] > picks.UI_CLICK_MAX_MS:
            problems.append(f"{fname}: UI click reaches -3 dB of peak at {r['to_3db_ms']} ms (max {picks.UI_CLICK_MAX_MS})")
        if "ping_partial_hz" in r:
            cents = 1200 * np.log2(r["ping_partial_hz"] / picks.PING_HZ)
            if abs(cents) > picks.PING_TOLERANCE_CENTS:
                problems.append(f"{fname}: strongest 1.0-1.1 kHz partial {r['ping_partial_hz']} Hz = {cents:+.1f} cents off C6")
    for row in report["headroom"]:
        if row["max_after_trim_db"] > -picks.HEADROOM_MIN_MARGIN_DB:
            problems.append(f"headroom: {row['scenario']} over {row['loop']} peaks at {row['max_after_trim_db']:+.2f} dBFS "
                            f"after the planned {picks.PLANNED_MASTER_TRIM_DB} dB master trim "
                            f"(needs {picks.HEADROOM_MIN_MARGIN_DB} dB margin)")
    return problems


def headroom(sfx_dir: str, bgm_dir: str, step_s: float = 0.25) -> list:
    """Sum worst-case event chains over whole loops like Unity would play them (mono SFX on a 2D source = same signal on
    both channels at unity gain, default sliders, stinger over music ducked linearly to `duck` in 0.15 s) and report the
    output peak at every `step_s` offset, raw and after the planned master trim."""
    cache = {}

    def load(path, channels):
        if path not in cache:
            cache[path] = al.decode(path, channels)
        return cache[path]

    rows = []
    for label, loops, events, stinger, duck in picks.HEADROOM_SCENARIOS:
        clips = [(load(os.path.join(sfx_dir, f), 1), t) for f, t in events]
        st = load(os.path.join(bgm_dir, stinger), 2) if stinger else None
        length = max([int(t * al.SR) + len(x) for x, t in clips] + ([len(st)] if st is not None else []))
        overlay = np.zeros((length, 2))
        for x, t in clips:
            i = int(t * al.SR)
            overlay[i:i + len(x)] += x[:, None]
        if st is not None:
            overlay[:len(st)] += st
        ramp = np.full(length, duck)
        n_duck = int(0.15 * al.SR)
        ramp[:n_duck] = np.linspace(1.0, duck, n_duck)
        for loop in loops:
            music = load(os.path.join(bgm_dir, f"{loop}.ogg"), 2)
            wrapped = np.concatenate([music, music[:length]])  # offsets near the end wrap into the loop start
            peaks = np.array([np.abs(wrapped[o:o + length] * ramp[:, None] + overlay).max()
                              for o in range(0, len(music), int(step_s * al.SR))])
            peaks_db = 20 * np.log10(peaks)
            rows.append({"scenario": label, "loop": loop, "offsets": len(peaks),
                         "max_db": round(float(peaks_db.max()), 2), "median_db": round(float(np.median(peaks_db)), 2),
                         "clipping_offsets_pct": round(float(np.mean(peaks > 1.0) * 100), 1),
                         "max_after_trim_db": round(float(peaks_db.max() + picks.PLANNED_MASTER_TRIM_DB), 2)})
    return rows


def print_table(report):
    print(f"{'file':28s} {'dur':>6s} {'peak':>6s} {'sLUFS':>6s} {'onset':>6s} {'cen':>6s} {'-3dB':>6s} {'ping':>8s}")
    for fname, r in report["sfx"].items():
        print(f"{fname:28s} {r['dur_s']:6.2f} {r['peak_db']:6.1f} {r['short_lufs']:6.1f} {r['onset_lufs']:6.1f} "
              f"{r['centroid_hz']:6d} {r['to_3db_ms']:6.1f} {r.get('ping_partial_hz', ''):>8}")
    for fname, r in {**report["bgm"], **report["stingers"]}.items():
        print(f"{fname:28s} {r['samples'] / al.SR:6.1f}s I {r['int_lufs']:6.1f} pk {r['peak_db']:5.1f} "
              f"wrap {r['wrap_step_vs_p999']:.2f} {r['bytes'] // 1024} KB")
    for tier, effects in picks.LADDER:
        key = "onset_lufs" if tier == "big" else "short_lufs"
        values = [r[key] for r in files_of(report, effects)]
        print(f"ladder {tier:6s} {key:10s} {min(values):6.2f} .. {max(values):6.2f}  ({', '.join(effects)})")
    for row in report["headroom"]:
        print(f"headroom {row['scenario']:30s} {row['loop']:5s} max {row['max_db']:+6.2f} median {row['median_db']:+6.2f} dBFS, "
              f"{row['clipping_offsets_pct']:5.1f}% of offsets > 0 dBFS raw; after {picks.PLANNED_MASTER_TRIM_DB:+.0f} dB trim "
              f"max {row['max_after_trim_db']:+6.2f}")


# ----------------------------------------------------------------------------------------------------------
# Review output (not part of the game assets)
# ----------------------------------------------------------------------------------------------------------

SHEETS = {
    "audio_sfx_ui_flow.png": ("UI, flow, pickups, player", {"ui", "ui_accent", "pickup"}, 1.2),
    "audio_sfx_combat.png": ("Cue, ball, hits, enemies, status", {"ball", "bounce", "hit", "big", "magic", "enemy"}, 1.0),
    "audio_sfx_accents.png": ("Flow accents (layer on top of Stinger_*.ogg)", {"accent"}, 1.4),
}
SHEET_CATEGORY = {"ui": "ui", "ui_accent": "flow", "pickup": "pickup", "ball": "ball", "bounce": "ball", "hit": "impact",
                  "big": "enemy", "magic": "magic", "enemy": "enemy", "accent": "stinger"}


def write_preview(out_dir, rows, report, bgm_dir):
    os.makedirs(out_dir, exist_ok=True)
    for png, (title, cats, max_s) in SHEETS.items():
        items = []
        for name, fname, spec, x, info in rows:
            if spec.category not in cats:
                continue
            items.append({"label": f"{fname}  [{spec.category}]",
                          "sub": f"{info['dur_s']:.2f}s pk {info['peak_db']:.1f} sL {info['short_lufs']:.1f}/{target_of(spec):.1f} "
                                 f"on {info['onset_lufs']:.1f} cen {info['centroid_hz']} -3dB@{info['to_3db_ms']}ms",
                          "samples": x, "category": SHEET_CATEGORY[spec.category]})
        sheet.sfx_sheet(items, os.path.join(out_dir, png), f"Billiard Rogue SFX - {title}", cols=4, max_s=max_s)
    bars = [{"label": f"{fname} [{spec.category}]", "value_db": info["short_lufs"], "onset_db": info["onset_lufs"],
             "category": SHEET_CATEGORY[spec.category], "target": target_of(spec)} for name, fname, spec, x, info in rows]
    sheet.loudness_bars(bars, os.path.join(out_dir, "audio_sfx_loudness.png"),
                        f"SFX 100 ms loudness (LUFS): white = target, dark = onset, cap {PEAK_CEILING_DB:g} dBFS")
    timed = [(fname, spec, x) for name, fname, spec, x, info in rows
             if name in picks.INSTANT or name in picks.UI_CLICKS or name in ("RewardReveal", "CountdownTick")]
    sheet.onset_sheet([{"label": fname, "samples": x, "category": SHEET_CATEGORY[spec.category]} for fname, spec, x in timed],
                      os.path.join(out_dir, "audio_sfx_onsets.png"), "SFX onsets: instant-VFX sounds, UI clicks, card deals")
    sheet.headroom_bars(report["headroom"], os.path.join(out_dir, "audio_headroom.png"),
                        "Worst-case summing over the loudest loops (Unity 2D, unity gain)", picks.PLANNED_MASTER_TRIM_DB)
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
