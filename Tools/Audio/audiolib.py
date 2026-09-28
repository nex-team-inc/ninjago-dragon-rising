"""Audio I/O, analysis and processing helpers for the Billiard Rogue audio pipeline (numpy + ffmpeg).

Everything here is deterministic: decoding goes through ffmpeg/soxr to float32, processing is pure numpy,
WAV writing uses the stdlib `wave` module (no dither), and Ogg Vorbis is encoded with ffmpeg in bit-exact mode
(fixed stream serial, no encoder tag) so re-runs produce byte-identical files.
"""
import subprocess
import wave

import numpy as np
from scipy.signal import lfilter

SR = 44100
FFMPEG = "/opt/homebrew/bin/ffmpeg"


# ----------------------------------------------------------------------------------------------------------
# I/O
# ----------------------------------------------------------------------------------------------------------

def decode(path: str, channels: int) -> np.ndarray:
    """Decode any file to float32 at 44.1 kHz. Returns shape (n,) for mono or (n, 2) for stereo.

    Mono downmix is done here (not by ffmpeg) so anti-phase stereo sources can be detected by the caller.
    """
    cmd = [FFMPEG, "-v", "error", "-i", path, "-map", "0:a:0", "-af", "aresample=44100:resampler=soxr:precision=28",
           "-f", "f32le", "-acodec", "pcm_f32le", "-"]
    raw = subprocess.run(cmd, capture_output=True, check=True).stdout
    probe_ch = _channels(path)
    x = np.frombuffer(raw, dtype="<f4").astype(np.float64).reshape(-1, probe_ch)
    if channels == 1:
        if probe_ch == 1:
            return x[:, 0].copy()
        left, right = x[:, 0], x[:, 1]
        mid = 0.5 * (left + right)
        # Anti-phase guard: if the mid loses > 6 dB against the louder side, keep that side instead.
        side_rms = max(_rms(left), _rms(right))
        return mid if _rms(mid) > side_rms * 0.5 else (left if _rms(left) >= _rms(right) else right).copy()
    if probe_ch == 1:
        return np.repeat(x, 2, axis=1)
    return x[:, :2].copy()


def _channels(path: str) -> int:
    out = subprocess.run(["/opt/homebrew/bin/ffprobe", "-v", "error", "-select_streams", "a:0", "-show_entries",
                          "stream=channels", "-of", "csv=p=0", path], capture_output=True, text=True, check=True)
    return int(out.stdout.strip().split(",")[0])


def write_wav_mono16(path: str, x: np.ndarray) -> None:
    pcm = np.round(np.clip(x, -1.0, 32767 / 32768) * 32768).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def encode_ogg(path: str, x: np.ndarray, quality: float = 5.0) -> None:
    """Stereo float -> Ogg Vorbis (libvorbis VBR q`quality`), bit-exact for reproducible bytes."""
    data = np.ascontiguousarray(np.clip(x, -1.0, 1.0).astype("<f4"))
    cmd = [FFMPEG, "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-",
           "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact",
           "-c:a", "libvorbis", "-q:a", str(quality), path]
    subprocess.run(cmd, input=data.tobytes(), check=True)


# ----------------------------------------------------------------------------------------------------------
# Loudness (ITU-R BS.1770-4 K-weighting, coefficients re-derived for 44.1 kHz as in libebur128)
# ----------------------------------------------------------------------------------------------------------

