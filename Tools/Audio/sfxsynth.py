"""Tiny sfxr-style retro SFX synthesizer (numpy). 44.1 kHz, mono, 16-bit PCM WAV output.

Copied from Docs/BilliardRogue/research/asset-toolchain-templates/Audio/sfxsynth.py (+ `peak_db=None`).

A sound = list of Voice layers mixed together. Each Voice: oscillator + pitch envelope + amp envelope
+ optional vibrato / arpeggio / duty sweep / filters / bitcrush. Deterministic for a given seed.
"""
import wave
from dataclasses import dataclass, field

import numpy as np

SR = 44100


@dataclass
class Voice:
    wave: str = "square"  # square | saw | triangle | sine | noise | pnoise (NES-style periodic noise)
    freq: float = 440.0  # start frequency Hz
    slide: float = 0.0  # octaves per second (+ up, - down)
    slide_accel: float = 0.0  # octaves per second^2
    min_freq: float = 20.0
    duty: float = 0.5  # square duty cycle
    duty_sweep: float = 0.0  # duty change per second
    vib_depth: float = 0.0  # semitones
    vib_speed: float = 0.0  # Hz
    arp: list = field(default_factory=list)  # [(time_s, semitone_offset), ...] cumulative jumps
    attack: float = 0.005
    sustain: float = 0.05
    punch: float = 0.0  # extra gain at start of sustain (sfxr "sustain punch"), 0..1
    decay: float = 0.15
    volume: float = 1.0
    lowpass: float = 0.0  # cutoff Hz, 0 = off
    highpass: float = 0.0  # cutoff Hz, 0 = off
    start: float = 0.0  # delay in seconds inside the sound
    crush_bits: int = 0  # 0 = off, e.g. 6 for crunchy
    crush_rate: int = 0  # sample-and-hold rate in Hz, 0 = off, e.g. 11025


def _envelope(v: Voice, n: int) -> np.ndarray:
    t = np.arange(n) / SR
    env = np.zeros(n)
    a, s, d = v.attack, v.sustain, v.decay
    env = np.where(t < a, t / max(a, 1e-6), env)
    in_s = (t >= a) & (t < a + s)
    env = np.where(in_s, 1.0 + v.punch * (1 - (t - a) / max(s, 1e-6)), env)
    in_d = (t >= a + s) & (t < a + s + d)
    env = np.where(in_d, (1 - (t - a - s) / max(d, 1e-6)) ** 2, env)
    return env


def _frequency(v: Voice, n: int) -> np.ndarray:
    t = np.arange(n) / SR
    octaves = v.slide * t + 0.5 * v.slide_accel * t * t
    semis = np.zeros(n)
    for when, offset in v.arp:
        semis += np.where(t >= when, offset, 0.0)
    if v.vib_depth:
        semis += v.vib_depth * np.sin(2 * np.pi * v.vib_speed * t)
    f = v.freq * 2.0 ** (octaves + semis / 12.0)
    return np.clip(f, v.min_freq, SR / 2 - 1)


def _oscillator(v: Voice, f: np.ndarray, rng: np.random.Generator) -> np.ndarray:
    n = len(f)
    phase = np.cumsum(f / SR)  # cycles
    frac = phase % 1.0
    if v.wave == "square":
        duty = np.clip(v.duty + v.duty_sweep * np.arange(n) / SR, 0.05, 0.95)
        return np.where(frac < duty, 1.0, -1.0)
    if v.wave == "saw":
        return 2.0 * frac - 1.0
    if v.wave == "triangle":
        return 4.0 * np.abs(frac - 0.5) - 1.0
    if v.wave == "sine":
        return np.sin(2 * np.pi * phase)
    if v.wave in ("noise", "pnoise"):
        # new random value each oscillator cycle -> pitch controls noise "colour" like sfxr
        idx = np.floor(phase * (2 if v.wave == "noise" else 1)).astype(np.int64)
        table_len = int(idx[-1]) + 2
        if v.wave == "noise":
            table = rng.uniform(-1, 1, table_len)
        else:  # 93-step periodic noise (metallic, NES short mode)
            base = rng.choice([-1.0, 1.0], 93)
            table = np.resize(base, table_len)
        return table[idx]
    raise ValueError(v.wave)


def _one_pole(x: np.ndarray, cutoff: float, high: bool) -> np.ndarray:
    from scipy.signal import lfilter
    alpha = np.exp(-2 * np.pi * cutoff / SR)
    low = lfilter([1 - alpha], [1, -alpha], x)
    return x - low if high else low


def render_voice(v: Voice, rng: np.random.Generator) -> np.ndarray:
    n = int((v.attack + v.sustain + v.decay) * SR)
    f = _frequency(v, n)
    x = _oscillator(v, f, rng) * _envelope(v, n) * v.volume
    if v.lowpass:
        x = _one_pole(x, v.lowpass, high=False)
    if v.highpass:
        x = _one_pole(x, v.highpass, high=True)
    if v.crush_rate:
        hold = max(1, SR // v.crush_rate)
        x = np.repeat(x[::hold], hold)[:n]
    if v.crush_bits:
        q = 2 ** (v.crush_bits - 1)
        x = np.round(x * q) / q
    return np.concatenate([np.zeros(int(v.start * SR)), x])


def render(voices: list, seed: int = 0, peak_db: float = -1.0, tail_fade_ms: float = 5.0) -> np.ndarray:
    """Mix, remove DC, fade the tail and peak-normalise (peak_db=None keeps the raw level for layering)."""
    rng = np.random.default_rng(seed)
    parts = [render_voice(v, rng) for v in voices]
    out = np.zeros(max(len(p) for p in parts))
    for p in parts:
        out[:len(p)] += p
    out -= out.mean()  # remove DC from asymmetric duty cycles
    fade = int(tail_fade_ms / 1000 * SR)
    if fade:
        out[-fade:] *= np.linspace(1, 0, fade)
    if peak_db is None:
        return out
    peak = np.max(np.abs(out)) or 1.0
    return out * (10 ** (peak_db / 20) / peak)


def write_wav(path: str, samples: np.ndarray) -> None:
    pcm = (np.clip(samples, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
