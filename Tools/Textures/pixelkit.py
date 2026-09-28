"""Shared pixel-art helpers for the Billiard Rogue 2D pipeline (numpy + Pillow + scipy).

Used by make_surfaces.py, make_particles.py, make_icons.py, make_ui.py and Tools/Fonts/*.
Everything is deterministic (fixed seeds, no time/os dependent state) so re-runs are byte-identical.

Conventions
- Images are numpy uint8 arrays shaped (H, W, 4) RGBA, row 0 = top (PNG order).
- Masks are bool arrays shaped (H, W).
- Torus helpers (torus_*) draw with wrap-around so tileable textures stay seamless.
- Normal maps: OpenGL / Unity convention (+G = up). Cavity maps: 0.5 = neutral.
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
STAGING_ASSETS = os.path.join(REPO, "Tools", "Staging", "Assets")
_PALETTE_CANDIDATES = [
    os.path.join(STAGING_ASSETS, "Textures", "BilliardRogue", "Palette", "palette.json"),
    os.path.join(REPO, "Starter", "Assets", "Textures", "BilliardRogue", "Palette", "palette.json"),
]


def staging(*parts):
    """Absolute path inside the staging mirror (Tools/Staging/Assets/...); creates the parent folder."""
    path = os.path.join(STAGING_ASSETS, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    return path


def default_preview_dir(area):
    root = os.environ.get("BR_PREVIEW_DIR") or os.path.join("/tmp", "billiard_rogue_previews")
    path = os.path.join(root, area)
    os.makedirs(path, exist_ok=True)
    return path


# ----------------------------------------------------------------------------------------------
# Colour / palette
# ----------------------------------------------------------------------------------------------

def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def rgba(c, a=255):
    """Accepts '#rrggbb', (r,g,b) or (r,g,b,a); returns an (r,g,b,a) tuple."""
    if isinstance(c, str):
        return hex_rgb(c) + (a,)
    if len(c) == 3:
        return tuple(int(v) for v in c) + (a,)
    return tuple(int(v) for v in c)


class Palette:
    """Read-only access to the shared model palette (families x 16 shades, dark -> light)."""

    def __init__(self, path=None):
        path = path or next(p for p in _PALETTE_CANDIDATES if os.path.exists(p))
        with open(path) as f:
            data = json.load(f)
        self.families = {name: [hex_rgb(h) for h in fam["hex"]] for name, fam in data["families"].items()}

    def __call__(self, family, shade):
        return self.families[family][int(np.clip(shade, 0, 15))]

    def ramp(self, family, shades):
        return np.array([self(family, s) for s in shades], np.float32)

    def mix(self, fam_a, fam_b, t, shades):
        a, b = self.ramp(fam_a, shades), self.ramp(fam_b, shades)
        return (a * (1 - t) + b * t).round()


_PAL = None


def palette():
    global _PAL
    if _PAL is None:
        _PAL = Palette()
    return _PAL


def lerp_rgb(a, b, t):
    a, b = np.array(rgba(a)[:3], np.float32), np.array(rgba(b)[:3], np.float32)
    return tuple(int(round(v)) for v in a + (b - a) * t)


def ramp_from_hex(*hexes):
    return np.array([hex_rgb(h) for h in hexes], np.float32)


# ----------------------------------------------------------------------------------------------
# Canvas + masks (non-wrapping, for sprites)
# ----------------------------------------------------------------------------------------------

def canvas(w, h, color=None):
    img = np.zeros((h, w, 4), np.uint8)
    if color is not None:
        img[:] = rgba(color)
    return img


def grid(w, h):
    """Pixel-centre coordinates (x, y), each shaped (h, w)."""
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    return x + 0.5, y + 0.5


def put(img, mask, color):
    img[mask] = rgba(color)
    return img


def disc(w, h, cx, cy, r):
    x, y = grid(w, h)
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r


def ellipse(w, h, cx, cy, rx, ry, angle_deg=0.0):
    x, y = grid(w, h)
    a = np.deg2rad(angle_deg)
    dx, dy = x - cx, y - cy
    u = dx * np.cos(a) + dy * np.sin(a)
    v = -dx * np.sin(a) + dy * np.cos(a)
    return (u / rx) ** 2 + (v / ry) ** 2 <= 1.0


def rect(w, h, x0, y0, x1, y1):
    """Inclusive-exclusive integer rectangle mask."""
    m = np.zeros((h, w), bool)
    m[max(0, y0):max(0, y1), max(0, x0):max(0, x1)] = True
    return m


def poly(w, h, pts):
    """Hard-edged polygon mask (Pillow rasteriser, no anti-aliasing). pts in pixel coordinates."""
    im = Image.new("L", (w, h), 0)
    ImageDraw.Draw(im).polygon([tuple(p) for p in pts], fill=255, outline=255)
    return np.asarray(im) > 0


def line(w, h, pts, width=1):
    im = Image.new("L", (w, h), 0)
    ImageDraw.Draw(im).line([tuple(p) for p in pts], fill=255, width=width)
    return np.asarray(im) > 0


def pixels(w, h, coords):
    m = np.zeros((h, w), bool)
    for x, y in coords:
        if 0 <= x < w and 0 <= y < h:
            m[y, x] = True
    return m


def ascii_mask(rows, char="#"):
    return np.array([[c in char for c in r] for r in rows], bool)


def ascii_art(rows, legend):
    """rows: list of equal-length strings; legend: {char: colour}. '.' and ' ' are transparent."""
    h, w = len(rows), max(len(r) for r in rows)
    img = canvas(w, h)
    for y, r in enumerate(rows):
        for x, c in enumerate(r):
            if c in legend:
                img[y, x] = rgba(legend[c])
    return img


def alpha(img):
    return img[..., 3] > 0


def blit(dst, src, x, y):
    """Hard-alpha blit (src pixels with alpha > 0 overwrite dst). Clips at the borders."""
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    if x0 >= x1 or y0 >= y1:
        return dst
    s = src[y0 - y:y1 - y, x0 - x:x1 - x]
    d = dst[y0:y1, x0:x1]
    m = s[..., 3] > 0
    d[m] = s[m]
    return dst


def blend(dst, src, x=0, y=0):
    """Straight-alpha 'over' composite (for UI glows that keep partial alpha)."""
    h, w = src.shape[:2]
    d = dst[y:y + h, x:x + w].astype(np.float32) / 255
    s = src.astype(np.float32) / 255
    sa, da = s[..., 3:4], d[..., 3:4]
    oa = sa + da * (1 - sa)
    rgb = np.where(oa > 0, (s[..., :3] * sa + d[..., :3] * da * (1 - sa)) / np.maximum(oa, 1e-6), 0)
    dst[y:y + h, x:x + w] = (np.concatenate([rgb, oa], -1) * 255).round().astype(np.uint8)
    return dst


def dilate(mask, diagonal=True, iterations=1):
    st = np.ones((3, 3), bool) if diagonal else ndimage.generate_binary_structure(2, 1)
    return ndimage.binary_dilation(mask, structure=st, iterations=iterations)


def erode(mask, diagonal=True, iterations=1):
    st = np.ones((3, 3), bool) if diagonal else ndimage.generate_binary_structure(2, 1)
    return ndimage.binary_erosion(mask, structure=st, iterations=iterations, border_value=0)


def outline(img, color, diagonal=False, thickness=1):
    """Adds a hard outline in transparent pixels around the opaque shape."""
    m = alpha(img)
    ring = dilate(m, diagonal, thickness) & ~m
    out = img.copy()
    out[ring] = rgba(color)
    return out


def inner_edge(mask, side):
    """Pixels of mask whose neighbour on `side` ('top','bottom','left','right') is outside the mask."""
    shifted = np.zeros_like(mask)
    if side == "top":
        shifted[1:] = mask[:-1]
    elif side == "bottom":
        shifted[:-1] = mask[1:]
    elif side == "left":
        shifted[:, 1:] = mask[:, :-1]
    elif side == "right":
        shifted[:, :-1] = mask[:, 1:]
    return mask & ~shifted


def shift(mask, dx, dy):
    out = np.zeros_like(mask)
    h, w = mask.shape[:2]
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = mask[ys, xs]
    return out


def flip_h(img):
    return img[:, ::-1].copy()


def rotate90(img, k=1):
    return np.rot90(img, k).copy()


def upscale(img, k):
    return np.repeat(np.repeat(img, k, axis=0), k, axis=1)


def sphere_shading(w, h, cx, cy, r, light=(-0.55, -0.65, 0.52)):
    """Per-pixel Lambert term of a sphere (0..1) plus the inside mask; light in image space (y down)."""
    x, y = grid(w, h)
    dx, dy = (x - cx) / r, (y - cy) / r
    d2 = dx * dx + dy * dy
    inside = d2 <= 1.0
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    L = np.array(light, np.float32)
    L /= np.linalg.norm(L)
    ndl = np.clip(dx * L[0] + dy * L[1] + nz * L[2], 0, 1)
    return ndl, inside, (dx, dy, nz)


# ----------------------------------------------------------------------------------------------
# Tileable texture helpers
# ----------------------------------------------------------------------------------------------

def value_noise(size, cells, rng, order=3):
    """Tileable value noise in [0,1]: random wrapped lattice + spline zoom."""
    lattice = rng.random((cells, cells))
    tiled = np.tile(lattice, (3, 3))
    z = ndimage.zoom(tiled, size / cells, order=order, mode="grid-wrap")
    return np.clip(z[size:2 * size, size:2 * size], 0, 1)


def fbm(size, rng, octaves=((4, 0.55), (8, 0.3), (16, 0.15))):
    out = np.zeros((size, size), np.float32)
    for cells, amp in octaves:
        out += value_noise(size, cells, rng) * amp
    total = sum(a for _, a in octaves)
    return out / total


def aniso_noise(size, cells_x, cells_y, rng):
    """Tileable noise stretched along one axis (wood grain)."""
    lattice = rng.random((cells_y, cells_x))
    tiled = np.tile(lattice, (3, 3))
    z = ndimage.zoom(tiled, (size / cells_y, size / cells_x), order=3, mode="grid-wrap")
    return np.clip(z[size:2 * size, size:2 * size], 0, 1)


def fold3(big, size, op="or"):
    """Fold a (3*size, 3*size) canvas back onto (size, size) with wrap-around."""
    blocks = big.reshape(3, size, 3, size).swapaxes(1, 2).reshape(9, size, size)
    if op == "or":
        return blocks.any(axis=0)
    return blocks.max(axis=0)


def torus_poly(size, pts):
    pts = [(x + size, y + size) for x, y in pts]
    return fold3(poly(3 * size, 3 * size, pts), size)


def torus_line(size, pts, width=1):
    pts = [(x + size, y + size) for x, y in pts]
    return fold3(line(3 * size, 3 * size, pts, width), size)


def torus_ellipse(size, cx, cy, rx, ry, angle_deg=0.0):
    return fold3(ellipse(3 * size, 3 * size, cx + size, cy + size, rx, ry, angle_deg), size)


def torus_pixels(size, coords):
    m = np.zeros((size, size), bool)
    for x, y in coords:
        m[int(y) % size, int(x) % size] = True
    return m


def torus_distance(mask):
    """Chessboard-ish (taxicab) distance from each True pixel to the nearest False pixel, wrapping."""
    size_y, size_x = mask.shape
    pad = 8
    tiled = np.pad(mask, pad, mode="wrap")
    d = ndimage.distance_transform_cdt(tiled, metric="taxicab")
    return d[pad:pad + size_y, pad:pad + size_x].astype(np.float32)


def torus_edt(mask):
    size_y, size_x = mask.shape
    pad = 12
    tiled = np.pad(mask, pad, mode="wrap")
    d = ndimage.distance_transform_edt(tiled)
    return d[pad:pad + size_y, pad:pad + size_x].astype(np.float32)


def torus_voronoi(size, points):
    """Labels + distance to the nearest cell border for wrapped Voronoi cells."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) + 0.5
    d = []
    for px_, py_ in points:
        dx = np.abs(x - px_)
        dy = np.abs(y - py_)
        dx = np.minimum(dx, size - dx)
        dy = np.minimum(dy, size - dy)
        d.append(np.sqrt(dx * dx + dy * dy))
    d = np.stack(d)
    order = np.argsort(d, axis=0)
    labels = order[0]
    d_sorted = np.take_along_axis(d, order[:2], axis=0)
    border = (d_sorted[1] - d_sorted[0]) / 2.0
    return labels, border


