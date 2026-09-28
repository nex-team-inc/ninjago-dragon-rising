"""Billiard Rogue retro SFX presets -> WAV files.

python make_sfx.py --out-dir OUT [--only ball_hit_wall,coin] [--variants 3]
File names: SFX_BR_<Name>[_<n>].wav  (variants differ by seed + slight pitch, feed SfxManager random pick)
"""
import argparse
import os
import time

from sfxsynth import Voice, render, write_wav


def presets(p=1.0):
    """p = pitch multiplier for variants."""
    return {
        "BallShoot": [
            Voice("square", freq=520 * p, slide=2.5, duty=0.25, duty_sweep=1.0, attack=0.0, sustain=0.03, decay=0.12, punch=0.4),
            Voice("noise", freq=6000, slide=-3, attack=0.0, sustain=0.01, decay=0.08, volume=0.35, highpass=1500),
        ],
        "BallHitWall": [
            Voice("square", freq=880 * p, slide=-1.0, duty=0.5, attack=0.0, sustain=0.015, decay=0.05, crush_bits=6),
        ],
        "BallHitEnemy": [
            Voice("square", freq=330 * p, slide=-3.0, duty=0.3, attack=0.0, sustain=0.02, decay=0.10, punch=0.6),
            Voice("noise", freq=2500, slide=-2, attack=0.0, sustain=0.02, decay=0.09, volume=0.6, lowpass=5000),
        ],
        "EnemyDie": [
            Voice("noise", freq=1800 * p, slide=-2.2, attack=0.0, sustain=0.06, decay=0.45, punch=0.5, lowpass=4000, crush_rate=11025),
            Voice("square", freq=200 * p, slide=-2.0, duty=0.5, attack=0.0, sustain=0.03, decay=0.25, volume=0.5),
        ],
        "Coin": [
            Voice("square", freq=988 * p, duty=0.5, arp=[(0.06, 5)], attack=0.0, sustain=0.10, decay=0.18, punch=0.3),
        ],
        "PowerUp": [
            Voice("square", freq=300 * p, slide=2.2, duty=0.25, vib_depth=0.4, vib_speed=18, attack=0.0, sustain=0.25, decay=0.2),
            Voice("triangle", freq=150 * p, slide=2.2, attack=0.0, sustain=0.25, decay=0.2, volume=0.5),
        ],
        "PlayerHurt": [
            Voice("square", freq=440 * p, slide=-2.5, duty=0.5, duty_sweep=-1.2, attack=0.0, sustain=0.05, decay=0.22, punch=0.5),
            Voice("noise", freq=900, attack=0.0, sustain=0.03, decay=0.12, volume=0.5, crush_rate=8000),
        ],
        "BallReturn": [
            Voice("sine", freq=600 * p, slide=1.5, attack=0.002, sustain=0.02, decay=0.07),
            Voice("triangle", freq=1200 * p, slide=1.5, attack=0.002, sustain=0.01, decay=0.05, volume=0.3),
        ],
        "BossRoar": [
            Voice("saw", freq=90 * p, slide=-0.4, vib_depth=1.5, vib_speed=9, attack=0.05, sustain=0.5, decay=0.6, lowpass=1800, crush_bits=7),
            Voice("noise", freq=700, vib_depth=3, vib_speed=9, attack=0.05, sustain=0.5, decay=0.5, volume=0.45, lowpass=1200),
        ],
        "TurnStart": [
            Voice("triangle", freq=523 * p, arp=[(0.09, 4), (0.18, 3)], attack=0.005, sustain=0.28, decay=0.3),
            Voice("square", freq=1046 * p, duty=0.125, arp=[(0.09, 4), (0.18, 3)], attack=0.005, sustain=0.28, decay=0.3, volume=0.25),
        ],
        "UiMove": [
            Voice("square", freq=1200 * p, duty=0.5, attack=0.0, sustain=0.012, decay=0.03, volume=0.8),
        ],
        "UiConfirm": [
            Voice("square", freq=660 * p, duty=0.25, arp=[(0.05, 7)], attack=0.0, sustain=0.08, decay=0.12),
        ],
        "UiBack": [
            Voice("square", freq=660 * p, duty=0.25, arp=[(0.05, -5)], attack=0.0, sustain=0.06, decay=0.10),
        ],
        "Combo": [
            Voice("square", freq=784 * p, duty=0.5, arp=[(0.04, 4), (0.08, 3), (0.12, 5)], attack=0.0, sustain=0.16, decay=0.12, crush_bits=5),
        ],
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out-dir", required=True)
    ap.add_argument("--only", default="")
    ap.add_argument("--variants", type=int, default=1)
    a = ap.parse_args()
    os.makedirs(a.out_dir, exist_ok=True)
    only = set(filter(None, a.only.split(",")))
    t0 = time.time()
    count, total_bytes = 0, 0
    for vi in range(a.variants):
        pitch = [1.0, 1.06, 0.94, 1.12, 0.89][vi % 5]
        for name, voices in presets(pitch).items():
            if only and name not in only:
                continue
            suffix = f"_{vi + 1}" if a.variants > 1 else ""
            path = os.path.join(a.out_dir, f"SFX_BR_{name}{suffix}.wav")
            write_wav(path, render(voices, seed=vi * 101 + 7))
            count += 1
            total_bytes += os.path.getsize(path)
    print("SFX_STATS", {"files": count, "bytes": total_bytes, "s": round(time.time() - t0, 3)})


if __name__ == "__main__":
    main()