def _k_filters(fs: int = SR):
    f0, gain, q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    k = np.tan(np.pi * f0 / fs)
    vh, vb = 10 ** (gain / 20), (10 ** (gain / 20)) ** 0.4996667741545416
    a0 = 1 + k / q + k * k
    shelf_b = [(vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0]
    shelf_a = [1.0, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0]
    f0, q = 38.13547087602444, 0.5003270373238773
    k = np.tan(np.pi * f0 / fs)
    a0 = 1 + k / q + k * k
    hp_b = [1.0, -2.0, 1.0]
    hp_a = [1.0, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0]
    return shelf_b, shelf_a, hp_b, hp_a


def k_weight(x: np.ndarray) -> np.ndarray:
    sb, sa, hb, ha = _k_filters()
    return lfilter(hb, ha, lfilter(sb, sa, x, axis=0), axis=0)


def _block_power(y: np.ndarray, block: int, hop: int) -> np.ndarray:
    """Mean-square per block summed over channels (BS.1770 channel weights 1.0 for L/R/mono)."""
    if y.ndim == 1:
        y = y[:, None]
    sq = (y ** 2).sum(axis=1)
    if len(sq) < block:
        sq = np.concatenate([sq, np.zeros(block - len(sq))])
    c = np.concatenate([[0.0], np.cumsum(sq)])
    starts = np.arange(0, len(sq) - block + 1, hop)
    return (c[starts + block] - c[starts]) / block


def _lufs(power):
    return -0.691 + 10 * np.log10(np.maximum(power, 1e-20))


def integrated_lufs(x: np.ndarray) -> float:
    """Gated integrated loudness (400 ms blocks, 75 % overlap, -70 LUFS absolute / -10 LU relative gate).

    A mono signal is counted once (as Unity plays a mono clip on both speakers this reads ~3 LU lower than
    the same clip as dual-mono stereo; only compare like with like)."""
    p = _block_power(k_weight(x), int(0.4 * SR), int(0.1 * SR))
    p = p[_lufs(p) > -70]
    if len(p) == 0:
        return -70.0
    rel = _lufs(p.mean()) - 10
    p = p[_lufs(p) > rel]
    return float(_lufs(p.mean()))


def max_short_lufs(x: np.ndarray, window_s: float = 0.1) -> float:
    """Peak loudness over a sliding window (default 100 ms). Our loudness measure for short SFX, where
    integrated/short-term EBU windows (0.4 s / 3 s) are longer than the sound itself."""
    p = _block_power(k_weight(x), int(window_s * SR), int(0.005 * SR))
    return float(_lufs(p.max()))


# ----------------------------------------------------------------------------------------------------------
# Analysis
# ----------------------------------------------------------------------------------------------------------

def _rms(x) -> float:
    return float(np.sqrt(np.mean(np.square(x)))) if len(x) else 0.0


def db(v: float) -> float:
    return float(20 * np.log10(max(v, 1e-12)))


def envelope(x: np.ndarray, ms: float = 2.0) -> np.ndarray:
    mono = np.abs(x) if x.ndim == 1 else np.abs(x).max(axis=1)
    hop = max(1, int(ms / 1000 * SR))
    n = len(mono) // hop
    if n == 0:
        return mono
    return mono[:n * hop].reshape(n, hop).max(axis=1)


def spectral_centroid(x: np.ndarray) -> float:
    mono = x if x.ndim == 1 else x.mean(axis=1)
    if len(mono) < 64:
        return 0.0
    n = 1 << int(np.ceil(np.log2(min(len(mono), SR))))
    seg = mono[:n] if len(mono) >= n else np.pad(mono, (0, n - len(mono)))
    mag = np.abs(np.fft.rfft(seg * np.hanning(n)))
    freqs = np.fft.rfftfreq(n, 1 / SR)
    return float((freqs * mag).sum() / max(mag.sum(), 1e-12))


def attack_ms(x: np.ndarray) -> float:
    """Time from the first sample above -40 dB (re peak) to 90 % of the peak envelope."""
    env = envelope(x, 0.5)
    if env.max() <= 0:
        return 0.0
    peak = env.max()
    start = int(np.argmax(env > peak * 0.01))
    reach = int(np.argmax(env >= peak * 0.9))
    return (reach - start) * 0.5


def analyze(x: np.ndarray) -> dict:
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    info = {
        "dur_s": round(len(x) / SR, 3),
        "peak_db": round(db(peak), 2),
        "rms_db": round(db(_rms(x)), 2),
        "short_lufs": round(max_short_lufs(x), 2),
        "centroid_hz": round(spectral_centroid(x)),
        "attack_ms": round(attack_ms(x), 1),
    }
    if len(x) >= int(0.4 * SR):
        info["int_lufs"] = round(integrated_lufs(x), 2)
    return info


def loop_report(x: np.ndarray) -> dict:
    """How well a stereo loop wraps end -> start (lower is better)."""
    edge = int(0.05 * SR)
    head, tail = x[:edge], x[-edge:]
    steps = np.abs(np.diff(x, axis=0))
    steps = steps if steps.ndim == 1 else steps.max(axis=1)
    typical_step = float(np.median(steps[:SR * 5])) + 1e-9
    wrap_step = float(np.max(np.abs(x[0] - x[-1])))
    lead = _silence_len(x)
    trail = _silence_len(x[::-1])
    return {
        "head_rms_db": round(db(_rms(head)), 1),
        "tail_rms_db": round(db(_rms(tail)), 1),
        "edge_jump_db": round(db(_rms(head)) - db(_rms(tail)), 1),
        "wrap_step_ratio": round(wrap_step / typical_step, 1),  # vs median step: > 1 is common and harmless
        "wrap_step_vs_p999": round(wrap_step / (float(np.percentile(steps, 99.9)) + 1e-9), 3),  # > 1 = audible click
        "lead_silence_ms": round(lead / SR * 1000, 1),
        "trail_silence_ms": round(trail / SR * 1000, 1),
    }


def music_features(x: np.ndarray) -> dict:
    """Rough mood descriptors for BGM comparison: tempo (onset autocorrelation), onset rate, bass share,
    brightness and loudness range (p10..p95 of 3 s short-term loudness)."""
    mono = x if x.ndim == 1 else x.mean(axis=1)
    hop, n_fft = 512, 1024
    n = (len(mono) - n_fft) // hop
    frames = np.lib.stride_tricks.sliding_window_view(mono, n_fft)[::hop][:n] * np.hanning(n_fft)
    mag = np.abs(np.fft.rfft(frames, axis=1))
    flux = np.maximum(np.diff(np.log1p(mag * 10), axis=0), 0).sum(axis=1)
    flux = flux - np.convolve(flux, np.ones(16) / 16, mode="same")
    flux = np.maximum(flux, 0)
    fps = SR / hop
    ac = np.correlate(flux, flux, mode="full")[len(flux) - 1:]
    lags = np.arange(len(ac)) / fps
    valid = (lags >= 60 / 190) & (lags <= 60 / 60)
    bpm = 60 / lags[valid][np.argmax(ac[valid])] if valid.any() else 0.0
    thresh = flux.mean() + 1.5 * flux.std()
    peaks = (flux[1:-1] > thresh) & (flux[1:-1] >= flux[:-2]) & (flux[1:-1] >= flux[2:])
    freqs = np.fft.rfftfreq(n_fft, 1 / SR)
    power = (mag ** 2).sum(axis=0)
    st = _block_power(k_weight(x), int(3 * SR), int(0.5 * SR))
    st_l = _lufs(st)
    st_l = st_l[st_l > -60]
    return {
        "bpm": round(float(bpm), 1),
        "onsets_per_s": round(float(peaks.sum()) / (len(mono) / SR), 2),
        "bass_share": round(float(power[freqs < 200].sum() / power.sum()), 3),
        "centroid_hz": round(float((freqs * power).sum() / power.sum())),
        "lra_lu": round(float(np.percentile(st_l, 95) - np.percentile(st_l, 10)), 1) if len(st_l) else 0.0,
    }


def _silence_len(x: np.ndarray, thresh_db: float = -60.0) -> int:
    mono = np.abs(x) if x.ndim == 1 else np.abs(x).max(axis=1)
    above = np.nonzero(mono > 10 ** (thresh_db / 20))[0]
    return int(above[0]) if len(above) else len(mono)


# ----------------------------------------------------------------------------------------------------------
# Processing
# ----------------------------------------------------------------------------------------------------------

def trim(x: np.ndarray, start_db: float = -45.0, end_db: float = -55.0, preroll_ms: float = 1.0) -> np.ndarray:
    """Trim leading/trailing silence relative to the peak (keeps a tiny pre-roll so the attack is intact)."""
    env = np.abs(x) if x.ndim == 1 else np.abs(x).max(axis=1)
    peak = env.max()
    if peak <= 0:
        return x
    above_start = np.nonzero(env > peak * 10 ** (start_db / 20))[0]
    above_end = np.nonzero(env > peak * 10 ** (end_db / 20))[0]
    s = max(0, int(above_start[0]) - int(preroll_ms / 1000 * SR))
    e = int(above_end[-1]) + 1
    return x[s:e]


def cut(x: np.ndarray, start_s: float = 0.0, end_s: float = 0.0) -> np.ndarray:
    s = int(start_s * SR)
    e = int(end_s * SR) if end_s > 0 else len(x)
    return x[s:e]


def fade(x: np.ndarray, in_ms: float = 0.0, out_ms: float = 8.0) -> np.ndarray:
    y = x.copy()
    n_in, n_out = int(in_ms / 1000 * SR), int(out_ms / 1000 * SR)
    if n_in > 1:
        ramp = np.linspace(0, 1, n_in)
        y[:n_in] *= ramp if y.ndim == 1 else ramp[:, None]
    if n_out > 1:
        n_out = min(n_out, len(y))
        ramp = np.linspace(1, 0, n_out) ** 2  # quadratic: quick but click-free tail
        y[-n_out:] *= ramp if y.ndim == 1 else ramp[:, None]
    return y


def fit_length(x: np.ndarray, max_s: float, fade_ms: float) -> np.ndarray:
    """Cap a sound's length with a smooth fade (for sources with long reverb/music tails)."""
    n = int(max_s * SR)
    if len(x) <= n:
        return x
    return fade(x[:n], 0.0, fade_ms)


def highpass(x: np.ndarray, hz: float, order: int = 2) -> np.ndarray:
    from scipy.signal import butter
    b, a = butter(order, hz / (SR / 2), "high")
    return lfilter(b, a, x, axis=0)


def lowpass(x: np.ndarray, hz: float, order: int = 2) -> np.ndarray:
    from scipy.signal import butter
    b, a = butter(order, hz / (SR / 2), "low")
    return lfilter(b, a, x, axis=0)


def repitch(x: np.ndarray, semitones: float) -> np.ndarray:
    """Tape-style pitch shift (changes duration too) with high-quality polyphase resampling."""
    if abs(semitones) < 1e-6:
        return x
    from fractions import Fraction
    from scipy.signal import resample_poly
    ratio = Fraction(2 ** (semitones / 12)).limit_denominator(2000)
    return resample_poly(x, ratio.denominator, ratio.numerator, axis=0)


def normalize_short(x: np.ndarray, target_short_lufs: float, peak_ceiling_db: float = -1.0) -> np.ndarray:
    """Gain to hit a short-window loudness target, but never let the sample peak exceed the ceiling."""
    gain_db = target_short_lufs - max_short_lufs(x)
    peak = float(np.max(np.abs(x)))
    gain_db = min(gain_db, peak_ceiling_db - db(peak))
    return x * 10 ** (gain_db / 20)


def normalize_integrated(x: np.ndarray, target_lufs: float, peak_ceiling_db: float = -1.0):
    """Static (loop-safe) gain to an integrated loudness target, peak-capped. Returns (y, gain_db, capped)."""
    gain_db = target_lufs - integrated_lufs(x)
    peak = float(np.max(np.abs(x)))
    capped = gain_db > peak_ceiling_db - db(peak)
    if capped:
        gain_db = peak_ceiling_db - db(peak)
    return x * 10 ** (gain_db / 20), gain_db, capped


def soft_limit(x: np.ndarray, ceiling_db: float = -1.0, knee_db: float = 6.0) -> np.ndarray:
    """Memoryless tanh soft clipper above (ceiling - knee): loop-safe (no state across the wrap)."""
    ceiling = 10 ** (ceiling_db / 20)
    knee = ceiling * 10 ** (-knee_db / 20)
    y = x.copy()
    over = np.abs(y) > knee
    span = ceiling - knee
    y[over] = np.sign(y[over]) * (knee + span * np.tanh((np.abs(y[over]) - knee) / span))
    return y


def mix(*layers) -> np.ndarray:
    """Sum mono layers of different lengths: each layer is (signal, gain_db, offset_s)."""
    n = max(int(off * SR) + len(sig) for sig, _, off in layers)
    out = np.zeros(n)
    for sig, gain_db, off in layers:
        o = int(off * SR)
        out[o:o + len(sig)] += sig * 10 ** (gain_db / 20)
    return out