def normal_from_height(h, strength=2.0):
    """Tangent-space normal map, OpenGL convention (+G = up), what Unity's NormalMap import expects."""
    dx = ndimage.sobel(h, axis=1, mode="wrap") / 8.0
    dy = ndimage.sobel(h, axis=0, mode="wrap") / 8.0
    n = np.dstack([-dx * strength, dy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n


def encode_normal(n):
    return ((n * 0.5 + 0.5) * 255).round().astype(np.uint8)


def cavity_from_height(h, radius=1.0, gain=1.2):
    blur = ndimage.gaussian_filter(h, radius, mode="wrap")
    return np.clip(0.5 + (h - blur) * gain, 0, 1)


def quantize_to_ramp(values01, ramp):
    idx = np.clip((values01 * (len(ramp) - 1)).round().astype(int), 0, len(ramp) - 1)
    return ramp[idx]


def bayer(h, w, n=4):
    b4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.5
    if n == 2:
        b4 = np.array([[0, 2], [3, 1]]) / 4.0 - 0.5
    return np.tile(b4, (h // b4.shape[0] + 1, w // b4.shape[1] + 1))[:h, :w]


# ----------------------------------------------------------------------------------------------
# IO + previews
# ----------------------------------------------------------------------------------------------

def save_rgba(path, arr):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    arr = np.asarray(arr).astype(np.uint8).copy()
    arr[arr[..., 3] == 0, :3] = 0  # canonical transparent pixels (smaller, deterministic)
    Image.fromarray(arr, "RGBA").save(path, optimize=True)


def save_rgb(path, arr):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    Image.fromarray(np.asarray(arr)[..., :3].astype(np.uint8), "RGB").save(path, optimize=True)


def save_gray(path, arr01):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    Image.fromarray((np.clip(arr01, 0, 1) * 255).round().astype(np.uint8), "L").save(path, optimize=True)


def save_json(path, data):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w") as f:
        json.dump(data, f, indent=1, ensure_ascii=False)
        f.write("\n")


def checker(w, h, a=(58, 58, 70), b=(46, 46, 56), cell=8):
    x, y = np.meshgrid(np.arange(w) // cell, np.arange(h) // cell)
    out = np.zeros((h, w, 4), np.uint8)
    out[..., 3] = 255
    m = (x + y) % 2 == 0
    out[m, :3] = a
    out[~m, :3] = b
    return out


def on_background(img, bg=None, checker_cell=None):
    h, w = img.shape[:2]
    base = checker(w, h, cell=checker_cell) if checker_cell else canvas(w, h, bg or (36, 36, 46))
    return blend(base, img)


def label_strip(text, w, h=14, fg=(230, 230, 240), bg=(20, 20, 26)):
    im = Image.new("RGB", (w, h), bg)
    ImageDraw.Draw(im).text((3, 1), text, fill=fg)
    return np.dstack([np.asarray(im), np.full((h, w), 255, np.uint8)])


def contact_sheet(items, scale=4, cols=4, pad=8, bg=(28, 28, 36), label=True, checker_bg=True):
    """items: [(label, rgba_array)] -> RGB contact sheet (nearest upscale by `scale`)."""
    tiles = []
    for name, img in items:
        up = upscale(img, scale)
        up = on_background(up, checker_cell=4 * scale if checker_bg else None, bg=None if checker_bg else bg)
        if label:
            lab = label_strip(name, up.shape[1])
            up = np.concatenate([lab, up], axis=0)
        tiles.append(up)
    col_w = [0] * cols
    rows = []
    for i in range(0, len(tiles), cols):
        rows.append(tiles[i:i + cols])
    for r in rows:
        for c, t in enumerate(r):
            col_w[c] = max(col_w[c], t.shape[1])
    row_h = [max(t.shape[0] for t in r) for r in rows]
    W = sum(col_w) + pad * (cols + 1)
    H = sum(row_h) + pad * (len(rows) + 1)
    sheet = canvas(W, H, bg)
    y = pad
    for r, rh in zip(rows, row_h):
        x = pad
        for c, t in enumerate(r):
            sheet[y:y + t.shape[0], x:x + t.shape[1]] = t
            x += col_w[c] + pad
        y += rh + pad
    return sheet
