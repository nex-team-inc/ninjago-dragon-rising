"""Billiard Rogue synthesized SFX layers (sfxr-style, see sfxsynth.py). Each preset -> mono float array.

Used where the shared repo has no good file, or as a layer on top of a sourced sample:
  hit_ping      tonal "tink" on every ball hit. Fixed pitch (C6) in every variant, so the runtime combo pitch
                steps (AudioSource.pitch = 2^(combo/12)) read as a rising scale instead of random noise.
  cue_thump     low body under the pool-ball clack of the cue strike.
  enemy_step    cute squishy thud for the row-advance hop (all enemies land together).
  heartbeat     low-HP "lub-dub"; fundamentals ~75 Hz plus saturation harmonics so TV speakers reproduce it.
  turn_start    rising C-E-G chime ("your turn").
  ui_pause      two-note falling square blip.
  ui_resume     the same two notes rising.
  crit_shing    bright crushed arpeggio (from a just fifth over the C6 ping) + air for critical hits.
  power_boom    sub boom + noise for the power strike.
  ice_chime     C7-E7-G7 crystalline chime for Freeze.
  bubbles       rising sine chirps for Poison.
  enemy_pop     square "poof" layer for enemy deaths.
  player_hurt   falling square + crushed noise (template preset).
  boss_thud     weight layer for boss hits.
  sub_thump     weight layer for hard hits.
  blast_crack   t=0 noise crack + crushed square snap: the front transient of the Bomber explosions.
"""
import numpy as np

from sfxsynth import SR, Voice, render

C6 = 1046.5


def _saturate(x: np.ndarray, drive: float) -> np.ndarray:
    return np.tanh(x * drive) / np.tanh(drive)


def hit_ping(p: float = 1.0) -> list:
    return [
        Voice("square", freq=C6 * p, duty=0.25, attack=0.0, sustain=0.004, decay=0.09, volume=0.35, lowpass=7000),
        Voice("sine", freq=C6 * p, attack=0.001, sustain=0.01, decay=0.16, volume=0.8),
        Voice("sine", freq=2 * C6 * p, attack=0.001, sustain=0.0, decay=0.06, volume=0.25),
    ]


def cue_thump(p: float = 1.0) -> list:
    return [
        Voice("sine", freq=180 * p, slide=-3.0, attack=0.0, sustain=0.008, decay=0.08, punch=0.5),
        Voice("noise", freq=6000, attack=0.0, sustain=0.003, decay=0.025, volume=0.5, lowpass=3500),
    ]


def enemy_step(p: float = 1.0) -> list:
    return [
        Voice("sine", freq=140 * p, slide=-2.5, attack=0.0, sustain=0.01, decay=0.10, punch=0.6),
        Voice("triangle", freq=280 * p, slide=-3.0, attack=0.0, sustain=0.005, decay=0.05, volume=0.35),
        Voice("noise", freq=1800, attack=0.0, sustain=0.004, decay=0.04, volume=0.35, lowpass=1500),
    ]


def _beat(freq: float, start: float, volume: float) -> list:
    return [
        Voice("sine", freq=freq, slide=-0.8, attack=0.004, sustain=0.02, decay=0.13, punch=0.4, volume=volume, start=start),
        Voice("triangle", freq=2 * freq, slide=-0.8, attack=0.004, sustain=0.01, decay=0.08, volume=0.35 * volume, start=start),
        Voice("noise", freq=900, attack=0.001, sustain=0.005, decay=0.03, volume=0.25 * volume, lowpass=350, start=start),
    ]


def heartbeat(p: float = 1.0) -> list:
    return _beat(75 * p, 0.0, 1.0) + _beat(86 * p, 0.2, 0.8)


def turn_start(p: float = 1.0) -> list:
    arp = [(0.07, 4), (0.14, 3)]
    return [
        Voice("triangle", freq=523.25 * p, arp=arp, attack=0.004, sustain=0.2, decay=0.22),
        Voice("square", freq=1046.5 * p, duty=0.125, arp=arp, attack=0.004, sustain=0.2, decay=0.22, volume=0.22, lowpass=9000),
    ]


def ui_pause(p: float = 1.0) -> list:
    return [
        Voice("square", freq=987.8 * p, duty=0.25, arp=[(0.07, -7)], attack=0.0, sustain=0.1, decay=0.12, volume=0.6, lowpass=8000),
        Voice("triangle", freq=493.9 * p, arp=[(0.07, -7)], attack=0.0, sustain=0.1, decay=0.12, volume=0.6),
    ]


def ui_resume(p: float = 1.0) -> list:
    return [
        Voice("square", freq=659.3 * p, duty=0.25, arp=[(0.07, 7)], attack=0.0, sustain=0.1, decay=0.12, volume=0.6, lowpass=8000),
        Voice("triangle", freq=329.6 * p, arp=[(0.07, 7)], attack=0.0, sustain=0.1, decay=0.12, volume=0.6),
    ]


