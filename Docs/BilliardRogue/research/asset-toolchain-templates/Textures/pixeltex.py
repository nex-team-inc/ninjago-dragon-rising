"""Shared helpers for procedural pixel-art textures (numpy + Pillow + scipy). All maps tile (wrap)."""
import json

import numpy as np
from PIL import Image
from scipy import ndimage


def load_ramp(palette_json, family):
    with open(palette_json) as f:
        pal = json.load(f)
    hexes = pal["families"][family]["hex"]
    return np.array([[int(h[i:i + 2], 16) for i in (1, 3, 5)] for h in hexes], np.float32)


def value_noise(size, cells, rng):
    """Tileable value noise in [0,1]: random lattice (wrapped) + bicubic zoom."""
    lattice = rng.random((cells, cells))
    tiled = np.tile(lattice, (3, 3))
    z = ndimage.zoom(tiled, size / cells, order=3, mode="grid-wrap")
    return np.clip(z[size:2 * size, size:2 * size], 0, 1)


def normal_from_height(h, strength=2.0):
    """Tangent-space normal map, OpenGL convention (+G = up) which is what Unity expects."""
    dx = ndimage.sobel(h, axis=1, mode="wrap") / 8.0
    dy = ndimage.sobel(h, axis=0, mode="wrap") / 8.0
    # image rows grow downward, so a height increase toward the top gives +Y
    n = np.dstack([-dx * strength, dy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return ((n * 0.5 + 0.5) * 255).round().astype(np.uint8)


def cavity_from_height(h, radius=1.0, gain=1.2):
    """0.5 = neutral, <0.5 crevice, >0.5 exposed edge (matches BilliardRogue/ToonLit _CavityMap).
    Height minus local average, wrap-around so the map tiles."""
    blur = ndimage.gaussian_filter(h, radius, mode="wrap")
    return np.clip(0.5 + (h - blur) * gain, 0, 1)


def quantize_to_ramp(values01, ramp):
    """Map scalar [0,1] -> nearest ramp entry (keeps textures on-palette)."""
    idx = np.clip((values01 * (len(ramp) - 1)).round().astype(int), 0, len(ramp) - 1)
    return ramp[idx]


def ordered_dither(size_y, size_x):
    bayer4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.5
    return np.tile(bayer4, (size_y // 4 + 1, size_x // 4 + 1))[:size_y, :size_x]


def save_rgb(path, arr):
    Image.fromarray(np.asarray(arr).astype(np.uint8), "RGB").save(path, optimize=True)


def save_gray(path, arr01):
    Image.fromarray((np.clip(arr01, 0, 1) * 255).round().astype(np.uint8), "L").save(path, optimize=True)


def save_rgba(path, arr):
    Image.fromarray(np.asarray(arr).astype(np.uint8), "RGBA").save(path, optimize=True)
