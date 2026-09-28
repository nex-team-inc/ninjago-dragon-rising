"""Review images (PIL): waveform + log-frequency spectrogram rows, loop-wrap close-ups, loudness bars, onset close-ups,
worst-case summing headroom.

Used by build_audio.py (final contact sheet) and by ad-hoc audition runs. Pure numpy/Pillow, deterministic.
"""
import numpy as np
from PIL import Image, ImageDraw, ImageFont

import audiolib as al

BG = (22, 24, 30)
PANEL = (32, 35, 44)
GRID = (58, 62, 74)
TEXT = (226, 228, 235)
DIM = (150, 155, 170)
CATEGORY_COLORS = {
    "ui": (120, 200, 255), "impact": (255, 150, 90), "ball": (255, 214, 102), "magic": (190, 140, 255),
    "enemy": (255, 110, 120), "pickup": (120, 230, 150), "stinger": (255, 230, 170), "bgm": (140, 220, 210),
    "player": (255, 170, 200), "flow": (200, 200, 120),
}
GUIDE_DB = -3.0  # red guide lines = the SFX peak ceiling


def font(size: int):
    for path in ("/System/Library/Fonts/Menlo.ttc", "/System/Library/Fonts/Monaco.ttf"):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    return ImageFont.load_default()


def waveform(draw, x: np.ndarray, box, color, max_s: float = 0.0):
    """Min/max waveform in box (x0, y0, x1, y1). With max_s, the time axis is fixed (so durations compare)."""
    x0, y0, x1, y1 = box
    mono = x if x.ndim == 1 else x.mean(axis=1)
    w = x1 - x0
    total = int(max_s * al.SR) if max_s else len(mono)
    total = max(total, len(mono))
    per = max(1, total // w)
    mid = (y0 + y1) / 2
    half = (y1 - y0) / 2 - 1
    draw.line([(x0, mid), (x1, mid)], fill=GRID)
    cols = min(w, int(np.ceil(len(mono) / per)))
    for c in range(cols):
        seg = mono[c * per:(c + 1) * per]
        if len(seg) == 0:
            break
        lo, hi = float(seg.min()), float(seg.max())
        draw.line([(x0 + c, mid - hi * half), (x0 + c, mid - lo * half)], fill=color)
    # peak-ceiling guide lines
    g = 10 ** (GUIDE_DB / 20) * half
    for yy in (mid - g, mid + g):
        draw.line([(x0, yy), (x1, yy)], fill=(80, 60, 60))


def spectrogram_image(x: np.ndarray, width: int, height: int, max_s: float = 0.0) -> Image.Image:
    """Log-frequency (40 Hz..18 kHz) magnitude spectrogram, dB-scaled, inferno-like palette."""
    mono = x if x.ndim == 1 else x.mean(axis=1)
    total = max(len(mono), int(max_s * al.SR)) if max_s else len(mono)
    n_fft = 1024
    hop = max(64, total // width)
    padded = np.pad(mono, (n_fft // 2, n_fft // 2 + total - len(mono)))
    frames = []
    win = np.hanning(n_fft)
    for i in range(width):
        s = i * hop
        seg = padded[s:s + n_fft]
        if len(seg) < n_fft:
            seg = np.pad(seg, (0, n_fft - len(seg)))
        frames.append(np.abs(np.fft.rfft(seg * win)))
    spec = np.array(frames).T / (n_fft / 4)  # (bins, width); full-scale sine = 0 dB
    freqs = np.fft.rfftfreq(n_fft, 1 / al.SR)
    rows = np.geomspace(40, 18000, height)[::-1]
    idx = np.clip(np.searchsorted(freqs, rows), 1, len(freqs) - 1)
    img = 20 * np.log10(spec[idx] + 1e-9)
    img = np.clip((img + 96) / 84, 0, 1)  # -96..-12 dB
    r = np.clip(img * 3.0, 0, 1)
    g = np.clip(img * 3.0 - 1.0, 0, 1)
    b = np.clip(0.35 + img * 1.5 - np.clip(img * 3 - 1.3, 0, 1), 0, 1) * (img > 0.02)
    rgb = (np.stack([r, g, b], axis=-1) * 255).astype(np.uint8)
    return Image.fromarray(rgb, "RGB")


def sfx_sheet(rows, path: str, title: str, cols: int = 3, max_s: float = 1.2):
    """rows: list of dict(label, sub, samples, category). Fixed time axis (max_s) so lengths compare."""
    cell_w, wave_h, spec_h, text_h, pad = 560, 64, 48, 34, 10
    cell_h = text_h + wave_h + spec_h + pad
    n = len(rows)
    grid_rows = (n + cols - 1) // cols
    W = cols * (cell_w + pad) + pad
    H = 60 + grid_rows * (cell_h + pad) + pad
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    f_title, f_lab, f_sub = font(22), font(14), font(11)
    d.text((pad, 14), title, fill=TEXT, font=f_title)
    d.text((pad, 40), f"time axis 0..{max_s:.1f}s per cell (longer clips are truncated); red lines = {GUIDE_DB:g} dBFS",
           fill=DIM, font=f_sub)
    for i, row in enumerate(rows):
        cx = pad + (i % cols) * (cell_w + pad)
        cy = 60 + (i // cols) * (cell_h + pad)
        d.rectangle([cx, cy, cx + cell_w, cy + cell_h], fill=PANEL)
        color = CATEGORY_COLORS.get(row.get("category", ""), TEXT)
        d.rectangle([cx, cy, cx + 5, cy + cell_h], fill=color)
        d.text((cx + 12, cy + 3), row["label"], fill=TEXT, font=f_lab)
        d.text((cx + 12, cy + 20), row["sub"], fill=DIM, font=f_sub)
        x = row["samples"][: int(max_s * al.SR)]
        waveform(d, x, (cx + 10, cy + text_h, cx + cell_w - 6, cy + text_h + wave_h), color, max_s)
        spec = spectrogram_image(x, cell_w - 16, spec_h, max_s)
        im.paste(spec, (cx + 10, cy + text_h + wave_h + 2))
    im.save(path, optimize=True)


def bgm_sheet(rows, path: str, title: str):
    """rows: dict(label, sub, samples stereo). Full-track waveform + spectrogram + loop-wrap close-up."""
    full_w, wrap_w, wave_h, spec_h, text_h, pad = 1100, 300, 70, 70, 36, 10
    cell_h = text_h + wave_h + spec_h + pad
    W = full_w + wrap_w + pad * 4
    H = 60 + len(rows) * (cell_h + pad) + pad
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    f_title, f_lab, f_sub = font(22), font(14), font(11)
    d.text((pad, 14), title, fill=TEXT, font=f_title)
    d.text((pad, 40), "left: whole loop (waveform + log spectrogram 40 Hz-18 kHz); right: loop wrap = last 0.25 s | first "
                      "0.25 s (white line = wrap point)", fill=DIM, font=f_sub)
    for i, row in enumerate(rows):
        cy = 60 + i * (cell_h + pad)
        x = row["samples"]
        d.rectangle([pad, cy, W - pad, cy + cell_h], fill=PANEL)
        d.text((pad + 8, cy + 3), row["label"], fill=TEXT, font=f_lab)
        d.text((pad + 8, cy + 20), row["sub"], fill=DIM, font=f_sub)
        waveform(d, x, (pad + 8, cy + text_h, pad + 8 + full_w, cy + text_h + wave_h), CATEGORY_COLORS["bgm"])
        im.paste(spectrogram_image(x, full_w, spec_h), (pad + 8, cy + text_h + wave_h + 2))
        wx = pad * 3 + full_w
        if not row.get("loop", True):
            d.text((wx, cy + text_h + 40), "one-shot (no loop wrap)", fill=DIM, font=f_sub)
            continue
        q = int(0.25 * al.SR)
        wrap = np.concatenate([x[-q:], x[:q]])
        waveform(d, wrap, (wx, cy + text_h, wx + wrap_w, cy + text_h + wave_h + spec_h), (255, 214, 102))
        d.line([(wx + wrap_w / 2, cy + text_h), (wx + wrap_w / 2, cy + text_h + wave_h + spec_h)], fill=TEXT)
    im.save(path, optimize=True)


def loudness_bars(rows, path: str, title: str):
    """rows: dict(label, value_db, category, target). Horizontal bars of short-window loudness."""
    bar_h, pad, label_w, plot_w = 16, 4, 330, 700
    W = label_w + plot_w + 60
    H = 70 + len(rows) * (bar_h + pad) + 30
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    f_title, f_lab = font(20), font(11)
    d.text((10, 12), title, fill=TEXT, font=f_title)
    lo, hi = -36.0, 0.0

    def xpos(v):
        return label_w + (np.clip(v, lo, hi) - lo) / (hi - lo) * plot_w

    for v in range(int(lo), int(hi) + 1, 6):
        d.line([(xpos(v), 50), (xpos(v), H - 20)], fill=GRID)
        d.text((xpos(v) - 10, H - 18), f"{v}", fill=DIM, font=f_lab)
    for i, row in enumerate(rows):
        y = 56 + i * (bar_h + pad)
        color = CATEGORY_COLORS.get(row["category"], TEXT)
        d.text((10, y + 2), row["label"], fill=TEXT, font=f_lab)
        d.rectangle([label_w, y, xpos(row["value_db"]), y + bar_h], fill=color)
        if row.get("target") is not None:
            tx = xpos(row["target"])
            d.line([(tx, y - 1), (tx, y + bar_h + 1)], fill=(255, 255, 255), width=2)
        if row.get("onset_db") is not None:
            ox = xpos(row["onset_db"])
            d.line([(ox, y + 3), (ox, y + bar_h - 3)], fill=(20, 20, 20), width=3)
        d.text((xpos(row["value_db"]) + 4, y + 2), f"{row['value_db']:.1f}", fill=DIM, font=f_lab)
    im.save(path, optimize=True)


def onset_sheet(rows, path: str, title: str, window_s: float = 0.25, cols: int = 3):
    """rows: dict(label, samples, category). First `window_s` of each clip (peak-normalised |x|) with markers:
    green = first reach of -3 dB under the peak, red = absolute peak; grid every 10 ms (brighter every 50 ms)."""
    cell_w, cell_h, pad = 600, 92, 8
    grid_rows = (len(rows) + cols - 1) // cols
    W = cols * (cell_w + pad) + pad
    H = 60 + grid_rows * (cell_h + pad) + pad
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    f_title, f_lab = font(20), font(11)
    d.text((pad, 12), title, fill=TEXT, font=f_title)
    d.text((pad, 38), f"first {window_s * 1000:.0f} ms, grid 10 ms (bright = 50 ms); green = -3 dB of peak, red = peak",
           fill=DIM, font=f_lab)
    for i, row in enumerate(rows):
        cx = pad + (i % cols) * (cell_w + pad)
        cy = 60 + (i // cols) * (cell_h + pad)
        d.rectangle([cx, cy, cx + cell_w, cy + cell_h], fill=PANEL)
        n = int(window_s * al.SR)
        x = np.abs(row["samples"])
        peak = x.max() or 1.0
        x = np.pad(x, (0, max(0, n - len(x))))[:n] / peak
        x0, x1, base, top = cx + 8, cx + cell_w - 8, cy + cell_h - 6, cy + 22
        for ms in range(0, int(window_s * 1000) + 1, 10):
            gx = x0 + ms / (window_s * 1000) * (x1 - x0)
            d.line([(gx, top), (gx, base)], fill=GRID if ms % 50 else (90, 95, 110))
        for c, seg in enumerate(np.array_split(x, x1 - x0)):
            d.line([(x0 + c, base), (x0 + c, base - float(seg.max()) * (base - top))], fill=CATEGORY_COLORS.get(row["category"], TEXT))
        t3 = float(np.argmax(x >= 10 ** (-3 / 20))) / al.SR
        tp = float(np.argmax(x)) / al.SR
        for t, colour in ((tp, (235, 80, 80)), (t3, (90, 220, 120))):
            gx = x0 + min(t, window_s) / window_s * (x1 - x0)
            d.line([(gx, top), (gx, base)], fill=colour, width=2)
        d.text((cx + 8, cy + 4), f"{row['label']}   -3 dB @ {t3 * 1000:.1f} ms   peak @ {tp * 1000:.1f} ms", fill=TEXT, font=f_lab)
    im.save(path, optimize=True)


def headroom_bars(rows, path: str, title: str, trim_db: float):
    """rows: headroom results (scenario, loop, max_db, median_db, max_after_trim_db). Output peak per scenario."""
    bar_h, pad, label_w, plot_w = 18, 6, 330, 640
    W = label_w + plot_w + 200
    H = 76 + len(rows) * (bar_h + pad) + 34
    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)
    f_title, f_lab = font(18), font(11)
    d.text((10, 12), title, fill=TEXT, font=f_title)
    d.text((10, 38), f"grey = raw max, colour = max after the planned {trim_db:+.0f} dB master trim, | = raw median; "
                     "red line = 0 dBFS (clip)", fill=DIM, font=f_lab)
    lo, hi = -12.0, 9.0

    def xpos(v):
        return label_w + (np.clip(v, lo, hi) - lo) / (hi - lo) * plot_w

    for v in range(int(lo), int(hi) + 1, 3):
        d.line([(xpos(v), 58), (xpos(v), H - 24)], fill=GRID)
        d.text((xpos(v) - 8, H - 20), f"{v:+d}", fill=DIM, font=f_lab)
    for i, row in enumerate(rows):
        y = 62 + i * (bar_h + pad)
        d.text((10, y + 3), f"{row['scenario']} / {row['loop']}", fill=TEXT, font=f_lab)
        d.rectangle([xpos(lo), y, xpos(row["max_db"]), y + bar_h], fill=(80, 84, 96))
        ok = row["max_after_trim_db"] <= 0
        d.rectangle([xpos(lo), y + 4, xpos(row["max_after_trim_db"]), y + bar_h - 4], fill=(120, 230, 150) if ok else (255, 110, 120))
        mx = xpos(row["median_db"])
        d.line([(mx, y), (mx, y + bar_h)], fill=TEXT, width=2)
        d.text((xpos(max(row["max_db"], row["max_after_trim_db"])) + 6, y + 3),
               f"raw {row['max_db']:+.1f} / trimmed {row['max_after_trim_db']:+.1f} dBFS", fill=DIM, font=f_lab)
    d.line([(xpos(0), 58), (xpos(0), H - 24)], fill=(235, 80, 80), width=2)
    im.save(path, optimize=True)