def crit_shing(p: float = 1.0) -> list:
    return [
        # Just fifth over the C6 ping (1569.75 Hz, not 1568): the drive's 2*G - 2*C product then lands on C6 itself.
        Voice("square", freq=1.5 * C6 * p, duty=0.25, arp=[(0.035, 5), (0.07, 7)], attack=0.0, sustain=0.1, decay=0.16,
              crush_bits=5, volume=0.5, lowpass=10000),
        Voice("noise", freq=12000, attack=0.0, sustain=0.01, decay=0.08, highpass=4000, volume=0.5),
    ]


def power_boom(p: float = 1.0) -> list:
    return [
        Voice("sine", freq=110 * p, slide=-1.8, attack=0.0, sustain=0.03, decay=0.28, punch=0.8),
        Voice("noise", freq=3000, slide=-1.5, attack=0.0, sustain=0.02, decay=0.2, volume=0.6, lowpass=2500),
        Voice("square", freq=220 * p, slide=1.5, duty=0.3, attack=0.0, sustain=0.03, decay=0.12, volume=0.3, crush_bits=6),
    ]


def ice_chime(p: float = 1.0) -> list:
    voices = []
    for i, f in enumerate((2093.0, 2637.0, 3136.0)):
        voices.append(Voice("sine", freq=f * p, attack=0.001, sustain=0.01, decay=0.22, volume=0.7 - 0.12 * i, start=0.035 * i))
        voices.append(Voice("triangle", freq=f * p * 2, attack=0.001, sustain=0.0, decay=0.08, volume=0.15, start=0.035 * i))
    voices.append(Voice("noise", freq=14000, attack=0.0, sustain=0.01, decay=0.12, highpass=6000, volume=0.25))
    return voices


def bubbles(p: float = 1.0) -> list:
    starts, freqs = (0.0, 0.055, 0.1, 0.165), (380, 520, 450, 610)
    return [Voice("sine", freq=f * p, slide=4.0, attack=0.002, sustain=0.012, decay=0.035, volume=1.0 - 0.15 * i, start=s)
            for i, (s, f) in enumerate(zip(starts, freqs))]


def enemy_pop(p: float = 1.0) -> list:
    return [
        Voice("square", freq=520 * p, slide=-4.0, duty=0.5, attack=0.0, sustain=0.01, decay=0.09, volume=0.6, lowpass=6000),
        Voice("noise", freq=5000, slide=-2.0, attack=0.0, sustain=0.02, decay=0.18, volume=0.5, lowpass=5000),
    ]


def player_hurt(p: float = 1.0) -> list:
    return [
        Voice("square", freq=440 * p, slide=-2.5, duty=0.5, duty_sweep=-1.2, attack=0.0, sustain=0.05, decay=0.22, punch=0.5),
        Voice("noise", freq=900, attack=0.0, sustain=0.03, decay=0.12, volume=0.5, crush_rate=8000),
    ]


def boss_thud(p: float = 1.0) -> list:
    return [
        Voice("sine", freq=65 * p, slide=-1.0, attack=0.0, sustain=0.02, decay=0.2, punch=0.6),
        Voice("triangle", freq=130 * p, slide=-1.0, attack=0.0, sustain=0.01, decay=0.12, volume=0.4),
    ]


def sub_thump(p: float = 1.0) -> list:
    return [Voice("sine", freq=95 * p, slide=-2.0, attack=0.0, sustain=0.01, decay=0.09, punch=0.5)]


def blast_crack(p: float = 1.0) -> list:
    return [
        Voice("noise", freq=9000, attack=0.0, sustain=0.006, decay=0.045, highpass=700, volume=1.0),
        Voice("square", freq=200 * p, slide=-3.5, duty=0.4, attack=0.0, sustain=0.008, decay=0.06, volume=0.55, crush_bits=5),
    ]


PRESETS = {fn.__name__: fn for fn in (hit_ping, cue_thump, enemy_step, heartbeat, turn_start, ui_pause, ui_resume, crit_shing,
                                       power_boom, ice_chime, bubbles, enemy_pop, player_hurt, boss_thud, sub_thump,
                                       blast_crack)}
SATURATION = {"heartbeat": 2.5, "enemy_step": 1.6, "boss_thud": 1.8, "sub_thump": 1.5}


def synth(name: str, p: float = 1.0, seed: int = 0) -> np.ndarray:
    """Render a preset at -1 dBFS peak (layer gains are applied by the caller)."""
    x = render(PRESETS[name](p), seed=seed, peak_db=None)
    if name in SATURATION:
        x = _saturate(x / (np.max(np.abs(x)) or 1.0), SATURATION[name])
    peak = np.max(np.abs(x)) or 1.0
    return x * (10 ** (-1 / 20) / peak)


assert SR == 44100
